namespace PS5WindowsHub;

internal static class UpdateTrust
{
    // P-256 SPKI trust anchor embedded in released clients. Change only during a deliberate
    // key-rotation procedure that also provisions and verifies the matching offline private key.
    internal const string PublicKeyBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEijzMLeIFygR8rMMlITN1rfCHTncbjih/+N4uWikQG4y2v+XPS8iuh5w+STGLKG0zBw8oiZw2GwH7cyh/sdRajg==";

    internal static readonly Uri ManifestUri = new(
        "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/manifest.json");

    internal static readonly Uri SignatureUri = new(
        "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/manifest.sig.base64");
}
