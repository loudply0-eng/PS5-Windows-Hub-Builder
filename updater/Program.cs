using System.Diagnostics;

internal static class Program
{
    private const string ApplicationFileName = "PS5-Windows-Hub.exe";
    private const long MaximumCopiedBytes = 1L * 1024 * 1024 * 1024;
    private const int MaximumFiles = 100_000;

    private static int Main(string[] args)
    {
        using var updateMutex = new Mutex(initiallyOwned: false, name: @"Local\PS5WindowsHub.UpdateTransaction");
        var ownsMutex = false;
        try
        {
            try { ownsMutex = updateMutex.WaitOne(90_000); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex)
                throw new TimeoutException("Another application or update transaction held the update mutex past the timeout.");

            var options = Parse(args);
            Apply(options.InstallDirectory, options.PayloadDirectory, options.ParentProcessId);
            Console.WriteLine("Update installed and health check passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Updater failed: " + ex);
            return 1;
        }
        finally
        {
            if (ownsMutex) updateMutex.ReleaseMutex();
        }
    }

    private static (string InstallDirectory, string PayloadDirectory, int ParentProcessId) Parse(string[] args)
    {
        if (args.Length != 6 || args[0] != "--install-dir" || args[2] != "--payload-dir" || args[4] != "--parent-pid" ||
            !int.TryParse(args[5], out var parentPid) || parentPid <= 0)
            throw new ArgumentException("Invalid updater arguments.");
        return (Path.GetFullPath(args[1]), Path.GetFullPath(args[3]), parentPid);
    }

    private static void Apply(string installDirectory, string payloadDirectory, int parentPid)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var stagingRoot = Path.GetFullPath(Path.Combine(localData, "PS5WindowsHub", "Updates", "Staging"));
        var stagingPrefix = Path.TrimEndingDirectorySeparator(stagingRoot) + Path.DirectorySeparatorChar;
        if (!payloadDirectory.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(payloadDirectory) ||
            (File.GetAttributes(payloadDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Payload must be a normal directory inside the application's update staging directory.");

        if (!Directory.Exists(installDirectory) ||
            (File.GetAttributes(installDirectory) & FileAttributes.ReparsePoint) != 0 ||
            !File.Exists(Path.Combine(installDirectory, ApplicationFileName)))
            throw new InvalidDataException("The current application installation is invalid.");

        var installRoot = Path.GetPathRoot(installDirectory);
        if (string.Equals(Path.TrimEndingDirectorySeparator(installDirectory), Path.TrimEndingDirectorySeparator(installRoot ?? ""), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Refusing to replace a filesystem root.");

        WaitForParentExit(parentPid, Path.Combine(installDirectory, ApplicationFileName));

        var parentDirectory = Path.GetDirectoryName(installDirectory)
            ?? throw new InvalidDataException("Invalid application installation path.");
        var token = Guid.NewGuid().ToString("N");
        var replacementDirectory = Path.Combine(parentDirectory, ".PS5WindowsHub-new-" + token);
        var backupDirectory = Path.Combine(parentDirectory, ".PS5WindowsHub-backup-" + token);
        var movedOldInstallation = false;

        try
        {
            var copied = CopyTree(payloadDirectory, replacementDirectory);
            if (copied.Files == 0 ||
                !File.Exists(Path.Combine(replacementDirectory, ApplicationFileName)) ||
                !File.Exists(Path.Combine(replacementDirectory, "relinker.exe")))
                throw new InvalidDataException("The staged payload is missing required application files.");

            Directory.Move(installDirectory, backupDirectory);
            movedOldInstallation = true;
            Directory.Move(replacementDirectory, installDirectory);

            if (!RunHealthCheck(Path.Combine(installDirectory, ApplicationFileName)))
                throw new InvalidOperationException("The new application failed its startup health check.");

            // Retain the immediately previous installation as the rollback copy.
            foreach (var oldBackup in Directory.EnumerateDirectories(parentDirectory, ".PS5WindowsHub-backup-*"))
            {
                if (string.Equals(oldBackup, backupDirectory, StringComparison.OrdinalIgnoreCase))
                    continue;
                try { Directory.Delete(oldBackup, recursive: true); }
                catch { /* Keep the new update successful; an older backup may be removed manually. */ }
            }
        }
        catch
        {
            try
            {
                if (Directory.Exists(installDirectory) && movedOldInstallation)
                    Directory.Delete(installDirectory, recursive: true);
                if (movedOldInstallation && Directory.Exists(backupDirectory) && !Directory.Exists(installDirectory))
                    Directory.Move(backupDirectory, installDirectory);
            }
            catch (Exception rollbackError)
            {
                throw new IOException("Update failed and automatic rollback also failed. Backup: " + backupDirectory, rollbackError);
            }
            throw;
        }
        finally
        {
            try { if (Directory.Exists(replacementDirectory)) Directory.Delete(replacementDirectory, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private static void WaitForParentExit(int processId, string expectedApplicationPath)
    {
        try
        {
            using var parent = Process.GetProcessById(processId);
            var actualPath = parent.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(actualPath) ||
                !string.Equals(Path.GetFullPath(actualPath), Path.GetFullPath(expectedApplicationPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The supplied parent process is not the installed PS5 Windows Hub application.");
            if (!parent.WaitForExit(90_000))
                throw new TimeoutException("The application did not close before the updater timeout.");
        }
        catch (ArgumentException)
        {
            // The application exited before the updater started waiting.
        }
    }

    private static bool RunHealthCheck(string applicationPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = applicationPath,
            Arguments = "--health-check",
            WorkingDirectory = Path.GetDirectoryName(applicationPath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start the updated application health check.");

        if (!process.WaitForExit(30_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return false;
        }
        return process.ExitCode == 0;
    }

    private static (int Files, long Bytes) CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        var files = 0;
        long bytes = 0;

        void CopyDirectory(string from, string to)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(from))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Staged payload contains a reparse point.");

                var target = Path.Combine(to, Path.GetFileName(entry));
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    Directory.CreateDirectory(target);
                    CopyDirectory(entry, target);
                }
                else
                {
                    var info = new FileInfo(entry);
                    bytes = checked(bytes + info.Length);
                    files++;
                    if (files > MaximumFiles || bytes > MaximumCopiedBytes)
                        throw new InvalidDataException("Staged payload exceeds the updater copy limits.");
                    File.Copy(entry, target, overwrite: false);
                }
            }
        }

        CopyDirectory(source, destination);
        return (files, bytes);
    }
}
