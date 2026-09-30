using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexBar.Windows.Tray;

/// <summary>
/// Draws the notification-area meter: a thick bar for the session window of the most limited
/// account, a thin bar for its weekly window, and a status dot when a quota is exhausted or
/// has just recovered.
/// </summary>
public static class TrayIconRenderer
{
    private const int Size = 32;
    private static readonly Color Background = Color.FromArgb(15, 17, 21);
    private static readonly Color Frame = Color.FromArgb(43, 48, 57);
    private static readonly Color Track = Color.FromArgb(42, 47, 56);

    public static string CacheKey(TrayIconState state, AppearancePalette palette)
    {
        return string.Join(
            '|',
            Math.Round(state.SessionRemaining / 5) * 5,
            Math.Round(state.WeeklyRemaining / 5) * 5,
            state.HasExhaustedAccount,
            state.HasRecoveredAccount,
            palette.Accent.ToHex());
    }

    public static Icon Render(TrayIconState state, AppearancePalette palette)
    {
        var dark = RgbColor.Parse("#0F1115");
        StatusColors status = ThemeCatalog.StatusFor(ThemeCatalog.Find("grafito"));
        RgbColor accent = palette.Accent.EnsureContrast(dark, 3);
        RgbColor session = state.HasRecoveredAccount
            ? status.Ok
            : state.SessionRemaining < 25 ? status.Warn : accent;
        RgbColor weekly = state.WeeklyRemaining < 25 ? status.Warn : accent;

        using var bitmap = new Bitmap(Size, Size);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using (GraphicsPath frame = RoundedRectangle(new RectangleF(1, 1, Size - 2, Size - 2), 7))
            using (var fill = new SolidBrush(Background))
            using (var pen = new Pen(Frame, 1.5f))
            {
                graphics.FillPath(fill, frame);
                graphics.DrawPath(pen, frame);
            }

            DrawBar(graphics, new RectangleF(6, 9, 20, 8), state.SessionRemaining, ToColor(session));
            DrawBar(graphics, new RectangleF(6, 20, 20, 4), state.WeeklyRemaining, ToColor(weekly));

            if (state.HasRecoveredAccount || state.HasExhaustedAccount)
            {
                RgbColor badge = state.HasRecoveredAccount ? status.Ok : status.Critical;
                using var ring = new SolidBrush(Background);
                using var dot = new SolidBrush(ToColor(badge));
                graphics.FillEllipse(ring, 20, 0, 12, 12);
                graphics.FillEllipse(dot, 22, 2, 8, 8);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void DrawBar(Graphics graphics, RectangleF bounds, double remainingPercent, Color color)
    {
        float radius = bounds.Height / 2;
        using (GraphicsPath track = RoundedRectangle(bounds, radius))
        using (var trackBrush = new SolidBrush(Track))
        {
            graphics.FillPath(trackBrush, track);
        }

        float width = bounds.Width * (float)Math.Clamp(remainingPercent / 100, 0, 1);
        if (width < 1)
        {
            return;
        }

        var filled = new RectangleF(bounds.X, bounds.Y, Math.Max(width, bounds.Height), bounds.Height);
        using GraphicsPath path = RoundedRectangle(filled, radius);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color ToColor(RgbColor color) => Color.FromArgb(color.R, color.G, color.B);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
