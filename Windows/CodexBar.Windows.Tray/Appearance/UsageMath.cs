using System.Globalization;

namespace CodexBar.Windows.Tray;

public enum PaceKind
{
    Unknown,
    Enough,
    RunsOut,
    Exhausted,
}

/// <param name="ElapsedFraction">Share of the window already elapsed, from 0 to 1.</param>
/// <param name="MinutesToEmpty">Projected minutes until the quota reaches 0 at the current rate.</param>
public readonly record struct PaceEstimate(
    PaceKind Kind,
    double ElapsedFraction,
    double MinutesToEmpty);

/// <summary>A meter represents either quota usage or, only while exhausted, time until reset.</summary>
public readonly record struct QuotaMeterState(double Percent, bool IsResetCountdown);

/// <summary>Framework-independent usage math shared by the tray UI and its tests.</summary>
public static class UsageMath
{
    public const int SessionWindowMinutes = 300;
    public const int WeeklyWindowMinutes = 10_080;

    /// <summary>
    /// An exhausted quota counts down against its full window, not against when exhaustion was
    /// first observed. Missing reset metadata keeps the normal usage meter instead of guessing.
    /// </summary>
    public static QuotaMeterState CalculateMeter(
        double usedPercent,
        bool showUsed,
        int? windowMinutes,
        DateTimeOffset? resetsAt,
        DateTimeOffset now)
    {
        double used = Math.Clamp(usedPercent, 0, 100);
        if (used >= 100 && windowMinutes is > 0 && resetsAt is DateTimeOffset reset)
        {
            double fraction = Math.Clamp((reset - now).TotalMinutes / windowMinutes.Value, 0, 1);
            return new QuotaMeterState(fraction * 100, true);
        }

        return new QuotaMeterState(showUsed ? used : 100 - used, false);
    }

    /// <summary>Continuous red → yellow (50%) → green (25%) → lighter green (0%).</summary>
    public static RgbColor ResetCountdownColor(double percent, StatusColors status)
    {
        double remaining = Math.Clamp(percent, 0, 100);
        RgbColor yellow = RgbColor.Parse("#F5D547");
        if (remaining >= 50)
        {
            return yellow.Mix(status.Critical, (remaining - 50) / 50);
        }

        return remaining >= 25
            ? status.Ok.Mix(yellow, (remaining - 25) / 25)
            : status.Ok.Mix(RgbColor.White, (1 - (remaining / 25)) * 0.18);
    }

    /// <summary>Compact local reset date, e.g. "FRI-20NOV, 12:00PM".</summary>
    public static string FormatExactReset(DateTimeOffset reset, CultureInfo culture, TimeZoneInfo? timeZone = null)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(reset, timeZone ?? TimeZoneInfo.Local);
        string month = local.ToString("MMM", culture).Replace(".", string.Empty).ToUpper(culture);
        string weekday = local.ToString("ddd", culture).Replace(".", string.Empty).ToUpper(culture);
        return weekday + "-" + local.ToString("dd", CultureInfo.InvariantCulture) + month + ", " +
            local.ToString("hh:mmtt", CultureInfo.InvariantCulture);
    }

    public static int? DefaultWindowMinutes(string windowId)
    {
        return windowId.ToLowerInvariant() switch
        {
            "session" => SessionWindowMinutes,
            "weekly" => WeeklyWindowMinutes,
            _ => null,
        };
    }

    /// <summary>
    /// Projects whether the remaining quota lasts until the reset when consumption continues
    /// at the average rate observed since the window started.
    /// </summary>
    public static PaceEstimate EstimatePace(
        double usedPercent,
        int? windowMinutes,
        DateTimeOffset? resetsAt,
        DateTimeOffset now)
    {
        double remainingPercent = Math.Clamp(100 - usedPercent, 0, 100);
        if (remainingPercent <= 0)
        {
            return new PaceEstimate(PaceKind.Exhausted, 1, 0);
        }

        if (windowMinutes is not int span || span <= 0 || resetsAt is null)
        {
            return new PaceEstimate(PaceKind.Unknown, double.NaN, double.PositiveInfinity);
        }

        double minutesLeft = Math.Clamp((resetsAt.Value - now).TotalMinutes, 0, span);
        double minutesElapsed = span - minutesLeft;
        double elapsedFraction = minutesElapsed / span;
        double used = Math.Clamp(usedPercent, 0, 100);
        if (minutesElapsed < 1 || used <= 0)
        {
            return new PaceEstimate(PaceKind.Enough, elapsedFraction, double.PositiveInfinity);
        }

        double minutesToEmpty = remainingPercent / (used / minutesElapsed);
        return minutesToEmpty >= minutesLeft
            ? new PaceEstimate(PaceKind.Enough, elapsedFraction, minutesToEmpty)
            : new PaceEstimate(PaceKind.RunsOut, elapsedFraction, minutesToEmpty);
    }

    /// <summary>Formats a countdown as "41 min", "2 h 05 min" or "2 d 19 h".</summary>
    public static string FormatDuration(TimeSpan remaining)
    {
        int totalMinutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
        if (totalMinutes < 60)
        {
            return $"{totalMinutes} min";
        }

        int totalHours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (totalHours < 24)
        {
            return minutes == 0 ? $"{totalHours} h" : $"{totalHours} h {minutes:00} min";
        }

        int days = totalHours / 24;
        int hours = totalHours % 24;
        return hours == 0 ? $"{days} d" : $"{days} d {hours} h";
    }

    /// <summary>Formats a projection coarsely, since it is only an estimate.</summary>
    public static string FormatApproximate(double minutes)
    {
        if (!double.IsFinite(minutes))
        {
            return "—";
        }

        if (minutes < 90)
        {
            return $"{Math.Max(1, (int)Math.Round(minutes))} min";
        }

        int hours = (int)Math.Round(minutes / 60);
        return hours < 24 ? $"{hours} h" : FormatDuration(TimeSpan.FromHours(hours));
    }
}
