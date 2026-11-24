using System.Windows.Forms;

namespace PS5WindowsHub;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 1 && string.Equals(args[0], "--health-check", StringComparison.Ordinal))
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
