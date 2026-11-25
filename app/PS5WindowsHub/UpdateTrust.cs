namespace PS5WindowsHub;

internal static class UpdateTrust
{
    // Production P-256 SPKI public key is inserted only after the maintainer creates
    // and safely backs up the matching private key on an offline device.
    internal const string PublicKeyBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEijzMLeIFygR8rMMlITN1rfCHTncbjih/+N4uWikQG4y2v+XPS8iuh5w+STGLKG0zBw8oiZw2GwH7cyh/sdRajg==";

    internal static readonly Uri ManifestUri = new(
        "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/manifest.json");

    internal static readonly Uri SignatureUri = new(
        "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/manifest.sig.base64");
}
