using System.Diagnostics;

namespace PS5WindowsHub;

internal static class UpdateHelperLauncher
{
    internal static void Launch(string extractedPayloadDirectory)
    {
        var stagingRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PS5WindowsHub", "Updates", "Staging"));
        var stagingPrefix = Path.TrimEndingDirectorySeparator(stagingRoot) + Path.DirectorySeparatorChar;
        var payload = Path.GetFullPath(extractedPayloadDirectory);
        if (!payload.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(payload))
            throw new InvalidDataException("Verified update payload must remain inside the update staging directory.");

        var helperSource = Path.Combine(AppContext.BaseDirectory, "PS5WindowsHub.Updater.exe");
        if (!File.Exists(helperSource))
            throw new FileNotFoundException("The standalone updater helper is missing.", helperSource);

        var helperDirectory = Path.Combine(Path.GetDirectoryName(payload)!, "Updater-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(helperDirectory);
        var helperCopy = Path.Combine(helperDirectory, "PS5WindowsHub.Updater.exe");
        File.Copy(helperSource, helperCopy, overwrite: false);

        var start = new ProcessStartInfo
        {
            FileName = helperCopy,
            WorkingDirectory = helperDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("--install-dir");
        start.ArgumentList.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)));
        start.ArgumentList.Add("--payload-dir");
        start.ArgumentList.Add(payload);
        start.ArgumentList.Add("--parent-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        _ = Process.Start(start) ?? throw new InvalidOperationException("Could not start the standalone updater helper.");
        Application.Exit();
    }
}
