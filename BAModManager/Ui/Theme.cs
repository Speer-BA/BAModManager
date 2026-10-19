using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BAModManager.Ui;

internal enum ButtonKind { Primary, Secondary, Danger, Ghost }

/// <summary>Colours, fonts and small drawing helpers for the dark mod-manager look.</summary>
internal static class Theme
{
    public static readonly Color Sidebar = Color.FromArgb(0x15, 0x16, 0x19);
    public static readonly Color Background = Color.FromArgb(0x1D, 0x1F, 0x23);
    public static readonly Color Panel = Color.FromArgb(0x23, 0x25, 0x2A);
    public static readonly Color Surface = Color.FromArgb(0x2A, 0x2D, 0x33);
    public static readonly Color SurfaceHover = Color.FromArgb(0x34, 0x37, 0x3E);
    public static readonly Color Border = Color.FromArgb(0x3A, 0x3D, 0x45);
    public static readonly Color Text = Color.FromArgb(0xE9, 0xEA, 0xEC);
    public static readonly Color TextMuted = Color.FromArgb(0x96, 0x9A, 0xA3);
    public static readonly Color Accent = Color.FromArgb(0xE2, 0xA2, 0x3A);
    public static readonly Color AccentHover = Color.FromArgb(0xF0, 0xB6, 0x56);
    public static readonly Color AccentText = Color.FromArgb(0x1B, 0x14, 0x07);
    public static readonly Color Good = Color.FromArgb(0x4C, 0xC2, 0x6E);
    public static readonly Color Bad = Color.FromArgb(0xE5, 0x5B, 0x52);
    public static readonly Color ToggleOff = Color.FromArgb(0x4A, 0x4D, 0x56);

    public const string IconFont = "Segoe MDL2 Assets";

    // Segoe MDL2 glyphs
    public const string GlyphMods = "";
    public const string GlyphLoader = "";
    public const string GlyphSettings = "";
    public const string GlyphLog = "";
    public const string GlyphSearch = "";
    public const string GlyphCheck = "";
    public const string GlyphWarning = "";
    public const string GlyphDownload = "";
    public const string GlyphReadme = "\uE8A5";
    public const string GlyphFolder = "\uE838";
    public const string GlyphDelete = "\uE74D";

    // Inter, like the game's own UI (see AppFonts)
    public static Font Ui(float size, FontStyle style = FontStyle.Regular) => AppFonts.Get(AppFonts.Regular, size, style);
    public static Font Medium(float size) => AppFonts.Get(AppFonts.Medium, size);
    public static Font Semibold(float size) => AppFonts.Get(AppFonts.SemiBold, size);
    /// <summary>Big titles - Inter Bold, like "Campaign" / "Multiplayer" in the game.</summary>
    public static Font Heading(float size) => AppFonts.Get(AppFonts.Regular, size, FontStyle.Bold);

    // The "BROKEN ARROW / Mod Manager" sign: always English, always Inter
    public static Font BrandSmall(float size) => AppFonts.Get(AppFonts.SemiBold, size, latinOnly: true);
    public static Font BrandLarge(float size) => AppFonts.Get(AppFonts.Regular, size, FontStyle.Bold, latinOnly: true);
    public static Font Icons(float size) => new(IconFont, size);

