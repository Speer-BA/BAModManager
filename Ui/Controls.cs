using System.Drawing.Drawing2D;
using static BAModManager.Core.Loc;

namespace BAModManager.Ui;

/// <summary>
/// Sidebar navigation entry, styled like Broken Arrow's top tabs ("Home  Play  Arsenal  Editor"):
/// Inter SemiBold with wide letter spacing, muted when inactive, white with an underline when selected.
/// </summary>
internal sealed class NavButton : Control
{
    private static readonly Color Inactive = Color.FromArgb(0x9C, 0xA1, 0xA6);
    private static readonly Color HoverText = Color.FromArgb(0xCF, 0xD3, 0xD6);

    private bool _selected;
    private bool _hover;
    private string _badge = "";

    public string Glyph { get; }

    /// <summary>Extra space between letters, in logical pixels.</summary>
    public float Tracking { get; set; } = 1.4f;

    public string Badge
    {
        get => _badge;
        set { _badge = value; Invalidate(); }
    }

    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public NavButton(string text, string glyph)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Text = text;
        Glyph = glyph;
        Size = new Size(210, 46);
        Margin = new Padding(0, 0, 0, 2);
        Cursor = Cursors.Hand;
        Font = Theme.Semibold(12f);
        BackColor = Theme.Sidebar;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        float scale = DeviceDpi / 96f;
        var color = _selected ? Theme.Text : _hover ? HoverText : Inactive;

