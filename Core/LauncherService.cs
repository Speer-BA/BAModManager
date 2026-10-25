using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BAModManager.Core.Loc;

namespace BAModManager.Core;

/// <summary>A step of getting the game ready, for the launcher's status line.</summary>
public sealed record LaunchProgress(string Text, double? Fraction = null);

/// <summary>
/// The one-button launcher: installs/updates MelonLoader and the mod pack, starts the game without
/// Anti-Cheat - or turns the mods off and starts the normal game with Anti-Cheat.
/// </summary>
public sealed class LauncherService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    private readonly ManagerConfig _cfg;
    private readonly MelonLoaderService _ml;
    private readonly ModService _mods;

    public LauncherService(ManagerConfig cfg)
    {
        _cfg = cfg;
        _ml = new MelonLoaderService(cfg);
        _mods = new ModService(cfg);
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("BAModManager/1.0");
        return c;
    }

    // ───────────────────────── state ─────────────────────────

    /// <summary>Where version.dll is kept while mods are off, so turning them back on works offline.</summary>
    public static string ParkedProxy => Path.Combine(AppSettings.FolderPath, "vanilla", "version.dll");

    internal static string Proxy(string gameDir) => Path.Combine(gameDir, "version.dll");

    /// <summary>Mods load when MelonLoader's version.dll is in the game folder.</summary>
    public bool ModsOn(string gameDir) => _ml.IsInstalled(gameDir) && File.Exists(Proxy(gameDir));

    // ───────────────────────── modpack choice ─────────────────────────

    public const string Stable = "Stable";
    public const string Testing = "Testing";

    private static string? BuiltIn(string key)
    {
        var value = typeof(LauncherService).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
        return GitHubRepo.Parse(value);
    }

    /// <summary>
    /// The modpack this copy of the Mod Launcher is built with ("owner/repo"):
    /// build_launcher.bat Speer-BA/ActualArrow → an ActualArrow launcher; plain build_launcher.bat → none.
    /// </summary>
    public static string? BuiltInModPack => BuiltIn("DefaultModPack");

    /// <summary>The built-in Testing channel (second argument of build_launcher.bat), if any.</summary>
    public static string? BuiltInTestingPack => BuiltIn("TestingModPack");

    /// <summary>launcher.json next to the exe: the modpack a modder set up for this copy of the Mod Launcher.</summary>
    public static string ConfigFile => Path.Combine(AppContext.BaseDirectory, "launcher.json");

    /// <summary>
    /// The channels this copy offers: Stable (always, when set up) and Testing (optional).
    /// From launcher.json { "modpack": "owner/repo", "testing": "owner/repo-testing" }, else the built-in ones.
    /// </summary>
    public static IReadOnlyList<(string Name, string Repo)> Channels
    {
        get
        {
            string? stable = null, testing = null;
            bool fromFile = false;
            try
            {
                if (File.Exists(ConfigFile))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(ConfigFile));
                    if (doc.RootElement.TryGetProperty("modpack", out var v) && v.ValueKind == JsonValueKind.String)
                        stable = GitHubRepo.Parse(v.GetString());
                    if (doc.RootElement.TryGetProperty("testing", out var t) && t.ValueKind == JsonValueKind.String)
                        testing = GitHubRepo.Parse(t.GetString());
                    fromFile = stable != null;
                }
            }
            catch
            {
                // a broken file counts as not set up
            }
            if (!fromFile)
            {
                stable = BuiltInModPack;
                testing = BuiltInTestingPack;
            }

            var list = new List<(string, string)>();
            if (stable != null)
            {
                list.Add((Stable, stable));
                if (testing != null && !testing.Equals(stable, StringComparison.OrdinalIgnoreCase)) list.Add((Testing, testing));
            }
            return list;
        }
    }

    /// <summary>The Stable modpack this copy is set up for (null = not set up yet).</summary>
    public static string? ConfiguredRepo => Channels.FirstOrDefault().Repo;

    /// <summary>The channel the player picked ("Stable" when there's only one or nothing was picked).</summary>
    public static string ActiveChannel
    {
        get
        {
            var chosen = AppSettings.Load().Channel;
            var channels = Channels;
            return channels.Any(c => c.Name == chosen) ? chosen! : Stable;
        }
    }

    /// <summary>Switches channel; the other version is downloaded on the next PLAY.</summary>
    public static void SetChannel(string name)
    {
        var s = AppSettings.Load();
        s.Channel = name;
        AppSettings.Save(s);
    }

    /// <summary>The installed modpack came from another channel (or another repo) than the active one.</summary>
    public static bool InstalledFromOtherRepo()
    {
        var installed = AppSettings.Load().ModPackRepo;
        var active = ActiveRepo();
        return installed != null && active != null && !installed.Equals(active, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>For modders: saves the modpack link(s) into launcher.json (after checking each has a .bamod release).</summary>
    public async Task<string> SetUpModpackAsync(string link, string? testingLink, CancellationToken ct)
    {
        var repo = GitHubRepo.Parse(link) ?? throw new InvalidOperationException(T("That isn't a GitHub link. It should look like github.com/owner/name."));
        string? testing = null;
        if (!string.IsNullOrWhiteSpace(testingLink))
            testing = GitHubRepo.Parse(testingLink) ?? throw new InvalidOperationException(T("That isn't a GitHub link. It should look like github.com/owner/name."));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CheckTimeout);
        await GetLatestPackAsync(repo, cts.Token);   // throws when there's no release with a .bamod
        if (testing != null) await GetLatestPackAsync(testing, cts.Token);

        object config = testing == null ? new { modpack = repo } : new { modpack = repo, testing };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return repo;
    }

    /// <summary>GitHub repo of the active modpack.</summary>
    public static string? ActiveRepo()
    {
        if (AppInfo.IsLauncher)
        {
            // players can't change the modpack - only pick one of the channels the modder offers
            var channels = Channels;
            if (channels.Count == 0) return null;
            var name = ActiveChannel;
            return channels.First(c => c.Name == name).Repo;
        }
        var s = AppSettings.Load();
        return s.ModPackChosen ? s.ModPackRepo : null;
    }

    /// <summary>The active modpack's entry in the install record, if it's installed.</summary>
    public InstalledPackage? FindPack(Manifest manifest)
    {
        var repo = ActiveRepo();
        // the pack that came from this repo
        var byRepo = repo == null ? null : manifest.Packages.FirstOrDefault(p =>
            string.Equals(p.Info?.Repo, repo, StringComparison.OrdinalIgnoreCase));
        if (byRepo != null) return byRepo;
        var name = AppSettings.Load().ModPackName;
        return name == null ? null : manifest.Packages.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Makes <paramref name="pack"/> the one active modpack: switches it on and every other modpack off.</summary>
    public void Activate(string gameDir, Manifest manifest, InstalledPackage? pack, Action<string> log)
    {
        var s = AppSettings.Load();
        s.ModPackChosen = true;
        s.ModPackName = pack?.Name;
        s.ModPackRepo = pack?.Info?.Repo;
        s.ModPackTag = null;
        AppSettings.Save(s);
        EnforceOnePack(gameDir, manifest, pack, log);
    }

    /// <summary>
    /// Installs the modpack from a .bamod file (e.g. a player without good internet got it from a friend).
    /// Only the modpack this launcher is set up for is accepted; returns false for anything else.
    /// </summary>
    public bool InstallPackFile(string gameDir, string path, Action<string> log)
    {
        var repo = ActiveRepo();
        using var plan = _mods.BuildPlan(path, _ => null) ?? throw new InvalidDataException(T("The mod pack download is empty."));
        var manifest = LoadManifest(gameDir);
        var current = FindPack(manifest);
        bool matches = (repo != null && string.Equals(plan.Info?.Repo, repo, StringComparison.OrdinalIgnoreCase))
                       || (current?.Info?.Id != null && string.Equals(plan.Info?.Id, current.Info.Id, StringComparison.OrdinalIgnoreCase));
        if (!matches) return false;
        try
        {
            _mods.Execute(gameDir, manifest, plan, log);
            var pack = manifest.Packages.FirstOrDefault(p => p.Name.Equals(plan.PackageName, StringComparison.OrdinalIgnoreCase));
            if (pack?.Info != null)
            {
                pack.Info.Type ??= "modpack";
                pack.Info.Repo ??= repo;
            }
            Activate(gameDir, manifest, pack, log);
        }
        finally
        {
            manifest.Save(gameDir);
        }
        return true;
    }

    /// <summary>Only the active modpack stays on; other modpacks are switched off (not deleted).</summary>
    private void EnforceOnePack(string gameDir, Manifest manifest, InstalledPackage? active, Action<string> log)
    {
        var entries = _mods.Scan(gameDir, manifest);
        foreach (var e in entries)
        {
            if (e.Package == null) continue;
            if (e.Package == active)
            {
                if (!e.Enabled) _mods.SetEnabled(gameDir, e, true, log);
            }
            else if (e.Info?.IsModpack == true && e.Enabled)
            {
                _mods.SetEnabled(gameDir, e, false, log);
            }
        }
    }

    private static Manifest LoadManifest(string gameDir)
    {
        try { return Manifest.Load(gameDir); }
        catch { return new Manifest(); }
    }

    public string? FindPackPreview(string gameDir, Manifest manifest, InstalledPackage pack)
    {
        var entry = _mods.Scan(gameDir, manifest).FirstOrDefault(e => e.Package == pack);
        return entry == null ? null : _mods.FindPreview(gameDir, manifest, entry);
    }

    // ───────────────────────── PLAY ─────────────────────────

    /// <summary>Installs or updates MelonLoader and the mod pack, switches mods on and hides MelonLoader's console.</summary>
    public async Task PrepareModsAsync(string gameDir, IProgress<LaunchProgress> progress, Action<string> log, CancellationToken ct)
    {
        var manifest = LoadManifest(gameDir);
        try
        {
            await EnsureMelonLoaderAsync(gameDir, manifest, progress, log, ct);
            if (ActiveRepo() != null) await EnsureModPackAsync(gameDir, manifest, progress, log, ct);
            else EnforceOnePack(gameDir, manifest, FindPack(manifest), log);   // a pack from a file, or none
        }
        finally
        {
            manifest.Save(gameDir);
        }

        HideConsole(gameDir, log);
    }

    private async Task EnsureMelonLoaderAsync(string gameDir, Manifest manifest, IProgress<LaunchProgress> progress, Action<string> log, CancellationToken ct)
    {
        progress.Report(new LaunchProgress(T("Checking MelonLoader…")));
        ReleaseInfo? latest = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CheckTimeout);
            latest = await _ml.GetLatestReleaseAsync(cts.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log(T("Couldn't check for MelonLoader updates: ") + ex.Message);
        }

        bool installed = _ml.IsInstalled(gameDir);
        var current = manifest.MelonLoader?.Version ?? _ml.GetInstalledVersion(gameDir);
        bool needInstall = !installed || (latest != null && !SameVersion(current, latest.Tag));

        if (!needInstall && !File.Exists(Proxy(gameDir)))
        {
            // Mods were turned off: put version.dll back
            if (File.Exists(ParkedProxy))
            {
                File.Copy(ParkedProxy, Proxy(gameDir), overwrite: true);
                log(T("Mods turned on: version.dll is back in the game folder."));
            }
            else
            {
                needInstall = true;
            }
        }
        if (!needInstall) return;

        if (latest == null)
            throw new InvalidOperationException(T("MelonLoader couldn't be downloaded. Check your internet connection and try again."));

        var zip = TempFile(latest.AssetName);
        try
        {
            var label = installed ? T("Updating MelonLoader to {0}…", latest.Tag) : T("Downloading MelonLoader {0}…", latest.Tag);
            progress.Report(new LaunchProgress(label, 0));
            await _ml.DownloadAsync(latest.Url, zip, new Progress<double>(f => progress.Report(new LaunchProgress(label, f))), ct);
            progress.Report(new LaunchProgress(T("Installing MelonLoader…")));
            await Task.Run(() => _ml.Install(gameDir, zip, latest.Tag, manifest, log), ct);
            TryDelete(ParkedProxy); // a parked copy would be the old version now
            log(T("MelonLoader {0} installed.", latest.Tag));
        }
        finally
        {
            TryDelete(zip);
        }
    }

    private async Task EnsureModPackAsync(string gameDir, Manifest manifest, IProgress<LaunchProgress> progress, Action<string> log, CancellationToken ct,
        bool mustDownload = false)
    {
        var repo = ActiveRepo()!;
        progress.Report(new LaunchProgress(T("Checking the mod pack for updates…")));

        ReleaseInfo? release = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CheckTimeout);
            release = await GetLatestPackAsync(repo, cts.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            if (mustDownload) throw;
            log(T("Couldn't check for mod pack updates: ") + ex.Message);
        }

        var settings = AppSettings.Load();
        var pack = FindPack(manifest);
        bool missingFiles = pack != null && pack.Files.Any(f =>
            !File.Exists(PathUtil.ToFull(gameDir, f)) && !File.Exists(PathUtil.ToFull(gameDir, f) + PathUtil.DisabledSuffix));

        if (release == null)
        {
            if (pack == null)
                throw new InvalidOperationException(T("The mod pack couldn't be downloaded from {0}. Check your internet connection and try again.", "github.com/" + repo));
            EnforceOnePack(gameDir, manifest, pack, log);
            return;
        }

        // Compare version numbers (release "v1.0.5" vs mod.json "1.0.5"); fall back to the tag
        var installed = pack?.Info?.Version;
        // Same version number isn't enough when the installed copy came from the other channel (Stable ↔ Testing)
        bool sameRepo = settings.ModPackRepo == null || settings.ModPackRepo.Equals(repo, StringComparison.OrdinalIgnoreCase);
        bool upToDate = pack != null && sameRepo && (installed != null
            ? SameVersion(installed, release.Tag)
            : string.Equals(settings.ModPackTag, release.Tag, StringComparison.Ordinal));
        if (upToDate && !missingFiles)
        {
            EnforceOnePack(gameDir, manifest, pack, log);
            return;
        }

        var file = TempFile(release.AssetName);
        try
        {
            var label = pack == null || !sameRepo
                ? T("Downloading the mod pack ({0})…", release.Tag)
                : T("Updating the mod pack to {0}…", release.Tag);
            progress.Report(new LaunchProgress(label, 0));
            await DownloadAsync(release.Url, file, new Progress<double>(f => progress.Report(new LaunchProgress(label, f))), ct);

            progress.Report(new LaunchProgress(T("Installing the mod pack…")));
            var name = await Task.Run(() =>
            {
                using var plan = _mods.BuildPlan(file, _ => null)
                    ?? throw new InvalidDataException(T("The mod pack download is empty."));
                _mods.Execute(gameDir, manifest, plan, log);

                // The pack was renamed: remove the old one
                if (pack != null && !pack.Name.Equals(plan.PackageName, StringComparison.OrdinalIgnoreCase)
                    && manifest.Packages.Contains(pack))
                {
                    var old = _mods.Scan(gameDir, manifest).FirstOrDefault(e => e.Package == pack);
                    if (old != null) _mods.Remove(gameDir, manifest, old, log);
                }
                return plan.PackageName;
            }, ct);

            var active = manifest.Packages.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (active?.Info != null)
            {
                // whatever its mod.json says, it's used as a modpack and updates from this repo
                active.Info.Type ??= "modpack";
                active.Info.Repo ??= repo;
            }
            EnforceOnePack(gameDir, manifest, active, log);
            settings = AppSettings.Load();
            settings.ModPackTag = release.Tag;
            settings.ModPackName = name;
            settings.ModPackRepo = repo;
            AppSettings.Save(settings);
        }
        finally
        {
            TryDelete(file);
        }
    }

    /// <summary>The newest release of the mod pack repo and its .bamod (or .zip) download.</summary>
    internal static async Task<ReleaseInfo> GetLatestPackAsync(string repo, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(T("GitHub answered {0} for {1}.", $"{(int)resp.StatusCode} {resp.ReasonPhrase}", repo));

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "latest";

        (string Name, string Url)? zip = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name.EndsWith(".bamod", StringComparison.OrdinalIgnoreCase)) return new ReleaseInfo(tag, name, url);
            if (zip == null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) zip = (name, url);
        }
        if (zip != null) return new ReleaseInfo(tag, zip.Value.Name, zip.Value.Url);
        throw new InvalidOperationException(T("The latest release of {0} has no .bamod file.", repo));
    }

    private static async Task DownloadAsync(string url, string destination, IProgress<double> progress, CancellationToken ct)
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
            if (total > 0) progress.Report((double)done / total.Value);
        }
    }

    /// <summary>No MelonLoader console window: the launcher shows a loading screen instead.</summary>
    private static void HideConsole(string gameDir, Action<string> log)
    {
        try
        {
            var cfg = Path.Combine(gameDir, "UserData", "Loader.cfg");
            if (!File.Exists(cfg))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cfg)!);
                File.WriteAllText(cfg, "[console]\nhide_console = true\n");
                return;
            }
            var text = File.ReadAllText(cfg);
            var line = new Regex(@"^(\s*hide_console\s*=\s*)\S+", RegexOptions.Multiline);
            string updated;
            if (line.IsMatch(text)) updated = line.Replace(text, "${1}true");
            else if (Regex.IsMatch(text, @"^\s*\[console\]\s*$", RegexOptions.Multiline))
                updated = Regex.Replace(text, @"^(\s*\[console\]\s*)$", "$1\nhide_console = true", RegexOptions.Multiline);
            else updated = text.TrimEnd() + "\n\n[console]\nhide_console = true\n";
            if (updated != text) File.WriteAllText(cfg, updated);
        }
        catch (Exception ex)
        {
            log(T("Couldn't hide the MelonLoader console: ") + ex.Message);
        }
    }

    // ───────────────────────── turning mods off ─────────────────────────

    /// <summary>Removes version.dll from the game folder (a copy is kept so PLAY can turn mods back on offline).</summary>
    public void TurnOffMods(string gameDir, Action<string> log)
    {
        var proxy = Proxy(gameDir);
        if (!File.Exists(proxy)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ParkedProxy)!);
        File.Copy(proxy, ParkedProxy, overwrite: true);
        File.Delete(proxy);
        log(T("Mods turned off: version.dll was removed from the game folder."));
    }

    // ───────────────────────── starting the game ─────────────────────────

    /// <summary>False when Steam isn't running (it's started, and the player has to press the button again).</summary>
    public static bool EnsureSteam()
    {
        if (Process.GetProcessesByName("steam").Length > 0) return true;
        Process.Start(new ProcessStartInfo("steam://open/main") { UseShellExecute = true })?.Dispose();
        return false;
    }

    /// <summary>Starts the game exe directly, like Steam's "Anti-Cheat Disabled" option.</summary>
    public Process StartModded(string gameDir)
    {
        var exe = GameLocator.FindExe(gameDir) ?? throw new FileNotFoundException(T("Couldn't find the game's .exe."));
        return Start(exe, gameDir);
    }

    /// <summary>Starts the normal game with Anti-Cheat (EACLauncher.exe, like Steam's "Play Broken Arrow").</summary>
    public void StartVanilla(string gameDir)
    {
        var eac = Path.Combine(gameDir, "EACLauncher.exe");
        if (File.Exists(eac))
        {
            Start(eac, gameDir).Dispose();
            return;
        }
        var appId = GameLocator.FindSteamAppId(gameDir) ?? ManagerConfig.SteamAppId;
        Process.Start(new ProcessStartInfo($"steam://rungameid/{appId}") { UseShellExecute = true })?.Dispose();
    }

    private static Process Start(string exe, string gameDir)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = gameDir };
        // Tells the Steam API it was started by Steam, so it doesn't restart itself through Steam's default option
        var id = GameLocator.FindSteamAppId(gameDir) ?? ManagerConfig.SteamAppId;
        psi.Environment["SteamAppId"] = id;
        psi.Environment["SteamGameId"] = id;
        return Process.Start(psi) ?? throw new InvalidOperationException(T("Couldn't start the game"));
    }

    // ───────────────────────── helpers ─────────────────────────

    private static bool SameVersion(string? a, string? b)
    {
        static string Norm(string? v) => string.Join(".", Regex.Matches(v ?? "", @"\d+").Select(m => m.Value).Take(3));
        var na = Norm(a);
        return na.Length > 0 && na == Norm(b);
    }

    private static string TempFile(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "BAModManager");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Path.GetFileName(name));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* not important */ }
    }
}
