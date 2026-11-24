using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PS5WindowsHub;

// Fail-closed verifier for the signed release manifest. The embedded public key
// must be provisioned from the maintainer's offline key-generation process.
// Never place the private signing key in this application or its build pipeline.
internal static class SignedUpdateManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    internal sealed record Manifest
    {
        [JsonPropertyName("version")] public string Version { get; init; } = "";
        [JsonPropertyName("minimum_supported_version")] public string MinimumSupportedVersion { get; init; } = "";
        [JsonPropertyName("asset_name")] public string AssetName { get; init; } = "";
        [JsonPropertyName("asset_url")] public string AssetUrl { get; init; } = "";
        [JsonPropertyName("size_bytes")] public long SizeBytes { get; init; }
        [JsonPropertyName("sha256")] public string Sha256 { get; init; } = "";
        [JsonPropertyName("release_notes")] public string ReleaseNotes { get; init; } = "";
    }

    internal static Manifest Verify(byte[] manifestBytes, string signatureBase64, string publicKeyBase64)
    {
        if (manifestBytes.Length is 0 or > 64 * 1024)
            throw new CryptographicException("Manifest size is invalid.");
        if (string.IsNullOrWhiteSpace(publicKeyBase64))
            throw new CryptographicException("Update verification key has not been provisioned.");

        byte[] signature;
        byte[] publicKey;
        try
        {
            signature = Convert.FromBase64String(signatureBase64);
            publicKey = Convert.FromBase64String(publicKeyBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Signature or public key encoding is invalid.", ex);
        }

        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);
        if (bytesRead != publicKey.Length || !verifier.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256))
            throw new CryptographicException("Release manifest signature is invalid.");

        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, JsonOptions)
                ?? throw new JsonException("Manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new CryptographicException("Signed manifest JSON is invalid.", ex);
        }

        if (!Version.TryParse(manifest.Version, out var version) ||
            !Version.TryParse(manifest.MinimumSupportedVersion, out var minimum) ||
            version < minimum)
            throw new CryptographicException("Manifest version constraints are invalid.");
        if (manifest.SizeBytes is <= 0 or > 4L * 1024 * 1024 * 1024)
            throw new CryptographicException("Asset size is outside the allowed range.");
        if (!Regex.IsMatch(manifest.AssetName, @"\APS5-Windows-Hub-[A-Za-z0-9._-]+\.zip\z", RegexOptions.CultureInvariant))
            throw new CryptographicException("Unexpected update asset name.");
        if (!Regex.IsMatch(manifest.Sha256, @"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant))
            throw new CryptographicException("SHA-256 digest is invalid.");
        if (!Uri.TryCreate(manifest.AssetUrl, UriKind.Absolute, out var assetUri) ||
            assetUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(assetUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Update asset URL is not an approved HTTPS GitHub URL.");
        if (manifest.ReleaseNotes.Length > 16 * 1024)
            throw new CryptographicException("Release notes are too large.");

        return manifest;
    }
}