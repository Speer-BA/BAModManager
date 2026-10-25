using System.Diagnostics;
using BAModManager.Core;
using static BAModManager.Core.Loc;

namespace BAModManager.Ui;

/// <summary>
/// The Mod Launcher's few settings: language, game folder, removing the mod, and uninstalling the launcher.
/// Nothing here changes the modpack - that's the modder's choice.
/// </summary>
internal sealed class LauncherSettingsForm : Form
{
    private readonly ManagerConfig _cfg;
    private readonly List<string> _log;
    private readonly Label _gamePath;
    private readonly Label _gameStatus;

    /// <summary>The game folder after the dialog (null = unchanged / not set).</summary>
    public string? GameDir { get; private set; }

    /// <summary>The launcher should close (language restart or uninstalled).</summary>
    public bool ExitApp { get; private set; }

    public LauncherSettingsForm(ManagerConfig cfg, string? gameDir, List<string> log, Icon? icon)
    {
        _cfg = cfg;
        _log = log;
        GameDir = gameDir;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = T("Settings");
        if (icon != null) Icon = icon;
        Font = Theme.Ui(10f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 520);

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Theme.Background,
        };
        Controls.Add(stack);
        const int width = 450;

        Label Heading(string text) => new() { Text = text, AutoSize = true, Font = Theme.Semibold(8.75f), ForeColor = Theme.TextMuted, Margin = new Padding(0, 14, 0, 6) };
        Label Line(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 6) };
        FlowLayoutPanel Row() => new() { AutoSize = true, WrapContents = true, MaximumSize = new Size(width, 0), Margin = new Padding(0, 0, 0, 4), BackColor = Theme.Background };

        // ── language ──
        stack.Controls.Add(Heading(T("LANGUAGE")));
        var langRow = Row();
        foreach (var (code, name) in Languages)
        {
            var b = Theme.MakeButton(name, code == Current ? ButtonKind.Primary : ButtonKind.Secondary);
            if (code == "zh") b.Font = new Font("Microsoft YaHei UI", b.Font.Size, FontStyle.Bold);
            b.Margin = new Padding(0, 0, 6, 6);
            b.Click += (_, _) => SwitchLanguage(code);
            langRow.Controls.Add(b);
        }
        stack.Controls.Add(langRow);

        // ── game folder ──
        stack.Controls.Add(Heading(T("GAME FOLDER")));
        _gamePath = new Label { AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 6) };
        _gameStatus = Line("");
        var gameRow = Row();
        var browse = Theme.MakeButton(T("Browse…"));
        var detect = Theme.MakeButton(T("Auto-detect"));
        gameRow.Controls.AddRange(new Control[] { browse, detect });
        stack.Controls.Add(_gamePath);
        stack.Controls.Add(_gameStatus);
        stack.Controls.Add(gameRow);
        browse.Click += (_, _) => BrowseGame();
        detect.Click += (_, _) => DetectGame();

        // ── the mod ──
        stack.Controls.Add(Heading(T("MOD")));
        stack.Controls.Add(Line(T("Removes the modpack, MelonLoader and all mods from the game. The launcher stays - PLAY installs them again.")));
        var removeMod = Theme.MakeButton(T("Uninstall mod…"), ButtonKind.Danger);
        removeMod.Margin = new Padding(0, 0, 0, 6);
        removeMod.Click += (_, _) => UninstallMod();
        stack.Controls.Add(removeMod);

        // ── uninstall ──
        stack.Controls.Add(Heading(T("UNINSTALL")));
        stack.Controls.Add(Line(T("Removes the launcher from this PC. You choose whether MelonLoader and the mods stay.")));
        var uninstall = Theme.MakeButton(T("Uninstall {0}…", AppInfo.ShortName), ButtonKind.Danger);
        uninstall.Margin = new Padding(0, 0, 0, 6);
        uninstall.Click += (_, _) => Uninstall();
        stack.Controls.Add(uninstall);

        var close = Theme.MakeButton(T("Close"), ButtonKind.Primary);
        close.Click += (_, _) => Close();
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 20, 8), BackColor = Theme.Sidebar };
        bottom.Controls.Add(close);
        Controls.Add(bottom);
        CancelButton = close;

        UpdateGameFolder();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    // ───────────────────────── game folder ─────────────────────────

    private void UpdateGameFolder()
    {
        _gamePath.Text = GameDir ?? T("No folder chosen yet.");
        bool ok = GameDir != null && GameLocator.IsGameFolder(GameDir);
        _gameStatus.ForeColor = ok ? Theme.Good : Theme.Bad;
        _gameStatus.Text = ok ? "✔  " + T("Found {0}", _cfg.GameName) : "✖  " + T("Game folder not set");
    }

    private void SetGame(string dir)
    {
        GameDir = PathUtil.RealCase(dir);
        AppSettings.SaveGamePath(GameDir);
        _log.Add($"[{DateTime.Now:HH:mm:ss}] " + T("Found the game at {0}", GameDir));
        UpdateGameFolder();
    }

    private void BrowseGame()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Select the {0} game folder (the one with the .exe)", _cfg.GameName),
            UseDescriptionForTitle = true,
        };
        if (GameDir != null && Directory.Exists(GameDir)) dlg.InitialDirectory = GameDir;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!GameLocator.IsGameFolder(dlg.SelectedPath))
        {
            MessageBox.Show(this, "✖  " + T("This doesn't look like the game folder (no *_Data folder inside)."), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SetGame(dlg.SelectedPath);
    }

    private void DetectGame()
    {
        var found = GameLocator.AutoDetect(_cfg.SteamFolderName);
        if (found == null)
        {
            MessageBox.Show(this, T("Couldn't find {0} in your Steam libraries.\nUse Browse… to pick the game folder yourself.", _cfg.GameName),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SetGame(found);
    }

    // ───────────────────────── uninstall the mod ─────────────────────────

    private void UninstallMod()
    {
        if (GameDir == null || !GameLocator.IsGameFolder(GameDir))
        {
            MessageBox.Show(this, "✖  " + T("Game folder not set"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var dir = GameDir;
        if (GameLocator.IsGameRunning(dir))
        {
            MessageBox.Show(this, T("Close {0} first.", _cfg.GameName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(this,
                T("Remove the modpack, MelonLoader and all mods from {0}?\n\nThe game goes back to normal. The launcher stays - press PLAY to install the mod again.", _cfg.GameName),
                T("Uninstall mod"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

        UseWaitCursor = true;
        try
        {
            Manifest manifest;
            try { manifest = Manifest.Load(dir); }
            catch { manifest = new Manifest(); }
            new MelonLoaderService(_cfg).Uninstall(dir, manifest, removeUserContent: true,
                m => _log.Add($"[{DateTime.Now:HH:mm:ss}] {m}"));
            // a version.dll parked by "Play vanilla" would bring MelonLoader back - it's gone with the rest
            try { if (File.Exists(LauncherService.ParkedProxy)) File.Delete(LauncherService.ParkedProxy); }
            catch { /* harmless */ }
            UseWaitCursor = false;
            MessageBox.Show(this, T("The mod has been removed. {0} is back to normal.", _cfg.GameName), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            UseWaitCursor = false;
            MessageBox.Show(this, T("Windows didn't allow changing a file:\n\n{0}\n\nMake sure the game is closed. If the game is in Program Files, run the mod manager as administrator.", ex.Message),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ───────────────────────── language ─────────────────────────

    private void SwitchLanguage(string code)
    {
        if (code == Current) return;
        var data = AppSettings.Load();
        data.Language = code;
        if (!AppSettings.Save(data)) return;

        var old = Current;
        SetLanguage(code);
        var question = T("Restart {0} now to switch the language?", AppInfo.ShortName);
        SetLanguage(old);
        if (MessageBox.Show(this, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, RestartArg) { UseShellExecute = false })?.Dispose();
            ExitApp = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, T("Couldn't restart: ") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ───────────────────────── uninstall ─────────────────────────

    private void Uninstall()
    {
        var keep = new TaskDialogRadioButton(T("Keep MelonLoader and my mods")) { Checked = true };
        var wipe = new TaskDialogRadioButton(T("Also uninstall MelonLoader and remove ALL mods and their settings"));
        var confirm = new TaskDialogVerificationCheckBox(T("I understand - remove {0}", AppInfo.ShortName));
        var uninstall = new TaskDialogButton(T("Uninstall")) { Enabled = false };
        var cancel = TaskDialogButton.Cancel;
        confirm.CheckedChanged += (_, _) => uninstall.Enabled = confirm.Checked;

        var page = new TaskDialogPage
        {
            Caption = AppInfo.FullName,
            Heading = T("Uninstall {0}?", AppInfo.FullName),
            Text = T("This removes from your PC:") + "\n" +
                   "•  " + T("the launcher ({0} and launcher.json)", Path.GetFileName(Environment.ProcessPath ?? "BAModLauncher.exe")) + "\n" +
                   "•  " + T("its settings ({0})", AppSettings.FolderPath) + "\n" +
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
        var dir = GameDir != null && GameLocator.IsGameFolder(GameDir) ? GameDir : null;
        if (removeAll && dir != null && GameLocator.IsGameRunning(dir))
        {
            MessageBox.Show(this, T("Close {0} first.", _cfg.GameName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        UseWaitCursor = true;
        string? exe = null;
        try
        {
            FileAssociation.Unregister();
            if (removeAll && dir != null)
            {
                Manifest manifest;
                try { manifest = Manifest.Load(dir); }
                catch { manifest = new Manifest(); }
                new MelonLoaderService(_cfg).Uninstall(dir, manifest, removeUserContent: true, _ => { });
            }
            else if (dir != null && File.Exists(LauncherService.ParkedProxy) && !File.Exists(LauncherService.Proxy(dir)))
            {
                // "Turn off mods" parked MelonLoader's version.dll in our settings folder - put it back before that folder goes.
                try { File.Copy(LauncherService.ParkedProxy, LauncherService.Proxy(dir)); }
                catch { /* the game still runs vanilla */ }
            }
            try
            {
                if (Directory.Exists(AppSettings.FolderPath)) Directory.Delete(AppSettings.FolderPath, recursive: true);
            }
            catch { /* left behind; harmless */ }
            exe = SelfDelete.Schedule("launcher.json");
        }
        catch (Exception ex)
        {
            UseWaitCursor = false;
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show(this,
            (removeAll
                ? T("{0}, MelonLoader and all mods have been removed.", AppInfo.ShortName)
                : T("{0} has been removed. MelonLoader and your mods are still installed.", AppInfo.ShortName))
            + (exe != null ? "\n\n" + T("Last step: delete {0} - its folder opens now.", Path.GetFileName(exe)) : ""),
            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        SelfDelete.ShowExe(exe);
        ExitApp = true;
        Close();
    }
}
