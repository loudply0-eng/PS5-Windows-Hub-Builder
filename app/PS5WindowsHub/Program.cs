using System.Windows.Forms;

namespace PS5WindowsHub;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var isHealthCheck = args.Length == 1 &&
            string.Equals(args[0], "--health-check", StringComparison.Ordinal);

        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\PS5WindowsHub.AppInstance",
            createdNew: out var createdNew);

        if (!createdNew)
        {
            if (isHealthCheck) return 5;
            MessageBox.Show(
                "PS5 WINDOWS HUB is already running.",
                "PS5 WINDOWS HUB",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        if (isHealthCheck)
        {
            try
            {
                using var form = new MainForm();
                form.CreateControl();
                var baseDirectory = AppContext.BaseDirectory;
                return File.Exists(Path.Combine(baseDirectory, "relinker.exe")) &&
                    File.Exists(Path.Combine(baseDirectory, "PS5WindowsHub.Updater.exe")) ? 0 : 3;
            }
            catch
            {
                return 4;
            }
        }

        Application.Run(new MainForm());
        return 0;
    }
}
