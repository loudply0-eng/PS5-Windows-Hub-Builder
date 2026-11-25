using System.Drawing;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PS5WindowsHubReleaseSigner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SignerForm());
    }
}

internal sealed class SignerForm : Form
{
    private const string ExpectedPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEijzMLeIFygR8rMMlITN1rfCHTncbjih/+N4uWikQG4y2v+XPS8iuh5w+STGLKG0zBw8oiZw2GwH7cyh/sdRajg==";
    private const string ExpectedFingerprint =
        "773d9d46e3f8904cacdf4b4bcf0ac415db529c2dc11d1b827cc3ca1edb2bca6d";

    private readonly TextBox keyPath = new() { ReadOnly = true };
    private readonly TextBox passphrase = new() { UseSystemPasswordChar = true };
    private readonly TextBox assetPath = new() { ReadOnly = true };
    private readonly TextBox version = new() { Text = "0.1.1" };
    private readonly TextBox minimumVersion = new() { Text = "0.1.0" };
    private readonly TextBox outputPath = new() { ReadOnly = true };
    private readonly TextBox releaseNotes = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Text = "PS5 WINDOWS HUB 0.1.1 — first release with automatic signed updates."
    };
    private readonly Button signButton = new();
    private readonly Label status = new();

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
        Text = "PS5 WINDOWS HUB — OFFLINE RELEASE SIGNER";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 660);
        Size = new Size(940, 760);
        BackColor = Color.FromArgb(16, 20, 29);
        ForeColor = Color.FromArgb(235, 239, 246);
        Font = new Font("Segoe UI", 10F);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 10,
            Padding = new Padding(24),
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        for (var i = 1; i <= 6; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var heading = new Label
        {
            Text = "توقيع إصدار PS5 WINDOWS HUB محلياً",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White
        };
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 3);

        keyPath.Text = GetDefaultPrivateKeyPath();
        assetPath.Text = FindDefaultAssetPath();
        if (!string.IsNullOrWhiteSpace(assetPath.Text))
        {
            var detectedVersion = Regex.Match(Path.GetFileName(assetPath.Text),
                @"^PS5-Windows-Hub-(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.zip$");
            if (detectedVersion.Success)
                version.Text = $"{detectedVersion.Groups[1].Value}.{detectedVersion.Groups[2].Value}.{detectedVersion.Groups[3].Value}";
        }
        version.ReadOnly = true;
        minimumVersion.ReadOnly = true;
        outputPath.Text = !string.IsNullOrWhiteSpace(assetPath.Text)
            ? Path.Combine(Path.GetDirectoryName(assetPath.Text)!, "PS5WindowsHub-SignedRelease")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PS5WindowsHub-SignedRelease");
        AddRow(layout, 1, "المفتاح الخاص (يُكتشف تلقائياً)", keyPath, BrowseKey);
        AddRow(layout, 2, "كلمة مرور المفتاح", passphrase);
        AddRow(layout, 3, "حزمة الإصدار المكتشفة", assetPath, BrowseAsset);
        AddRow(layout, 4, "الإصدار المكتشف", version);
        AddRow(layout, 5, "أقل إصدار مدعوم", minimumVersion);
        AddRow(layout, 6, "مكان حفظ التوقيع تلقائياً", outputPath, BrowseOutput);

        var notesLabel = new Label
        {
            Text = "ملاحظات الإصدار",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(188, 200, 219)
        };
        layout.Controls.Add(notesLabel, 0, 7);
        releaseNotes.Dock = DockStyle.Fill;
        releaseNotes.BackColor = Color.FromArgb(28, 35, 48);
        releaseNotes.ForeColor = Color.FromArgb(230, 236, 246);
        releaseNotes.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(releaseNotes, 1, 7);
        layout.SetColumnSpan(releaseNotes, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = BackColor
        };
        signButton.Text = "إنشاء ملفات التوقيع";
        signButton.Width = 210;
        signButton.Height = 44;
        signButton.FlatStyle = FlatStyle.Flat;
        signButton.FlatAppearance.BorderSize = 0;
        signButton.BackColor = Color.FromArgb(35, 105, 210);
        signButton.ForeColor = Color.White;
        signButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        signButton.Click += async (_, _) => await SignAsync();
        actions.Controls.Add(signButton);
        layout.Controls.Add(actions, 0, 8);
        layout.SetColumnSpan(actions, 3);

        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.ForeColor = Color.FromArgb(120, 196, 255);
        status.Text = "المفتاح الخاص يبقى على هذا الجهاز. لا يوجد اتصال بالشبكة أو رفع للمفتاح.";
        layout.Controls.Add(status, 0, 9);
        layout.SetColumnSpan(status, 3);
    }

    private static string FindDefaultAssetPath()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? "",
            Directory.GetParent(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? "")?.FullName ?? "",
            Environment.CurrentDirectory
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in candidates)
        {
            var matches = Directory.GetFiles(directory, "PS5-Windows-Hub-*.zip", SearchOption.TopDirectoryOnly)
                .Where(file => Regex.IsMatch(Path.GetFileName(file),
                    @"^PS5-Windows-Hub-(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.zip$"))
                .ToArray();
            if (matches.Length == 1) return matches[0];
        }
        return "";
    }

    private static string GetDefaultPrivateKeyPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsKey = Path.Combine(local, "PS5WindowsHub", "SigningKeys", "ecdsa-p256-private-encrypted.pem");
        if (File.Exists(windowsKey)) return windowsKey;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, ".local", "share", "ps5-windows-hub-signing", "update-signing-private.pem");
    }

    private static void AddRow(TableLayoutPanel layout, int row, string caption, TextBox editor, Action? browse = null)
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
        editor.Margin = new Padding(4, 7, 8, 7);
        editor.BackColor = Color.FromArgb(28, 35, 48);
        editor.ForeColor = Color.FromArgb(230, 236, 246);
        editor.BorderStyle = BorderStyle.FixedSingle;
        editor.RightToLeft = RightToLeft.No;
        layout.Controls.Add(editor, 1, row);

        if (browse is null) return;
        var button = new Button
        {
            Text = "استعراض",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 7, 0, 7),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 57, 76),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => browse();
        layout.Controls.Add(button, 2, row);
    }

    private void BrowseKey()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "اختر المفتاح الخاص المشفر",
            Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) keyPath.Text = dialog.FileName;
    }

    private void BrowseAsset()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "اختر حزمة التحديث ZIP",
            Filter = "PS5 Windows Hub update (*.zip)|PS5-Windows-Hub-*.zip",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var detectedVersion = Regex.Match(Path.GetFileName(dialog.FileName),
                @"^PS5-Windows-Hub-(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.zip$");
            if (!detectedVersion.Success)
            {
                MessageBox.Show(this, "اسم الحزمة يجب أن يكون PS5-Windows-Hub-X.Y.Z.zip.", "حزمة غير صالحة",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            assetPath.Text = dialog.FileName;
            version.Text = $"{detectedVersion.Groups[1].Value}.{detectedVersion.Groups[2].Value}.{detectedVersion.Groups[3].Value}";
            if (string.IsNullOrWhiteSpace(outputPath.Text))
                outputPath.Text = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, "PS5WindowsHub-SignedRelease");
        }
    }

    private void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "اختر مجلد حفظ manifest.json و manifest.sig.base64",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (Directory.Exists(outputPath.Text)) dialog.SelectedPath = outputPath.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK) outputPath.Text = dialog.SelectedPath;
    }

    private async Task SignAsync()
    {
        signButton.Enabled = false;
        status.Text = "جاري فحص المفتاح والحزمة وتوقيع بيانات الإصدار محلياً...";
        var password = passphrase.Text.ToCharArray();
        passphrase.Clear();

        try
        {
            var releaseVersion = version.Text.Trim();
            var minimum = minimumVersion.Text.Trim();
            if (!IsStableVersion(releaseVersion, out var parsedVersion) ||
                !IsStableVersion(minimum, out var parsedMinimum) ||
                parsedMinimum > parsedVersion)
                throw new InvalidDataException("رقم الإصدار أو أقل إصدار مدعوم غير صالح.");

            var privatePath = Path.GetFullPath(keyPath.Text.Trim());
            var zipPath = Path.GetFullPath(assetPath.Text.Trim());
            var destination = Path.GetFullPath(outputPath.Text.Trim());
            if (!File.Exists(privatePath)) throw new FileNotFoundException("لم يتم العثور على المفتاح الخاص المشفر.");
            if (!File.Exists(zipPath)) throw new FileNotFoundException("لم أجد حزمة الإصدار بجانب الأداة. ضع الأداة وملف ZIP في مجلد واحد ثم أعد التشغيل.");
            if (password.Length == 0) throw new InvalidDataException("أدخل عبارة مرور المفتاح محلياً.");

            var expectedAssetName = $"PS5-Windows-Hub-{releaseVersion}.zip";
            if (!string.Equals(Path.GetFileName(zipPath), expectedAssetName, StringComparison.Ordinal))
                throw new InvalidDataException($"اسم الحزمة يجب أن يكون {expectedAssetName}.");

            var notes = releaseNotes.Text;
            if (Encoding.UTF8.GetByteCount(notes) > 16 * 1024)
                throw new InvalidDataException("ملاحظات الإصدار تتجاوز الحد المسموح (16 KiB).");

            using var signingKey = ECDsa.Create();
            try
            {
                var encryptedPem = await File.ReadAllTextAsync(privatePath);
                signingKey.ImportFromEncryptedPem(encryptedPem, password.AsSpan());
            }
            catch (CryptographicException)
            {
                throw new CryptographicException("تعذر فتح المفتاح. تحقق من عبارة المرور وصيغة المفتاح المشفر.");
            }

            if (signingKey.KeySize != 256 ||
                !string.Equals(signingKey.ExportParameters(false).Curve.Oid.Value, "1.2.840.10045.3.1.7", StringComparison.Ordinal))
                throw new CryptographicException("المفتاح ليس ECDSA P-256.");

            var expectedPublic = Convert.FromBase64String(ExpectedPublicKeyBase64);
            var actualPublic = signingKey.ExportSubjectPublicKeyInfo();
            var actualFingerprint = Convert.ToHexString(SHA256.HashData(actualPublic)).ToLowerInvariant();
            if (!string.Equals(actualFingerprint, ExpectedFingerprint, StringComparison.Ordinal) ||
                !CryptographicOperations.FixedTimeEquals(actualPublic, expectedPublic))
                throw new CryptographicException("هذا المفتاح لا يطابق مفتاح التحقق الموثوق المضمّن في التطبيق.");

            var info = new FileInfo(zipPath);
            if (info.Length <= 0 || info.Length > 1024L * 1024 * 1024)
                throw new InvalidDataException("حجم حزمة التحديث خارج الحد المسموح.");

            await using var package = new FileStream(
                zipPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var digest = await SHA256.HashDataAsync(package);
            var sha256 = Convert.ToHexString(digest).ToLowerInvariant();
            var assetUrl = $"https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v{releaseVersion}/{expectedAssetName}";

            var manifest = new ReleaseManifest
            {
                Version = releaseVersion,
                MinimumSupportedVersion = minimum,
                AssetName = expectedAssetName,
                AssetUrl = assetUrl,
                SizeBytes = info.Length,
                Sha256 = sha256,
                ReleaseNotes = notes
            };
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });
            if (manifestBytes.Length is 0 or > 64 * 1024)
                throw new InvalidDataException("ملف manifest.json خارج الحد المسموح.");

            var signature = signingKey.SignData(
                manifestBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            if (!signingKey.VerifyData(
                manifestBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new CryptographicException("فشل التحقق المحلي من التوقيع الناتج.");

            Directory.CreateDirectory(destination);
            AtomicWrite(Path.Combine(destination, "manifest.json"), manifestBytes);
            AtomicWrite(
                Path.Combine(destination, "manifest.sig.base64"),
                Encoding.ASCII.GetBytes(Convert.ToBase64String(signature)));

            status.Text = $"تم إنشاء manifest والتوقيع بنجاح. SHA-256: {sha256}";
            MessageBox.Show(
                this,
                "تم التوقيع محلياً والتحقق منه. لم تتم قراءة المفتاح خارج هذا الجهاز ولم يتم رفع أي ملف.",
                "PS5 WINDOWS HUB — Release Signer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            status.Text = "تعذر إنشاء التوقيع. راجع الحقول ومسار المفتاح.";
            MessageBox.Show(this, ex.Message, "تعذر توقيع الإصدار", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
            passphrase.Clear();
            signButton.Enabled = true;
        }
    }

    private static bool IsStableVersion(string value, out Version parsed)
    {
        parsed = new Version(0, 0, 0);
        if (!Regex.IsMatch(value, @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant))
            return false;
        if (!Version.TryParse(value, out var valueParsed) || valueParsed is null) return false;
        parsed = valueParsed;
        return true;
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
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch { /* best-effort cleanup */ }
        }
    }
}
