using System.IO.Compression;

namespace PS5WindowsHub;

// Extracts only regular files and directories into a newly-created, isolated staging directory.
internal static class SafeUpdateArchiveExtractor
{
    private const int MaximumEntries = 100_000;
    private const long MaximumExpandedBytes = 8L * 1024 * 1024 * 1024;
    private const long MaximumSingleFileBytes = 4L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static bool IsSafeWindowsSegment(string segment)
    {
        if (segment.Length is 0 or > 240 || segment is "." or ".." ||
            segment.EndsWith(".", StringComparison.Ordinal) ||
            segment.EndsWith(" ", StringComparison.Ordinal) ||
            segment.Any(ch => ch < 32 || ch is '<' or '>' or '"' or '|' or '?' or '*' or ':'))
            return false;

        var stem = segment.Split('.')[0];
        return !ReservedDeviceNames.Contains(stem);
    }

    internal static void Extract(string archivePath, string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("Verified update archive was not found.", archivePath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("A staging destination is required.", nameof(destinationDirectory));
        if (Directory.Exists(destinationDirectory) && Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
            throw new IOException("Archive extraction destination must be empty.");

        Directory.CreateDirectory(destinationDirectory);
        try
        {
            ExtractIntoEmptyDirectory(archivePath, destinationDirectory);
        }
        catch
        {
            try { Directory.Delete(destinationDirectory, recursive: true); } catch { /* best-effort staging cleanup */ }
            throw;
        }
    }

    private static void ExtractIntoEmptyDirectory(string archivePath, string destinationDirectory)
    {
        var root = Path.GetFullPath(destinationDirectory);
        var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;

        using var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count is 0 or > MaximumEntries)
            throw new InvalidDataException("Archive entry count is outside the allowed range.");

        var planned = new List<(ZipArchiveEntry Entry, string RelativePath, bool IsDirectory)>(archive.Entries.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long declaredTotal = 0;

        foreach (var entry in archive.Entries)
        {
            var raw = entry.FullName;
            if (string.IsNullOrEmpty(raw) || raw.Contains('\\') || raw.Contains('\0') ||
                raw.StartsWith("/", StringComparison.Ordinal) ||
                raw.StartsWith("//", StringComparison.Ordinal) ||
                raw.Contains(':'))
                throw new InvalidDataException("Archive contains a rooted or invalid path.");

            if (raw.Length > 1024)
                throw new InvalidDataException("Archive path is too long.");
            var isDirectory = raw.EndsWith("/", StringComparison.Ordinal);
            var relative = isDirectory ? raw[..^1] : raw;
            if (relative.Length == 0)
                throw new InvalidDataException("Archive contains an empty path.");
            var segments = relative.Split('/');
            if (segments.Length > 64 || segments.Any(s => !IsSafeWindowsSegment(s)))
                throw new InvalidDataException("Archive contains traversal or a path segment unsafe on Windows.");

            if (!names.Add(relative))
                throw new InvalidDataException("Archive contains duplicate paths (case-insensitive).");

            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 ||
                (unixType != 0 && unixType != 0x8000 && unixType != 0x4000))
                throw new InvalidDataException("Archive contains a link or unsupported special file.");

            for (var i = 1; i < segments.Length; i++)
                directoryNames.Add(string.Join("/", segments.Take(i)));
            if (isDirectory)
                directoryNames.Add(relative);

            if (!isDirectory)
            {
                if (entry.Length < 0 || entry.Length > MaximumSingleFileBytes)
                    throw new InvalidDataException("Archive file exceeds the per-file size limit.");
                declaredTotal = checked(declaredTotal + entry.Length);
                if (declaredTotal > MaximumExpandedBytes)
                    throw new InvalidDataException("Archive exceeds the expanded size limit.");
                fileNames.Add(relative);
            }
            planned.Add((entry, relative, isDirectory));
        }

        if (fileNames.Overlaps(directoryNames))
            throw new InvalidDataException("Archive contains a file/directory path collision.");

        long writtenTotal = 0;
        foreach (var item in planned)
        {
            var targetPath = Path.GetFullPath(Path.Combine(root, item.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive entry escapes the extraction directory.");

            if (item.IsDirectory)
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var input = item.Entry.Open();
            using var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024);
            var buffer = new byte[128 * 1024];
            long fileWritten = 0;
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                fileWritten = checked(fileWritten + read);
                writtenTotal = checked(writtenTotal + read);
                if (fileWritten > item.Entry.Length || fileWritten > MaximumSingleFileBytes ||
                    writtenTotal > MaximumExpandedBytes)
                    throw new InvalidDataException("Archive expanded beyond its declared size.");
                output.Write(buffer, 0, read);
            }
            if (fileWritten != item.Entry.Length)
                throw new InvalidDataException("Archive entry length does not match its central directory record.");
        }
    }
}
