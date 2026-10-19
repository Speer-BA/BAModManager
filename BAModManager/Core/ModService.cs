using System.IO.Compression;
using static BAModManager.Core.Loc;

namespace BAModManager.Core;

/// <summary>One row in the mod list.</summary>
public sealed class ModEntry
{
    public required string Name { get; init; }
    public required string TypeText { get; init; }
    public required string Location { get; init; }
    public bool Enabled { get; init; }
    public int MissingFiles { get; init; }

    /// <summary>Set for things installed with this tool.</summary>
    public InstalledPackage? Package { get; init; }

    /// <summary>Set for files found in the game folder that this tool didn't install.</summary>
    public string? LooseRel { get; init; }

    public bool IsTracked => Package != null;

    /// <summary>mod.json of a .bamod package, if any.</summary>
    public ModInfo? Info => Package?.Info;

    /// <summary>What the mod's DLL says about itself ([MelonInfo], assembly attributes), if it has one.</summary>
    public DllMeta? Meta { get; init; }

    /// <summary>The DLL file name this entry was named after, when the name comes from [MelonInfo].</summary>
    public string? FileName { get; init; }

    public string? Id => Info?.Id;
    public string? Version => Info?.Version ?? Meta?.Version;
    public string? Author => Info?.Author ?? Meta?.Author;
    public string? Description => Info?.Description ?? Meta?.Description;
    public string? Website => Info?.Website ?? Meta?.DownloadLink;
}

public sealed class PlannedFile
{
    public required string TargetRel { get; init; }
    public required Func<Stream> Open { get; init; }
}

public sealed class InstallPlan : IDisposable
{
    public required string PackageName { get; set; }
    public required string Source { get; init; }
    public List<PlannedFile> Files { get; } = new();

    /// <summary>mod.json found in the package.</summary>
    public ModInfo? Info { get; set; }

    /// <summary>README.md / readme.txt found at the root of the package.</summary>
    public Func<Stream>? ReadmeOpen { get; set; }
    public List<string> Skipped { get; } = new();
    public IDisposable? Owner { get; set; }

    /// <summary>preview.png / .jpg found at the root of a mod pack.</summary>
    public Func<Stream>? PreviewOpen { get; set; }
    public string PreviewExtension { get; set; } = ".png";
    public void Dispose() => Owner?.Dispose();
}

public sealed class ModService
{
    private static readonly HashSet<string> KnownRoots =
        new(StringComparer.OrdinalIgnoreCase) { "Mods", "Plugins", "UserData", "UserLibs" };

    /// <summary>File names recognised as a mod's preview picture.</summary>
    private static readonly string[] PreviewNames = { "preview.png", "preview.jpg", "preview.jpeg", "thumbnail.png", "icon.png" };
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly ManagerConfig _cfg;

    public ModService(ManagerConfig cfg) => _cfg = cfg;

    /// <summary>Stable id of a list entry (used for selection and custom pictures).</summary>
    public static string Key(ModEntry e) =>
        e.Package != null ? "p:" + (e.Package.Info?.Id ?? e.Package.Name) : "f:" + StripDisabled(e.LooseRel ?? "");

    /// <summary>File extensions treated as mod packages (zip files).</summary>
    public static readonly string[] PackageExtensions = { ".bamod", ".zip" };

    private static readonly string[] ReadmeNames = { "README.md", "readme.txt", "README.txt" };

    /// <summary>The installed package an install would replace: same mod.json id, or else same name.</summary>
    public static InstalledPackage? FindSame(Manifest manifest, InstallPlan plan)
    {
        if (plan.Info?.Id is { } id)
        {
            var byId = manifest.Packages.FirstOrDefault(p => string.Equals(p.Info?.Id, id, StringComparison.OrdinalIgnoreCase));
            if (byId != null) return byId;
        }
        return manifest.Packages.FirstOrDefault(p => p.Name.Equals(plan.PackageName, StringComparison.OrdinalIgnoreCase));
    }

    // ───────────────────────── helpers ─────────────────────────

    public static string StripDisabled(string rel) =>
        rel.EndsWith(PathUtil.DisabledSuffix, StringComparison.OrdinalIgnoreCase)
            ? rel[..^PathUtil.DisabledSuffix.Length]
            : rel;

