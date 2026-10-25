using System.Diagnostics;
using BAModManager.Core;
using static BAModManager.Core.Loc;

namespace BAModManager.Ui;

/// <summary>
/// The Mod Launcher window (BAModLauncher.exe): one PLAY button that installs/updates MelonLoader and the modpack
/// this copy is set up for, and starts the game; a button to turn mods off and play normally with Anti-Cheat;
/// and a few settings. Players can't change the modpack - the modder sets it up once (launcher.json).
/// </summary>
internal sealed class LauncherForm : Form
{
    private readonly ManagerConfig _cfg;
    private readonly LauncherService _launcher;
    private readonly string[] _startupFiles;
    private readonly Image? _logo;
    private readonly Icon? _icon;

    private readonly PictureBox _packPicture;
    private readonly Label _packName;
    private readonly Label _packVersion;
    private readonly Label _modsState;
    private readonly Label _status;
    private readonly ThinProgress _progress;
    private readonly Button _play;
    private readonly Button _vanilla;
    private readonly Button _settings;
    private readonly Button _setUp;
    private readonly FlowLayoutPanel _channels;

    private string? _gameDir;
    private bool _busy;
    private readonly List<string> _log = new();
    private readonly System.Windows.Forms.Timer _gameWatch = new() { Interval = 1000 };
    private bool _gameRunning;

    public LauncherForm(ManagerConfig cfg, string[]? startupFiles = null)
    {
        _cfg = cfg;
        _launcher = new LauncherService(cfg);
        _startupFiles = startupFiles ?? Array.Empty<string>();
        _logo = MainForm.LoadEmbeddedImage("Assets.logo.png");
        _icon = MainForm.LoadEmbeddedIcon("Assets.icon.ico");

        AutoScaleDimensions = new SizeF(96F, 96F);   // laid out at 100 %, scaled for the screen
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppInfo.FullName;
        if (_icon != null) Icon = _icon;
        Font = Theme.Ui(10f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 624);
        Padding = new Padding(28, 22, 28, 18);

        // ── brand ──
        var brand = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Theme.Background };
        var logoBox = new PictureBox
        {
            Image = _logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(52, 52),
            Location = new Point(0, 4),
            BackColor = Color.Transparent,
        };
        var brandSmall = new Label { Text = "BROKEN ARROW", AutoSize = true, Font = Theme.BrandSmall(8.5f), ForeColor = Theme.Accent, Location = new Point(62, 8) };
        var brandBig = new Label { Text = AppInfo.ShortName, AutoSize = true, Font = Theme.BrandLarge(16f), ForeColor = Theme.Text, Location = new Point(60, 24) };
        brand.Controls.AddRange(new Control[] { logoBox, brandSmall, brandBig });

