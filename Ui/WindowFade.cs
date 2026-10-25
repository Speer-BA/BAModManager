namespace BAModManager.Ui;

/// <summary>Short fade-out / fade-in so switching between the launcher and the mod manager is easy to follow.</summary>
internal static class WindowFade
{
    private const double Duration = 160; // ms

    /// <summary>Fades the window out, hides it, then runs <paramref name="done"/>.</summary>
    public static void Out(Form form, Action done)
    {
        Animate(form, 1, 0, () =>
        {
            form.Hide();
            form.Opacity = 1;
            done();
        });
    }

    /// <summary>Shows the window and fades it in.</summary>
    public static void In(Form form)
    {
        form.Opacity = 0;
        if (!form.Visible) form.Show();
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.Activate();
        Animate(form, 0, 1, null);
    }

    /// <summary>Puts <paramref name="form"/> where <paramref name="from"/> is (same centre, kept on screen).</summary>
    public static void CenterOn(Form form, Form from)
    {
        var c = new Point(from.Left + from.Width / 2, from.Top + from.Height / 2);
        var area = Screen.FromPoint(c).WorkingArea;
        int x = Math.Clamp(c.X - form.Width / 2, area.Left, Math.Max(area.Left, area.Right - form.Width));
        int y = Math.Clamp(c.Y - form.Height / 2, area.Top, Math.Max(area.Top, area.Bottom - form.Height));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(x, y);
    }

    private static void Animate(Form form, double from, double to, Action? done)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        form.Opacity = from;
        timer.Tick += (_, _) =>
        {
            if (form.IsDisposed)
            {
                timer.Dispose();
                return;
            }
            double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / Duration);
            double eased = 1 - Math.Pow(1 - t, 3);   // ease-out
            form.Opacity = from + (to - from) * eased;
            if (t < 1) return;
            timer.Stop();
            timer.Dispose();
            done?.Invoke();
        };
        timer.Start();
    }
}