    private static bool IsDisabledName(string rel) =>
        rel.EndsWith(PathUtil.DisabledSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Files whose extension has a rule (.dll, .bundle…) are switched on/off by renaming.</summary>
    private static string? ExistingRel(string gameDir, string rel)
    {
        try
        {
            if (File.Exists(PathUtil.ToFull(gameDir, rel))) return rel;
            var off = rel + PathUtil.DisabledSuffix;
            if (File.Exists(PathUtil.ToFull(gameDir, off))) return off;
        }
        catch
        {
            // bad path in record
        }
        return null;
    }

    private IEnumerable<string> ContentDirs() =>
        new[] { "Mods", "Plugins" }
            .Concat(_cfg.ExtensionRules.Values)
            .Select(v => v.Replace('\\', '/').Trim('/'))
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private HashSet<string> ProtectedDirs()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "." };
        foreach (var r in KnownRoots) set.Add(r);
        foreach (var d in ContentDirs()) set.Add(d);
        return set;
    }

    /// <summary>What kind of mod this is, for the Type column: "Mod", "Model pack", "Mod + models"…</summary>
    public static string Kind(IEnumerable<string> files)
    {
        var (mods, plugins, libs, bundles, _) = Count(files);
        bool code = mods + plugins + libs > 0;
        if (code && bundles > 0) return T("Mod + models");
        if (mods > 0) return T("Mod");
        if (plugins > 0) return T("Plugin");
        if (libs > 0) return T("Library");
        if (bundles > 0) return bundles == 1 ? T("Model") : T("Model pack");
        return T("Files");
    }

    /// <summary>Exact breakdown of the files, e.g. "1 mod + 7 models".</summary>
    public static string Describe(IEnumerable<string> files)
    {
        var (mods, plugins, libs, bundles, other) = Count(files);
        var parts = new List<string>();
        if (mods > 0) parts.Add(mods == 1 ? T("1 mod") : T("{0} mods", mods));
        if (plugins > 0) parts.Add(plugins == 1 ? T("1 plugin") : T("{0} plugins", plugins));
        if (libs > 0) parts.Add(libs == 1 ? T("1 library") : T("{0} libraries", libs));
        if (bundles > 0) parts.Add(bundles == 1 ? T("1 model") : T("{0} models", bundles));
        if (other > 0) parts.Add(other == 1 ? T("1 file") : T("{0} files", other));
        return parts.Count == 0 ? "-" : string.Join(" + ", parts);
    }

    private static (int mods, int plugins, int libs, int bundles, int other) Count(IEnumerable<string> files)
    {
        int mods = 0, plugins = 0, libs = 0, bundles = 0, other = 0;
        foreach (var f0 in files)
        {
            var f = StripDisabled(f0);
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var top = PathUtil.FirstSegment(f);
            if (ext == ".dll")
            {
                if (top.Equals("Plugins", StringComparison.OrdinalIgnoreCase)) plugins++;
                else if (top.Equals("UserLibs", StringComparison.OrdinalIgnoreCase)) libs++;
                else mods++;
            }
            else if (ext is ".bundle" or ".unity3d" or ".assets" or ".ab") bundles++;
            else other++;
        }
        return (mods, plugins, libs, bundles, other);
    }

    private static string Locations(IEnumerable<string> files)
    {
        var dirs = files.Select(f => PathUtil.DirOf(StripDisabled(f)))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return dirs.Count switch
        {
            0 => "-",
            <= 3 => string.Join(", ", dirs),
            _ => string.Join(", ", dirs.Take(3)) + ", …",
        };
    }

    // ───────────────────────── listing ─────────────────────────

    public List<ModEntry> Scan(string gameDir, Manifest manifest)
    {
        var result = new List<ModEntry>();
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in manifest.Packages.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var f in p.Files)
            {
                owned.Add(f);
                owned.Add(f + PathUtil.DisabledSuffix);
            }
            DllMeta? meta = null;
            if (p.Info == null)
            {
                var dll = p.Files.FirstOrDefault(f => Path.GetExtension(f).Equals(".dll", StringComparison.OrdinalIgnoreCase)
                                                      && !PathUtil.FirstSegment(f).Equals("UserLibs", StringComparison.OrdinalIgnoreCase));
                if (dll != null && ExistingRel(gameDir, dll) is { } dllRel)
                    meta = DllMetaReader.Read(PathUtil.ToFull(gameDir, dllRel));
            }
            result.Add(new ModEntry
            {
                Name = p.Name,
                TypeText = Kind(p.Files),
                Location = Locations(p.Files),
                Enabled = p.Enabled,
                MissingFiles = p.Files.Count(f => ExistingRel(gameDir, f) == null),
                Package = p,
                Meta = meta,
            });
        }

        // Files that were put there by hand (or by another tool)
        var loose = new List<ModEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dirRel in ContentDirs())
        {
            string full;
            try { full = PathUtil.ToFull(gameDir, dirRel); }
            catch { continue; }
            if (!Directory.Exists(full)) continue;

            bool loaderDir = dirRel.Equals("Mods", StringComparison.OrdinalIgnoreCase)
                             || dirRel.Equals("Plugins", StringComparison.OrdinalIgnoreCase);
            List<string> files;
            try
            {
                files = Directory.EnumerateFiles(full, "*",
                    loaderDir ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var f in files)
            {
                var rel = PathUtil.ToRel(gameDir, f);
                if (!seen.Add(rel) || owned.Contains(rel)) continue;
                if (rel.StartsWith(Manifest.FolderRel + "/", StringComparison.OrdinalIgnoreCase)) continue;

                var baseRel = StripDisabled(rel);
                var ext = Path.GetExtension(baseRel);
                bool wanted = loaderDir
                    ? ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                    : _cfg.ExtensionRules.ContainsKey(ext);
                if (!wanted) continue;

                var meta = ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ? DllMetaReader.Read(f) : null;
                var fileName = Path.GetFileName(baseRel);
                loose.Add(new ModEntry
                {
                    Name = meta?.Name ?? fileName,
                    FileName = meta?.Name != null ? fileName : null,
                    TypeText = Kind(new[] { baseRel }),
                    Location = PathUtil.DirOf(baseRel),
                    Enabled = !IsDisabledName(rel),
                    LooseRel = rel,
                    Meta = meta,
                });
            }
        }

        result.AddRange(loose.OrderBy(e => e.Location, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase));
        return result;
    }

    // ───────────────────────── installing ─────────────────────────

    /// <summary>
    /// Works out where everything goes. <paramref name="askDestination"/> is called for a loose file
    /// whose extension has no rule; it returns a game-relative folder, or null to skip the file.
    /// Returns null if nothing should be installed.
    /// </summary>
    public InstallPlan? BuildPlan(string path, Func<string, string?> askDestination)
    {
        if (Directory.Exists(path))
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => (Rel: Path.GetRelativePath(root, f).Replace('\\', '/'),
                              Open: (Func<Stream>)(() => File.OpenRead(f))))
                .ToList();
            var plan = new InstallPlan { PackageName = Path.GetFileName(root), Source = path };
            FillFromTree(plan, entries);
            return plan;
        }

        if (!File.Exists(path)) throw new FileNotFoundException(T("File not found."), path);
        var ext = Path.GetExtension(path);

        if (PackageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            ZipArchive zip;
            try
            {
                zip = ZipFile.OpenRead(path);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException(T("This isn't a valid mod package - it can't be opened as a zip file."));
            }
            try
            {
                var entries = zip.Entries
                    .Where(e => e.Name.Length > 0)
                    .Select(e => (Rel: e.FullName.Replace('\\', '/'), Open: (Func<Stream>)e.Open))
                    .ToList();
                var plan = new InstallPlan
                {
                    PackageName = Path.GetFileNameWithoutExtension(path),
                    Source = path,
                    Owner = zip,
                };
                FillFromTree(plan, entries);
                return plan;
            }
            catch (InvalidDataException) when (zip.Entries.Count == 0)
            {
                zip.Dispose();
                throw new InvalidDataException(T("This package is empty."));
            }
            catch
            {
                zip.Dispose();
                throw;
            }
        }

        if (ext.Equals(PathUtil.DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(T("This file is a disabled mod. Rename it (remove .disabled) first."));

        var name = Path.GetFileName(path);
        string? destDir;
        if (_cfg.ExtensionRules.TryGetValue(ext, out var ruleDir)) destDir = ruleDir;
        else
        {
            destDir = askDestination(path);
            if (destDir == null) return null;
        }

        var single = new InstallPlan { PackageName = name, Source = path };
        single.Files.Add(new PlannedFile
        {
            TargetRel = PathUtil.NormalizeRel(PathUtil.JoinRel(destDir, name)),
            Open = () => File.OpenRead(path),
        });
        return single;
    }

    /// <summary>
    /// Zips/folders: if they contain Mods/, UserData/… folders, that layout is kept as-is.
    /// Otherwise each file goes where its extension rule says (and unknown files like readmes are skipped).
    /// </summary>
    private void FillFromTree(InstallPlan plan, List<(string Rel, Func<Stream> Open)> entries)
    {
        var items = entries.Select(e => (Rel: e.Rel.Trim('/'), e.Open)).Where(e => e.Rel.Length > 0).ToList();

        // Strip a single wrapper folder, e.g. "MyMod-1.2/Mods/MyMod.dll"
        string? wrapper = null;
        var tops = items.Select(i => PathUtil.FirstSegment(i.Rel)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool rootsAtTop = items.Any(i => i.Rel.Contains('/') && KnownRoots.Contains(PathUtil.FirstSegment(i.Rel)));
        if (!rootsAtTop && tops.Count == 1 && items.All(i => i.Rel.Contains('/'))) wrapper = tops[0];

        var targets = new Dictionary<string, PlannedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rel0, open) in items)
        {
            var rel = wrapper != null ? rel0[(wrapper.Length + 1)..] : rel0;

            if (PathUtil.FirstSegment(rel).Equals("MelonLoader", StringComparison.OrdinalIgnoreCase)
                || rel.Equals("version.dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    T("This looks like MelonLoader itself. Use \"Install from zip…\" in the MelonLoader section instead."));

            if (!rel.Contains('/') && PreviewNames.Contains(rel, StringComparer.OrdinalIgnoreCase))
            {
                // "preview.*" wins over "thumbnail.png" / "icon.png"
                if (plan.PreviewOpen == null || rel.StartsWith("preview", StringComparison.OrdinalIgnoreCase))
                {
                    plan.PreviewOpen = open;
                    plan.PreviewExtension = Path.GetExtension(rel).ToLowerInvariant();
                }
                continue;
            }

            if (rel.Equals(ModInfo.FileName, StringComparison.OrdinalIgnoreCase))
            {
                string json;
                using (var s = open())
                using (var reader = new StreamReader(s))
                    json = reader.ReadToEnd();
                try
                {
                    plan.Info = ModInfo.Parse(json);
                }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
                {
                    throw new InvalidDataException(T("mod.json in this package isn't valid: {0}", ex.Message));
                }
                continue;
            }

            if (!rel.Contains('/') && ReadmeNames.Contains(rel, StringComparer.OrdinalIgnoreCase))
            {
                plan.ReadmeOpen = open;
                continue;
            }

            var segs = rel.Split('/');
            string target;
            if (segs.Length > 1 && KnownRoots.TryGetValue(segs[0], out var canonical))
                target = canonical + "/" + string.Join('/', segs.Skip(1));
            else if (_cfg.ExtensionRules.TryGetValue(Path.GetExtension(rel), out var dir))
                target = PathUtil.JoinRel(dir, segs[^1]);
            else
            {
                plan.Skipped.Add(rel);
                continue;
            }

            target = PathUtil.NormalizeRel(target);
            targets[target] = new PlannedFile { TargetRel = target, Open = open };
        }

        plan.Files.AddRange(targets.Values.OrderBy(f => f.TargetRel, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(plan.Info?.Name)) plan.PackageName = plan.Info.Name!;
    }

    /// <summary>Files that already exist and would be overwritten (ignoring an older copy of the same package).</summary>
    public List<string> FindConflicts(string gameDir, Manifest manifest, InstallPlan plan)
    {
        var same = FindSame(manifest, plan);
        var result = new List<string>();
        foreach (var f in plan.Files)
        {
            if (same != null && same.Files.Contains(f.TargetRel, StringComparer.OrdinalIgnoreCase)) continue;
            if (ExistingRel(gameDir, f.TargetRel) != null) result.Add(f.TargetRel);
        }
        return result;
    }

    public void Execute(string gameDir, Manifest manifest, InstallPlan plan, Action<string> log)
    {
        var newTargets = plan.Files.Select(f => f.TargetRel).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Installing the same mod again = update: drop the old version's files first
        var same = FindSame(manifest, plan);
        if (same != null)
        {
            log(same.Info?.Version != null && plan.Info?.Version != null
                ? T("Updating {0} {1} → {2}…", same.Name, same.Info.Version, plan.Info.Version)
                : T("Updating {0}…", same.Name));
            RemovePackageFiles(gameDir, same, newTargets, log);
            DeletePackPreview(gameDir, same);
            manifest.Packages.Remove(same);
        }

        // A file can only belong to one package
        foreach (var other in manifest.Packages.ToList())
        {
            if (other.Files.RemoveAll(newTargets.Contains) > 0 && other.Files.Count == 0)
                manifest.Packages.Remove(other);
        }

        foreach (var f in plan.Files)
        {
            var full = PathUtil.ToFull(gameDir, f.TargetRel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var disabledTwin = full + PathUtil.DisabledSuffix;
            if (File.Exists(disabledTwin)) File.Delete(disabledTwin);

            using (var src = f.Open())
            using (var dst = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None))
                src.CopyTo(dst);

            log($"  + {f.TargetRel}");
        }

        foreach (var s in plan.Skipped) log("  (" + T("skipped {0}", s) + ")");

        string? previewRel = null;
        if (plan.PreviewOpen != null)
        {
            previewRel = $"{Manifest.PreviewFolderRel}/{SafeFileName(plan.PackageName)}{plan.PreviewExtension}";
            var full = PathUtil.ToFull(gameDir, previewRel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            using (var src = plan.PreviewOpen())
            using (var dst = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None))
                src.CopyTo(dst);
            log("  + " + T("preview picture"));
        }

        string? readmeRel = null;
        if (plan.ReadmeOpen != null)
        {
            readmeRel = $"{Manifest.FolderRel}/info/{SafeFileName(plan.Info?.Id ?? plan.PackageName)}.md";
            var full = PathUtil.ToFull(gameDir, readmeRel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            using (var src = plan.ReadmeOpen())
            using (var dst = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None))
                src.CopyTo(dst);
        }

        manifest.Packages.Add(new InstalledPackage
        {
            Name = plan.PackageName,
            Source = plan.Source,
            InstalledAt = DateTime.Now,
            Enabled = true,
            Files = plan.Files.Select(f => f.TargetRel).ToList(),
            Preview = previewRel,
            Readme = readmeRel,
            Info = plan.Info,
        });
        log(plan.Info?.Version != null ? T("Installed {0}.", $"{plan.PackageName} {plan.Info.Version}") : T("Installed {0}.", plan.PackageName));
    }

    // ───────────────────────── removing / toggling ─────────────────────────

    private void RemovePackageFiles(string gameDir, InstalledPackage pkg, ISet<string>? keep, Action<string> log)
    {
        var protectedDirs = ProtectedDirs();
        foreach (var f in pkg.Files)
        {
            // Files the update writes again are kept - but a switched-off copy of them is still removed
            var candidates = keep != null && keep.Contains(f)
                ? new[] { f + PathUtil.DisabledSuffix }
                : new[] { f, f + PathUtil.DisabledSuffix };
            foreach (var candidate in candidates)
            {
                string full;
                try { full = PathUtil.ToFull(gameDir, candidate); }
                catch { continue; }
                if (!File.Exists(full)) continue;
                FileOps.DeleteFile(full);
                FileOps.RemoveEmptyParents(gameDir, full, protectedDirs);
                log($"  - {candidate}");
            }
        }
    }

    public void Remove(string gameDir, Manifest manifest, ModEntry entry, Action<string> log)
    {
        ClearCustomPreview(gameDir, manifest, entry);
        if (entry.Package != null)
        {
            RemovePackageFiles(gameDir, entry.Package, null, log);
            DeletePackPreview(gameDir, entry.Package);
            manifest.Packages.Remove(entry.Package);
        }
        else if (entry.LooseRel != null)
        {
            var full = PathUtil.ToFull(gameDir, entry.LooseRel);
            FileOps.DeleteFile(full);
            FileOps.RemoveEmptyParents(gameDir, full, ProtectedDirs());
            log($"  - {entry.LooseRel}");
        }
        log(T("Removed {0}.", entry.Name));
    }

    public void SetEnabled(string gameDir, ModEntry entry, bool enabled, Action<string> log)
    {
        if (entry.Package != null)
        {
            // Every file of the mod is renamed, so model-only or asset-only mods really switch off too
            foreach (var f in entry.Package.Files)
            {
                var on = PathUtil.ToFull(gameDir, f);
                var off = on + PathUtil.DisabledSuffix;
                if (enabled && File.Exists(off)) File.Move(off, on, overwrite: true);
                else if (!enabled && File.Exists(on)) File.Move(on, off, overwrite: true);
            }
            entry.Package.Enabled = enabled;
        }
        else if (entry.LooseRel != null)
        {
            var current = PathUtil.ToFull(gameDir, entry.LooseRel);
            var isOff = IsDisabledName(entry.LooseRel);
            if (enabled && isOff) File.Move(current, current[..^PathUtil.DisabledSuffix.Length], overwrite: true);
            else if (!enabled && !isOff) File.Move(current, current + PathUtil.DisabledSuffix, overwrite: true);
        }
        log(enabled ? T("Enabled {0}.", entry.Name) : T("Disabled {0}.", entry.Name));
    }

    // ───────────────────────── preview pictures ─────────────────────────

    private static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => bad.Contains(c) || c == ':' ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "mod" : clean;
    }

    /// <summary>Removes the preview picture and readme that came with a package.</summary>
    private static void DeletePackPreview(string gameDir, InstalledPackage pkg)
    {
        foreach (var rel in new[] { pkg.Preview, pkg.Readme })
        {
            if (string.IsNullOrEmpty(rel)) continue;
            try { File.Delete(PathUtil.ToFull(gameDir, rel)); }
            catch { /* not important */ }
        }
    }

    public static string? ReadmePath(string gameDir, ModEntry entry) =>
        entry.Package?.Readme is { } rel ? ExistingFile(gameDir, rel) : null;

    private static string? ExistingFile(string gameDir, string rel)
    {
        try
        {
            var full = PathUtil.ToFull(gameDir, rel);
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Full path of the picture to show for a mod, or null for the default.
    /// Order: preview.png from the mod pack →
    /// UserData\&lt;ModName&gt;\preview.png for a .dll → &lt;name&gt;.png next to a bundle.
    /// </summary>
    public string? FindPreview(string gameDir, Manifest manifest, ModEntry entry)
    {
        if (entry.Package?.Preview is { } packPreview && ExistingFile(gameDir, packPreview) is { } p)
            return p;

        var files = entry.Package != null ? entry.Package.Files : new List<string> { StripDisabled(entry.LooseRel ?? "") };
        foreach (var file in files.OrderBy(f => Path.GetExtension(f).Equals(".dll", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
        {
            var noExt = file[..^Path.GetExtension(file).Length];
            var name = Path.GetFileName(noExt);
            var candidates = Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase)
                ? ImageExtensions.Select(x => $"UserData/{name}/preview{x}")
                : ImageExtensions.Select(x => noExt + x);
            foreach (var cand in candidates)
                if (ExistingFile(gameDir, cand) is { } found) return found;
        }
        return null;
    }

    /// <summary>Where a picture chosen by the user is stored for this mod.</summary>
    public static string CustomPreviewRel(ModEntry entry) =>
        $"{Manifest.PreviewFolderRel}/custom_{SafeFileName(Key(entry).Replace('/', '_'))}.png";

    public void ClearCustomPreview(string gameDir, Manifest manifest, ModEntry entry)
    {
        var key = Key(entry);
        if (!manifest.CustomPreviews.TryGetValue(key, out var rel)) return;
        try { File.Delete(PathUtil.ToFull(gameDir, rel)); }
        catch { /* not important */ }
        manifest.CustomPreviews.Remove(key);
    }
}