        // ── mod pack card ──
        var card = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Theme.Panel, Padding = new Padding(16) };
        var cardLabel = new Label { Text = T("MODPACK"), AutoSize = true, Location = new Point(151, 14), Font = Theme.Semibold(8.5f), ForeColor = Theme.TextMuted };
        _packPicture = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(118, 118), Location = new Point(16, 16), BackColor = Theme.Panel };
        _packName = new Label { AutoSize = false, Location = new Point(150, 28), Size = new Size(250, 30), Font = Theme.Heading(15f), ForeColor = Theme.Text, AutoEllipsis = true };
        _packVersion = new Label { AutoSize = false, Location = new Point(151, 58), Size = new Size(250, 22), ForeColor = Theme.TextMuted, AutoEllipsis = true };
        _modsState = new Label { AutoSize = false, Location = new Point(151, 78), Size = new Size(250, 22), ForeColor = Theme.TextMuted };
        // Only shown before a modder has set the modpack up
        _setUp = Theme.MakeButton(T("Set up modpack (for modders)…"));
        _setUp.Location = new Point(151, 100);
        // Stable / Testing - only when the modder offers a testing channel
        _channels = new FlowLayoutPanel { Location = new Point(151, 102), Size = new Size(250, 32), WrapContents = false, BackColor = Theme.Panel, Visible = false };
        card.Controls.AddRange(new Control[] { cardLabel, _packPicture, _packName, _packVersion, _modsState, _setUp, _channels });

        // ── buttons ──
        _play = Theme.MakeButton("▶   " + T("PLAY"), ButtonKind.Primary);
        _play.AutoSize = false;
        _play.Dock = DockStyle.Top;
        _play.Height = 58;
        _play.Font = Theme.Heading(14f);
        _play.Margin = new Padding(0);

        _vanilla = Theme.MakeButton(T("Play vanilla with no mods"));
        _vanilla.AutoSize = false;
        _vanilla.Dock = DockStyle.Top;
        _vanilla.Height = 40;

        _status = new Label { Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.TextMuted, AutoEllipsis = true };
        _progress = new ThinProgress { Dock = DockStyle.Top, Visible = false };

        // ── footer ──
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Theme.Background };
        _settings = Theme.MakeButton("⚙   " + T("Settings"), ButtonKind.Ghost);
        _settings.Dock = DockStyle.Left;
        _settings.AutoSize = true;
        footer.Controls.Add(_settings);

        // Docked top controls are added bottom-up
        Controls.Add(_vanilla);
        Controls.Add(Spacer(10));
        Controls.Add(_play);
        Controls.Add(Spacer(12));
        Controls.Add(_progress);
        Controls.Add(_status);
        Controls.Add(Spacer(4));
        Controls.Add(card);
        Controls.Add(Spacer(18));
        Controls.Add(brand);
        Controls.Add(footer);

        _play.Click += async (_, _) => await PlayModded();
        _vanilla.Click += (_, _) => PlayVanilla();
        _settings.Click += (_, _) => OpenSettings();
        _setUp.Click += async (_, _) => await SetUpModpack();

        // The modpack's .bamod dropped on the launcher (e.g. downloaded by hand) is installed; anything else is refused
        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = !_busy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) await InstallPackFile(files[0]);
        };
        Shown += async (_, _) => await OnFirstShown();
        // Only look for the game while the launcher is the active window: a program that keeps checking
        // other processes in the background is one more thing antivirus behaviour monitors hold against it.
        _gameWatch.Tick += (_, _) => UpdateGameRunning();
        Activated += (_, _) => { UpdateGameRunning(); _gameWatch.Start(); };
        Deactivate += (_, _) => _gameWatch.Stop();
        FormClosed += (_, _) => _gameWatch.Dispose();
        FormClosing += (_, e) =>
        {
            if (_busy && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        };
    }

    private static Control Spacer(int height) => new Panel { Dock = DockStyle.Top, Height = height, BackColor = Theme.Background };

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    // ───────────────────────── start-up ─────────────────────────

    private async Task OnFirstShown()
    {
        SetStatus(T("Looking for Broken Arrow…"));
        var saved = AppSettings.LoadGamePath();
        var steamFolder = _cfg.SteamFolderName;
        _gameDir = await Task.Run(() => saved != null && GameLocator.IsGameFolder(saved) ? saved : GameLocator.AutoDetect(steamFolder));
        if (_gameDir != null)
        {
            _gameDir = PathUtil.RealCase(_gameDir);
            AppSettings.SaveGamePath(_gameDir);
        }
        RefreshCard();
        SetStatus(_gameDir == null ? T("Broken Arrow wasn't found. Choose its folder in Settings.") : "");

        // Double-clicked .bamod files (if the player turned that on in Settings)
        SingleInstance.StartServer(OnFilesFromExplorer);
        if (_startupFiles.Length > 0) await InstallPackFile(_startupFiles[0]);
    }

    private void OnFilesFromExplorer(string[] files)
    {
        if (IsDisposed || files.Length == 0) return;
        try
        {
            BeginInvoke(new Action(async () =>
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Activate();
                await InstallPackFile(files[0]);
            }));
        }
        catch (InvalidOperationException) { /* closing */ }
    }

    private void RefreshCard()
    {
        Manifest? manifest = null;
        InstalledPackage? pack = null;
        string? preview = null;
        bool on = false;
        if (_gameDir != null)
        {
            try
            {
                manifest = Manifest.Load(_gameDir);
                pack = _launcher.FindPack(manifest);
                if (pack != null) preview = _launcher.FindPackPreview(_gameDir, manifest, pack);
            }
            catch { /* shown as not installed */ }
            on = _launcher.ModsOn(_gameDir);
        }

        var old = _packPicture.Image;
        _packPicture.Image = LoadPicture(preview);
        if (old != null && old != _logo) old.Dispose();

        if (pack != null)
        {
            _packName.Text = pack.Info?.Name ?? pack.Name;
            var version = pack.Info?.Version;
            _packVersion.Text = version != null ? "v" + version.TrimStart('v', 'V') : "";
        }
        else if (LauncherService.ActiveRepo() is { } repo)
        {
            _packName.Text = repo.Split('/')[1];
            _packVersion.Text = T("Downloaded when you press PLAY");
        }
        if (pack != null && LauncherService.InstalledFromOtherRepo())
            _packVersion.Text = T("{0}: installed on next PLAY", T(LauncherService.ActiveChannel));
        else if (pack != null && LauncherService.Channels.Count > 1)
            _packVersion.Text += "  ·  " + T(LauncherService.ActiveChannel);
        else
        {
            _packName.Text = T("No modpack set up");
            _packVersion.Text = T("Only MelonLoader is installed");
        }
        _setUp.Visible = LauncherService.ConfiguredRepo == null;
        _modsState.Visible = !_setUp.Visible;   // same spot in the card
        RefreshChannels();

        _modsState.ForeColor = on ? Theme.Good : Theme.TextMuted;
        _modsState.Text = on
            ? "●  " + T("Mods are on")
            : "○  " + T("Mods are off - the game starts normally with Anti-Cheat");
    }

    private void RefreshChannels()
    {
        var channels = LauncherService.Channels;
        _channels.Visible = channels.Count > 1;
        if (!_channels.Visible) return;
        var active = LauncherService.ActiveChannel;
        foreach (Control c in _channels.Controls) c.Dispose();
        _channels.Controls.Clear();
        foreach (var (name, _) in channels)
        {
            var b = Theme.MakeButton(T(name), name == active ? ButtonKind.Primary : ButtonKind.Secondary);
            b.MinimumSize = new Size(0, 30);
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;   // MakeButton's default height (34) doesn't fit the card
            b.Padding = new Padding(10, 0, 10, 0);
            b.Margin = new Padding(0, 0, 6, 0);
            b.Font = Theme.Ui(9f);
            b.Enabled = !_busy && !_gameRunning;
            var channel = name;
            b.Click += (_, _) => SwitchChannel(channel);
            _channels.Controls.Add(b);
        }
    }

    private void SwitchChannel(string channel)
    {
        if (_busy || _gameRunning || channel == LauncherService.ActiveChannel) return;
        if (channel == LauncherService.Testing &&
            MessageBox.Show(this,
                T("Testing has the newest changes before they come to Stable. It can have bugs.\n\nSwitch to Testing?"),
                T("Testing"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        LauncherService.SetChannel(channel);
        Log(T("Switched to {0}.", T(channel)));
        RefreshCard();
        SetStatus(LauncherService.InstalledFromOtherRepo() || _gameDir == null
            ? T("Switched to {0}. Press PLAY to download it.", T(channel))
            : T("Switched to {0}.", T(channel)));
    }

    private Image? LoadPicture(string? path)
    {
        try
        {
            if (path != null && File.Exists(path))
            {
                using var tmp = Image.FromFile(path);
                return new Bitmap(tmp);
            }
        }
        catch { /* fall back to the default picture */ }
        return MainForm.LoadEmbeddedImage("Assets.default_preview.png");
    }

    // ───────────────────────── PLAY ─────────────────────────

    private bool CheckGame()
    {
        if (_gameDir == null || !GameLocator.IsGameFolder(_gameDir))
        {
            SetStatus(T("Broken Arrow wasn't found. Choose its folder in Settings."));
            OpenSettings();
            return false;
        }
        if (GameLocator.IsGameRunning(_gameDir))
        {
            SetStatus(T("{0} is already running.", _cfg.GameName));
            return false;
        }
        return true;
    }

    private async Task PlayModded()
    {
        if (_busy || !CheckGame()) return;
        var dir = _gameDir!;

        if (!LauncherService.EnsureSteam())
        {
            SetStatus(T("Steam is starting - press PLAY again once it's open."));
            return;
        }

        SetBusy(true);
        try
        {
            var progress = new Progress<LaunchProgress>(p =>
            {
                SetStatus(p.Text);
                _progress.Visible = true;
                _progress.Marquee = p.Fraction == null;
                if (p.Fraction != null) _progress.Value = (float)p.Fraction.Value;
            });
            await _launcher.PrepareModsAsync(dir, progress, Log, CancellationToken.None);
            RefreshCard();

            SetStatus(T("Starting {0}…", _cfg.GameName));
            var game = _launcher.StartModded(dir);
            _gameRunning = false;
            ShowLoadingScreen(game, dir);
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            SetStatus(T("Couldn't start the game: ") + ex.Message);
            MessageBox.Show(this, ex.Message, T("Couldn't start the game"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowLoadingScreen(Process game, string dir)
    {
        var screen = new GameLoadingScreen(game, dir, _logo, _icon);
        screen.FormClosed += (_, _) =>
        {
            var failure = screen.Failure;
            screen.Dispose();
            game.Dispose();
            if (failure == null)
            {
                SetStatus(T("Have fun!"));
                return;
            }
            WindowState = FormWindowState.Normal;
            Activate();
            SetStatus(failure);
            var log = Path.Combine(dir, "MelonLoader", "Latest.log");
            if (MessageBox.Show(this, failure + "\n\n" + T("Open MelonLoader's log to see what went wrong?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes && File.Exists(log))
            {
                try { Process.Start(new ProcessStartInfo(log) { UseShellExecute = true })?.Dispose(); }
                catch { /* no viewer */ }
            }
        };
        WindowState = FormWindowState.Minimized;
        screen.Show();
    }

    // ───────────────────────── turn off mods ─────────────────────────

    private void PlayVanilla()
    {
        if (_busy || !CheckGame()) return;
        var dir = _gameDir!;
        try
        {
            _launcher.TurnOffMods(dir, Log);
            RefreshCard();
            if (!LauncherService.EnsureSteam())
            {
                SetStatus(T("Mods are off. Steam is starting - press the button again once it's open."));
                return;
            }
            _launcher.StartVanilla(dir);
            SetStatus(T("Mods are off. Starting {0} with Anti-Cheat…", _cfg.GameName));
            _play.Enabled = _vanilla.Enabled = false;   // until the game window closes again (checked every second)
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            SetStatus(ex.Message);
            MessageBox.Show(this, T("Windows didn't allow changing a file:\n\n{0}\n\nMake sure the game is closed. If the game is in Program Files, run the mod manager as administrator.", ex.Message),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ───────────────────────── modpack file / setup ─────────────────────────

    /// <summary>The modpack's own .bamod (dropped or double-clicked) is installed; other mods are turned away.</summary>
    private async Task InstallPackFile(string path)
    {
        if (_busy || !CheckGame()) return;
        var dir = _gameDir!;
        SetBusy(true);
        bool accepted = false;
        try
        {
            SetStatus(T("Installing the mod pack…"));
            _progress.Visible = true;
            _progress.Marquee = true;
            accepted = await Task.Run(() => _launcher.InstallPackFile(dir, path, Log));
            SetStatus(accepted ? T("Installed {0}.", Path.GetFileName(path)) : "");
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        finally
        {
            SetBusy(false);
            RefreshCard();
        }

        if (!accepted)
        {
            var pack = LauncherService.ActiveRepo()?.Split('/')[1] ?? T("its modpack");
            MessageBox.Show(this,
                T("This launcher is set up for {0} only. To install other mods, use Broken Arrow Mod Manager.", pack),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>For modders: link this copy of the launcher to their modpack's GitHub repo.</summary>
    private async Task SetUpModpack()
    {
        if (_busy) return;
        using var dlg = new ModpackSetupForm();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        SetBusy(true);
        try
        {
            SetStatus(T("Checking {0}…", dlg.Link));
            var repo = await _launcher.SetUpModpackAsync(dlg.Link, dlg.TestingLink, CancellationToken.None);
            RefreshCard();
            SetStatus(T("Set up for {0}.", repo));
            if (MessageBox.Show(this,
                    T("This launcher is now set up for {0}.\n\nShare these two files with your players:\n•  {1}\n•  launcher.json\n\nPlayers can't change the modpack. To change it yourself, delete launcher.json.\n\nOpen the folder now?",
                        repo, Path.GetFileName(Environment.ProcessPath ?? "BAModLauncher.exe")),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LauncherService.ConfigFile}\"") { UseShellExecute = true })?.Dispose(); }
                catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ───────────────────────── settings ─────────────────────────

    private void OpenSettings()
    {
        if (_busy) return;
        using var dlg = new LauncherSettingsForm(_cfg, _gameDir, _log, _icon);
        dlg.ShowDialog(this);
        if (dlg.ExitApp)
        {
            Close();
            return;
        }
        if (dlg.GameDir != null) _gameDir = dlg.GameDir;
        RefreshCard();
        if (_gameDir != null && _status.Text.StartsWith(T("Broken Arrow wasn't found."), StringComparison.Ordinal)) SetStatus("");
        UpdateGameRunning();
    }

    /// <summary>The buttons can't be used while Broken Arrow is open.</summary>
    private void UpdateGameRunning()
    {
        bool running = _gameDir != null && GameLocator.IsGameRunning(_gameDir);
        if (running == _gameRunning) return;
        _gameRunning = running;
        _play.Text = running ? T("GAME IS RUNNING") : "▶   " + T("PLAY");
        if (running) SetStatus(T("{0} is running. Close the game to use these buttons again.", _cfg.GameName));
        else if (!_busy) SetStatus("");
        ApplyEnabled();
    }

    private void ApplyEnabled()
    {
        _play.Enabled = _vanilla.Enabled = !_busy && !_gameRunning;
        _settings.Enabled = _setUp.Enabled = !_busy;
        foreach (Control c in _channels.Controls) c.Enabled = !_busy && !_gameRunning;
    }

    // ───────────────────────── helpers ─────────────────────────

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ApplyEnabled();
        UseWaitCursor = busy;
        if (!busy)
        {
            _progress.Marquee = false;
            _progress.Visible = false;
        }
    }

    private void SetStatus(string text) => _status.Text = text;

    private void Log(string message)
    {
        _log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        Debug.WriteLine(message);
    }
}
