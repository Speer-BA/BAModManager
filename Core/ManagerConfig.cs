namespace BAModManager.Core;

/// <summary>
/// Mod Manager settings. All of them can be changed on the Settings page;
/// they're stored in %APPDATA%\BAModManager\settings.json.
/// </summary>
public sealed class ManagerConfig
{
    public const string DefaultSteamFolderName = "broken_arrow";
    /// <summary>Broken Arrow on Steam - used when the app manifest can't be read.</summary>
    public const string SteamAppId = "1604270";

    public const string DefaultMelonLoaderRepo = "LavaGang/MelonLoader";
    public const string DefaultMelonLoaderAsset = "MelonLoader.x64.zip";

    public string GameName { get; } = "Broken Arrow";

    /// <summary>Folder name under steamapps\common used for auto-detection.</summary>
    public string SteamFolderName { get; set; } = DefaultSteamFolderName;

    /// <summary>GitHub "owner/repo" that MelonLoader is downloaded from.</summary>
    public string MelonLoaderRepo { get; set; } = DefaultMelonLoaderRepo;

    /// <summary>File name of the MelonLoader download in that repo's releases.</summary>
    public string MelonLoaderAsset { get; set; } = DefaultMelonLoaderAsset;

    /// <summary>
    /// Where loose files go, by extension (path relative to the game folder).
    /// Files with an extension listed here can also be enabled/disabled.
    /// </summary>
    public Dictionary<string, string> ExtensionRules { get; set; } = DefaultRules();

    /// <summary>Kept for compatibility; settings problems are now handled silently.</summary>
    public string? LoadError => null;

    public static Dictionary<string, string> DefaultRules() => new(StringComparer.OrdinalIgnoreCase)
    {
        [".dll"] = "Mods",
    };

    public static ManagerConfig Load()
    {
        var cfg = new ManagerConfig();
        var data = AppSettings.Load();
        if (!string.IsNullOrWhiteSpace(data.SteamFolderName)) cfg.SteamFolderName = data.SteamFolderName.Trim();
        if (!string.IsNullOrWhiteSpace(data.MelonLoaderRepo)) cfg.MelonLoaderRepo = data.MelonLoaderRepo.Trim();
        if (!string.IsNullOrWhiteSpace(data.MelonLoaderAsset)) cfg.MelonLoaderAsset = data.MelonLoaderAsset.Trim();
        if (data.ExtensionRules != null) cfg.ExtensionRules = NormalizeRules(data.ExtensionRules);
        return cfg;
    }

    /// <summary>Saves everything except the game folder (that's saved as soon as it's chosen).</summary>
    public bool Save()
    {
        var data = AppSettings.Load();
        data.SteamFolderName = SteamFolderName == DefaultSteamFolderName ? null : SteamFolderName;
        data.MelonLoaderRepo = MelonLoaderRepo == DefaultMelonLoaderRepo ? null : MelonLoaderRepo;
        data.MelonLoaderAsset = MelonLoaderAsset == DefaultMelonLoaderAsset ? null : MelonLoaderAsset;
        data.ExtensionRules = new Dictionary<string, string>(ExtensionRules, StringComparer.OrdinalIgnoreCase);
        return AppSettings.Save(data);
    }

    /// <summary>Case-insensitive keys that start with a dot, forward-slash folders.</summary>
    public static Dictionary<string, string> NormalizeRules(IEnumerable<KeyValuePair<string, string>> rules)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in rules)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var ext = key.Trim();
            if (!ext.StartsWith('.')) ext = "." + ext;
            result[ext.ToLowerInvariant()] = (value ?? "").Trim().Replace('\\', '/').Trim('/');
        }
        return result;
    }
}
