using System.Net;
using System.Security.Cryptography;

namespace PS5WindowsHub;

internal static class VerifiedUpdateDownloader
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
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
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The signed update URL is not an approved GitHub HTTPS URL.");

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PS5WindowsHub", "Updates", "Staging");
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var destination = Path.Combine(staging, manifest.AssetName);

        try
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                if (total > manifest.SizeBytes || total > 4L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("The downloaded asset exceeded its signed size limit.");
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report(total);
            }
            await target.FlushAsync(cancellationToken);
            if (total != manifest.SizeBytes)
                throw new InvalidDataException("The downloaded asset is incomplete.");

            var actualHash = Convert.ToHexString(hash.GetHashAndReset());
            if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash), Convert.FromHexString(manifest.Sha256)))
                throw new CryptographicException("The update asset SHA-256 digest does not match the signed manifest.");

            return destination;
        }
        catch
        {
            try { Directory.Delete(staging, recursive: true); } catch { /* best-effort cleanup */ }
            throw;
        }
    }
}