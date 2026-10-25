using System.Diagnostics;
using BAModManager.Core;
using BAModManager.Ui;
using static BAModManager.Core.Loc;

namespace BAModManager;

/// <summary>Everything the buttons do.</summary>
public sealed partial class MainForm
{
    // ───────────────────────── game folder / play ─────────────────────────

    private void BrowseGame()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Select the {0} game folder (the one with the .exe)", _cfg.GameName),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (Directory.Exists(GameDir)) dlg.InitialDirectory = GameDir;
        if (dlg.ShowDialog(this) == DialogResult.OK) _gamePath.Text = dlg.SelectedPath;
    }

    private void DetectGame(bool showFailure)
    {
        var found = GameLocator.AutoDetect(_cfg.SteamFolderName);
        if (found != null)
        {
            _gamePath.Text = found;
            Log(T("Found the game at {0}", found));
        }
        else if (showFailure)
        {
            MessageBox.Show(this,
                T("Couldn't find {0} in your Steam libraries.\nUse Browse… to pick the game folder yourself.", _cfg.GameName),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Play()
    {
        if (!GameValid)
        {
            ShowPage("settings");
            return;
        }
        var dir = PathUtil.RealCase(GameDir);
        if (GameLocator.IsGameRunning(dir))
        {
            Log(T("{0} is already running.", _cfg.GameName));
            return;
        }
        if (!_ml.IsInstalled(dir) && _entries.Any(e => e.Enabled))
            Log(T("Note: MelonLoader isn't installed, so mods won't load."));

        try
        {
            var appId = GameLocator.FindSteamAppId(dir);
            var exe = GameLocator.FindExe(dir);

            // MelonLoader installed → never go through Steam's default launch option (EACLauncher.exe).
            // Start the game exe directly, like Steam's "Anti-Cheat Disabled" option does.
            if (_ml.IsInstalled(dir))
            {
                if (exe == null)
                {
                    Log(T("Couldn't find the game's .exe."));
                    return;
                }
                if (Process.GetProcessesByName("steam").Length == 0)
                {
                    Process.Start(new ProcessStartInfo("steam://open/main") { UseShellExecute = true })?.Dispose();
                    Log(T("Steam isn't running. Starting Steam - press PLAY again once it's open."));
                    return;
                }
                var direct = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = dir };
                // Tells the Steam API it was started by Steam, so it doesn't restart through the default (Anti-Cheat) option
                var id = appId ?? ManagerConfig.SteamAppId;
                direct.Environment["SteamAppId"] = id;
                direct.Environment["SteamGameId"] = id;
                Process.Start(direct)?.Dispose();
                Log(T("Launching {0} with Anti-Cheat disabled (MelonLoader is installed)…", _cfg.GameName));
                return;
            }

            ProcessStartInfo psi;
            if (appId != null)
                psi = new ProcessStartInfo($"steam://rungameid/{appId}") { UseShellExecute = true };
            else if (exe != null)
                psi = new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = dir };
            else
            {
                Log(T("Couldn't find the game's .exe."));
                return;
            }
            Process.Start(psi)?.Dispose();
            Log(T("Launching {0}…", _cfg.GameName));
        }
        catch (Exception ex)
        {
            Log(T("Couldn't start the game: ") + ex.Message);
            MessageBox.Show(this, ex.Message, T("Couldn't start the game"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenPath(string path)
    {
        try
        {
            if (Directory.Exists(path) || File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
            else
                MessageBox.Show(this, T("This doesn't exist yet:\n{0}", path), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(T("Couldn't open: ") + ex.Message);
        }
    }

    private void OpenLoaderLog()
    {
        var log = Path.Combine(GameDir, "MelonLoader", "Latest.log");
        if (File.Exists(log)) OpenPath(log);
        else
            MessageBox.Show(this, T("There's no Latest.log yet. It appears after you start the game with MelonLoader installed."),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ───────────────────────── MelonLoader ─────────────────────────

    private async Task InstallLatestMelonLoader()
    {
        if (!CheckReady()) return;
        var dir = GameDir;
        if (_ml.IsInstalled(dir) && _ml.HasProxyDll(dir) &&
            MessageBox.Show(this,
                T("Download the latest MelonLoader release and reinstall it?\n\nYour mods and their settings are kept."),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        await RunBusy(T("Installing MelonLoader…"), async () =>
        {
            Log(T("Looking up the latest MelonLoader release on GitHub…"));
            var release = await _ml.GetLatestReleaseAsync(CancellationToken.None);
            Log(T("Latest is {0}. Downloading {1}…", release.Tag, release.AssetName));

            var tmp = Path.Combine(Path.GetTempPath(), $"BAModManager_{Guid.NewGuid():N}.zip");
            try
            {
                _progress.Marquee = false;
                var progress = new Progress<double>(p => _progress.Value = (float)p);
                await _ml.DownloadAsync(release.Url, tmp, progress, CancellationToken.None);
                _progress.Marquee = true;

                Log(T("Installing…"));
                await Task.Run(() =>
                {
                    _ml.Install(dir, tmp, release.Tag, _manifest, Log);
                    _manifest.Save(dir);
                });
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* temp file */ }
            }

            Log(T("MelonLoader {0} installed.", release.Tag));
            MessageBox.Show(this,
                T("MelonLoader {0} is installed.\n\nThe first launch of {1} will take a few minutes while MelonLoader prepares the game.", release.Tag, _cfg.GameName),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private async Task InstallMelonLoaderFromZip()
    {
        if (!CheckReady()) return;
        using var dlg = new OpenFileDialog
        {
            Title = T("Choose a MelonLoader zip (e.g. MelonLoader.x64.zip)"),
            Filter = T("MelonLoader zip") + " (*.zip)|*.zip",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var dir = GameDir;
        var zip = dlg.FileName;
        await RunBusy(T("Installing MelonLoader…"), () => Task.Run(() =>
        {
            Log(T("Installing MelonLoader from {0}…", Path.GetFileName(zip)));
            _ml.Install(dir, zip, null, _manifest, Log);
            _manifest.Save(dir);
            Log(T("MelonLoader {0} installed.", _manifest.MelonLoader?.Version ?? "").Replace("  ", " "));
        }));
    }

    private async Task UninstallMelonLoader()
    {
        if (!CheckReady()) return;

        var yes = TaskDialogButton.Yes;
        var wipe = new TaskDialogVerificationCheckBox(T("Also delete all mods, model files and mod settings (Mods, Plugins, UserData, UserLibs)"));
        var page = new TaskDialogPage
        {
            Caption = Text,
            Heading = T("Uninstall MelonLoader?"),
            Text = T("{0} will start without mods again. Removed files go to the Recycle Bin.", _cfg.GameName) + "\n\n" +
                   T("Leave the box unticked to keep your mods for later."),
            Icon = TaskDialogIcon.Warning,
            Verification = wipe,
            Buttons = { yes, TaskDialogButton.Cancel },
        };
        if (TaskDialog.ShowDialog(this, page) != yes) return;

        var dir = GameDir;
        bool removeAll = wipe.Checked;
        await RunBusy(T("Uninstalling MelonLoader…"), () => Task.Run(() =>
        {
            _ml.Uninstall(dir, _manifest, removeAll, Log);
            Log(removeAll ? T("MelonLoader and all mods removed.") : T("MelonLoader removed."));
        }));
    }

    // ───────────────────────── mods ─────────────────────────

    private string BuildFileFilter()
    {
        var patterns = new[] { "*.bamod" }.Concat(_cfg.ExtensionRules.Keys.Select(k => "*" + k)).Append("*.zip")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var joined = string.Join(";", patterns);
        return $"{T("Mods")} ({joined})|{joined}|{T("Mod packages")} (*.bamod)|*.bamod|{T("All files")} (*.*)|*.*";
    }

    private async Task AddFiles()
    {
        if (!GameValid) { ShowPage("settings"); return; }
        using var dlg = new OpenFileDialog
        {
            Title = T("Choose mods, models or mod packs"),
            Filter = BuildFileFilter(),
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) await InstallPaths(dlg.FileNames);
    }

    private async Task AddFolder()
    {
        if (!GameValid) { ShowPage("settings"); return; }
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Choose an unpacked mod folder"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) await InstallPaths(new[] { dlg.SelectedPath });
    }

    /// <summary>For files with no extension rule: ask where they go.</summary>
    private string? AskDestination(string file)
    {
        var dir = GameDir;
        var answer = MessageBox.Show(this,
            T("The mod manager doesn't know where \"{0}\" goes.\n\nPick a folder inside the game folder for it?", Path.GetFileName(file)),
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return null;

        using var dlg = new FolderBrowserDialog
        {
            Description = T("Where should \"{0}\" go?", Path.GetFileName(file)),
            UseDescriptionForTitle = true,
            InitialDirectory = dir,
            ShowNewFolderButton = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return null;

        var rel = Path.GetRelativePath(dir, dlg.SelectedPath);
        if (rel.StartsWith("..") || Path.IsPathRooted(rel))
        {
            MessageBox.Show(this, T("That folder is outside the game folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        return rel == "." ? "" : rel.Replace('\\', '/');
    }

    private async Task InstallPaths(string[] paths)
    {
        if (!CheckReady()) return;
        var dir = GameDir;
        var plans = new List<InstallPlan>();
        try
        {
            foreach (var path in paths)
            {
                try
                {
                    var plan = _mods.BuildPlan(path, AskDestination);
                    if (plan == null) continue;
                    if (plan.Files.Count == 0 && plan.Info == null)
                    {
                        Log($"{Path.GetFileName(path)}: " + T("nothing to install (no mods or bundles inside)."));
                        plan.Dispose();
                        continue;
                    }
                    plans.Add(plan);
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
                {
                    Log($"{Path.GetFileName(path)}: {ex.Message}");
                    MessageBox.Show(this, $"{Path.GetFileName(path)}:\n\n{ex.Message}", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            if (plans.Count == 0) return;

            var conflicts = plans.SelectMany(p => _mods.FindConflicts(dir, _manifest, p))
                                 .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (conflicts.Count > 0)
            {
                var shown = string.Join("\n", conflicts.Take(12)) +
                            (conflicts.Count > 12 ? "\n" + T("…and {0} more", conflicts.Count - 12) : "");
                if (MessageBox.Show(this, T("These files already exist and will be replaced:\n\n{0}\n\nContinue?", shown),
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            // Requirements of the new mods, checked against what's installed plus what's being installed
            var toEnable = new List<ModEntry>();
            if (!ConfirmRequirements(plans, toEnable)) return;

            var names = plans.Select(p => p.PackageName).ToList();
            await RunBusy(T("Installing…"), () => Task.Run(() =>
            {
                try
                {
                    foreach (var plan in plans) _mods.Execute(dir, _manifest, plan, Log);

                    // A newly installed modpack becomes the active one
                    var packPlan = plans.LastOrDefault(p => p.Info?.IsModpack == true);
                    var newPack = packPlan == null ? null
                        : _manifest.Packages.FirstOrDefault(p => p.Name.Equals(packPlan.PackageName, StringComparison.OrdinalIgnoreCase));
                    if (newPack != null)
                    {
                        new LauncherService(_cfg).Activate(dir, _manifest, newPack, Log);
                        Log(T("{0} is now your modpack.", newPack.Name));
                    }
                    foreach (var req in toEnable)
                    {
                        if (req.Enabled || (req.Package != null && !_manifest.Packages.Contains(req.Package))) continue;
                        _mods.SetEnabled(dir, req, true, Log);
                    }
                }
                finally
                {
                    _manifest.Save(dir);
                }
                if (!_ml.IsInstalled(dir))
                    Log(T("Note: MelonLoader isn't installed yet - mods won't load until you install it."));
            }));

            SelectByName(names);
        }
        finally
        {
            foreach (var p in plans) p.Dispose();
        }
    }

    /// <summary>Highlights freshly installed mods in the list.</summary>
    private void SelectByName(ICollection<string> names)
    {
        _grid.ClearSelection();
        DataGridViewRow? first = null;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is ModEntry m && names.Contains(m.Name))
            {
                row.Selected = true;
                first ??= row;
            }
        }
        if (first != null) _grid.FirstDisplayedScrollingRowIndex = first.Index;
    }

    private async Task ToggleSelected()
    {
        var sel = SelectedEntries();
        if (sel.Count == 0) return;
        await ToggleEntries(sel, !sel.All(e => e.Enabled));
    }

    private async Task ToggleEntries(List<ModEntry> entries, bool enable)
    {
        if (_busy) return;
        var targets = entries.Where(e => e.Enabled != enable).ToList();
        if (targets.Count == 0 || !CheckReady()) return;

        if (enable)
        {
            // Switch on what these mods need, if it's installed but off
            var needed = Dependencies.RequiredTree(targets, _entries).Where(d => !d.Enabled && !targets.Contains(d)).ToList();
            if (needed.Count > 0)
            {
                var answer = MessageBox.Show(this,
                    T("{0} need(s) these mods, which are switched off:", NameList(targets)) + "\n\n" +
                    Bullets(needed) + "\n\n" + T("Switch them on too?"),
                    Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes) targets.AddRange(needed);
            }
        }
        else
        {
            // Mods that stop working when these are switched off
            var dependents = Dependencies.DependentTree(targets, _entries).Where(d => d.Enabled && !targets.Contains(d)).ToList();
            if (dependents.Count > 0)
            {
                var answer = MessageBox.Show(this,
                    T("These mods need {0} and won't work without it:", NameList(targets)) + "\n\n" +
                    Bullets(dependents) + "\n\n" + T("Switch them off too?"),
                    Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes) targets.AddRange(dependents);
            }
        }

        var dir = GameDir;
        await RunBusy(enable ? T("Enabling…") : T("Disabling…"), () => Task.Run(() =>
        {
            try
            {
                foreach (var e in targets) _mods.SetEnabled(dir, e, enable, Log);

                // Only one modpack at a time: switching one on makes it the active one
                var launcher = new LauncherService(_cfg);
                var packOn = enable ? targets.LastOrDefault(e => e.Info?.IsModpack == true) : null;
                if (packOn != null)
                {
                    launcher.Activate(dir, _manifest, packOn.Package, Log);
                    Log(T("{0} is now your modpack.", packOn.Name));
                }
                else if (!enable && targets.Any(e => e.Info?.IsModpack == true && e.Package == launcher.FindPack(_manifest)))
                {
                    launcher.Activate(dir, _manifest, null, Log);
                }
            }
            finally
            {
                if (targets.Any(e => e.IsTracked)) _manifest.Save(dir);
            }
        }));
    }

    private async Task RemoveSelected()
    {
        var entries = SelectedEntries();
        if (entries.Count == 0 || !CheckReady()) return;

        var names = string.Join("\n", entries.Take(10).Select(e => "•  " + e.Name)) +
                    (entries.Count > 10 ? "\n" + T("…and {0} more", entries.Count - 10) : "");
        var question = entries.Count == 1 ? T("Remove this mod?") : T("Remove these {0} mods?", entries.Count);
        var dependents = Dependencies.DependentTree(entries, _entries).Where(d => !entries.Contains(d)).ToList();
        var warning = dependents.Count > 0
            ? "\n\n⚠  " + T("These mods need it and will stop working:") + "\n" + Bullets(dependents)
            : "";
        if (MessageBox.Show(this, question + " " + T("The files go to the Recycle Bin.") + $"\n\n{names}{warning}",
                Text, MessageBoxButtons.YesNo, dependents.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var dir = GameDir;
        await RunBusy(T("Removing…"), () => Task.Run(() =>
        {
            try
            {
                foreach (var e in entries) _mods.Remove(dir, _manifest, e, Log);
            }
            finally
            {
                _manifest.Save(dir);
            }
        }));
    }

    private void ShowSelectedInFolder()
    {
        if (!GameValid) return;
        var dir = GameDir;
        try
        {
            var entry = SelectedEntries().FirstOrDefault();
            string? file = null;
            if (entry?.LooseRel != null) file = PathUtil.ToFull(dir, entry.LooseRel);
            else if (entry?.Package != null)
            {
                foreach (var f in entry.Package.Files)
                {
                    var on = PathUtil.ToFull(dir, f);
                    if (File.Exists(on)) { file = on; break; }
                    if (File.Exists(on + PathUtil.DisabledSuffix)) { file = on + PathUtil.DisabledSuffix; break; }
                }
            }

            var args = file != null ? $"/select,\"{file}\"" : $"\"{dir}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log(T("Couldn't open Explorer: ") + ex.Message);
        }
    }

    // ───────────────────────── requirements ─────────────────────────

    private static string NameList(IReadOnlyList<ModEntry> mods) =>
        mods.Count <= 3 ? string.Join(", ", mods.Select(m => m.Name)) : T("{0} and {1} others", mods[0].Name, mods.Count - 1);

    private static string Bullets(IEnumerable<ModEntry> mods) =>
        string.Join("\n", mods.Select(m => "•  " + m.Name));

    /// <summary>
    /// Checks what the packages being installed need. Mods that are installed but switched off are
    /// added to <paramref name="toEnable"/>; anything missing is shown to the player. Returns false to cancel.
    /// </summary>
    private bool ConfirmRequirements(List<InstallPlan> plans, List<ModEntry> toEnable)
    {
        // Pretend the new packages are installed (replacing older copies) and check them
        var replaced = plans.Select(p => ModService.FindSame(_manifest, p)).Where(p => p != null).ToHashSet();
        var all = _entries.Where(e => e.Package == null || !replaced.Contains(e.Package)).ToList();
        var incoming = new List<(InstallPlan Plan, ModEntry Entry)>();
        foreach (var plan in plans)
        {
            var entry = new ModEntry
            {
                Name = plan.PackageName,
                TypeText = "",
                Location = "",
                Enabled = true,
                Package = new InstalledPackage
                {
                    Name = plan.PackageName,
                    Info = plan.Info,
                    Files = plan.Files.Select(f => f.TargetRel).ToList(),
                },
            };
            all.Add(entry);
            incoming.Add((plan, entry));
        }

        var lines = new List<string>();
        var urls = new List<string>();
        foreach (var (plan, entry) in incoming)
        {
            foreach (var issue in Dependencies.Check(entry, all))
            {
                if (issue.Problem == DepProblem.Disabled && issue.Found != null && issue.Found.Package?.Info != plan.Info)
                {
                    if (!toEnable.Contains(issue.Found)) toEnable.Add(issue.Found);
                    continue;
                }
                lines.Add($"•  {issue.Describe()}");
                if (!string.IsNullOrWhiteSpace(issue.Ref.Url) && issue.Problem != DepProblem.Incompatible)
                    urls.Add(issue.Ref.Url!);
            }
        }

        if (toEnable.Count > 0)
            Log(T("Will also switch on: ") + string.Join(", ", toEnable.Select(m => m.Name)));
        if (lines.Count == 0) return true;

        var who = incoming[0].Plan.PackageName;
        var install = new TaskDialogButton(T("Install anyway"));
        var download = new TaskDialogButton(urls.Count == 1 ? T("Open download page") : T("Open download pages"));
        var page = new TaskDialogPage
        {
            Caption = Text,
            Heading = incoming.Count == 1 ? T("{0} needs other mods", who) : T("These mods need other mods"),
            Text = string.Join("\n", lines.Distinct()) +
                   "\n\n" + T("You can install anyway and add the missing mods later - it won't work until you do."),
            Icon = TaskDialogIcon.Warning,
            Buttons = { install, TaskDialogButton.Cancel },
        };
        if (urls.Count > 0) page.Buttons.Insert(1, download);

        var result = TaskDialog.ShowDialog(this, page);
        if (result == download)
        {
            foreach (var url in urls.Distinct()) OpenUrl(url);
            return false;
        }
        return result == install;
    }

    // ───────────────────────── read me / links ─────────────────────────

    private void SwitchLanguage(string code)
    {
        if (code == Current) return;
        if (_busy)
        {
            MessageBox.Show(this, T("Please wait until the current job is finished."), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var data = AppSettings.Load();
        data.Language = code;
        if (!AppSettings.Save(data))
        {
            Log(T("Couldn't save the settings file."));
            return;
        }

        // Ask in the language the player just picked
        var old = Current;
        SetLanguage(code);
        var question = T("Restart the Mod Manager now to switch the language?");
        var later = T("The new language is used the next time the Mod Manager starts.");
        SetLanguage(old);

        if (MessageBox.Show(this, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            Log(later);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, RestartArg) { UseShellExecute = false });
            ExitApp = true;
            Close();
        }
        catch (Exception ex)
        {
            Log(T("Couldn't restart: ") + ex.Message);
        }
    }

    private void ShowReadme()
    {
        var sel = SelectedEntries();
        if (sel.Count != 1 || !GameValid) return;
        var path = ModService.ReadmePath(GameDir, sel[0]);
        if (path == null) return;
        try
        {
            using var viewer = new ReadmeForm(sel[0].Name, File.ReadAllText(path));
            viewer.Icon = Icon;
            viewer.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log(T("Couldn't open the read me: ") + ex.Message);
        }
    }

    private void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            Log(T("Not opening \"{0}\" - only http(s) links are allowed.", url));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log(T("Couldn't open the link: ") + ex.Message);
        }
    }

    // ───────────────────────── for modders ─────────────────────────

    private void CreatePackage()
    {
        using var form = new PackageCreatorForm();
        form.Icon = Icon;
        form.ShowDialog(this);
        if (form.CreatedFile != null) Log(T("Created {0}.", Path.GetFileName(form.CreatedFile)));
    }

    // ───────────────────────── uninstall the Mod Manager ─────────────────────────

    private async Task UninstallManager()
    {
        if (_busy) return;

        var keep = new TaskDialogRadioButton(T("Keep MelonLoader and my mods")) { Checked = true };
        var wipe = new TaskDialogRadioButton(T("Also uninstall MelonLoader and remove ALL mods and their settings"));
        var confirm = new TaskDialogVerificationCheckBox(T("I understand - remove the Mod Manager"));
        var uninstall = new TaskDialogButton(T("Uninstall")) { Enabled = false };
        var cancel = TaskDialogButton.Cancel;
        confirm.CheckedChanged += (_, _) => uninstall.Enabled = confirm.Checked;

        var page = new TaskDialogPage
        {
            Caption = Text,
            Heading = T("Uninstall Broken Arrow Mod Manager?"),
            Text = T("This removes from your PC:") + "\n" +
                   "•  " + T("the Mod Manager program (BAModManager.exe)") + "\n" +
                   "•  " + T("its settings ({0})", AppSettings.FolderPath) + "\n" +
                   "•  " + T("its records, mod pictures and readmes in the game folder (UserData\\BAModManager)") + "\n" +
                   "•  " + T("double-click opening of .bamod files") + "\n\n" +
                   T("This can't be undone."),
            Icon = TaskDialogIcon.Warning,
            Verification = confirm,
            Buttons = { uninstall, cancel },
            DefaultButton = cancel,
        };
        page.RadioButtons.Add(keep);
        page.RadioButtons.Add(wipe);

        if (TaskDialog.ShowDialog(this, page) != uninstall) return;
        bool removeAll = wipe.Checked;

        if (removeAll && GameValid && GameLocator.IsGameRunning(GameDir))
        {
            MessageBox.Show(this, T("Close {0} first.", _cfg.GameName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (removeAll && MessageBox.Show(this,
                T("Last check: this deletes MelonLoader and EVERY mod in the game folder (Mods, Plugins, UserData, UserLibs). Continue?"),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        var dir = GameValid ? GameDir : null;
        bool done = false;
        await RunBusy(T("Uninstalling the Mod Manager…"), () => Task.Run(() =>
        {
            FileAssociation.Unregister();
            Log(T("Removed .bamod file association."));

            if (dir != null)
            {
                if (removeAll)
                {
                    _ml.Uninstall(dir, _manifest, removeUserContent: true, Log);
                }
                var record = PathUtil.ToFull(dir, Manifest.FolderRel);
                if (Directory.Exists(record))
                {
                    Directory.Delete(record, recursive: true);
                    Log(T("Removed UserData\\BAModManager."));
                }
            }
            done = true;
        }));
        if (!done) return;

        // Settings last: refreshing the window above would write them again
        try
        {
            if (Directory.Exists(AppSettings.FolderPath)) Directory.Delete(AppSettings.FolderPath, recursive: true);
        }
        catch (Exception ex)
        {
            Log(T("Couldn't remove the settings folder: ") + ex.Message);
        }

        var exe = SelfDelete.Schedule();
        MessageBox.Show(this,
            (removeAll
                ? T("The Mod Manager, MelonLoader and all mods have been removed.")
                : T("The Mod Manager has been removed. MelonLoader and your mods are still installed."))
            + (exe != null ? "\n\n" + T("Last step: delete {0} - its folder opens now.", Path.GetFileName(exe)) : ""),
            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        SelfDelete.ShowExe(exe);
        _uninstalled = true;
        ExitApp = true;
        Close();
    }
}
