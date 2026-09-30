using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexBar.Windows.Tray;

/// <summary>
/// Documentation hook: when CODEXBAR_CAPTURE_DIR is set, the app renders its windows to PNG
/// after the first refresh and exits. Used by .github/workflows/screenshots.yml together with
/// the demo engine; it has no effect in normal use.
/// </summary>
internal static class ScreenshotCapture
{
    private const double Scale = 2;

    public static string? OutputDirectory =>
        Environment.GetEnvironmentVariable("CODEXBAR_CAPTURE_DIR") is { Length: > 0 } directory
            ? directory
            : null;

    public static string Name =>
        Environment.GetEnvironmentVariable("CODEXBAR_CAPTURE_NAME") is { Length: > 0 } name
            ? name
            : "main";

    /// <summary>File name (without extension) for the customization window, or null to skip it.</summary>
    public static string? CustomizationName =>
        Environment.GetEnvironmentVariable("CODEXBAR_CAPTURE_SETTINGS") is { Length: > 0 } name && name != "0"
            ? name
            : null;

    /// <summary>Renders the window content at 2x with a transparent background.</summary>
    public static void Save(Window window, string path)
    {
        window.UpdateLayout();
        if (window.Content is not FrameworkElement root || root.ActualWidth <= 0 || root.ActualHeight <= 0)
        {
            throw new InvalidOperationException($"{window.Title} has no rendered content to capture.");
        }

        // Windows caps window height to the screen; lay the content out at its natural height so
        // the capture is never clipped by a small display.
        double width = root.ActualWidth;
        root.Measure(new System.Windows.Size(width, double.PositiveInfinity));
        var size = new System.Windows.Size(width, root.DesiredSize.Height);
        root.Arrange(new Rect(size));

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(size.Width * Scale),
            (int)Math.Ceiling(size.Height * Scale),
            96 * Scale,
            96 * Scale,
            PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
