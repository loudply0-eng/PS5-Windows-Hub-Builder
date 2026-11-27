using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace PS5WindowsHubReleaseSigner;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--verify-bundle", StringComparison.Ordinal))
            return VerifyEmbeddedBundleForCi();

        ApplicationConfiguration.Initialize();
        Application.Run(new SignerForm());
        return 0;
    }

    private static int VerifyEmbeddedBundleForCi()
    {
        try
        {
            using var stream = OpenEmbeddedReleaseAsset();
            ValidateEmbeddedReleaseAsset(stream);
            return 0;
        }
        catch
        {
            return 2;
        }
    }

    internal static Stream OpenEmbeddedReleaseAsset()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(
            SignerForm.EmbeddedReleaseResourceName)
            ?? throw new InvalidDataException("حزمة الإصدار غير موجودة داخل ملف التوقيع.");
    }

    internal static void ValidateEmbeddedReleaseAsset(Stream stream)
    {
        if (!stream.CanSeek || stream.Length <= 0 || stream.Length > 1024L * 1024 * 1024)
            throw new InvalidDataException("حجم حزمة الإصدار المضمنة غير صالح.");

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var names = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
        {
            "PS5-Windows-Hub.exe",
            "PS5-Windows-Hub.dll",
            "PS5WindowsHub.Updater.exe",
            "relinker.exe",
            "Engine/KytyPS5/kyty_emulator.exe"
        })
        {
            if (!names.Contains(required))
                throw new InvalidDataException("حزمة الإصدار المضمنة لا تحتوي الملف المطلوب: " + required);
        }

        stream.Position = 0;
    }
}

internal sealed class SignerForm : Form
{
    internal const string EmbeddedReleaseResourceName =
        "PS5WindowsHubReleaseSigner.ReleaseAsset.zip";

    private const string ReleaseVersion = "0.1.1";
    private const string MinimumSupportedVersion = "0.1.0";
    private const string AssetName = "PS5-Windows-Hub-0.1.1.zip";
    private const string ExpectedPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEijzMLeIFygR8rMMlITN1rfCHTncbjih/+N4uWikQG4y2v+XPS8iuh5w+STGLKG0zBw8oiZw2GwH7cyh/sdRajg==";
    private const string ExpectedFingerprint =
        "773d9d46e3f8904cacdf4b4bcf0ac415db529c2dc11d1b827cc3ca1edb2bca6d";

