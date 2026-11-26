using System.Net;
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

async Task CheckAsync(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}

using var trustedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var publicKey = Convert.ToBase64String(trustedKey.ExportSubjectPublicKeyInfo());

byte[] BuildManifest(string version = "1.1.0", string minimumSupportedVersion = "1.0.0", string url = "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip", string extra = "", string assetName = "PS5-Windows-Hub-1.1.0.zip")
{
    var json = "{\"version\":\"" + version + "\",\"minimum_supported_version\":\"" + minimumSupportedVersion + "\",\"asset_name\":\"" + assetName + "\",\"asset_url\":\"" + url + "\",\"size_bytes\":1024,\"sha256\":\"" + new string('a', 64) + "\",\"release_notes\":\"Security test\"" + extra + "}";
    return Encoding.UTF8.GetBytes(json);
}

string Sign(byte[] bytes, ECDsa? key = null) =>
    Convert.ToBase64String((key ?? trustedKey).SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

string testRoot = Path.Combine(Path.GetTempPath(), "PS5WindowsHub-SecurityTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);

Check("application settings survive an exact JSON round trip", () =>
{
    var settingsPath = Path.Combine(testRoot, "settings-roundtrip", "settings.json");
    var expected = new HubSettings
    {
        KytyDirectory = @"C:\\Tools\\Kyty PS5",
        GameInput = @"D:\\Games\\My PS5 Game",
        RelinkerInput = @"D:\\Games\\eboot.bin",
        RelinkerOutput = @"D:\\Output\\game_windows.exe"
    };
    HubSettingsStore.Save(expected, settingsPath);
    var actual = HubSettingsStore.Load(settingsPath);
    if (actual != expected) throw new Exception("Persisted settings differ from the saved values.");
});

Check("corrupt or unsupported application settings fall back to defaults", () =>
{
    var settingsPath = Path.Combine(testRoot, "settings-corrupt.json");
    File.WriteAllText(settingsPath, "{ invalid json");
    var corrupt = HubSettingsStore.Load(settingsPath);
    if (corrupt != new HubSettings()) throw new Exception("Corrupt settings were not reset safely.");
    File.WriteAllText(settingsPath, "{\\"SchemaVersion\\":99,\\"KytyDirectory\\":\\"ignored\\"}");
    var unsupported = HubSettingsStore.Load(settingsPath);
    if (unsupported != new HubSettings()) throw new Exception("Unsupported settings schema was not ignored.");
});

KytyEngineLauncherTests.Run(Check, testRoot);

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

Check("0.1.0 bootstrap client accepts signed 0.1.1 update", () =>
{
    var bytes = BuildManifest(
        version: "0.1.1",
        minimumSupportedVersion: "0.1.0",
        url: "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v0.1.1/PS5-Windows-Hub-0.1.1.zip",
        assetName: "PS5-Windows-Hub-0.1.1.zip");
    var result = SignedUpdateManifest.Verify(bytes, Sign(bytes), publicKey, "0.1.0");
    if (result.Version != "0.1.1" || result.MinimumSupportedVersion != "0.1.0")
        throw new Exception("The first bootstrap manifest was not accepted for the 0.1.0 client.");
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



var updateBytes = Encoding.UTF8.GetBytes("verified update payload bytes");
var updateHash = Convert.ToHexString(SHA256.HashData(updateBytes)).ToLowerInvariant();
var downloadManifest = new SignedUpdateManifest.Manifest
{
    Version = "1.1.0",
    MinimumSupportedVersion = "1.0.0",
    AssetName = "PS5-Windows-Hub-1.1.0.zip",
    AssetUrl = "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v1.1.0/PS5-Windows-Hub-1.1.0.zip",
    SizeBytes = updateBytes.Length,
    Sha256 = updateHash,
    ReleaseNotes = "Downloader test"
};

await CheckAsync("verified downloader follows approved redirect and verifies bytes", async () =>
{
    var staging = Path.Combine(testRoot, "download-valid");
    using var handler = new SequenceHandler((_, index) =>
    {
        if (index == 0)
            return new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("https://release-assets.githubusercontent.com/assets/update.zip?token=test") }
            };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(updateBytes) };
    });
    using var client = new HttpClient(handler);
    var downloaded = await VerifiedUpdateDownloader.DownloadAsync(downloadManifest, client, staging);
    if (!File.ReadAllBytes(downloaded).SequenceEqual(updateBytes))
        throw new Exception("Downloaded bytes differ from the signed payload.");
    if (handler.RequestCount != 2)
        throw new Exception("Expected exactly one approved redirect.");
});

await CheckAsync("verified downloader rejects a wrong hash and clears staging", async () =>
{
    var staging = Path.Combine(testRoot, "download-bad-hash");
    var badManifest = downloadManifest with { Sha256 = new string('0', 64) };
    using var handler = new SequenceHandler((_, _) =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(updateBytes) });
    using var client = new HttpClient(handler);
    try
    {
        await VerifiedUpdateDownloader.DownloadAsync(badManifest, client, staging);
        throw new Exception("A package with the wrong hash was accepted.");
    }
    catch (CryptographicException) { }
    if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
        throw new Exception("Failed download left staged files behind.");
});

