using System.Diagnostics;

namespace PS5WindowsHub;

internal static class KytyEngineLauncher
{
    internal const string EmulatorExecutableName = "kyty_emulator.exe";

    internal static string ResolveBundledEngineDirectory(string? applicationDirectory = null)
    {
        var applicationRoot = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory);
        var bundledDirectory = Path.Combine(applicationRoot, "Engine", "KytyPS5");
        return File.Exists(Path.Combine(bundledDirectory, EmulatorExecutableName)) ? bundledDirectory : string.Empty;
    }

    internal static string ResolveEmulatorPath(string engineDirectory)
    {
        if (string.IsNullOrWhiteSpace(engineDirectory))
            throw new ArgumentException("حدد مجلد تثبيت KytyPS5 أولاً.", nameof(engineDirectory));

        var fullDirectory = Path.GetFullPath(engineDirectory);
        if (!Directory.Exists(fullDirectory))
            throw new DirectoryNotFoundException("مجلد محرك KytyPS5 غير موجود.");

        var executable = Path.Combine(fullDirectory, EmulatorExecutableName);
        if (!File.Exists(executable))
            throw new FileNotFoundException(
                $"لم يتم العثور على {EmulatorExecutableName} داخل مجلد المحرك المحدد.",
                executable);

        return executable;
    }

    internal static string ValidateGameInput(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            throw new ArgumentException("حدد مجلد اللعبة أو ملفها أولاً.", nameof(gamePath));

        var fullPath = Path.GetFullPath(gamePath);
        if (Directory.Exists(fullPath))
        {
            var bootFile = Path.Combine(fullPath, "eboot.bin");
            if (!File.Exists(bootFile))
                throw new InvalidDataException(
                    "المجلد المحدد لا يحتوي على eboot.bin في جذر مجلد اللعبة. اختر مجلد اللعبة الصحيح.");
            return fullPath;
        }

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("ملف اللعبة المحدد غير موجود.", fullPath);

        var extension = Path.GetExtension(fullPath);
        var isBootFile = string.Equals(Path.GetFileName(fullPath), "eboot.bin", StringComparison.OrdinalIgnoreCase);
        var isSupportedArchive = string.Equals(extension, ".zar", StringComparison.OrdinalIgnoreCase);
        var isElf = string.Equals(extension, ".elf", StringComparison.OrdinalIgnoreCase);

        if (!isBootFile && !isSupportedArchive && !isElf)
            throw new InvalidDataException("الصيغ المدعومة هنا هي مجلد لعبة يحتوي eboot.bin أو ملف eboot.bin / ELF / ZArchive (.zar).");

        return fullPath;
    }

    internal static ProcessStartInfo CreateStartInfo(string engineDirectory, string gamePath)
    {
        var executable = ResolveEmulatorPath(engineDirectory);
        var validatedGamePath = ValidateGameInput(gamePath);

        return new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = false,
            ArgumentList = { "--game", validatedGamePath }
        };
    }
}
