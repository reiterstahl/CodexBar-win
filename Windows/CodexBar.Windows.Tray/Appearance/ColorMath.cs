using System.Globalization;

namespace CodexBar.Windows.Tray;

/// <summary>
/// Framework-independent sRGB color used for theme math. Kept free of WPF types so the
/// contrast rules can be tested on any platform.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static readonly RgbColor Black = new(0, 0, 0);
    public static readonly RgbColor White = new(255, 255, 255);

    public static bool TryParse(string? value, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(character => $"{character}{character}"));
        }

        if (hex.Length != 6 ||
            !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int packed))
        {
            return false;
        }

        color = new RgbColor(
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF));
        return true;
    }

    public static RgbColor Parse(string value)
    {
        return TryParse(value, out RgbColor color)
            ? color
            : throw new FormatException($"'{value}' is not a #RRGGBB color.");
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public RgbColor Mix(RgbColor other, double amount)
    {
        double t = Math.Clamp(amount, 0, 1);
        return new RgbColor(
            Blend(R, other.R, t),
            Blend(G, other.G, t),
            Blend(B, other.B, t));
    }

    public double RelativeLuminance =>
        (0.2126 * Linearize(R)) + (0.7152 * Linearize(G)) + (0.0722 * Linearize(B));

    public static double Contrast(RgbColor first, RgbColor second)
    {
        double lighter = Math.Max(first.RelativeLuminance, second.RelativeLuminance);
        double darker = Math.Min(first.RelativeLuminance, second.RelativeLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Nudges a user-chosen color toward black or white until it reaches the requested
    /// WCAG contrast ratio on <paramref name="background"/>.
    /// </summary>
    public RgbColor EnsureContrast(RgbColor background, double minimumRatio)
    {
        RgbColor target = background.RelativeLuminance > 0.35 ? Black : White;
        RgbColor color = this;
        for (int step = 0; step < 16 && Contrast(color, background) < minimumRatio; step++)
        {
            color = color.Mix(target, 0.12);
        }
        return color;
    }

    /// <summary>Text color for content drawn on top of this color.</summary>
    public RgbColor ReadableInk()
    {
        var nearBlack = new RgbColor(11, 11, 11);
        return Contrast(this, nearBlack) >= Contrast(this, White) ? nearBlack : White;
    }

    private static byte Blend(byte from, byte to, double amount)
    {
        return (byte)Math.Clamp(Math.Round(from + ((to - from) * amount)), 0, 255);
    }

    private static double Linearize(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
