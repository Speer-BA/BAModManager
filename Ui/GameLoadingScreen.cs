using System.Diagnostics;
using static BAModManager.Core.Loc;

namespace BAModManager.Ui;

/// <summary>
/// Shown instead of MelonLoader's console while the modded game starts: follows MelonLoader's
/// Latest.log and closes itself once the mods are loaded and the game window is up.
/// </summary>
internal sealed class GameLoadingScreen : Form
{
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(20);

    private readonly Process _game;
    private readonly string _logPath;
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 300 };
    private readonly LoadingOverlay _overlay = new();
    private readonly Label _hint;
    private DateTime? _doneAt;
    private bool _sawLog;

    /// <summary>Set when the game closed before it finished loading.</summary>
    public string? Failure { get; private set; }

    public GameLoadingScreen(Process game, string gameDir, Image? logo, Icon? icon)
    {
        _game = game;
        _logPath = Path.Combine(gameDir, "MelonLoader", "Latest.log");

        Text = "Broken Arrow";
        if (icon != null) Icon = icon;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 320);
        BackColor = Theme.Background;
        TopMost = true;
        ShowInTaskbar = true;
        Padding = new Padding(1);
        KeyPreview = true;

        var brand = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Background };
        brand.Paint += (_, e) =>
        {
            float s = DeviceDpi / 96f;
            using var small = Theme.BrandSmall(8.5f);
            using var big = Theme.BrandLarge(15f);
            TextRenderer.DrawText(e.Graphics, "BROKEN ARROW", small, new Point((int)(24 * s), (int)(16 * s)), Theme.Accent);
            TextRenderer.DrawText(e.Graphics, T("Starting with mods"), big, new Point((int)(23 * s), (int)(30 * s)), Theme.Text);
        };

        _hint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextMuted,
            Font = Theme.Ui(9f),
            BackColor = Theme.Background,
            Text = "",
        };

        _overlay.Logo = logo;
        _overlay.Cursor = Cursors.Default;

        Controls.Add(_overlay);
        Controls.Add(_hint);
        Controls.Add(brand);

        _poll.Tick += (_, _) => Poll();
        Shown += (_, _) =>
        {
            _overlay.ShowWith(T("Starting MelonLoader…"));
            _poll.Start();
        };
        // Esc hides the loading screen (the game keeps loading)
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private void Poll()
    {
        bool exited;
        try { exited = _game.HasExited; }
        catch { exited = true; }
        if (exited)
        {
            if (_doneAt == null) Failure = T("Broken Arrow closed while it was loading.");
            Close();
            return;
        }

        if (ReadNewLog())
        {
            _sawLog = true;
            if (_seen.Contains("Support Module Loaded"))
            {
                _overlay.SetMessage(T("Starting Broken Arrow…"));
                _hint.Text = "";
                _doneAt ??= DateTime.UtcNow;
            }
            else if (_seen.Contains("Mods loaded."))
            {
                _overlay.SetMessage(T("Starting Broken Arrow…"));
            }
            else if (_seen.Contains("Loading Mods..."))
            {
                _overlay.SetMessage(T("Loading mods…"));
                _hint.Text = "";
            }
            else if (_seen.Contains("Il2CppAssemblyGenerator"))
            {
                _overlay.SetMessage(T("Preparing game files…"));
                bool generating = _seen.Contains("Executing")
                                  || (_seen.Contains("Generation Needed") && !_seen.Contains("No Generation Needed"));
                if (generating) _hint.Text = T("First start after installing or updating - this takes a few minutes.");
            }
            else
            {
                _overlay.SetMessage(T("Starting MelonLoader…"));
            }
        }

        if (_doneAt != null)
        {
            var since = DateTime.UtcNow - _doneAt.Value;
            bool window = false;
            try { _game.Refresh(); window = _game.MainWindowHandle != IntPtr.Zero; }
            catch { /* ignore */ }
            if ((window && since > TimeSpan.FromSeconds(2)) || since > TimeSpan.FromSeconds(15)) Close();
        }
        else if (!_sawLog && DateTime.UtcNow - _startedUtc > TimeSpan.FromSeconds(60))
        {
            // MelonLoader never wrote a log: it isn't loading (e.g. version.dll missing) - don't hang around
            Close();
        }
        else if (DateTime.UtcNow - _startedUtc > GiveUpAfter)
        {
            Close();
        }
    }

    private static readonly string[] Markers =
    {
        "Support Module Loaded", "Mods loaded.", "Loading Mods...", "Il2CppAssemblyGenerator",
        "Executing", "Generation Needed", "No Generation Needed",
    };

    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private long _logPos;
    private string _carry = "";

    /// <summary>
    /// Reads only what MelonLoader added to the log since the last poll (the log grows to megabytes;
    /// reading all of it 3 times a second made the animation stutter). False until this start's log exists.
    /// </summary>
    private bool ReadNewLog()
    {
        try
        {
            var info = new FileInfo(_logPath);
            if (!info.Exists || info.LastWriteTimeUtc < _startedUtc.AddSeconds(-2)) return false;
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length < _logPos) { _logPos = 0; _carry = ""; _seen.Clear(); }   // a new log file
            if (fs.Length == _logPos) return true;
            fs.Position = _logPos;
            var buffer = new byte[Math.Min(fs.Length - _logPos, 4 * 1024 * 1024)];
            int read = fs.Read(buffer, 0, buffer.Length);
            _logPos += read;
            var text = _carry + System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            foreach (var m in Markers)
                if (text.Contains(m, StringComparison.Ordinal)) _seen.Add(m);
            _carry = text.Length > 64 ? text[^64..] : text;   // a marker split between two reads
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _poll.Stop();
        _poll.Dispose();
        base.OnFormClosed(e);
    }
}
