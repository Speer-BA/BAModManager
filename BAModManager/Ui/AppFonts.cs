using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BAModManager.Ui;

/// <summary>
/// Loads the embedded Inter font (the typeface Broken Arrow's UI uses) for the whole app.
/// Falls back to Segoe UI if anything goes wrong.
/// </summary>
internal static class AppFonts
{
    public const string Regular = "Inter";
    public const string Medium = "Inter Medium";
    public const string SemiBold = "Inter SemiBold";
    public const string ExtraBold = "Inter ExtraBold";

    // Inter has a taller x-height than Segoe UI; this keeps sizes visually the same.
    private const float SizeScale = 0.94f;

    private static readonly PrivateFontCollection Collection = new();
    private static readonly Dictionary<string, FontFamily> Families = new(StringComparer.OrdinalIgnoreCase);

    public static bool InterLoaded { get; private set; }

    // GDI (TextRenderer, used by labels, buttons, the grid) only sees private fonts registered here.
    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

    public static void Load()
    {
        var asm = typeof(AppFonts).Assembly;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) continue;
                var data = new byte[stream.Length];
                stream.ReadExactly(data);

                // The memory must stay alive while the app runs, so it is never freed.
                var ptr = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, ptr, data.Length);
                Collection.AddMemoryFont(ptr, data.Length);
                AddFontMemResourceEx(ptr, (uint)data.Length, IntPtr.Zero, out _);
            }
            catch
            {
                // skip this weight
            }
        }

        foreach (var family in Collection.Families) Families[family.Name] = family;
        InterLoaded = Families.ContainsKey(Regular);
    }

    /// <summary>Gets a font from the embedded set, with sensible fallbacks.</summary>
    public static Font Get(string family, float size, FontStyle style = FontStyle.Regular, bool latinOnly = false)
    {
        // Inter has no Chinese characters: use Windows' Chinese UI font for the whole app
        // (except Latin-only text such as the brand, which keeps Inter)
        if (Core.Loc.IsChinese && !latinOnly)
        {
            var cjk = family is SemiBold or ExtraBold ? style | FontStyle.Bold : style;
            return new Font("Microsoft YaHei UI", size * 0.95f, cjk);
        }

        if (Families.TryGetValue(family, out var ff) && ff.IsStyleAvailable(style))
            return new Font(ff, size * SizeScale, style);

        // Weight missing: use Inter Regular/Bold instead
        if (InterLoaded && Families.TryGetValue(Regular, out var inter))
        {
            var heavy = family is SemiBold or ExtraBold ? FontStyle.Bold : FontStyle.Regular;
            var s = style | heavy;
            if (inter.IsStyleAvailable(s)) return new Font(inter, size * SizeScale, s);
        }

        // No Inter at all
        return family switch
        {
            SemiBold or Medium => new Font("Segoe UI Semibold", size, style),
            ExtraBold => new Font("Segoe UI", size, style | FontStyle.Bold),
            _ => new Font("Segoe UI", size, style),
        };
    }
}
