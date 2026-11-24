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

byte[] BuildManifest(string version = "1.1.0", string url = "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip", string extra = "", string assetName = "PS5-Windows-Hub-1.1.0.zip")
{
    var json = "{\"version\":\"" + version + "\",\"minimum_supported_version\":\"1.0.0\",\"asset_name\":\"" + assetName + "\",\"asset_url\":\"" + url + "\",\"size_bytes\":1024,\"sha256\":\"" + new string('a', 64) + "\",\"release_notes\":\"Security test\"" + extra + "}";
    return Encoding.UTF8.GetBytes(json);
}

string Sign(byte[] bytes, ECDsa? key = null) =>
    Convert.ToBase64String((key ?? trustedKey).SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

string testRoot = Path.Combine(Path.GetTempPath(), "PS5WindowsHub-SecurityTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);

void BuildZip(string zipPath, params (string Name, int Attributes, string Content)[] entries)
{
    using var file = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write);
    using var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create);
    foreach (var item in entries)
    {
        var entry = zip.CreateEntry(item.Name);
        entry.ExternalAttributes = item.Attributes;
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(item.Content);
        stream.Write(bytes, 0, bytes.Length);
    }
}

void ExpectArchiveRejected(string testName, params (string Name, int Attributes, string Content)[] entries)
{
    Check(testName, () =>
    {
        var zipPath = Path.Combine(testRoot, Guid.NewGuid().ToString("N") + ".zip");
        var destination = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
        BuildZip(zipPath, entries);
        try
        {
            SafeUpdateArchiveExtractor.Extract(zipPath, destination);
            throw new Exception("Unsafe archive was accepted.");
        }
        catch (InvalidDataException) { }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
        }
    });
}

Check("valid OpenSSL-compatible P-256 DER signature accepted", () =>
{
    var bytes = BuildManifest();
    var result = SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0");
    if (result.Version != "1.1.0") throw new Exception("Wrong version returned.");
});

Check("signed manifest for current version reports no update", () =>
{
    var bytes = BuildManifest(
        version: "1.0.0",
        url: "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v1.0.0/PS5-Windows-Hub-1.0.0.zip",
        assetName: "PS5-Windows-Hub-1.0.0.zip");
    var result = SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0");
    if (result.Version != "1.0.0") throw new Exception("Current version was not recognized.");
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

Check("non-P-256 public key rejected", () =>
{
    using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
    var wrongCurvePublicKey = Convert.ToBase64String(wrongCurve.ExportSubjectPublicKeyInfo());
    var bytes = BuildManifest();
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), wrongCurvePublicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Non-P-256 verification key was accepted.");
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

Check("query string on asset URL rejected", () =>
{
    var bytes = BuildManifest(url: "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip?download=1");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Asset URL query string was accepted.");
});

Check("asset URL from another GitHub repository rejected", () =>
{
    var bytes = BuildManifest(url: "https://github.com/attacker/other/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Foreign GitHub repository URL was accepted.");
});

Check("null manifest string field rejected", () =>
{
    var json = Encoding.UTF8.GetString(BuildManifest()).Replace("\"release_notes\":\"Security test\"", "\"release_notes\":null", StringComparison.Ordinal);
    var bytes = Encoding.UTF8.GetBytes(json);
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Null release notes were accepted.");
});

Check("minimum supported version above installed version rejected", () =>
{
    var json = Encoding.UTF8.GetString(BuildManifest()).Replace("\"minimum_supported_version\":\"1.0.0\"", "\"minimum_supported_version\":\"1.0.1\"", StringComparison.Ordinal);
    var bytes = Encoding.UTF8.GetBytes(json);
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("An unsupported installation was accepted.");
});


Check("asset URL must use the exact Builder repository", () =>
{
    var bytes = BuildManifest(url: "https://github.com/attacker/other/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("A different repository URL was accepted.");
});

Check("asset filename must match signed version", () =>
{
    var bytes = BuildManifest(version: "1.2.0");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Asset name/version mismatch was accepted.");
});

Check("malformed signed JSON rejected", () =>
{
    var bytes = Encoding.UTF8.GetBytes("{not-json");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Malformed signed JSON was accepted.");
});

Check("noncanonical version rejected", () =>
{
    var bytes = BuildManifest(version: "01.1.0");
    try { SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Noncanonical version was accepted.");
});


Check("oversized manifest rejected", () =>
{
    var bytes = new byte[64 * 1024 + 1];
    try { SignedUpdateManifest.Verify(bytes, Convert.ToBase64String(new byte[64]), publicKey, "1.0.0"); }
    catch (CryptographicException) { return; }
    throw new Exception("Oversized manifest was accepted.");
});

Check("release metadata endpoints are pinned", () =>
{
    UpdateCoordinator.ValidateMetadataUri(UpdateTrust.ManifestUri, "manifest.json");
    UpdateCoordinator.ValidateMetadataUri(UpdateTrust.SignatureUri, "manifest.sig.base64");
    try
    {
        UpdateCoordinator.ValidateMetadataUri(new Uri("https://evil.example/manifest.json"), "manifest.json");
    }
    catch (InvalidOperationException) { return; }
    throw new Exception("Unapproved metadata endpoint was accepted.");
});



Check("safe ZIP extraction succeeds", () =>
{
    var zipPath = Path.Combine(testRoot, Guid.NewGuid().ToString("N") + ".zip");
    var destination = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
    BuildZip(zipPath, ("bin/PS5-Windows-Hub.exe", 0, "verified-app"));
    try
    {
        SafeUpdateArchiveExtractor.Extract(zipPath, destination);
        var extracted = File.ReadAllText(Path.Combine(destination, "bin", "PS5-Windows-Hub.exe"));
        if (extracted != "verified-app") throw new Exception("Extracted file content differs.");
    }
    finally
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
    }
});

ExpectArchiveRejected("ZIP parent traversal rejected", ("../escape.txt", 0, "x"));
ExpectArchiveRejected("ZIP rooted path rejected", ("/rooted.txt", 0, "x"));
ExpectArchiveRejected("ZIP duplicate paths rejected",
    ("same.txt", 0, "one"), ("same.txt", 0, "two"));
ExpectArchiveRejected("ZIP case-insensitive duplicate paths rejected",
    ("Folder/File.txt", 0, "one"), ("folder/file.txt", 0, "two"));
ExpectArchiveRejected("ZIP file/directory collision rejected",
    ("node", 0, "file"), ("node/child.txt", 0, "child"));
ExpectArchiveRejected("ZIP symbolic link rejected",
    ("link", unchecked((int)0xA0000000u), "target"));
ExpectArchiveRejected("ZIP Windows device name rejected", ("CON.txt", 0, "x"));
ExpectArchiveRejected("ZIP invalid Windows filename character rejected", ("bad?.txt", 0, "x"));
ExpectArchiveRejected("ZIP trailing-dot filename rejected", ("badname.", 0, "x"));

Console.WriteLine($"Security tests: {passed} passed, {failed} failed.");
try { Directory.Delete(testRoot, recursive: true); } catch { }
return failed == 0 ? 0 : 1;
