using System.Text.Json;

namespace BAModManager.Core;

/// <summary>One thing the user installed with this tool (a .dll, a .bundle, or a whole zip).</summary>
public sealed class InstalledPackage
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime InstalledAt { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Game-relative paths, always stored without the .disabled suffix.</summary>
    public List<string> Files { get; set; } = new();

    /// <summary>Preview picture that came with the mod pack (game-relative), if any.</summary>
    public string? Preview { get; set; }

    /// <summary>README.md that came with the mod pack (game-relative), if any.</summary>
    public string? Readme { get; set; }

    /// <summary>mod.json from a .bamod package (null for plain files and zips).</summary>
    public ModInfo? Info { get; set; }
}

public sealed class MelonLoaderRecord
{
    public string? Version { get; set; }
    public DateTime InstalledAt { get; set; }

    /// <summary>Top-level files/folders MelonLoader put into the game folder.</summary>
    public List<string> Files { get; set; } = new();
}

/// <summary>
/// Install record kept inside the game folder, so uninstalling knows exactly what to remove.
/// </summary>
public sealed class Manifest
{
    public const string FolderRel = "UserData/BAModManager";

    public int FormatVersion { get; set; } = 1;
    public MelonLoaderRecord? MelonLoader { get; set; }
    public List<InstalledPackage> Packages { get; set; } = new();

    /// <summary>Pictures the user picked themselves: mod key → game-relative image path.</summary>
    public Dictionary<string, string> CustomPreviews { get; set; } = new();

    public const string PreviewFolderRel = FolderRel + "/previews";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static string FilePath(string gameDir) => PathUtil.ToFull(gameDir, FolderRel + "/manifest.json");

    public static Manifest Load(string gameDir)
    {
        var path = FilePath(gameDir);
        if (!File.Exists(path)) return new Manifest();

        var m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), JsonOptions) ?? new Manifest();
        m.Packages ??= new List<InstalledPackage>();
        m.Packages.RemoveAll(p => p == null);
        foreach (var p in m.Packages) p.Files ??= new List<string>();
        if (m.MelonLoader != null) m.MelonLoader.Files ??= new List<string>();
        m.CustomPreviews ??= new Dictionary<string, string>();
        return m;
    }

    public void Save(string gameDir)
    {
        var path = FilePath(gameDir);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(tmp, path, overwrite: true);
    }
}
