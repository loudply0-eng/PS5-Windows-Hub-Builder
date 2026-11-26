using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PS5WindowsHub;

internal sealed class MainForm : Form
{
    private readonly TextBox input = new();
    private readonly TextBox output = new();
    private readonly TextBox kytyDirectory = new();
    private readonly TextBox gameInput = new();
    private readonly Button browseInput = new();
    private readonly Button browseOutput = new();
    private readonly Button browseKyty = new();
    private readonly Button browseGameFolder = new();
    private readonly Button browseGameFile = new();
    private readonly Button convert = new();
    private readonly Button launchGame = new();
    private readonly Button stopGame = new();
    private readonly Button openKyty = new();
    private readonly Button update = new();
    private readonly Label status = new();
    private readonly RichTextBox log = new();
    private readonly System.Windows.Forms.Timer automaticUpdateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private bool conversionInProgress;
    private int updateCheckInProgress;
    private Process? kytyProcess;
    private static readonly bool EnableSecureUpdates = true; // Do not change until release signing and end-to-end update tests are complete.
    private string Relinker => Path.Combine(AppContext.BaseDirectory, "relinker.exe");

    public MainForm()
    {
        Text = "PS5 WINDOWS HUB";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 650);
        Size = new Size(1060, 760);
        BackColor = Color.FromArgb(16, 20, 29);
        ForeColor = Color.FromArgb(235, 239, 246);
        Font = new Font("Segoe UI", 10F);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(22), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        Controls.Add(root);

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "PS5 WINDOWS HUB", AutoSize = true, Font = new Font("Segoe UI", 23F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "واجهة تشغيل واختبار ألعاب PS5 عبر محرك KytyPS5 الرسمي", AutoSize = true, ForeColor = Color.FromArgb(160, 174, 196), Location = new Point(2, 49) });
        root.Controls.Add(heading, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 0, 0), BackColor = BackColor };
        var updatesReady = EnableSecureUpdates && !string.IsNullOrWhiteSpace(UpdateTrust.PublicKeyBase64);
        StyleButton(update, updatesReady ? "التحقق من التحديثات" : "التحديث التلقائي — قيد التجهيز", Color.FromArgb(54, 63, 78), 220);
        update.Click += async (_, _) => await CheckForUpdatesAsync();
        update.Enabled = updatesReady;
        actions.Controls.Add(update);
        root.Controls.Add(actions, 0, 1);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F), RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
        tabs.TabPages.Add(BuildKytyTab());
        tabs.TabPages.Add(BuildRelinkerTab());
        root.Controls.Add(tabs, 0, 2);

        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.ForeColor = Color.FromArgb(120, 196, 255);
        root.Controls.Add(status, 0, 3);

        log.Dock = DockStyle.Fill;
        log.ReadOnly = true;
        log.BackColor = Color.FromArgb(10, 13, 20);
        log.ForeColor = Color.FromArgb(206, 216, 230);
        log.BorderStyle = BorderStyle.FixedSingle;
        log.Font = new Font("Consolas", 9.5F);
        log.RightToLeft = RightToLeft.No;
        log.Text = "PS5 WINDOWS HUB — KytyPS5 integration preview\r\n"
            + "محرك KytyPS5 برنامج مستقل؛ نزّله من المصدر الرسمي وحدد مجلده أدناه.\r\n"
            + "لا يتم تضمين المحرك أو ملفات الألعاب أو مفاتيح/برامج النظام ضمن هذه الحزمة.\r\n"
            + "أداة AnyPS5 relinker في التبويب الآخر تجريبية ولا تمثل محاكي PS5 كاملاً.\r\n";
        root.Controls.Add(log, 0, 4);
        var savedSettings = HubSettingsStore.Load();
        kytyDirectory.Text = savedSettings.KytyDirectory;
        gameInput.Text = savedSettings.GameInput;
        input.Text = savedSettings.RelinkerInput;
        output.Text = savedSettings.RelinkerOutput;
        kytyDirectory.TextChanged += (_, _) => SaveSettings();
        gameInput.TextChanged += (_, _) => SaveSettings();
        input.TextChanged += (_, _) => SaveSettings();
        output.TextChanged += (_, _) => SaveSettings();
        SetStatus("جاهز — الإعدادات والمسارات تُحفظ محلياً تلقائياً.");

        Shown += async (_, _) =>
        {
            if (!EnableSecureUpdates) return;
            automaticUpdateTimer.Tick += async (_, _) =>
            {
                if (!IsDisposed) await CheckForUpdatesAsync(automatic: true);
            };
            automaticUpdateTimer.Start();
            await Task.Delay(1200);
            if (!IsDisposed) await CheckForUpdatesAsync(automatic: true);
        };
        FormClosed += (_, _) =>
        {
            automaticUpdateTimer.Stop();
            // The emulator is intentionally a separate process and is not force-terminated when the Hub closes.
            kytyProcess?.Dispose();
            kytyProcess = null;
        };
    }

    private TabPage BuildKytyTab()
    {
        var page = new TabPage("تشغيل ألعاب PS5") { BackColor = BackColor, ForeColor = ForeColor, Padding = new Padding(10) };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12), BackColor = BackColor };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(panel);

        panel.Controls.Add(PathRow("مجلد محرك KytyPS5 الرسمي (يحتوي kyty_emulator.exe)", kytyDirectory, browseKyty, ChooseKytyDirectory), 0, 0);
        panel.Controls.Add(GamePathRow(), 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0), BackColor = BackColor };
        StyleButton(launchGame, "تشغيل اللعبة", Color.FromArgb(35, 125, 85), 150);
        launchGame.Click += (_, _) => LaunchKytyGame();
        StyleButton(stopGame, "إيقاف المحاكي", Color.FromArgb(145, 65, 65), 150);
        stopGame.Enabled = false;
        stopGame.Click += (_, _) => StopKytyGame();
        StyleButton(openKyty, "تحميل KytyPS5 الرسمي", Color.FromArgb(45, 57, 76), 210);
        openKyty.Click += (_, _) => OpenKytyReleases();
        buttons.Controls.Add(launchGame);
        buttons.Controls.Add(stopGame);
        buttons.Controls.Add(openKyty);
        panel.Controls.Add(buttons, 0, 2);

        var note = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(165, 180, 202),
            TextAlign = ContentAlignment.TopRight,
            Text = "اختر مجلد لعبة يحتوي eboot.bin، أو ملف eboot.bin / ELF / ZArchive (.zar).\r\n"
                + "يجب أن تكون نسخة اللعبة وملفاتها مملوكة لك ومجهزة محلياً؛ هذا البرنامج لا يفك التشفير ولا يستخرج مفاتيح PS5."
        };
        panel.Controls.Add(note, 0, 3);
        return page;
    }

    private TabPage BuildRelinkerTab()
    {
        var page = new TabPage("أداة ELF التجريبية") { BackColor = BackColor, ForeColor = ForeColor, Padding = new Padding(10) };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12), BackColor = BackColor };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.Controls.Add(panel);

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(235, 190, 135),
            Text = "هذه أداة relinker فقط؛ تحويل ELF لا يعني أن اللعبة أصبحت قابلة للتشغيل على Windows."
        }, 0, 0);
        panel.Controls.Add(PathRow("ملف اللعبة (ELF / eboot.bin)", input, browseInput, ChooseInput), 0, 1);
        panel.Controls.Add(PathRow("مكان حفظ الملف الناتج", output, browseOutput, ChooseOutput), 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0), BackColor = BackColor };
        StyleButton(convert, "تشغيل أداة التحويل", Color.FromArgb(35, 105, 210), 190);
        convert.Click += async (_, _) => await ConvertAsync();
        actions.Controls.Add(convert);
        panel.Controls.Add(actions, 0, 3);
        return page;
    }

    private Control PathRow(string caption, TextBox box, Button button, EventHandler handler)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 7, 0, 7), BackColor = BackColor };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
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
        StyleButton(button, "استعراض", Color.FromArgb(45, 57, 76), 118);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 4, 0, 0);
        button.Click += handler;
        panel.Controls.Add(button, 1, 1);
        return panel;
    }

    private Control GamePathRow()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 7, 0, 7), BackColor = BackColor };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = new Label { Text = "مجلد اللعبة أو ملفها", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(188, 200, 219), TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 3);
        gameInput.Dock = DockStyle.Fill;
        gameInput.ReadOnly = true;
        gameInput.BackColor = Color.FromArgb(28, 35, 48);
        gameInput.ForeColor = Color.FromArgb(230, 236, 246);
        gameInput.BorderStyle = BorderStyle.FixedSingle;
        gameInput.RightToLeft = RightToLeft.No;
        gameInput.Margin = new Padding(0, 4, 8, 0);
        panel.Controls.Add(gameInput, 0, 1);

        StyleButton(browseGameFolder, "مجلد اللعبة", Color.FromArgb(45, 57, 76), 118);
        browseGameFolder.Dock = DockStyle.Fill;
        browseGameFolder.Margin = new Padding(0, 4, 8, 0);
        browseGameFolder.Click += ChooseGameFolder;
        panel.Controls.Add(browseGameFolder, 1, 1);

        StyleButton(browseGameFile, "ملف ELF / ZAR", Color.FromArgb(45, 57, 76), 136);
        browseGameFile.Dock = DockStyle.Fill;
        browseGameFile.Margin = new Padding(0, 4, 0, 0);
        browseGameFile.Click += ChooseGameFile;
        panel.Controls.Add(browseGameFile, 2, 1);
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

    private void ChooseKytyDirectory(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "اختر مجلد إصدار KytyPS5 الرسمي الذي يحتوي kyty_emulator.exe", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _ = KytyEngineLauncher.ResolveEmulatorPath(dialog.SelectedPath);
            kytyDirectory.Text = Path.GetFullPath(dialog.SelectedPath);
            AppendLog("KytyPS5 engine selected: " + kytyDirectory.Text);
            SetStatus("تم التعرف على محرك KytyPS5.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "مجلد المحرك غير صالح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ChooseGameFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "اختر مجلد اللعبة الذي يحتوي eboot.bin", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SetGameInput(dialog.SelectedPath);
    }

    private void ChooseGameFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "اختر eboot.bin أو ELF أو ZArchive",
            Filter = "PS5 game inputs (*.zar;*.elf;eboot.bin)|*.zar;*.elf;eboot.bin|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetGameInput(dialog.FileName);
    }

    private void SetGameInput(string path)
    {
        try
        {
            gameInput.Text = KytyEngineLauncher.ValidateGameInput(path);
            SetStatus("تم اختيار مصدر اللعبة.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ملف اللعبة غير صالح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LaunchKytyGame()
    {
        if (kytyProcess is not null)
        {
            try
            {
                if (!kytyProcess.HasExited)
                {
                    MessageBox.Show(this, "هناك لعبة تعمل بالفعل عبر KytyPS5.", "المحاكي يعمل", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            catch { }
            kytyProcess.Dispose();
            kytyProcess = null;
        }

        try
        {
            var start = KytyEngineLauncher.CreateStartInfo(kytyDirectory.Text, gameInput.Text);
            var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.Exited += (_, _) =>
            {
                var exitCode = -1;
                try { exitCode = process.ExitCode; } catch { }
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(() => CompleteKytyRun(process, exitCode))); } catch { }
            };

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("تعذر تشغيل kyty_emulator.exe.");
            }

            kytyProcess = process;
            launchGame.Enabled = false;
            stopGame.Enabled = true;
            AppendLog("Starting KytyPS5: " + start.FileName);
            AppendLog("Game input: " + gameInput.Text);
            SetStatus("تم تشغيل KytyPS5. انتظر نتيجة الإقلاع داخل نافذة المحرك.");
        }
        catch (Exception ex)
        {
            AppendLog("KytyPS5 launch error: " + ex);
            SetStatus("تعذر تشغيل محرك KytyPS5.");
            MessageBox.Show(this, ex.Message, "فشل تشغيل المحرك", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CompleteKytyRun(Process process, int exitCode)
    {
        if (!ReferenceEquals(kytyProcess, process)) return;
        AppendLog("KytyPS5 process exited. Exit code: " + exitCode);
        kytyProcess.Dispose();
        kytyProcess = null;
        launchGame.Enabled = true;
        stopGame.Enabled = false;
        SetStatus("انتهت عملية KytyPS5. راجع سجل المحرك لتحديد مرحلة التوافق.");
    }

    private void StopKytyGame()
    {
        var process = kytyProcess;
        if (process is null) return;
        try
        {
            if (process.HasExited)
            {
                CompleteKytyRun(process, process.ExitCode);
                return;
            }

            if (process.CloseMainWindow())
            {
                AppendLog("Requested KytyPS5 to close its main window.");
                SetStatus("تم إرسال طلب إغلاق المحاكي.");
                return;
            }

            var answer = MessageBox.Show(this,
                "لم يستقبل المحرك طلب الإغلاق العادي. هل تريد إنهاء عملية KytyPS5 بالقوة؟",
                "تأكيد إيقاف المحاكي", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes)
            {
                process.Kill(entireProcessTree: true);
                AppendLog("KytyPS5 process was terminated by user request.");
            }
        }
        catch (Exception ex)
        {
            AppendLog("KytyPS5 stop error: " + ex);
            MessageBox.Show(this, ex.Message, "تعذر إيقاف المحاكي", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenKytyReleases()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/KytyPS5/KytyPS5/releases/latest") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "تعذر فتح صفحة المحرك", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChooseInput(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "اختر ملف ELF أو eboot.bin", Filter = "ELF files (*.elf;eboot.bin)|*.elf;eboot.bin|All files (*.*)|*.*", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        input.Text = dialog.FileName;
        var folder = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
        output.Text = Path.Combine(folder, Path.GetFileNameWithoutExtension(dialog.FileName) + "_windows.exe");
        SetStatus("تم اختيار الملف لأداة relinker التجريبية.");
    }

    private void ChooseOutput(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Title = "حفظ الملف الناتج من relinker", Filter = "Windows executable (*.exe)|*.exe", DefaultExt = "exe", AddExtension = true, OverwritePrompt = true, FileName = string.IsNullOrWhiteSpace(output.Text) ? "game_windows.exe" : Path.GetFileName(output.Text) };
        if (dialog.ShowDialog(this) == DialogResult.OK) output.Text = dialog.FileName;
    }

    private async Task CheckForUpdatesAsync(bool automatic = false)
    {
        if (Interlocked.Exchange(ref updateCheckInProgress, 1) != 0) return;
        update.Enabled = false;
        try
        {
            SetStatus("جاري التحقق من التحديث الموقّع...");
            var statusProgress = new Progress<string>(SetStatus);
            await UpdateCoordinator.CheckDownloadAndInstallAsync(statusProgress, canInstall: () => !conversionInProgress);
        }
        catch (OperationCanceledException)
        {
            SetStatus("تم إلغاء التحديث. النسخة الحالية لم تتغير.");
        }
        catch (Exception ex)
        {
            SetStatus("تعذر إكمال التحديث الآمن.");
            AppendLog("UPDATE ERROR: " + ex);
            if (!automatic)
                MessageBox.Show(this, ex.Message, "التحديث الآمن", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Interlocked.Exchange(ref updateCheckInProgress, 0);
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
        if (!File.Exists(Relinker))
        {
            MessageBox.Show(this, "لم يتم العثور على relinker.exe بجانب البرنامج. أعد تثبيت الحزمة كاملة.", "أداة relinker غير موجودة", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("أداة relinker غير موجودة.");
            return;
        }

        convert.Enabled = browseInput.Enabled = browseOutput.Enabled = false;
        conversionInProgress = true;
        log.Clear();
        AppendLog("Starting AnyPS5 relinker conversion...");
        SetStatus("جاري التحويل — لا تغلق البرنامج.");
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("تعذر تحديد مجلد الإخراج.");
            Directory.CreateDirectory(directory);

            var start = new ProcessStartInfo { FileName = Relinker, WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("--windows");
            start.ArgumentList.Add("--registry");
            start.ArgumentList.Add(source);
            start.ArgumentList.Add(destination);

            using var process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, a) => { if (a.Data is not null) AppendLog(a.Data); };
            process.ErrorDataReceived += (_, a) => { if (a.Data is not null) AppendLog("[stderr] " + a.Data); };
            if (!process.Start()) throw new InvalidOperationException("تعذر تشغيل أداة relinker.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(destination))
            {
                AppendLog("Relinker output: " + destination);
                SetStatus("اكتمل التحويل فقط؛ لم يتم تشغيل اللعبة.");
                MessageBox.Show(this, "اكتمل تحويل الملف. هذا لا يعني أن اللعبة تعمل على Windows؛ محرك PS5 منفصل مطلوب لتشغيل الألعاب.", "اكتمل التحويل", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppendLog("Relinker failed. Exit code: " + process.ExitCode);
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
            conversionInProgress = false;
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

    private void SaveSettings()
    {
        try
        {
            HubSettingsStore.Save(new HubSettings
            {
                KytyDirectory = kytyDirectory.Text,
                GameInput = gameInput.Text,
                RelinkerInput = input.Text,
                RelinkerOutput = output.Text
            });
        }
        catch (Exception ex)
        {
            AppendLog("Settings could not be saved: " + ex.Message);
        }
    }

    private void SetStatus(string message) => status.Text = message;
}
