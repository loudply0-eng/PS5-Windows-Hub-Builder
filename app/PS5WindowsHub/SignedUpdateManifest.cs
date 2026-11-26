using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PS5WindowsHub;

// Fail-closed verifier. Only the public key belongs in the application.
internal static class SignedUpdateManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "version", "minimum_supported_version", "asset_name", "asset_url",
        "size_bytes", "sha256", "release_notes"
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

    private static bool IsStableVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrEmpty(value) ||
            !Regex.IsMatch(value, @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant))
            return false;
        if (!Version.TryParse(value, out var parsed) || parsed is null)
            return false;
        version = parsed;
        return true;
    }

    internal static Manifest Verify(byte[] manifestBytes, string signatureBase64, string publicKeyBase64, string installedVersion)
    {
        if (manifestBytes is null || manifestBytes.Length is 0 or > 64 * 1024)
            throw new CryptographicException("Manifest size is invalid.");
        if (string.IsNullOrWhiteSpace(publicKeyBase64) || publicKeyBase64.Length > 512)
            throw new CryptographicException("Update verification key is missing or oversized.");
        if (string.IsNullOrWhiteSpace(signatureBase64) || signatureBase64.Length > 256)
            throw new CryptographicException("Detached signature is missing or oversized.");

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

        // OpenSSL's ECDSA output is the standard ASN.1 DER sequence (RFC 3279).
        // Bound the encoded size before asking the crypto provider to parse it.
        if (signature.Length is < 8 or > 80)
            throw new CryptographicException("ECDSA P-256 DER signature length is invalid.");

        using var verifier = ECDsa.Create();
        try
        {
            verifier.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);
            if (bytesRead != publicKey.Length || verifier.KeySize != 256 ||
                !string.Equals(verifier.ExportParameters(false).Curve.Oid.Value, "1.2.840.10045.3.1.7", StringComparison.Ordinal))
                throw new CryptographicException("The embedded verification key is not a valid P-256 public key.");
        }
        catch (CryptographicException)
        {
            throw;
        }

        if (!verifier.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
            throw new CryptographicException("Release manifest signature is invalid.");

        Manifest manifest;
        try
        {
            using var document = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Manifest root must be an object.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!AllowedFields.Contains(property.Name) || !seen.Add(property.Name))
                    throw new JsonException("Unknown or duplicate manifest property.");
            }
            if (!seen.SetEquals(AllowedFields))
                throw new JsonException("Manifest is missing required properties.");

            manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, JsonOptions)
                ?? throw new JsonException("Manifest is empty.");
            if (manifest.Version is null || manifest.MinimumSupportedVersion is null ||
                manifest.AssetName is null || manifest.AssetUrl is null ||
                manifest.Sha256 is null || manifest.ReleaseNotes is null)
                throw new JsonException("Manifest string fields must not be null.");
        }
        catch (JsonException ex)
        {
            throw new CryptographicException("Signed manifest JSON is invalid.", ex);
        }

        if (!IsStableVersion(manifest.Version, out var version) ||
            !IsStableVersion(manifest.MinimumSupportedVersion, out var minimum) ||
            !IsStableVersion(installedVersion, out var installed) ||
            version < minimum || minimum > installed || version < installed)
            throw new CryptographicException("Manifest version constraints reject downgrade or unsupported installation.");

        if (manifest.SizeBytes is <= 0 or > 1L * 1024 * 1024 * 1024)
            throw new CryptographicException("Asset size is outside the allowed range.");
        var expectedAssetName = $"PS5-Windows-Hub-{manifest.Version}.zip";
        if (!Regex.IsMatch(manifest.AssetName, @"\APS5-Windows-Hub-[A-Za-z0-9._-]+\.zip\z", RegexOptions.CultureInvariant) ||
            !string.Equals(manifest.AssetName, expectedAssetName, StringComparison.Ordinal))
            throw new CryptographicException("Unexpected update asset name or version.");
        if (!Regex.IsMatch(manifest.Sha256, @"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant))
            throw new CryptographicException("SHA-256 digest is invalid.");
        var expectedAssetPath = $"/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v{manifest.Version}/{manifest.AssetName}";
        if (!Uri.TryCreate(manifest.AssetUrl, UriKind.Absolute, out var assetUri) ||
            assetUri.Scheme != Uri.UriSchemeHttps || assetUri.Port != 443 ||
            !string.Equals(assetUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(assetUri.UserInfo) || !string.IsNullOrEmpty(assetUri.Fragment) ||
            !string.IsNullOrEmpty(assetUri.Query) ||
            !string.Equals(assetUri.AbsolutePath, expectedAssetPath, StringComparison.Ordinal))
            throw new CryptographicException("Update asset URL is not the exact approved GitHub release asset.");
        if (Encoding.UTF8.GetByteCount(manifest.ReleaseNotes) > 16 * 1024)
            throw new CryptographicException("Release notes are too large.");

        return manifest;
    }
}
