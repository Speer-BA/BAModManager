using System.Text.Json;

namespace BAModManager.Core;

/// <summary>
/// Everything the player sets in the Mod Manager, saved in %APPDATA%\BAModManager\settings.json.
/// </summary>
public static class AppSettings
{
    public sealed class Data
    {
        public string? GamePath { get; set; }
        public string? SteamFolderName { get; set; }
        public string? MelonLoaderRepo { get; set; }
        public string? MelonLoaderAsset { get; set; }
        public Dictionary<string, string>? ExtensionRules { get; set; }

        /// <summary>The player has chosen a modpack (or "no modpack") - otherwise the built-in default is used.</summary>
        public bool ModPackChosen { get; set; }

        /// <summary>GitHub "owner/repo" of the active modpack, for updates; null = no updates (installed from a file).</summary>
        public string? ModPackRepo { get; set; }

        /// <summary>Release tag of the installed modpack (used when the pack has no version number).</summary>
        public string? ModPackTag { get; set; }

        /// <summary>Name of the active modpack in the install record; null = not installed yet.</summary>
        public string? ModPackName { get; set; }

        /// <summary>Mod Launcher: the chosen channel ("Stable" / "Testing"); null = Stable.</summary>
        public string? Channel { get; set; }

        /// <summary>"en", "de", "fr", "ru" or "zh"; null = follow Windows.</summary>
        public string? Language { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>%APPDATA%\BAModManager - everything the Mod Manager keeps outside the game folder.</summary>
    public static string FolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Id);

    private static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static Data Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath), Options) ?? new Data()
                : new Data();
        }
        catch
        {
            return new Data();
        }
    }

    public static bool Save(Data data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, Options));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? LoadGamePath() => Load().GamePath;

    public static void SaveGamePath(string path)
    {
        var data = Load();
        if (string.Equals(data.GamePath, path, StringComparison.OrdinalIgnoreCase)) return;
        data.GamePath = path;
        Save(data);
    }
}