    private readonly TextBox keyPath = new() { ReadOnly = true };
    private readonly TextBox passphrase = new() { UseSystemPasswordChar = true };
    private readonly TextBox assetInfo = new() { ReadOnly = true, Text = AssetName + " — مضمّن داخل هذا الملف" };
    private readonly TextBox versionInfo = new() { ReadOnly = true, Text = ReleaseVersion + " (الحد الأدنى " + MinimumSupportedVersion + ")" };
    private readonly TextBox outputPath = new() { ReadOnly = true };
    private readonly Button signButton = new();
    private readonly Label status = new();
    private readonly string privateKeyPath = GetDefaultPrivateKeyPath();
    private readonly string destinationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "PS5WindowsHub-SignedRelease");

    private sealed class ReleaseManifest
    {
        [JsonPropertyName("version")] public string Version { get; init; } = "";
        [JsonPropertyName("minimum_supported_version")] public string MinimumSupportedVersion { get; init; } = "";
        [JsonPropertyName("asset_name")] public string AssetName { get; init; } = "";
        [JsonPropertyName("asset_url")] public string AssetUrl { get; init; } = "";
        [JsonPropertyName("size_bytes")] public long SizeBytes { get; init; }
        [JsonPropertyName("sha256")] public string Sha256 { get; init; } = "";
        [JsonPropertyName("release_notes")] public string ReleaseNotes { get; init; } = "";
    }

    internal SignerForm()
    {
        Text = "PS5 WINDOWS HUB — أداة التوقيع التلقائي";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        Size = new Size(820, 610);
        BackColor = Color.FromArgb(16, 20, 29);
        ForeColor = Color.FromArgb(235, 239, 246);
        Font = new Font("Segoe UI", 10F);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(28),
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        for (var i = 1; i <= 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var heading = new Label
        {
            Text = "توقيع إصدار PS5 WINDOWS HUB تلقائياً",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White
        };
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 2);

        keyPath.Text = privateKeyPath;
        outputPath.Text = destinationPath;
        AddRow(layout, 1, "حزمة الإصدار", assetInfo);
        AddRow(layout, 2, "رقم الإصدار", versionInfo);
        AddRow(layout, 3, "المفتاح المحلي المكتشف", keyPath);
        AddRow(layout, 4, "مكان حفظ النتيجة", outputPath);
        AddRow(layout, 5, "كلمة مرور المفتاح", passphrase);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = BackColor
        };
        signButton.Text = "إنشاء ملفات التوقيع";
        signButton.Width = 245;
        signButton.Height = 46;
        signButton.FlatStyle = FlatStyle.Flat;
        signButton.FlatAppearance.BorderSize = 0;
        signButton.BackColor = Color.FromArgb(35, 105, 210);
        signButton.ForeColor = Color.White;
        signButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        signButton.Click += async (_, _) => await SignAsync();
        actions.Controls.Add(signButton);
        layout.Controls.Add(actions, 0, 6);
        layout.SetColumnSpan(actions, 2);

        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.ForeColor = Color.FromArgb(120, 196, 255);
        status.Text = File.Exists(privateKeyPath)
            ? "تم اكتشاف المفتاح والحزمة داخل البرنامج. أدخل كلمة المرور فقط."
            : "لم يتم العثور على المفتاح في المسار الافتراضي. تأكد من وجوده قبل المتابعة.";
        layout.Controls.Add(status, 0, 7);
        layout.SetColumnSpan(status, 2);

        if (!File.Exists(privateKeyPath))
        {
            signButton.Enabled = false;
            status.ForeColor = Color.FromArgb(255, 145, 120);
        }

        try
        {
            using var asset = Program.OpenEmbeddedReleaseAsset();
            assetInfo.Text = $"{AssetName} — مضمّن ({asset.Length:N0} بايت)";
            Program.ValidateEmbeddedReleaseAsset(asset);
        }
        catch (Exception ex)
        {
            signButton.Enabled = false;
            status.ForeColor = Color.FromArgb(255, 145, 120);
            status.Text = "حزمة الإصدار داخل البرنامج غير صالحة: " + ex.Message;
        }
    }

    private static string GetDefaultPrivateKeyPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "PS5WindowsHub", "SigningKeys", "ecdsa-p256-private-encrypted.pem");
    }

    private static void AddRow(TableLayoutPanel layout, int row, string caption, TextBox editor)
    {
        var label = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(188, 200, 219)
        };
        layout.Controls.Add(label, 0, row);

        editor.Dock = DockStyle.Fill;
        editor.Margin = new Padding(4, 8, 8, 8);
        editor.BackColor = Color.FromArgb(28, 35, 48);
        editor.ForeColor = Color.FromArgb(230, 236, 246);
        editor.BorderStyle = BorderStyle.FixedSingle;
        editor.RightToLeft = RightToLeft.No;
        layout.Controls.Add(editor, 1, row);
    }

    private async Task SignAsync()
    {
        signButton.Enabled = false;
        status.Text = "جاري فحص المفتاح المضمن وحزمة الإصدار...";
        var password = passphrase.Text.ToCharArray();
        passphrase.Clear();

        try
        {
            if (!File.Exists(privateKeyPath))
                throw new FileNotFoundException("لم يتم العثور على المفتاح الخاص المشفر في المسار الافتراضي.");

            if (password.Length == 0)
                throw new InvalidDataException("أدخل عبارة مرور المفتاح.");

            using var signingKey = ECDsa.Create();
            try
            {
                var encryptedPem = await File.ReadAllTextAsync(privateKeyPath);
                signingKey.ImportFromEncryptedPem(encryptedPem, password.AsSpan());
            }
            catch (CryptographicException)
            {
                throw new CryptographicException("تعذر فتح المفتاح. تحقق من عبارة المرور.");
            }

            if (signingKey.KeySize != 256 ||
                !string.Equals(signingKey.ExportParameters(false).Curve.Oid.Value, "1.2.840.10045.3.1.7", StringComparison.Ordinal))
                throw new CryptographicException("المفتاح ليس ECDSA P-256.");

            var expectedPublic = Convert.FromBase64String(ExpectedPublicKeyBase64);
            var actualPublic = signingKey.ExportSubjectPublicKeyInfo();
            var actualFingerprint = Convert.ToHexString(SHA256.HashData(actualPublic)).ToLowerInvariant();
            if (!string.Equals(actualFingerprint, ExpectedFingerprint, StringComparison.Ordinal) ||
                !CryptographicOperations.FixedTimeEquals(actualPublic, expectedPublic))
                throw new CryptographicException("المفتاح الموجود على هذا الجهاز لا يطابق مفتاح التحقق الموثوق داخل التطبيق.");

            await using var package = Program.OpenEmbeddedReleaseAsset();
            Program.ValidateEmbeddedReleaseAsset(package);
            package.Position = 0;
            var sizeBytes = package.Length;
            var digest = await SHA256.HashDataAsync(package);
            var sha256 = Convert.ToHexString(digest).ToLowerInvariant();
            var assetUrl =
                "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v" +
                ReleaseVersion + "/" + AssetName;

            var manifest = new ReleaseManifest
            {
                Version = ReleaseVersion,
                MinimumSupportedVersion = MinimumSupportedVersion,
                AssetName = AssetName,
                AssetUrl = assetUrl,
                SizeBytes = sizeBytes,
                Sha256 = sha256,
                ReleaseNotes = "إصدار تأسيسي يتضمن التحديث الآلي الآمن ودعم محرك Kyty المرفق."
            };
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });
            if (manifestBytes.Length is 0 or > 64 * 1024)
                throw new InvalidDataException("ملف بيانات الإصدار خارج الحد المسموح.");

            var signature = signingKey.SignData(
                manifestBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            if (!signingKey.VerifyData(
                manifestBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new CryptographicException("فشل التحقق المحلي من التوقيع الناتج.");

            Directory.CreateDirectory(destinationPath);
            AtomicWrite(Path.Combine(destinationPath, "manifest.json"), manifestBytes);
            AtomicWrite(
                Path.Combine(destinationPath, "manifest.sig.base64"),
                Encoding.ASCII.GetBytes(Convert.ToBase64String(signature)));

            status.ForeColor = Color.FromArgb(120, 220, 160);
            status.Text = "اكتمل التوقيع والتحقق المحلي. بصمة الحزمة: " + sha256;
            MessageBox.Show(
                this,
                "تم إنشاء ملفي التوقيع والتحقق منهما بنجاح.\n\nمكان الحفظ:\n" + destinationPath +
                "\n\nلم يتم رفع المفتاح أو كلمة المرور أو إرسال أي بيانات عبر الإنترنت.",
                "اكتمل توقيع الإصدار",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + destinationPath + "\"")
            {
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            status.ForeColor = Color.FromArgb(255, 145, 120);
            status.Text = "تعذر إنشاء التوقيع. راجع كلمة المرور والمفتاح.";
            MessageBox.Show(this, ex.Message, "تعذر توقيع الإصدار", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
            passphrase.Clear();
            signButton.Enabled = File.Exists(privateKeyPath);
        }
    }

    private static void AtomicWrite(string targetPath, byte[] bytes)
    {
        var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Best-effort cleanup of the temporary output.
            }
        }
    }
}
