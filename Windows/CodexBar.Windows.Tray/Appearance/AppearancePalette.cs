using System.Windows;
using System.Windows.Media;

namespace CodexBar.Windows.Tray;

public sealed record AppearanceOptions(
    ViewMode View,
    ChartKind Chart,
    ChartColorMode ColorMode,
    Density Density,
    bool ShowUsed,
    bool ShowPace);

/// <summary>
/// Resolved colors for the active theme and accent. Theme brushes are published as
/// application resources so every <c>DynamicResource</c> reference updates in place.
/// </summary>
public sealed class AppearancePalette
{
    private readonly Dictionary<RgbColor, SolidColorBrush> _brushes = [];

    private AppearancePalette(
        ThemeDefinition theme,
        RgbColor accent,
        RgbColor codexColor,
        RgbColor claudeColor)
    {
        Theme = theme;
        Desk = RgbColor.Parse(theme.Desk);
        Background = RgbColor.Parse(theme.Background);
        Surface = RgbColor.Parse(theme.Surface);
        Raised = RgbColor.Parse(theme.Raised);
        Border = RgbColor.Parse(theme.Border);
        Text = RgbColor.Parse(theme.Text);
        Muted = RgbColor.Parse(theme.Muted);
        Track = RgbColor.Parse(theme.Track);
        Status = ThemeCatalog.StatusFor(theme);
        Accent = accent;
        AccentFill = accent.EnsureContrast(Background, 3);
        AccentInk = AccentFill.ReadableInk();
        AccentText = accent.EnsureContrast(Background, 4.5);
        CodexColor = codexColor;
        ClaudeColor = claudeColor;
        ExhaustedSurface = Surface.Mix(Status.Critical, theme.IsLight ? 0.06 : 0.10);
        ExhaustedBorder = Border.Mix(Status.Critical, 0.5);
        RecoverySurface = Surface.Mix(Status.Ok, theme.IsLight ? 0.08 : 0.10);
        RecoveryBorder = Border.Mix(Status.Ok, 0.6);
        Hover = Raised.Mix(Text, 0.06);
    }

    public ThemeDefinition Theme { get; }

    public RgbColor Desk { get; }

    public RgbColor Background { get; }

    public RgbColor Surface { get; }

    public RgbColor Raised { get; }

    public RgbColor Border { get; }

    public RgbColor Text { get; }

    public RgbColor Muted { get; }

    public RgbColor Track { get; }

    public StatusColors Status { get; }

    public RgbColor Accent { get; }

    public RgbColor AccentFill { get; }

    public RgbColor AccentInk { get; }

    public RgbColor AccentText { get; }

    public RgbColor CodexColor { get; }

    public RgbColor ClaudeColor { get; }

    public RgbColor ExhaustedSurface { get; }

    public RgbColor ExhaustedBorder { get; }

    public RgbColor RecoverySurface { get; }

    public RgbColor RecoveryBorder { get; }

    public RgbColor Hover { get; }

    public static AppearancePalette Create(AppSettings settings)
    {
        return new AppearancePalette(
            ThemeCatalog.Find(settings.Theme),
            ParseOrDefault(settings.AccentColor, ThemeCatalog.DefaultAccent),
            ParseOrDefault(settings.CodexColor, ThemeCatalog.DefaultCodexColor),
            ParseOrDefault(settings.ClaudeColor, ThemeCatalog.DefaultClaudeColor));
    }

    public RgbColor ProviderColor(string provider)
    {
        return provider.Equals("claude", StringComparison.OrdinalIgnoreCase)
            ? ClaudeColor
            : CodexColor;
    }

    public SolidColorBrush Brush(RgbColor color)
    {
        if (!_brushes.TryGetValue(color, out SolidColorBrush? brush))
        {
            brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            _brushes[color] = brush;
        }
        return brush;
    }

    public void ApplyTo(ResourceDictionary resources)
    {
        resources["DeskBrush"] = Brush(Desk);
        resources["AppBackgroundBrush"] = Brush(Background);
        resources["SurfaceBrush"] = Brush(Surface);
        resources["RaisedBrush"] = Brush(Raised);
        resources["SurfaceHoverBrush"] = Brush(Hover);
        resources["BorderBrush"] = Brush(Border);
        resources["PrimaryTextBrush"] = Brush(Text);
        resources["SecondaryTextBrush"] = Brush(Muted);
        resources["TrackBrush"] = Brush(Track);
        resources["AccentBrush"] = Brush(AccentFill);
        resources["AccentInkBrush"] = Brush(AccentInk);
        resources["AccentTextBrush"] = Brush(AccentText);
        resources["AccentMutedBrush"] = Brush(Background.Mix(AccentFill, 0.16));
        resources["OkBrush"] = Brush(Status.Ok);
        resources["WarnBrush"] = Brush(Status.Warn);
        resources["CriticalBrush"] = Brush(Status.Critical);
        resources["ExhaustedSurfaceBrush"] = Brush(ExhaustedSurface);
        resources["ExhaustedBorderBrush"] = Brush(ExhaustedBorder);
        resources["RecoverySurfaceBrush"] = Brush(RecoverySurface);
        resources["RecoveryBorderBrush"] = Brush(RecoveryBorder);
        resources["CodexBrush"] = Brush(CodexColor.EnsureContrast(Raised, 3));
        resources["ClaudeBrush"] = Brush(ClaudeColor.EnsureContrast(Raised, 3));
        resources["CustomAccentBrush"] = Brush(Accent);
    }

    private static RgbColor ParseOrDefault(string? value, string fallback)
    {
        return RgbColor.TryParse(value, out RgbColor color) ? color : RgbColor.Parse(fallback);
    }
}