await CheckAsync("verified downloader rejects unapproved redirect host", async () =>
{
    var staging = Path.Combine(testRoot, "download-bad-redirect");
    using var handler = new SequenceHandler((_, _) =>
        new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://evil.example/update.zip") }
        });
    using var client = new HttpClient(handler);
    try
    {
        await VerifiedUpdateDownloader.DownloadAsync(downloadManifest, client, staging);
        throw new Exception("An unapproved redirect host was accepted.");
    }
    catch (InvalidDataException) { }
    if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
        throw new Exception("Rejected redirect left staged files behind.");
});

await CheckAsync("interrupted download leaves installed version untouched", async () =>
{
    var staging = Path.Combine(testRoot, "download-interrupted");
    var install = Path.Combine(testRoot, "installed-sentinel");
    Directory.CreateDirectory(install);
    var installedFile = Path.Combine(install, "PS5-Windows-Hub.exe");
    File.WriteAllText(installedFile, "currently installed version");
    var before = SHA256.HashData(File.ReadAllBytes(installedFile));

    using var handler = new SequenceHandler((_, _) =>
    {
        var content = new StreamContent(new FailingReadStream(updateBytes));
        content.Headers.ContentLength = updateBytes.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });
    using var client = new HttpClient(handler);
    try
    {
        await VerifiedUpdateDownloader.DownloadAsync(downloadManifest, client, staging);
        throw new Exception("Interrupted download unexpectedly succeeded.");
    }
    catch (IOException) { }
    var after = SHA256.HashData(File.ReadAllBytes(installedFile));
    if (!CryptographicOperations.FixedTimeEquals(before, after))
        throw new Exception("Installed application changed during interrupted download.");
    if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
        throw new Exception("Interrupted download left staging files behind.");
});

Check("safe ZIP extraction succeeds", () =>
{
    var zipPath = Path.Combine(testRoot, Guid.NewGuid().ToString("N") + ".zip");
    var destination = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
    BuildZip(zipPath, ("bin/PS5-Windows-Hub.exe", 0, "verified-app"));
    try
    {
        var expectedZipHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zipPath)));
        SafeUpdateArchiveExtractor.Extract(zipPath, destination, expectedZipHash);
        var extracted = File.ReadAllText(Path.Combine(destination, "bin", "PS5-Windows-Hub.exe"));
        if (extracted != "verified-app") throw new Exception("Extracted file content differs.");
    }
    finally
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
    }
});

Check("ZIP extraction rejects an archive whose signed digest changed", () =>
{
    var zipPath = Path.Combine(testRoot, Guid.NewGuid().ToString("N") + ".zip");
    var destination = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
    BuildZip(zipPath, ("file.txt", 0, "original"));
    try
    {
        try
        {
            SafeUpdateArchiveExtractor.Extract(zipPath, destination, new string('0', 64));
            throw new Exception("An archive with the wrong SHA-256 was extracted.");
        }
        catch (CryptographicException) { }
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            throw new Exception("Failed digest verification left extracted files behind.");
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


sealed class SequenceHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _factory;
    private int _requestCount;

    internal SequenceHandler(Func<HttpRequestMessage, int, HttpResponseMessage> factory) => _factory = factory;
    internal int RequestCount => _requestCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = Interlocked.Increment(ref _requestCount) - 1;
        return Task.FromResult(_factory(request, index));
    }
}

sealed class FailingReadStream : Stream
{
    private readonly byte[] _data;
    private bool _failed;

    internal FailingReadStream(byte[] data) => _data = data;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_failed) throw new IOException("Simulated interrupted network stream.");
        _failed = true;
        var copied = Math.Min(count, _data.Length);
        Array.Copy(_data, 0, buffer, offset, copied);
        return copied;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_failed) return ValueTask.FromException<int>(new IOException("Simulated interrupted network stream."));
        _failed = true;
        var copied = Math.Min(buffer.Length, _data.Length);
        _data.AsMemory(0, copied).CopyTo(buffer);
        return ValueTask.FromResult(copied);
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
