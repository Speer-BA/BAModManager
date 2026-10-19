using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BAModManager.Ui;

/// <summary>
/// Loads mod preview pictures, keeps small and large square copies in memory,
/// and falls back to the default picture.
/// </summary>
internal static class Previews
{
    public const int StoredSize = 256;

    private sealed record Entry(DateTime Stamp, Image Small, Image Large);

    private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static Entry? _default;
    private static int _smallSize = 48;
    private static int _largeSize = 200;

    /// <summary>Sizes in device pixels; changing them clears the cache.</summary>
    public static void SetSizes(int small, int large)
    {
        if (small == _smallSize && large == _largeSize) return;
        _smallSize = small;
        _largeSize = large;
        Clear();
    }

    public static Image Small(string? path) => Get(path).Small;
    public static Image Large(string? path) => Get(path).Large;

    public static void Clear()
    {
        foreach (var e in Cache.Values) { e.Small.Dispose(); e.Large.Dispose(); }
        Cache.Clear();
        if (_default != null) { _default.Small.Dispose(); _default.Large.Dispose(); _default = null; }
    }

    private static Entry Get(string? path)
    {
        if (path != null)
        {
            try
            {
                var stamp = File.GetLastWriteTimeUtc(path);
                if (Cache.TryGetValue(path, out var hit) && hit.Stamp == stamp) return hit;

                using var source = LoadUnlocked(path);
                var entry = new Entry(stamp, Square(source, _smallSize), Square(source, _largeSize));
                if (Cache.Remove(path, out var old)) { old.Small.Dispose(); old.Large.Dispose(); }
                Cache[path] = entry;
                return entry;
            }
            catch
            {
                // broken image: use the default
            }
        }
        return _default ??= LoadDefault();
    }

    private static Entry LoadDefault()
    {
        using var stream = typeof(Previews).Assembly.GetManifestResourceStream("Assets.default_preview.png");
        Image source = stream != null ? Image.FromStream(stream) : new Bitmap(1, 1);
        using (source)
            return new Entry(DateTime.MinValue, Square(source, _smallSize), Square(source, _largeSize));
    }

    /// <summary>Reads an image without keeping the file locked.</summary>
    private static Image LoadUnlocked(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var ms = new MemoryStream(bytes);
        using var img = Image.FromStream(ms);
        return new Bitmap(img);
    }

    /// <summary>Centre-crops to 1:1 and scales to <paramref name="size"/>.</summary>
    public static Bitmap Square(Image source, int size)
    {
        int side = Math.Min(source.Width, source.Height);
        var src = new Rectangle((source.Width - side) / 2, (source.Height - side) / 2, side, side);
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, size, size), src, GraphicsUnit.Pixel);
        return bmp;
    }

    /// <summary>Turns any picture the user picks into a 256×256 PNG at <paramref name="destination"/>.</summary>
    public static void SaveAsPreview(string sourcePath, string destination)
    {
        using var source = LoadUnlocked(sourcePath);
        using var square = Square(source, StoredSize);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        square.Save(destination, ImageFormat.Png);
    }

    /// <summary>Draws a picture with softly rounded corners.</summary>
    public static void DrawRounded(Graphics g, Image image, Rectangle rect, float radius)
    {
        var oldSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(rect, radius);
        using var brush = new TextureBrush(image, WrapMode.Clamp);
        brush.TranslateTransform(rect.X, rect.Y);
        brush.ScaleTransform(rect.Width / (float)image.Width, rect.Height / (float)image.Height);
        g.FillPath(brush, path);
        g.SmoothingMode = oldSmoothing;
    }
}
