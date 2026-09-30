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

    public static bool IncludeCustomization =>
        Environment.GetEnvironmentVariable("CODEXBAR_CAPTURE_SETTINGS") == "1";

    /// <summary>Renders the window content at 2x with a transparent background.</summary>
    public static void Save(Window window, string path)
    {
        window.UpdateLayout();
        if (window.Content is not FrameworkElement root || root.ActualWidth <= 0 || root.ActualHeight <= 0)
        {
            throw new InvalidOperationException($"{window.Title} has no rendered content to capture.");
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth * Scale),
            (int)Math.Ceiling(root.ActualHeight * Scale),
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
