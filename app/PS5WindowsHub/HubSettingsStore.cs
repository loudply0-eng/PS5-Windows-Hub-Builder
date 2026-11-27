using System.Text.Json;

namespace PS5WindowsHub;

internal sealed record HubSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string KytyDirectory { get; init; } = "";
    public string GameInput { get; init; } = "";
    public string RelinkerInput { get; init; } = "";
    public string RelinkerOutput { get; init; } = "";
}

internal static class HubSettingsStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static string SettingsPath
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
                throw new InvalidOperationException("Local application data directory is unavailable.");
            return Path.Combine(local, "PS5WindowsHub", "settings.json");
        }
    }

    internal static HubSettings Load(string? path = null)
    {
        try
        {
            var file = path ?? SettingsPath;
            if (!File.Exists(file)) return new HubSettings();
            var info = new FileInfo(file);
            if (info.Length is <= 0 or > 64 * 1024) return new HubSettings();
            var settings = JsonSerializer.Deserialize<HubSettings>(File.ReadAllText(file));
            if (settings is null || settings.SchemaVersion != CurrentSchemaVersion)
                return new HubSettings();
            return Sanitize(settings);
        }
        catch
        {
            // Settings are non-critical; corrupt or inaccessible files must not prevent startup.
            return new HubSettings();
        }
    }

    internal static void Save(HubSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var file = path ?? SettingsPath;
        var directory = Path.GetDirectoryName(Path.GetFullPath(file))
            ?? throw new InvalidOperationException("Settings directory is invalid.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var safe = Sanitize(settings);
            File.WriteAllText(temporary, JsonSerializer.Serialize(safe, JsonOptions));
            if (File.Exists(file))
            {
                try { File.Replace(temporary, file, null); }
                catch (PlatformNotSupportedException) { File.Move(temporary, file, overwrite: true); }
            }
            else
            {
                try { File.Move(temporary, file); }
                catch (IOException) when (File.Exists(file)) { File.Move(temporary, file, overwrite: true); }
            }
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private static HubSettings Sanitize(HubSettings value) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        KytyDirectory = Clean(value.KytyDirectory),
        GameInput = Clean(value.GameInput),
        RelinkerInput = Clean(value.RelinkerInput),
        RelinkerOutput = Clean(value.RelinkerOutput)
    };

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 32767 ? "" : value.Trim();
}
