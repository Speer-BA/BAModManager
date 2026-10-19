using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using static BAModManager.Core.Loc;

namespace BAModManager.Core;

public sealed record ReleaseInfo(string Tag, string AssetName, string Url);

public sealed class MelonLoaderService
{
    /// <summary>Folders MelonLoader creates for the user's own content.</summary>
    public static readonly string[] UserFolders = { "Mods", "Plugins", "UserData", "UserLibs" };

    /// <summary>Loader files in the game root, used when there's no install record.</summary>
    private static readonly string[] KnownRootFiles = { "version.dll", "dobby.dll", "NOTICE.txt" };

    private static readonly HttpClient Http = CreateClient();
    private readonly ManagerConfig _cfg;

    public MelonLoaderService(ManagerConfig cfg) => _cfg = cfg;

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("BAModManager/1.1");
        return c;
    }

    public bool IsInstalled(string gameDir) => Directory.Exists(Path.Combine(gameDir, "MelonLoader"));

    public bool HasProxyDll(string gameDir) => File.Exists(Path.Combine(gameDir, "version.dll"));

    public string? GetInstalledVersion(string gameDir)
    {
        var ml = Path.Combine(gameDir, "MelonLoader");
        if (!Directory.Exists(ml)) return null;

        string[] candidates =
        {
            Path.Combine(ml, "net6", "MelonLoader.dll"),
            Path.Combine(ml, "net8", "MelonLoader.dll"),
            Path.Combine(ml, "net35", "MelonLoader.dll"),
            Path.Combine(ml, "MelonLoader.dll"),
        };
        foreach (var dll in candidates)
        {
            if (!File.Exists(dll)) continue;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(dll);
                var v = info.ProductVersion ?? info.FileVersion;
                if (!string.IsNullOrWhiteSpace(v)) return "v" + v.Split('+')[0].Trim();
            }
            catch
            {
                // ignore
            }
        }
        return null;
    }

    public async Task<ReleaseInfo> GetLatestReleaseAsync(CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_cfg.MelonLoaderRepo}/releases/latest";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                T("GitHub answered {0}. Try again later, or download {1} from {2} yourself and use \"Install from zip…\".",
                    $"{(int)resp.StatusCode} {resp.ReasonPhrase}", _cfg.MelonLoaderAsset, $"github.com/{_cfg.MelonLoaderRepo}/releases"));

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "unknown";

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (string.Equals(name, _cfg.MelonLoaderAsset, StringComparison.OrdinalIgnoreCase))
                return new ReleaseInfo(tag, name!, asset.GetProperty("browser_download_url").GetString()!);
        }
        throw new InvalidOperationException(T("MelonLoader {0} has no {1} download.", tag, _cfg.MelonLoaderAsset));
    }

    public async Task DownloadAsync(string url, string destination, IProgress<double>? progress, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destination);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            if (total > 0) progress?.Report((double)done / total.Value);
        }
    }

    /// <summary>Extracts a MelonLoader zip into the game folder, replacing an older MelonLoader.</summary>
    public void Install(string gameDir, string zipPath, string? versionTag, Manifest manifest, Action<string> log)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.Where(e => e.Name.Length > 0).ToList();

        // Official zips are flat; tolerate one wrapper folder around it.
        var rels = entries.Select(e => e.FullName.Replace('\\', '/').TrimStart('/')).ToList();
        string prefix = "";
        if (!rels.Any(r => r.StartsWith("MelonLoader/", StringComparison.OrdinalIgnoreCase)))
        {
            var wrapped = rels.FirstOrDefault(r => r.Contains("/MelonLoader/", StringComparison.OrdinalIgnoreCase));
            if (wrapped == null)
                throw new InvalidDataException(T("This zip doesn't look like MelonLoader (there's no MelonLoader folder inside)."));
            prefix = wrapped[..(wrapped.IndexOf("/MelonLoader/", StringComparison.OrdinalIgnoreCase) + 1)];
        }

        var loaderDir = Path.Combine(gameDir, "MelonLoader");
        if (Directory.Exists(loaderDir))
        {
            log(T("Removing the old MelonLoader folder…"));
            Directory.Delete(loaderDir, recursive: true);
        }

        var topLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var rel = rels[i];
            if (prefix.Length > 0)
            {
                if (!rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                rel = rel[prefix.Length..];
            }
            rel = PathUtil.NormalizeRel(rel);
            if (rel.Length == 0) continue;

            var full = PathUtil.ToFull(gameDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            entries[i].ExtractToFile(full, overwrite: true);
            topLevel.Add(PathUtil.FirstSegment(rel));
            count++;
        }
        log(T("Extracted {0} files.", count));

        // Root files from the previous version that the new one no longer ships
        if (manifest.MelonLoader != null)
        {
            foreach (var old in manifest.MelonLoader.Files)
            {
                if (topLevel.Contains(old) || UserFolders.Contains(old, StringComparer.OrdinalIgnoreCase)) continue;
                var full = PathUtil.ToFull(gameDir, old);
                if (File.Exists(full))
                {
                    FileOps.DeleteFile(full);
                    log(T("Removed leftover {0}", old));
                }
            }
        }

        foreach (var folder in UserFolders)
            Directory.CreateDirectory(Path.Combine(gameDir, folder));

        topLevel.ExceptWith(UserFolders);
        manifest.MelonLoader = new MelonLoaderRecord
        {
            Version = versionTag ?? GetInstalledVersion(gameDir),
            InstalledAt = DateTime.Now,
            Files = topLevel.OrderBy(f => f).ToList(),
        };
    }

    /// <summary>Removes MelonLoader (to the Recycle Bin). Optionally also every mod and its settings.</summary>
    public void Uninstall(string gameDir, Manifest manifest, bool removeUserContent, Action<string> log)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MelonLoader" };
        foreach (var f in KnownRootFiles) targets.Add(f);
        if (manifest.MelonLoader != null)
            foreach (var f in manifest.MelonLoader.Files) targets.Add(f);
        targets.ExceptWith(UserFolders);

        foreach (var t in targets)
        {
            var full = PathUtil.ToFull(gameDir, t);
            if (Directory.Exists(full))
            {
                FileOps.DeleteDirectory(full);
                log(T("Removed folder {0}", t));
            }
            else if (File.Exists(full))
            {
                FileOps.DeleteFile(full);
                log(T("Removed {0}", t));
            }
        }

        if (removeUserContent)
        {
            foreach (var folder in UserFolders)
            {
                var full = Path.Combine(gameDir, folder);
                if (!Directory.Exists(full)) continue;
                FileOps.DeleteDirectory(full);
                log(T("Removed folder {0}", folder));
            }
            manifest.MelonLoader = null;
            manifest.Packages.Clear();
        }
        else
        {
            manifest.MelonLoader = null;
            manifest.Save(gameDir);
            log(T("Kept Mods, Plugins, UserData and UserLibs."));
        }
    }
}
