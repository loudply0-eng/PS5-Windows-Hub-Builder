using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PS5WindowsHub;

internal static class UpdateCoordinator
{
    private const int MaximumRedirects = 5;
    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumSignatureBytes = 256;
    private static readonly HashSet<string> ApprovedMetadataHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com",
        "github-releases.githubusercontent.com"
    };

    private static readonly HttpClient MetadataClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    internal static async Task CheckDownloadAndInstallAsync(
        IProgress<string>? status = null,
        IProgress<long>? downloadProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(UpdateTrust.PublicKeyBase64))
            throw new CryptographicException("The production update public key has not been provisioned.");

        ValidateMetadataUri(UpdateTrust.ManifestUri, "manifest.json");
        ValidateMetadataUri(UpdateTrust.SignatureUri, "manifest.sig.base64");

        status?.Report("Downloading signed release metadata...");
        var manifestBytes = await ReadBoundedResponseAsync(UpdateTrust.ManifestUri, MaximumManifestBytes, cancellationToken);
        var signatureBytes = await ReadBoundedResponseAsync(UpdateTrust.SignatureUri, MaximumSignatureBytes, cancellationToken);
        var signature = Encoding.ASCII.GetString(signatureBytes).Trim();
        if (signature.Length is 0 or > MaximumSignatureBytes)
            throw new CryptographicException("Detached release signature is empty or too large.");

        var installedVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? throw new InvalidOperationException("The installed application version is unavailable.");
        var manifest = SignedUpdateManifest.Verify(
            manifestBytes, signature, UpdateTrust.PublicKeyBase64, installedVersion);

        status?.Report("Downloading and verifying update package...");
        var archivePath = await VerifiedUpdateDownloader.DownloadAsync(manifest, downloadProgress, cancellationToken);
        var payloadDirectory = Path.Combine(Path.GetDirectoryName(archivePath)!, "payload");

        try
        {
            status?.Report("Safely extracting verified update...");
            SafeUpdateArchiveExtractor.Extract(archivePath, payloadDirectory);

            foreach (var requiredFile in new[]
            {
                "PS5-Windows-Hub.exe",
                "PS5WindowsHub.Updater.exe",
                "relinker.exe"
            })
            {
                if (!File.Exists(Path.Combine(payloadDirectory, requiredFile)))
                    throw new InvalidDataException("Verified update package is missing required file: " + requiredFile);
            }

            File.Delete(archivePath);
            status?.Report("Starting the verified updater and closing the application...");
            UpdateHelperLauncher.Launch(payloadDirectory);
        }
        catch
        {
            try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { /* best-effort cleanup */ }
            try { if (Directory.Exists(payloadDirectory)) Directory.Delete(payloadDirectory, recursive: true); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static void ValidateMetadataUri(Uri uri, string expectedFileName)
    {
        var expectedPath = "/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/" + expectedFileName;
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.AbsolutePath, expectedPath, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Release metadata endpoint is not the pinned GitHub release URL.");
    }

    private static async Task<byte[]> ReadBoundedResponseAsync(Uri initialUri, int maximumBytes, CancellationToken cancellationToken)
    {
        using var response = await SendWithApprovedRedirectsAsync(initialUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long declared && (declared <= 0 || declared > maximumBytes))
            throw new InvalidDataException("Release metadata response size is invalid.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException("Release metadata exceeded its size limit.");
            output.Write(buffer, 0, read);
        }
        if (output.Length == 0)
            throw new InvalidDataException("Release metadata response is empty.");
        return output.ToArray();
    }

    private static async Task<HttpResponseMessage> SendWithApprovedRedirectsAsync(Uri initialUri, CancellationToken cancellationToken)
    {
        var current = initialUri;
        for (var hop = 0; hop <= MaximumRedirects; hop++)
        {
            if (current.Scheme != Uri.UriSchemeHttps || current.Port != 443 ||
                !string.IsNullOrEmpty(current.UserInfo) || !ApprovedMetadataHosts.Contains(current.Host))
                throw new InvalidDataException("Release metadata redirected to an unapproved host.");

            var response = await MetadataClient.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!IsRedirect(response.StatusCode))
                return response;

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || hop == MaximumRedirects)
                throw new InvalidDataException("Release metadata redirect chain is invalid or too long.");
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }
        throw new InvalidDataException("Release metadata redirect chain exceeded its limit.");
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
}
