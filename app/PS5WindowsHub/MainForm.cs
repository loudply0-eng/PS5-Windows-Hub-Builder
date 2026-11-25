using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
namespace PS5WindowsHub;

internal sealed class MainForm : Form
{
    private readonly TextBox input = new();
    private readonly TextBox output = new();
    private readonly Button browseInput = new();
    private readonly Button browseOutput = new();
    private readonly Button convert = new();
    private readonly Button update = new();
    private readonly Label status = new();
    private readonly RichTextBox log = new();
    private const bool EnableSecureUpdates = false; // Do not change until release signing and end-to-end update tests are complete.
    private string Engine => Path.Combine(AppContext.BaseDirectory, "relinker.exe");

    public MainForm()
    {
        Text = "PS5 WINDOWS HUB";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 580);
        Size = new Size(1000, 680);
        BackColor = Color.FromArgb(16, 20, 29);
        ForeColor = Color.FromArgb(235, 239, 246);
        Font = new Font("Segoe UI", 10F);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(24), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "PS5 WINDOWS HUB", AutoSize = true, Font = new Font("Segoe UI", 23F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "واجهة Windows لتحويل ملفات ELF باستخدام محرك AnyPS5", AutoSize = true, ForeColor = Color.FromArgb(160, 174, 196), Location = new Point(2, 48) });
        root.Controls.Add(heading, 0, 0);

        root.Controls.Add(PathRow("ملف اللعبة (ELF)", input, browseInput, ChooseInput), 0, 1);
        root.Controls.Add(PathRow("مكان حفظ ملف Windows", output, browseOutput, ChooseOutput), 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0), BackColor = BackColor };
        StyleButton(convert, "تحويل إلى Windows", Color.FromArgb(35, 105, 210), 190);
        convert.Click += async (_, _) => await ConvertAsync();
        var updatesReady = EnableSecureUpdates && !string.IsNullOrWhiteSpace(UpdateTrust.PublicKeyBase64);
        StyleButton(update, updatesReady ? "التحقق من التحديثات" : "التحديث التلقائي — قيد التجهيز", Color.FromArgb(54, 63, 78), 220);
        update.Click += async (_, _) => await CheckForUpdatesAsync();
        update.Enabled = updatesReady;
        actions.Controls.Add(convert);
        actions.Controls.Add(update);
        root.Controls.Add(actions, 0, 3);

        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.ForeColor = Color.FromArgb(120, 196, 255);
        root.Controls.Add(status, 0, 4);

        log.Dock = DockStyle.Fill;
        log.ReadOnly = true;
        log.BackColor = Color.FromArgb(10, 13, 20);
        log.ForeColor = Color.FromArgb(206, 216, 230);
        log.BorderStyle = BorderStyle.FixedSingle;
        log.Font = new Font("Consolas", 9.5F);
        log.RightToLeft = RightToLeft.No;
        log.Text = "PS5 WINDOWS HUB — Engine Preview\r\n"
            + "هذه نسخة أولية من الواجهة. يلزم وجود relinker.exe بجانب البرنامج.\r\n"
            + "تنبيه: بناء relinker وحده لا يوفر جميع مكتبات النظام اللازمة لتشغيل الألعاب.\r\n";
        root.Controls.Add(log, 0, 5);
        SetStatus("جاهز — اختر ملف ELF للبدء.");

        // When secure updates are enabled for a signed stable release, check automatically after the main window is shown.
        Shown += async (_, _) =>
        {
            if (!EnableSecureUpdates) return;
            await Task.Delay(1200);
            if (!IsDisposed) await CheckForUpdatesAsync();
        };
    }

    private Control PathRow(string caption, TextBox box, Button button, EventHandler handler)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 7, 0, 7), BackColor = BackColor };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = new Label { Text = caption, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(188, 200, 219), TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);
        box.Dock = DockStyle.Fill;
        box.ReadOnly = true;
        box.BackColor = Color.FromArgb(28, 35, 48);
        box.ForeColor = Color.FromArgb(230, 236, 246);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.RightToLeft = RightToLeft.No;
        box.Margin = new Padding(0, 4, 8, 0);
        panel.Controls.Add(box, 0, 1);
        StyleButton(button, "استعراض", Color.FromArgb(45, 57, 76), 108);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 4, 0, 0);
        button.Click += handler;
        panel.Controls.Add(button, 1, 1);
        return panel;
    }

    private static void StyleButton(Button button, string caption, Color color, int width)
    {
        button.Text = caption;
        button.Width = width;
        button.Height = 42;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(0, 0, 10, 0);
    }

    private void ChooseInput(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "اختر ملف ELF", Filter = "ELF files (*.elf;eboot.bin)|*.elf;eboot.bin|All files (*.*)|*.*", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        input.Text = dialog.FileName;
        var folder = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
        output.Text = Path.Combine(folder, Path.GetFileNameWithoutExtension(dialog.FileName) + "_windows.exe");
        SetStatus("تم اختيار الملف. راجع مسار الإخراج قبل التحويل.");
    }

    private void ChooseOutput(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Title = "حفظ نسخة Windows", Filter = "Windows executable (*.exe)|*.exe", DefaultExt = "exe", AddExtension = true, OverwritePrompt = true, FileName = string.IsNullOrWhiteSpace(output.Text) ? "game_windows.exe" : Path.GetFileName(output.Text) };
        if (dialog.ShowDialog(this) == DialogResult.OK) output.Text = dialog.FileName;
    }

    private async Task CheckForUpdatesAsync()
    {
        update.Enabled = false;
        try
        {
            SetStatus("جاري التحقق من التحديث الموقّع...");
            var statusProgress = new Progress<string>(SetStatus);
            await UpdateCoordinator.CheckDownloadAndInstallAsync(statusProgress);
        }
        catch (OperationCanceledException)
        {
            SetStatus("تم إلغاء التحديث. النسخة الحالية لم تتغير.");
        }
        catch (Exception ex)
        {
            SetStatus("تعذر إكمال التحديث الآمن.");
            AppendLog("UPDATE ERROR: " + ex);
            MessageBox.Show(this, ex.Message, "التحديث الآمن", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            update.Enabled = EnableSecureUpdates && !string.IsNullOrWhiteSpace(UpdateTrust.PublicKeyBase64);
        }
    }

    private async Task ConvertAsync()
    {
        var source = input.Text.Trim();
        var destination = output.Text.Trim();
        if (!File.Exists(source))
        {
            MessageBox.Show(this, "اختر ملف ELF موجوداً.", "ملف الإدخال", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(destination) || !destination.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "حدد مسار إخراج ينتهي بـ .exe.", "مسار الإخراج", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "لا يمكن أن يكون ملف الإخراج هو ملف الإدخال نفسه.", "مسار غير آمن", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (!File.Exists(Engine))
        {
            MessageBox.Show(this, "لم يتم العثور على relinker.exe بجانب البرنامج. أعد تثبيت الحزمة كاملة.", "المحرك غير موجود", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("المحرك غير موجود.");
            return;
        }

        convert.Enabled = browseInput.Enabled = browseOutput.Enabled = false;
        log.Clear();
        AppendLog("Starting AnyPS5 relinker conversion...");
        SetStatus("جاري التحويل — لا تغلق البرنامج.");
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("تعذر تحديد مجلد الإخراج.");
            Directory.CreateDirectory(directory);

            var start = new ProcessStartInfo { FileName = Engine, WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("--windows");
            start.ArgumentList.Add("--registry");
            start.ArgumentList.Add(source);
            start.ArgumentList.Add(destination);

            using var process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, a) => { if (a.Data is not null) AppendLog(a.Data); };
            process.ErrorDataReceived += (_, a) => { if (a.Data is not null) AppendLog("[stderr] " + a.Data); };
            if (!process.Start()) throw new InvalidOperationException("تعذر تشغيل محرك relinker.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(destination))
            {
                AppendLog("Conversion completed: " + destination);
                SetStatus("اكتمل التحويل. لم يتم تشغيل اللعبة تلقائياً.");
                MessageBox.Show(this, "اكتمل تحويل الملف. هذا لا يضمن قابلية تشغيل اللعبة؛ قد تحتاج مكتبات PRX وميزات غير مدعومة.", "اكتمل التحويل", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppendLog("Conversion failed. Exit code: " + process.ExitCode);
                SetStatus("فشل التحويل. راجع سجل التفاصيل.");
                MessageBox.Show(this, "فشل التحويل. راجع سجل التفاصيل لمعرفة السبب.", "فشل", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex);
            SetStatus("حدث خطأ أثناء التحويل.");
            MessageBox.Show(this, ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            convert.Enabled = browseInput.Enabled = browseOutput.Enabled = true;
        }
    }

    private void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), line); return; }
        log.AppendText(line + Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    private void SetStatus(string message) => status.Text = message;
}
