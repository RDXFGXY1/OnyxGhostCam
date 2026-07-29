using OpenCvSharp;

namespace Onyx.Core.Processing;

/// <summary>
/// Draws a fighter-jet tactical HUD over the output frame: frame corner
/// brackets, a center reticle, lock-on targeting brackets around detected faces,
/// scanlines, and corner telemetry. Purely cosmetic — on-brand for Ghost Cam.
/// </summary>
public static class HudOverlay
{
    private static readonly Scalar Red = new(40, 40, 225);
    private static readonly Scalar White = new(235, 235, 235);
    private static int _tick;

    public static void Draw(Mat f, IReadOnlyList<Rect> faces, bool cloaked, bool live, int fps, int contacts)
    {
        if (f.Empty()) { return; }
        int w = f.Width, h = f.Height, m = 14;
        int L = Math.Min(w, h) / 12;
        _tick++;

        FrameBracket(f, m, m, 1, 1, L);
        FrameBracket(f, w - m, m, -1, 1, L);
        FrameBracket(f, m, h - m, 1, -1, L);
        FrameBracket(f, w - m, h - m, -1, -1, L);

        // Center reticle.
        var c = new Point(w / 2, h / 2);
        Cv2.Circle(f, c, 20, Red, 1, LineTypes.AntiAlias);
        Cv2.Line(f, new Point(c.X - 30, c.Y), new Point(c.X - 10, c.Y), Red, 1, LineTypes.AntiAlias);
        Cv2.Line(f, new Point(c.X + 10, c.Y), new Point(c.X + 30, c.Y), Red, 1, LineTypes.AntiAlias);
        Cv2.Line(f, new Point(c.X, c.Y - 30), new Point(c.X, c.Y - 10), Red, 1, LineTypes.AntiAlias);
        Cv2.Line(f, new Point(c.X, c.Y + 10), new Point(c.X, c.Y + 30), Red, 1, LineTypes.AntiAlias);

        // Lock-on targets.
        foreach (var r in faces) { Target(f, r); }

        // Telemetry.
        Text(f, "GHOST CAM // TACTICAL", new Point(m + L + 6, m + 18), White, 0.5);
        Text(f, cloaked ? "CLOAK: ACTIVE" : "CLOAK: OFFLINE",
            new Point(m + L + 6, m + 38), cloaked ? Red : White, 0.45);
        if ((_tick / 15) % 2 == 0) { Text(f, "REC", new Point(w - 74, m + 18), Red, 0.55); }
        Text(f, $"FPS {fps:00}   TRK {contacts}", new Point(m, h - 16), White, 0.45);
        Text(f, live ? "UPLINK LIVE" : "UPLINK STBY", new Point(w - 150, h - 16), live ? Red : White, 0.45);

        // Faint scanlines.
        for (int y = 0; y < h; y += 3)
        {
            using var row = f.Row(y);
            row.ConvertTo(row, -1, 0.88, 0);
        }
    }

    private static void FrameBracket(Mat f, int x, int y, int sx, int sy, int len)
    {
        Cv2.Line(f, new Point(x, y), new Point(x + sx * len, y), Red, 2, LineTypes.AntiAlias);
        Cv2.Line(f, new Point(x, y), new Point(x, y + sy * len), Red, 2, LineTypes.AntiAlias);
    }

    private static void Target(Mat f, Rect r)
    {
        int e = Math.Max(6, r.Width / 5);
        void Corner(int x, int y, int sx, int sy)
        {
            Cv2.Line(f, new Point(x, y), new Point(x + sx * e, y), Red, 2, LineTypes.AntiAlias);
            Cv2.Line(f, new Point(x, y), new Point(x, y + sy * e), Red, 2, LineTypes.AntiAlias);
        }
        Corner(r.Left, r.Top, 1, 1);
        Corner(r.Right, r.Top, -1, 1);
        Corner(r.Left, r.Bottom, 1, -1);
        Corner(r.Right, r.Bottom, -1, -1);
        Text(f, "LOCK", new Point(r.Left, r.Top - 6), Red, 0.42);
    }

    private static void Text(Mat f, string s, Point org, Scalar color, double scale)
        => Cv2.PutText(f, s, org, HersheyFonts.HersheyDuplex, scale, color, 1, LineTypes.AntiAlias);
}
