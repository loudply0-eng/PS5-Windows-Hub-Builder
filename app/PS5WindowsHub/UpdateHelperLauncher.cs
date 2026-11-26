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
        if (!File.Exists(helperSource) || (File.GetAttributes(helperSource) & FileAttributes.ReparsePoint) != 0)
            throw new FileNotFoundException("The standalone updater helper is missing or unsafe.", helperSource);

        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PS5WindowsHub");
        Directory.CreateDirectory(appDataDirectory);
        if ((File.GetAttributes(appDataDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The application data directory cannot be a reparse point.");

        var helperDirectory = Path.Combine(appDataDirectory, "Updater");
        Directory.CreateDirectory(helperDirectory);
        if ((File.GetAttributes(helperDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The updater helper directory cannot be a reparse point.");

        var helperCopy = Path.Combine(helperDirectory, "PS5WindowsHub.Updater.exe");
        if (File.Exists(helperCopy))
        {
            if ((File.GetAttributes(helperCopy) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The existing updater helper file cannot be a reparse point.");
            File.Delete(helperCopy);
        }
        File.Copy(helperSource, helperCopy, overwrite: false);
        var sourceInfo = new FileInfo(helperSource);
        var copiedInfo = new FileInfo(helperCopy);
        if (sourceInfo.Length != copiedInfo.Length)
            throw new IOException("The copied updater helper has an unexpected size.");

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
