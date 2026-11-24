using System.Security.Cryptography;
using System.Text;
using PS5WindowsHub;

var passed = 0;
var failed = 0;

void Check(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}

using var trustedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var publicKey = Convert.ToBase64String(trustedKey.ExportSubjectPublicKeyInfo());

byte[] BuildManifest(string version = "1.1.0", string url = "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip", string extra = "")
{
    var json = "{\"version\":\"" + version + "\",\"minimum_supported_version\":\"1.0.0\",\"asset_name\":\"PS5-Windows-Hub-1.1.0.zip\",\"asset_url\":\"" + url + "\",\"size_bytes\":1024,\"sha256\":\"" + new string('a', 64) + "\",\"release_notes\":\"Security test\"" + extra + "}";
    return Encoding.UTF8.GetBytes(json);
}

string Sign(byte[] bytes, ECDsa? key = null) =>
    Convert.ToBase64String((key ?? trustedKey).SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

Check("valid P-256 signature accepted", () =>
{
    var bytes = BuildManifest();
    var result = SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0");
    if (result.Version != "1.1.0") throw new Exception("Wrong version returned.");
});

Check("altered manifest rejected", () =>
{
    var bytes = BuildManifest();
    var signature = Sign(bytes);
    bytes[10] ^= 1;
    try { SignedUpdateManifest.Verify(bytes, signature, publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Altered bytes were accepted.");
});

Check("signature from another key rejected", () =>
{
    var bytes = BuildManifest();
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes, wrongKey), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Wrong signing key was accepted.");
});

Check("duplicate JSON property rejected", () =>
{
    var bytes = BuildManifest(extra: ",\"version\":\"1.2.0\"");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Duplicate property was accepted.");
});

Check("unknown JSON property rejected", () =>
{
    var bytes = BuildManifest(extra: ",\"unexpected\":true");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Unknown property was accepted.");
});

Check("downgrade rejected", () =>
{
    var bytes = BuildManifest(version: "0.9.0");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Downgrade was accepted.");
});

Check("unapproved asset host rejected", () =>
{
    var bytes = BuildManifest(url: "https://evil.example/PS5-Windows-Hub-1.1.0.zip");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Unapproved host was accepted.");
});

Check("oversized manifest rejected", () =>
{
    var bytes = new byte[64 * 1024 + 1];
    try { SignedUpdateManifest.Verify(bytes, Convert.ToBase64String(new byte[64]), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Oversized manifest was accepted.");
});

Console.WriteLine($"Security tests: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
