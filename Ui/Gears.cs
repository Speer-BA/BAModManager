using System.Drawing.Drawing2D;

namespace BAModManager.Ui;

/// <summary>
/// The Mod Manager logo as a turning gear (its Broken Arrow emblem stays upright), for the loading screens.
/// The two pictures are cut from the 256 px logo: gear_body.png (the gear, emblem area filled in) and
/// gear_emblem.png (the emblem, its cut-outs filled with the window background).
/// Every rotation step is drawn once in advance, so each animation frame is just a quick copy of a ready picture.
/// </summary>
internal static class Gears
{
    private const float SpriteSize = 256f;
    private const float CenterX = 128f, CenterY = 127f;   // gear centre in the pictures
    private const float TipRadius = 125.5f;                // tooth tips

    /// <summary>
    /// The emblem is drawn smaller than in the logo: its arrow tip reaches past the bottom of the teeth,
    /// so it stuck out of the gear whenever a gap between teeth turned under it.
    /// </summary>
    private const float EmblemScale = 0.8f;

    /// <summary>8 teeth: the picture repeats every 45°, so 45° of steps cover every angle.</summary>
    private const float Period = 45f;
    private const int Steps = 90;                           // 0.5° per step


    private sealed class Frames : IDisposable
    {
        public int Radius;
        public Color Background;
        public Bitmap[] Turns = Array.Empty<Bitmap>();
        public Bitmap? Emblem;
        public PointF Pivot;                                // gear centre inside each frame

        public void Dispose()
        {
            foreach (var b in Turns) b.Dispose();
            Emblem?.Dispose();
        }
    }

    private static Frames? _cache;

    /// <summary>Draws the logo gear centred on <paramref name="center"/>, turned clockwise by <paramref name="angle"/>.</summary>
    public static void Draw(Graphics g, PointF center, float radius, float angle, Color background)
    {
        var f = GetFrames((int)Math.Round(radius), background);
        if (f == null) return;

        int step = (int)Math.Round(((angle % Period) + Period) % Period / Period * Steps) % Steps;

        var state = g.Save();
        g.InterpolationMode = InterpolationMode.NearestNeighbor;   // frames are drawn 1:1
        g.PixelOffsetMode = PixelOffsetMode.Half;
        Blit(g, f.Turns[step], center.X, center.Y, f.Pivot);
        if (f.Emblem != null) Blit(g, f.Emblem, center.X, center.Y, new PointF(f.Emblem.Width / 2f, f.Emblem.Height / 2f));
        g.Restore(state);
    }

    private static void Blit(Graphics g, Bitmap bmp, float x, float y, PointF pivot) =>
        g.DrawImage(bmp, (int)Math.Round(x - pivot.X), (int)Math.Round(y - pivot.Y), bmp.Width, bmp.Height);

    /// <summary>All rotation steps at this size, drawn once and kept.</summary>
    private static Frames? GetFrames(int radius, Color background)
    {
        if (_cache != null && _cache.Radius == radius && _cache.Background == background) return _cache;

        var body = MainForm.LoadEmbeddedImage("Assets.gear_body.png");
        var emblem = MainForm.LoadEmbeddedImage("Assets.gear_emblem.png");
        if (body == null) return null;

        float scale = radius / TipRadius;
        int size = (int)Math.Ceiling(SpriteSize * scale * 1.05f) + 2;   // room for the corners while turning
        var pivot = new PointF(size / 2f, size / 2f);
        var frames = new Frames { Radius = radius, Background = background, Pivot = pivot, Turns = new Bitmap[Steps] };
        for (int i = 0; i < Steps; i++)
            frames.Turns[i] = Render(body, size, pivot, scale, i * Period / Steps);

        if (emblem != null)
        {
            float es = scale * EmblemScale;
            int esize = (int)Math.Ceiling(SpriteSize * es) + 2;
            var bmp = new Bitmap(esize, esize, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Quality(g);
                g.DrawImage(emblem, esize / 2f - CenterX * es, esize / 2f - CenterY * es, SpriteSize * es, SpriteSize * es);
            }
            frames.Emblem = bmp;
            emblem.Dispose();
        }
        body.Dispose();

        _cache?.Dispose();
        _cache = frames;
        return frames;
    }

    private static Bitmap Render(Image body, int size, PointF pivot, float scale, float angle)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        Quality(g);
        g.TranslateTransform(pivot.X, pivot.Y);
        g.RotateTransform(angle);
        g.DrawImage(body, -CenterX * scale, -CenterY * scale, SpriteSize * scale, SpriteSize * scale);
        return bmp;
    }

    private static void Quality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }
}
