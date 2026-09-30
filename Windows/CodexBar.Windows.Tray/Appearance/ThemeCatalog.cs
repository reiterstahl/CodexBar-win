namespace CodexBar.Windows.Tray;

public enum ViewMode
{
    Cards,
    Summary,
    Mini,
}

public enum ChartKind
{
    Bar,
    Ring,
    Gauge,
    Blocks,
    Numbers,
}

public enum ChartColorMode
{
    Accent,
    Level,
    Provider,
}

public enum Density
{
    Comfortable,
    Compact,
}

public sealed record ThemeDefinition(
    string Id,
    string Name,
    bool IsLight,
    string Desk,
    string Background,
    string Surface,
    string Raised,
    string Border,
    string Text,
    string Muted,
    string Track);

public sealed record StatusColors(RgbColor Ok, RgbColor Warn, RgbColor Critical);

public sealed record AccentPreset(string Hex, string Name);

/// <summary>Built-in themes and the defaults the customization panel restores.</summary>
public static class ThemeCatalog
{
    public const string DefaultThemeId = "grafito";
    public const string DefaultAccent = "#D9D900";
    public const string DefaultCodexColor = "#4F8CFF";
    public const string DefaultClaudeColor = "#D97757";

    public static IReadOnlyList<ThemeDefinition> Themes { get; } =
    [
        new("grafito", "Grafito", false, "#23262C", "#0F1115", "#171A20", "#1F232B", "#2B3039", "#F2F2EC", "#A3A9B3", "#2A2F38"),
        new("medianoche", "Medianoche", false, "#1B2238", "#0A0F1E", "#11182B", "#18213A", "#26314D", "#EEF1FA", "#9EA8C6", "#233050"),
        new("claro", "Claro", true, "#C9CED6", "#F3F4F6", "#FFFFFF", "#ECEEF2", "#D9DDE4", "#15171C", "#555B66", "#E3E6EB"),
        new("papel", "Papel", true, "#CFC6B6", "#F4EFE6", "#FBF8F2", "#EFE8DC", "#DDD2C1", "#1F1A14", "#625A4E", "#E7DFD1"),
        new("contraste", "Contraste", false, "#1C1C1C", "#000000", "#000000", "#141414", "#FFFFFF", "#FFFFFF", "#E6E6E6", "#3A3A3A"),
    ];

    public static IReadOnlyList<AccentPreset> Accents { get; } =
    [
        new("#D9D900", "Amarillo CodexBar"),
        new("#4F8CFF", "Azul"),
        new("#2BC4A3", "Verde agua"),
        new("#FF7A45", "Naranja"),
        new("#B388FF", "Lila"),
        new("#FF5C8A", "Rosa"),
    ];

    public static ThemeDefinition Find(string? id)
    {
        return Themes.FirstOrDefault(theme =>
            theme.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];
    }

    public static StatusColors StatusFor(ThemeDefinition theme)
    {
        return theme.IsLight
            ? new StatusColors(RgbColor.Parse("#1E8E57"), RgbColor.Parse("#A85A00"), RgbColor.Parse("#C3313A"))
            : new StatusColors(RgbColor.Parse("#3DD68C"), RgbColor.Parse("#F5A524"), RgbColor.Parse("#FF6B6F"));
    }

    public static TEnum ParseOption<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) && Enum.IsDefined(parsed)
            ? parsed
            : fallback;
    }

    public static string FormatOption<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
