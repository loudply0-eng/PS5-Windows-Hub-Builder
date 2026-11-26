namespace PS5WindowsHub;

internal static class KytyEngineLauncherTests
{
    internal static void Run(Action<string, Action> check, string testRoot)
    {
        var engineDirectory = Path.Combine(testRoot, "Kyty Engine With Spaces");
        var gameDirectory = Path.Combine(testRoot, "PS5 Game Dump With Spaces");
        Directory.CreateDirectory(engineDirectory);
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(Path.Combine(engineDirectory, KytyEngineLauncher.EmulatorExecutableName), "test executable");
        File.WriteAllText(Path.Combine(gameDirectory, "eboot.bin"), "test ELF");

        check("Kyty engine resolves the official emulator executable", () =>
        {
            var resolved = KytyEngineLauncher.ResolveEmulatorPath(engineDirectory);
            if (!string.Equals(resolved, Path.Combine(engineDirectory, KytyEngineLauncher.EmulatorExecutableName), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unexpected Kyty emulator path.");
        });

        check("PS5 game directory is accepted when it contains eboot.bin", () =>
        {
            var resolved = KytyEngineLauncher.ValidateGameInput(gameDirectory);
            if (!string.Equals(resolved, gameDirectory, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unexpected game directory.");
        });

        check("argument-safe launch keeps a path with spaces as one argument", () =>
        {
            var start = KytyEngineLauncher.CreateStartInfo(engineDirectory, gameDirectory);
            if (start.ArgumentList.Count != 2 ||
                start.ArgumentList[0] != "--game" ||
                !string.Equals(start.ArgumentList[1], gameDirectory, StringComparison.OrdinalIgnoreCase))
                throw new Exception("The game path was not preserved as one argument.");
            if (start.UseShellExecute)
                throw new Exception("Shell execution must remain disabled.");
        });

        check("supported ZArchive game file is accepted", () =>
        {
            var archive = Path.Combine(testRoot, "Game Archive With Spaces.zar");
            File.WriteAllText(archive, "test archive");
            if (!string.Equals(KytyEngineLauncher.ValidateGameInput(archive), archive, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unexpected ZArchive path.");
        });

        check("supported ELF and eboot.bin files are accepted", () =>
        {
            var elf = Path.Combine(testRoot, "Game With Spaces.elf");
            var boot = Path.Combine(testRoot, "eboot.bin");
            File.WriteAllText(elf, "test ELF");
            File.WriteAllText(boot, "test ELF");
            _ = KytyEngineLauncher.ValidateGameInput(elf);
            _ = KytyEngineLauncher.ValidateGameInput(boot);
        });

        check("game folder without eboot.bin is rejected", () =>
        {
            var invalidFolder = Path.Combine(testRoot, "Not A Game");
            Directory.CreateDirectory(invalidFolder);
            try
            {
                _ = KytyEngineLauncher.ValidateGameInput(invalidFolder);
                throw new Exception("Folder without eboot.bin was accepted.");
            }
            catch (InvalidDataException) { }
        });

        check("unsupported game file type is rejected", () =>
        {
            var invalidFile = Path.Combine(testRoot, "notes.txt");
            File.WriteAllText(invalidFile, "not a game");
            try
            {
                _ = KytyEngineLauncher.ValidateGameInput(invalidFile);
                throw new Exception("Unsupported file type was accepted.");
            }
            catch (InvalidDataException) { }
        });

        check("missing Kyty executable is rejected", () =>
        {
            var missingDirectory = Path.Combine(testRoot, "Missing Kyty");
            Directory.CreateDirectory(missingDirectory);
            try
            {
                _ = KytyEngineLauncher.ResolveEmulatorPath(missingDirectory);
                throw new Exception("Missing engine executable was accepted.");
            }
            catch (FileNotFoundException) { }
        });
    }
}