    public static Button MakeButton(string text, ButtonKind kind = ButtonKind.Secondary)
    {
        var b = new RoundButton
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Padding = new Padding(12, 3, 12, 3),
            Margin = new Padding(0, 0, 8, 0),
            MinimumSize = new Size(0, 34),
            Font = Ui(9.5f),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = kind is ButtonKind.Secondary or ButtonKind.Danger ? 1 : 0;

        switch (kind)
        {
            case ButtonKind.Primary:
                b.BackColor = Accent;
                b.ForeColor = AccentText;
                b.Font = Semibold(9.5f);
                b.FlatAppearance.MouseOverBackColor = AccentHover;
                b.FlatAppearance.MouseDownBackColor = Accent;
                break;
            case ButtonKind.Danger:
                b.BackColor = Surface;
                b.ForeColor = Bad;
                b.FlatAppearance.BorderColor = Color.FromArgb(0x6A, 0x33, 0x30);
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x3C, 0x26, 0x26);
                b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x4A, 0x2A, 0x2A);
                break;
            case ButtonKind.Ghost:
                b.BackColor = Background;
                b.ForeColor = TextMuted;
                b.FlatAppearance.MouseOverBackColor = Surface;
                b.FlatAppearance.MouseDownBackColor = SurfaceHover;
                break;
            default:
                b.BackColor = Surface;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = SurfaceHover;
                b.FlatAppearance.MouseDownBackColor = Border;
                break;
        }
        return b;
    }

    /// <summary>
    /// Draws text letter by letter with extra spacing (WinForms has no letter-spacing setting).
    /// Returns the drawn width; <paramref name="bottom"/> is the y of the text's bottom edge.
    /// </summary>
    public static int DrawTracked(Graphics g, string text, Font font, int x, int centerY, Color color, float tracking, out int bottom)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        var lineSize = TextRenderer.MeasureText(g, "Hg", font, Size.Empty, flags);
        int y = centerY - lineSize.Height / 2;
        bottom = y + lineSize.Height;

        // A lone space measures as 0 with NoPadding, so measure it between two letters.
        int space = TextRenderer.MeasureText(g, "a a", font, Size.Empty, flags).Width
                    - TextRenderer.MeasureText(g, "aa", font, Size.Empty, flags).Width;

        float cx = x;
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                cx += space + tracking;
                continue;
            }
            var s = ch.ToString();
            int w = TextRenderer.MeasureText(g, s, font, Size.Empty, flags).Width;
            TextRenderer.DrawText(g, s, font, new Point((int)Math.Round(cx), y), color, flags);
            cx += w + tracking;
        }
        return text.Length == 0 ? 0 : (int)Math.Round(cx - tracking - x);
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>iOS/Vortex-style on/off switch centred in <paramref name="bounds"/>.</summary>
    public static void DrawToggle(Graphics g, Rectangle bounds, bool on, float scale = 1f)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = 38 * scale, h = 20 * scale;
        float x = bounds.X + (bounds.Width - w) / 2f;
        float y = bounds.Y + (bounds.Height - h) / 2f;

        using (var track = RoundRect(new RectangleF(x, y, w, h), h / 2))
        using (var fill = new SolidBrush(on ? Good : ToggleOff))
            g.FillPath(fill, track);

        float knob = h - 6 * scale;
        float kx = on ? x + w - knob - 3 * scale : x + 3 * scale;
        using (var kb = new SolidBrush(Color.White))
            g.FillEllipse(kb, kx, y + 3 * scale, knob, knob);
        g.SmoothingMode = old;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    /// <summary>
    /// Gives a control's built-in scrollbars the dark Windows style (Windows 10 1809+ / 11).
    /// Safe to call before the control exists; it's applied when its window is created.
    /// </summary>
    public static void DarkScrollbars(Control control)
    {
        void Apply()
        {
            try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
            catch { /* older Windows: keep the light scrollbars */ }
        }
        if (control.IsHandleCreated) Apply();
        control.HandleCreated += (_, _) => Apply();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Dark window title bar on Windows 10 20H1+ / 11.</summary>
    public static void UseDarkTitleBar(IntPtr handle)
    {
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
        }
        catch
        {
            // older Windows: keep the normal title bar
        }
    }
}

internal sealed class DarkMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Surface;
    public override Color ImageMarginGradientBegin => Theme.Surface;
    public override Color ImageMarginGradientMiddle => Theme.Surface;
    public override Color ImageMarginGradientEnd => Theme.Surface;
    public override Color MenuBorder => Theme.Border;
    public override Color MenuItemBorder => Theme.SurfaceHover;
    public override Color MenuItemSelected => Theme.SurfaceHover;
    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.Border;
}
