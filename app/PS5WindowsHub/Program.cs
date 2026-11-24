using System.Threading;
using System.Windows.Forms;

namespace PS5WindowsHub;
internal static class Program
{
    private const string UpdateMutexName = @"Local\PS5WindowsHub.UpdateTransaction";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // The updater uses this mode while it owns the transaction mutex.
        if (args.Length == 1 && string.Equals(args[0], "--health-check", StringComparison.Ordinal))
        {
            try
            {
                using var form = new MainForm();
                form.CreateControl();
                return File.Exists(Path.Combine(AppContext.BaseDirectory, "relinker.exe")) ? 0 : 3;
            }
            catch
            {
                return 4;
            }
        }

        using var updateMutex = new Mutex(initiallyOwned: false, name: UpdateMutexName);
        var ownsMutex = false;
        try
        {
            try
            {
                ownsMutex = updateMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // Ownership is granted when an abandoned mutex is acquired.
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                MessageBox.Show("PS5 WINDOWS HUB is already running or updating.", "PS5 WINDOWS HUB",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 5;
            }

            Application.Run(new MainForm());
            return 0;
        }
        finally
        {
            if (ownsMutex)
                updateMutex.ReleaseMutex();
        }
    }
}
