using System.Net;
using System.Security.Cryptography;

namespace PS5WindowsHub;

internal static class VerifiedUpdateDownloader
{
    private const int MaximumRedirects = 5;
    private static readonly HashSet<string> ApprovedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com",
        "github-releases.githubusercontent.com"
    };

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    internal static async Task<string> DownloadAsync(
        SignedUpdateManifest.Manifest manifest,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(manifest.AssetUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.Port != 443 ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("The signed update URL is not an approved GitHub HTTPS URL.");

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PS5WindowsHub", "Updates", "Staging");
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var destination = Path.Combine(staging, manifest.AssetName);

        try
        {
            using var response = await SendWithApprovedRedirectsAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long declared && declared != manifest.SizeBytes)
                throw new InvalidDataException("The downloaded asset length does not match the signed manifest.");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0) break;
                total = checked(total + read);
                if (total > manifest.SizeBytes || total > 1L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("The downloaded asset exceeded its signed size limit.");
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report(total);
            }
            await target.FlushAsync(cancellationToken);
            if (total != manifest.SizeBytes)
                throw new InvalidDataException("The downloaded asset is incomplete.");

            var expectedHash = Convert.FromHexString(manifest.Sha256);
            var actualHash = hash.GetHashAndReset();
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                throw new CryptographicException("The update asset SHA-256 digest does not match the signed manifest.");

            return destination;
        }
        catch
        {
            try { Directory.Delete(staging, recursive: true); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static async Task<HttpResponseMessage> SendWithApprovedRedirectsAsync(Uri initialUri, CancellationToken cancellationToken)
    {
        var current = initialUri;
        for (var hop = 0; hop <= MaximumRedirects; hop++)
        {
            if (current.Scheme != Uri.UriSchemeHttps || current.Port != 443 ||
                !string.IsNullOrEmpty(current.UserInfo) || !ApprovedDownloadHosts.Contains(current.Host))
                throw new InvalidDataException("The update download attempted to use an unapproved redirect destination.");

            var response = await Client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!IsRedirect(response.StatusCode))
                return response;

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || hop == MaximumRedirects)
                throw new InvalidDataException("The update download redirect chain is invalid or too long.");

            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        throw new InvalidDataException("The update download redirect chain exceeded its limit.");
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
}