        int iconLeft = (int)(4 * scale), iconWidth = (int)(26 * scale);
        using (var icon = Theme.Icons(11f))
            TextRenderer.DrawText(g, Glyph, icon, new Rectangle(iconLeft, 0, iconWidth, Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

        int textX = iconLeft + iconWidth + (int)(10 * scale);
        // Longer translations: shrink the text until it fits next to the badge
        int avail = Width - textX - (int)((_badge.Length > 0 ? 40 : 8) * scale);
        float tracking = Tracking * scale;
        var font = Font;
        const TextFormatFlags fit = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        while (TextRenderer.MeasureText(g, Text, font, Size.Empty, fit).Width + tracking * Text.Length > avail && font.Size > 8f)
        {
            if (font != Font) font.Dispose();
            font = new Font(font.FontFamily, font.Size - 0.5f, font.Style);
            tracking = Math.Max(0.5f * scale, tracking - 0.2f * scale);
        }
        int textWidth = Theme.DrawTracked(g, Text, font, textX, Height / 2, color, tracking, out int textBottom);
        if (font != Font) font.Dispose();

        if (_selected)
        {
            using var line = new SolidBrush(Theme.Text);
            g.FillRectangle(line, textX, textBottom + (int)(4 * scale), textWidth, Math.Max(2, (int)(2 * scale)));
        }

        if (_badge.Length > 0)
        {
            using var small = Theme.Ui(8.5f);
            var size = TextRenderer.MeasureText(_badge, small);
            var r = new Rectangle(Width - size.Width - (int)(14 * scale), (Height - (int)(20 * scale)) / 2,
                                  size.Width + (int)(8 * scale), (int)(20 * scale));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.RoundRect(r, r.Height / 2f);
            using var bb = new SolidBrush(Theme.Surface);
            g.FillPath(bb, path);
            TextRenderer.DrawText(g, _badge, small, r, Theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }
}

/// <summary>Thin accent progress line; marquee mode for jobs without a known length.</summary>
internal sealed class ThinProgress : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private float _value;
    private bool _marquee;
    private float _phase;

    public ThinProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 3;
        BackColor = Theme.Sidebar;
        _timer.Tick += (_, _) =>
        {
            _phase = (_phase + 0.012f) % 1.4f;
            Invalidate();
        };
    }

    public float Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0f, 1f); Invalidate(); }
    }

    public bool Marquee
    {
        get => _marquee;
        set { _marquee = value; _timer.Enabled = value && Visible; Invalidate(); }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        _timer.Enabled = _marquee && Visible;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        using var brush = new SolidBrush(Theme.Accent);
        if (_marquee)
        {
            float seg = Width * 0.35f;
            float x = (_phase - 0.35f) * Width;
            e.Graphics.FillRectangle(brush, x, 0, seg, Height);
        }
        else
        {
            e.Graphics.FillRectangle(brush, 0, 0, Width * _value, Height);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Windows' timer normally ticks every ~15.6 ms, which makes a 60 fps animation stutter.
/// While an animation runs, ask for 1 ms timer resolution.
/// </summary>
internal static class HighResTimer
{
    [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
    [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

    public static void Begin() { try { timeBeginPeriod(1); } catch { /* not available */ } }
    public static void End() { try { timeEndPeriod(1); } catch { /* not available */ } }
}

/// <summary>T("Drop to install") overlay shown while files are dragged over the window.</summary>
internal sealed class DropOverlay : Control
{
    public DropOverlay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(0x1A, 0x1C, 0x20);
        AllowDrop = true;
        Visible = false;
        Dock = DockStyle.Fill;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(10, 10, Width - 21, Height - 21);
        using (var pen = new Pen(Theme.Accent, 2f) { DashStyle = DashStyle.Dash })
        using (var path = Theme.RoundRect(r, 12))
            g.DrawPath(pen, path);

        int cy = Height / 2;
        using (var icon = Theme.Icons(34f))
            TextRenderer.DrawText(g, Theme.GlyphDownload, icon, new Rectangle(0, cy - 90, Width, 60), Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        using (var title = Theme.Semibold(17f))
            TextRenderer.DrawText(g, T("Drop to install"), title, new Rectangle(0, cy - 26, Width, 36), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        using (var sub = Theme.Ui(10f))
            TextRenderer.DrawText(g, T("MelonLoader mods (.dll), models (.bundle) and mod packs (.zip)"), sub,
                new Rectangle(0, cy + 12, Width, 26), Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Coloured dot + text, used for the sidebar status lines.</summary>
internal sealed class StatusLine : Control
{
    private Color _dot = Theme.TextMuted;

    public StatusLine()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(210, 22);
        Margin = new Padding(0, 0, 0, 3);
        Font = Theme.Ui(9f);
        ForeColor = Theme.TextMuted;
        BackColor = Theme.Sidebar;
    }

    public Color DotColor
    {
        get => _dot;
        set { _dot = value; Invalidate(); }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var dot = new SolidBrush(_dot))
            g.FillEllipse(dot, 3, (Height - 8) / 2f, 8, 8);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(18, 0, Width - 18, Height), ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// Slim dark scrollbar (rounded thumb that widens on hover) used instead of the stock Windows one.
/// Vertical or horizontal. Units are up to the owner: rows for the mod list, pixels for the file list.
/// Maximum = total size, LargeChange = visible size, Value = first visible position.
/// </summary>
internal sealed class ThinScrollBar : Control
{
    private int _maximum;
    private int _largeChange = 1;
    private int _value;
    private bool _hover;
    private bool _dragging;
    private int _dragOffset;

    public event EventHandler? ValueChanged;

    /// <summary>How far one mouse-wheel notch moves.</summary>
    public int SmallChange { get; set; } = 1;

    public Orientation Orientation { get; set; } = Orientation.Vertical;

    private bool Horizontal => Orientation == Orientation.Horizontal;

    public ThinScrollBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Width = 14;
        Height = 14;
        BackColor = Theme.Panel;
        Cursor = Cursors.Default;
    }

    private int MaxValue => Math.Max(0, _maximum - _largeChange);

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, 0, MaxValue);
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Updates the range without raising ValueChanged.</summary>
    public void SetRange(int maximum, int largeChange, int value)
    {
        _maximum = Math.Max(0, maximum);
        _largeChange = Math.Max(1, largeChange);
        _value = Math.Clamp(value, 0, MaxValue);
        Invalidate();
    }

    public bool CanScroll => _maximum > _largeChange;

    private int Length => Horizontal ? Width : Height;
    private int Thickness => Horizontal ? Height : Width;

    /// <summary>Thumb position along the bar: (start, size).</summary>
    private (int Start, int Size) ThumbSpan()
    {
        int pad = (int)(4 * DeviceDpi / 96f);
        int track = Math.Max(0, Length - pad * 2);
        if (!CanScroll || track <= 0) return (0, 0);
        int minThumb = (int)(36 * DeviceDpi / 96f);
        int size = Math.Min(track, Math.Max(minThumb, (int)((long)track * _largeChange / Math.Max(1, _maximum))));
        int start = pad + (MaxValue == 0 ? 0 : (int)((long)(track - size) * _value / MaxValue));
        return (start, size);
    }

    private Rectangle Thumb
    {
        get
        {
            var (start, size) = ThumbSpan();
            if (size == 0) return Rectangle.Empty;
            int t = _hover || _dragging ? Thickness - 4 : Thickness - 8;
            int across = (Thickness - t) / 2;
            return Horizontal ? new Rectangle(start, across, size, t) : new Rectangle(across, start, t, size);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var thumb = Thumb;
        if (thumb.IsEmpty) return;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var color = _dragging ? Color.FromArgb(0x6A, 0x6E, 0x77) : _hover ? Color.FromArgb(0x55, 0x59, 0x61) : Theme.Border;
        using var path = Theme.RoundRect(thumb, Math.Min(thumb.Width, thumb.Height) / 2f);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    private int Along(MouseEventArgs e) => Horizontal ? e.X : e.Y;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !CanScroll) return;
        var (start, size) = ThumbSpan();
        int pos = Along(e);
        if (pos >= start && pos <= start + size)
        {
            _dragging = true;
            _dragOffset = pos - start;
            Capture = true;
            Invalidate();
        }
        else
        {
            Value += pos < start ? -_largeChange : _largeChange;   // page back / forward
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        int pad = (int)(4 * DeviceDpi / 96f);
        var (_, size) = ThumbSpan();
        int travel = Length - pad * 2 - size;
        if (travel <= 0) return;
        int p = Math.Clamp(Along(e) - _dragOffset - pad, 0, travel);
        Value = (int)Math.Round(p / (double)travel * MaxValue);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value -= Math.Sign(e.Delta) * SmallChange;
    }
}

/// <summary>
/// Read-only list of file paths with the same slim scrollbars as the mod list
/// (vertical and horizontal). Full path shows as a tooltip.
/// </summary>
internal sealed class FileListView : Control
{
    private readonly List<string> _items = new();
    private readonly ThinScrollBar _vbar = new() { Orientation = Orientation.Vertical, Visible = false };
    private readonly ThinScrollBar _hbar = new() { Orientation = Orientation.Horizontal, Visible = false };
    private readonly ToolTip _tip = new();
    private int _contentWidth;
    private int _hoverIndex = -1;

    public FileListView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Background;
        ForeColor = Theme.TextMuted;
        Font = new Font("Consolas", 9f);
        _vbar.BackColor = _hbar.BackColor = Theme.Background;
        Controls.Add(_vbar);
        Controls.Add(_hbar);
        _vbar.ValueChanged += (_, _) => Invalidate();
        _hbar.ValueChanged += (_, _) => Invalidate();
    }

    private int LineHeight => TextRenderer.MeasureText("Hg", Font).Height + (int)(3 * DeviceDpi / 96f);
    private int Pad => (int)(8 * DeviceDpi / 96f);
    private int BarSize => (int)(12 * DeviceDpi / 96f);

    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _contentWidth = 0;
        foreach (var item in _items)
            _contentWidth = Math.Max(_contentWidth, TextRenderer.MeasureText(item, Font, Size.Empty, TextFormatFlags.NoPadding).Width);
        _vbar.SetRange(0, 1, 0);
        _hbar.SetRange(0, 1, 0);
        LayoutBars();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutBars();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        SetItems(_items.ToList());
    }

    private void LayoutBars()
    {
        int lineH = LineHeight;
        bool needH = _contentWidth + Pad * 2 > Width;
        int viewH = Height - Pad * 2 - (needH ? BarSize : 0);
        int visibleLines = Math.Max(1, viewH / Math.Max(1, lineH));
        bool needV = _items.Count > visibleLines;
        if (needV && !needH) needH = _contentWidth + Pad * 2 > Width - BarSize;

        int viewW = Width - Pad * 2 - (needV ? BarSize : 0);
        _vbar.SetRange(_items.Count, visibleLines, _vbar.Value);
        _hbar.SetRange(_contentWidth, Math.Max(1, viewW), _hbar.Value);
        _hbar.SmallChange = lineH * 3;

        _vbar.Visible = needV;
        _hbar.Visible = needH;
        _vbar.Bounds = new Rectangle(Width - BarSize, 0, BarSize, Height - (needH ? BarSize : 0));
        _hbar.Bounds = new Rectangle(0, Height - BarSize, Width - (needV ? BarSize : 0), BarSize);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        int lineH = LineHeight;
        int first = _vbar.Visible ? _vbar.Value : 0;
        int x = Pad - (_hbar.Visible ? _hbar.Value : 0);
        int bottom = Height - (_hbar.Visible ? BarSize : 0);
        int right = Width - (_vbar.Visible ? BarSize : 0);
        g.SetClip(new Rectangle(0, 0, right, bottom));
        int y = Pad;
        for (int i = first; i < _items.Count && y < bottom; i++, y += lineH)
        {
            var color = i == _hoverIndex ? Theme.Text : ForeColor;
            TextRenderer.DrawText(g, _items[i], Font, new Point(x, y), color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        g.ResetClip();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((ModifierKeys & Keys.Shift) != 0 && _hbar.Visible) _hbar.Value -= Math.Sign(e.Delta) * _hbar.SmallChange;
        else if (_vbar.Visible) _vbar.Value -= Math.Sign(e.Delta) * 3;
        else if (_hbar.Visible) _hbar.Value -= Math.Sign(e.Delta) * _hbar.SmallChange;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = (e.Y - Pad) / Math.Max(1, LineHeight) + (_vbar.Visible ? _vbar.Value : 0);
        if (e.Y < Pad || index >= _items.Count) index = -1;
        if (index == _hoverIndex) return;
        _hoverIndex = index;
        _tip.SetToolTip(this, index >= 0 ? _items[index] : "");
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverIndex = -1;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Flat button with rounded corners. Colours come from BackColor and FlatAppearance
/// (MouseOverBackColor, MouseDownBackColor, BorderColor/BorderSize), so it's a drop-in for a flat Button.
/// </summary>
internal sealed class RoundButton : Button
{
    private bool _hover;
    private bool _down;

    /// <summary>Corner radius in logical pixels.</summary>
    public float Radius { get; set; } = 4f;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    /// <summary>The colour behind the button (first parent that isn't transparent).</summary>
    private Color SurroundColor()
    {
        for (Control? c = Parent; c != null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return Theme.Background;
    }

    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var surround = SurroundColor();
        g.Clear(surround);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var back = BackColor;
        if (Enabled && _down && FlatAppearance.MouseDownBackColor != Color.Empty) back = FlatAppearance.MouseDownBackColor;
        else if (Enabled && _hover && FlatAppearance.MouseOverBackColor != Color.Empty) back = FlatAppearance.MouseOverBackColor;
        if (!Enabled) back = Blend(back, surround, 0.55f);
        if (back.A < 255) back = Blend(surround, Color.FromArgb(back.R, back.G, back.B), back.A / 255f);

        float radius = Radius * DeviceDpi / 96f;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.RoundRect(rect, radius))
        {
            if (back != surround)
                using (var brush = new SolidBrush(back))
                    g.FillPath(brush, path);
            if (FlatAppearance.BorderSize > 0 && FlatAppearance.BorderColor != Color.Empty)
            {
                var border = Enabled ? FlatAppearance.BorderColor : Blend(FlatAppearance.BorderColor, surround, 0.5f);
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }
        }

        var fore = Enabled ? ForeColor : Blend(ForeColor, surround, 0.55f);
        var textRect = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
        TextRenderer.DrawText(g, Text, Font, textRect, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Full-page loading screen: the logo gear turning inside a ring with a spinning amber arc, and a status line.</summary>
internal sealed class LoadingOverlay : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private const double ArcDegreesPerSecond = 300;    // the amber arc, like before
    private const double GearDegreesPerSecond = 45;    // the logo gear turns slower
    private float _gearAngle;
    private float _angle;
    private string _message = "";
    private Rectangle _gearArea;

    /// <summary>Kept for older callers - the gears are drawn as shapes now.</summary>
    public Image? Logo { get; set; }

    public LoadingOverlay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Background;
        Dock = DockStyle.Fill;
        Visible = false;
        Cursor = Cursors.WaitCursor;
        _timer.Tick += (_, _) =>
        {
            // angle from the clock, not from counting ticks: a late tick doesn't slow the gears down
            var t = _clock.Elapsed.TotalSeconds;
            _angle = (float)(t * ArcDegreesPerSecond % 360);
            _gearAngle = (float)(t * GearDegreesPerSecond % 360);
            if (_gearArea.IsEmpty) Invalidate();
            else Invalidate(_gearArea);   // only the gears change
        };
    }

    public void ShowWith(string message)
    {
        _message = message;
        Visible = true;
        BringToFront();
        _clock.Start();
        if (!_timer.Enabled) HighResTimer.Begin();
        _timer.Start();
        Invalidate();
        Update();
    }

    public void SetMessage(string message)
    {
        if (message == _message) return;
        _message = message;
        Invalidate();
    }

    public void HideOverlay()
    {
        if (_timer.Enabled) HighResTimer.End();
        _timer.Stop();
        _clock.Stop();
        Visible = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        float scale = DeviceDpi / 96f;
        float radius = 38 * scale;
        var center = new PointF(Width / 2f, Height / 2f - 20 * scale);
        var ring = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        using (var track = new Pen(Theme.Border, 3 * scale))
            g.DrawEllipse(track, ring);
        using (var arc = new Pen(Theme.Accent, 3 * scale) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            g.DrawArc(arc, ring, _angle - 90, 100);

        // the logo, as a turning gear
        Gears.Draw(g, center, 25 * scale, _gearAngle, BackColor);

        int pad = (int)(radius + 6 * scale);
        _gearArea = new Rectangle((int)center.X - pad, (int)center.Y - pad, pad * 2, pad * 2);

        if (_message.Length > 0)
        {
            using var font = Theme.Ui(10.5f);
            var rect = new Rectangle(0, (int)(center.Y + radius + 18 * scale), Width, (int)(30 * scale));
            TextRenderer.DrawText(g, _message, font, rect, Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_timer.Enabled) HighResTimer.End();
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
