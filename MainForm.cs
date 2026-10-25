using BAModManager.Core;
using BAModManager.Ui;
using static BAModManager.Core.Loc;

namespace BAModManager;

/// <summary>Window shell: sidebar, page host, status bar and shared state.</summary>
public sealed partial class MainForm : Form
{
    private readonly ManagerConfig _cfg;
    private readonly MelonLoaderService _ml;
    private readonly ModService _mods;

    private Manifest _manifest = new();
    private List<ModEntry> _entries = new();
    private bool _busy;
    private readonly System.Windows.Forms.Timer _dropHide = new() { Interval = 120 };
    private bool _uninstalled;

    /// <summary>The whole app should quit after this window (language restart, Mod Manager uninstalled).</summary>
    public bool ExitApp { get; private set; }
    private string _filter = "all";
    private string _currentPage = "mods";

    // Shell
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
    private readonly Dictionary<string, Control> _pages = new();
    private readonly Dictionary<string, NavButton> _nav = new();
    private readonly StatusLine _sideGame = new();
    private readonly StatusLine _sideLoader = new();
    private readonly StatusLine _sideMods = new();
    private readonly Button _playBtn = Theme.MakeButton("▶   " + T("PLAY"), ButtonKind.Primary);
    private readonly Label _statusLabel = new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Theme.TextMuted,
        BackColor = Theme.Sidebar,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 0, 0, 0),
        AutoEllipsis = true,
        Text = T("Ready"),
    };
    private readonly ThinProgress _progress = new() { Dock = DockStyle.Top, Visible = false };
    private readonly LoadingOverlay _loading = new();
    private readonly System.Windows.Forms.Timer _loadingDelay = new() { Interval = 400 };

    private readonly string[] _startupFiles;

    private readonly System.Windows.Forms.Timer _gameWatch = new() { Interval = 1000 };
    private bool _gameRunning;

    public MainForm(ManagerConfig cfg, string[]? startupFiles = null)
    {
        _cfg = cfg;
        _startupFiles = startupFiles ?? Array.Empty<string>();
        _ml = new MelonLoaderService(cfg);
        _mods = new ModService(cfg);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = $"{cfg.GameName} Mod Manager";
        Font = Theme.Ui(9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 720);
        Previews.SetSizes(LogicalToDeviceUnits(72), LogicalToDeviceUnits(176));
        Size = new Size(1280, 780);
        AllowDrop = true;
        Icon = LoadEmbeddedIcon("Assets.icon.ico") ?? Icon;

        BuildShell();
        BuildModsPage();
        BuildLoaderPage();
        BuildSettingsPage();
        BuildLogPage();
        WireShellEvents();

        ResumeLayout(false);
        PerformLayout();
        ShowPage("mods");
    }

    internal static Image? LoadEmbeddedImage(string name)
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream(name);
            if (stream == null) return null;
            using var tmp = Image.FromStream(stream);
            return new Bitmap(tmp); // copy so the stream can be closed
        }
        catch
        {
            return null;
        }
    }

    internal static Icon? LoadEmbeddedIcon(string name)
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream(name);
            return stream == null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    // ───────────────────────── shell layout ─────────────────────────

    private void BuildShell()
    {
        // Sidebar
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 236,
            BackColor = Theme.Sidebar,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(13, 18, 13, 16),
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // brand
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // nav
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // spacer
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // status + play

        var brandRow = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 22),
        };
        brandRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        brandRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var logo = new PictureBox
        {
            Size = new Size(44, 44),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadEmbeddedImage("Assets.logo.png"),
            Margin = new Padding(0, 2, 5, 0),
            BackColor = Theme.Sidebar,
        };
        var brand = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        brandRow.Controls.Add(logo, 0, 0);
        brandRow.Controls.Add(brand, 1, 0);
        brand.Controls.Add(new Label
        {
            Text = _cfg.GameName.ToUpperInvariant(),
            AutoSize = true,
            Font = Theme.BrandSmall(8.5f),
            ForeColor = Theme.Accent,
            Margin = new Padding(1, 0, 0, 0),
        });
        brand.Controls.Add(new Label
        {
            Text = "Mod Manager", // product name: not translated
            AutoSize = true,
            Font = Theme.BrandLarge(14f),
            ForeColor = Theme.Text,
            Margin = new Padding(0),
        });
        sidebar.Controls.Add(brandRow, 0, 0);

        var nav = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        AddNav(nav, "mods", T("Mods"), Theme.GlyphMods);
        AddNav(nav, "loader", "MelonLoader", Theme.GlyphLoader);
        AddNav(nav, "settings", T("Settings"), Theme.GlyphSettings);
        AddNav(nav, "log", T("Activity log"), Theme.GlyphLog);
        sidebar.Controls.Add(nav, 0, 1);

        var bottom = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        bottom.Controls.Add(_sideGame);
        bottom.Controls.Add(_sideLoader);
        bottom.Controls.Add(_sideMods);
        _playBtn.AutoSize = false;
        _playBtn.Size = new Size(210, 46);
        _playBtn.Margin = new Padding(0, 10, 0, 0);
        _playBtn.Font = Theme.Semibold(11.5f);
        bottom.Controls.Add(_playBtn);
        sidebar.Controls.Add(bottom, 0, 3);

        // Status bar under the pages
        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Sidebar };
        statusBar.Controls.Add(_statusLabel);
        statusBar.Controls.Add(_progress);

        _loading.Logo = LoadEmbeddedImage("Assets.logo.png");
        _pageHost.Controls.Add(_loading);
        _loadingDelay.Tick += (_, _) =>
        {
            _loadingDelay.Stop();
            if (_busy) _loading.ShowWith(_statusLabel.Text);
        };

        Controls.Add(_pageHost);
        Controls.Add(statusBar);
        Controls.Add(sidebar);
    }

    private void AddNav(FlowLayoutPanel nav, string key, string text, string glyph)
    {
        var b = new NavButton(text, glyph);
        b.Click += (_, _) => ShowPage(key);
        nav.Controls.Add(b);
        _nav[key] = b;
    }

    private void AddPage(string key, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages[key] = page;
        _pageHost.Controls.Add(page);
    }

    private void ShowPage(string key)
    {
        _currentPage = key;
        foreach (var (k, page) in _pages) page.Visible = k == key;
        foreach (var (k, nav) in _nav) nav.Selected = k == key;
    }

    private void WireShellEvents()
    {
        Shown += async (_, _) => await OnFirstShown();
        FormClosing += (_, e) =>
        {
            if (!_busy || _uninstalled) return;
            e.Cancel = true;
            MessageBox.Show(this, T("Please wait until the current job is finished."), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _playBtn.Click += (_, _) => Play();
        _gameWatch.Tick += (_, _) =>
        {
            bool running = GameValid && GameLocator.IsGameRunning(GameDir);
            if (running == _gameRunning) return;
            _gameRunning = running;
            _playBtn.Text = running ? T("GAME IS RUNNING") : "▶   " + T("PLAY");
            UpdateButtons();
        };
        // Only while the Mod Manager is the active window (background process polling looks suspicious to antivirus)
        Activated += (_, _) => _gameWatch.Start();
        Deactivate += (_, _) => _gameWatch.Stop();
        FormClosed += (_, _) => _gameWatch.Dispose();

        // Drag & drop anywhere on the window
        _grid.AllowDrop = true;
        _emptyState.AllowDrop = true;
        foreach (var target in new Control[] { this, _grid, _emptyState })
        {
            target.DragEnter += OnShellDragEnter;
            target.DragLeave += OnShellDragLeave;
            target.DragDrop += async (_, e) => await HandleDrop(e);
        }
        _dropOverlay.DragEnter += (_, e) =>
        {
            _dropHide.Stop();
            e.Effect = !_busy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        };
        _dropOverlay.DragLeave += OnShellDragLeave;
        _dropOverlay.DragDrop += async (_, e) => await HandleDrop(e);

        // A DragLeave is either the cursor moving to another control (a DragEnter follows at once)
        // or the drag really ending: left the window, Esc pressed, or dropped somewhere else.
        _dropHide.Tick += (_, _) =>
        {
            _dropHide.Stop();
            _dropOverlay.Visible = false;
        };
    }

    private void OnShellDragEnter(object? sender, DragEventArgs e)
    {
        _dropHide.Stop();
        if (_busy || !GameValid || e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = DragDropEffects.Copy;
        if (_currentPage != "mods") ShowPage("mods");
        _dropOverlay.Visible = true;
        _dropOverlay.BringToFront();
    }

    private void OnShellDragLeave(object? sender, EventArgs e)
    {
        // Hide shortly after, unless the drag enters another part of the window first
        _dropHide.Stop();
        _dropHide.Start();
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        _dropHide.Stop();
        _dropOverlay.Visible = false;
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            await InstallPaths(paths);
    }

    private async Task OnFirstShown()
    {
        if (_cfg.LoadError != null) Log(_cfg.LoadError);

        // Finding the game and reading every mod's DLL can take a moment: do it behind the loading screen
        _loading.ShowWith(T("Loading mods…"));
        var saved = AppSettings.LoadGamePath();
        var steamFolder = _cfg.SteamFolderName;
        var found = await Task.Run(() =>
        {
            var dir = saved != null && GameLocator.IsGameFolder(saved) ? saved : GameLocator.AutoDetect(steamFolder);
            if (dir != null)
            {
                try { _mods.Scan(dir, Manifest.Load(dir)); }   // warms the DLL info cache
                catch { /* the real refresh below reports problems */ }
            }
            return dir;
        });
        if (found != null)
        {
            if (!string.Equals(found, saved, StringComparison.OrdinalIgnoreCase)) Log(T("Found the game at {0}", found));
            _gamePath.Text = found;
        }
        _loading.HideOverlay();

        if (!GameValid)
        {
            RefreshAll();
            ShowPage("settings");
        }
        Log(T("Ready."));

        // Later launches (double-clicked .bamod files) send their files here
        SingleInstance.StartServer(files =>
        {
            if (IsDisposed) return;
            try { BeginInvoke(new Action(() => _ = ReceiveFiles(files))); }
            catch (InvalidOperationException) { /* closing */ }
        });

        if (_startupFiles.Length > 0) _ = ReceiveFiles(_startupFiles);
    }

    /// <summary>Files opened from Explorer: bring the window forward and offer to install them.</summary>
    private async Task ReceiveFiles(string[] files)
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        TopMost = true;
        TopMost = false;

        var existing = files.Where(f => File.Exists(f) || Directory.Exists(f)).ToArray();
        if (existing.Length == 0) return;
        if (_busy)
        {
            Log(T("Busy - try opening the file again in a moment."));
            return;
        }
        ShowPage("mods");
        if (!GameValid)
        {
            MessageBox.Show(this, T("Set the {0} folder first, then open the file again.", _cfg.GameName), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowPage("settings");
            return;
        }

        var list = string.Join("\n", existing.Take(10).Select(f => "•  " + Path.GetFileName(f)));
        if (MessageBox.Show(this, (existing.Length == 1 ? T("Install this mod?") : T("Install these mods?")) + "\n\n" + list, Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        await InstallPaths(existing);
    }

    // ───────────────────────── shared state ─────────────────────────

    private string GameDir => (_gamePath?.Text ?? "").Trim().Trim('"');

    private bool GameValid => GameLocator.IsGameFolder(GameDir);

    private void RefreshAll()
    {
        if (_busy) return;
        var dir = GameDir;
        bool valid = GameValid;

        if (valid)
        {
            AppSettings.SaveGamePath(dir);
            try
            {
                _manifest = Manifest.Load(dir);
            }
            catch (Exception ex)
            {
                Log(T("Couldn't read the install record ({0}). Starting a new one.", ex.Message));
                _manifest = new Manifest();
            }
            _entries = _mods.Scan(dir, _manifest);
        }
        else
        {
            _manifest = new Manifest();
            _entries = new List<ModEntry>();
        }

        RefreshSidebar(valid);
        RefreshLoaderPage(valid);
        RefreshSettingsPage(valid);
        PopulateGrid();
        UpdateButtons();
    }

    private void RefreshSidebar(bool valid)
    {
        if (!valid)
        {
            SetSide(_sideGame, Theme.Bad, T("Game folder not set"));
            SetSide(_sideLoader, Theme.TextMuted, "MelonLoader -");
            SetSide(_sideMods, Theme.TextMuted, T("No mods"));
            _nav["mods"].Badge = "";
            return;
        }

        SetSide(_sideGame, Theme.Good, _cfg.GameName);
        if (_ml.IsInstalled(GameDir))
        {
            var v = _ml.GetInstalledVersion(GameDir) ?? _manifest.MelonLoader?.Version;
            SetSide(_sideLoader, _ml.HasProxyDll(GameDir) ? Theme.Good : Theme.Bad,
                _ml.HasProxyDll(GameDir) ? $"MelonLoader {v}".TrimEnd() : T("MelonLoader incomplete"));
        }
        else
        {
            SetSide(_sideLoader, Theme.Accent, T("MelonLoader not installed"));
        }

        int on = _entries.Count(e => e.Enabled);
        SetSide(_sideMods, on > 0 ? Theme.Good : Theme.TextMuted,
            _entries.Count == 0 ? T("No mods installed") : T("{0} of {1} mods enabled", on, _entries.Count));
        _nav["mods"].Badge = _entries.Count > 0 ? _entries.Count.ToString() : "";
    }

    private static void SetSide(StatusLine line, Color dot, string text)
    {
        line.Text = text;
        line.DotColor = dot;
        line.ForeColor = dot == Theme.TextMuted ? Theme.TextMuted : Theme.Text;
    }

    private void UpdateButtons()
    {
        if (_gamePath is null || _mlInstall is null || _dToggle is null) return; // still building
        bool valid = GameValid;
        bool idle = !_busy;
        bool mlInstalled = valid && _ml.IsInstalled(GameDir);
        var selected = SelectedEntries();

        _playBtn.Enabled = idle && valid && !_gameRunning;

        _addBtn.Enabled = idle && valid;
        _addFolderBtn.Enabled = idle && valid;
        _refreshBtn.Enabled = idle;
        _search.Enabled = valid;

        _dToggle.Enabled = idle && selected.Count > 0;
        _dRemove.Enabled = idle && selected.Count > 0;
        _dShow.Enabled = selected.Count > 0;

        _mlInstall.Enabled = idle && valid;
        _mlZip.Enabled = idle && valid;
        _mlUninstall.Enabled = idle && mlInstalled;
        _mlLog.Enabled = valid;
        _mlFolder.Enabled = mlInstalled;

        _gamePath.Enabled = idle;
        _browse.Enabled = idle;
        _detect.Enabled = idle;
        foreach (var b in _folderButtons) b.Enabled = valid;
    }

    private void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}]  {message}{Environment.NewLine}");
        _statusLabel.Text = message.Trim();
        if (_loading.Visible) _loading.SetMessage(message.Trim());
    }

    /// <summary>Runs a job with the UI locked, a progress line and friendly error messages.</summary>
    private async Task RunBusy(string status, Func<Task> work)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        _statusLabel.Text = status;
        _progress.Marquee = true;
        _progress.Visible = true;
        UseWaitCursor = true;
        _loadingDelay.Stop();
        _loadingDelay.Start();   // loading screen only if this takes longer than ~0.4 s

        try
        {
            await work();
        }
        catch (UnauthorizedAccessException ex)
        {
            Log(T("Access denied: ") + ex.Message);
            MessageBox.Show(this,
                T("Windows didn't allow changing a file:\n\n{0}\n\nMake sure the game is closed. If the game is in Program Files, run the mod manager as administrator.", ex.Message),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex) when (ex.HResult == unchecked((int)0x80070020))
        {
            Log(T("File in use: ") + ex.Message);
            MessageBox.Show(this, T("A file is in use by another program - is the game still running?\n\n{0}", ex.Message),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Log(T("Error: ") + ex.Message);
            MessageBox.Show(this, ex.Message, T("Something went wrong"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            _loadingDelay.Stop();
            _loading.HideOverlay();
            UseWaitCursor = false;
            _progress.Visible = false;
            _progress.Marquee = false;
            _progress.Value = 0;
            RefreshAll();
        }
    }

    private bool CheckReady()
    {
        if (_busy) return false;
        if (!GameValid)
        {
            MessageBox.Show(this, T("Set the {0} folder in Settings first.", _cfg.GameName), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowPage("settings");
            return false;
        }
        if (GameLocator.IsGameRunning(GameDir))
        {
            MessageBox.Show(this, T("Close {0} first - files can't be changed while the game is running.", _cfg.GameName),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }
}
