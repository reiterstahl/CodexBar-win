using System.Text.Json.Serialization;

namespace CodexBar.EngineClient;

public sealed record EngineSnapshot(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    ProviderSnapshot[] Providers,
    ProviderFailure[] Failures);

public sealed record ProviderSnapshot(
    string Provider,
    string DisplayName,
    string Source,
    RateWindow[] Windows,
    ProviderIdentity? Identity,
    DateTimeOffset UpdatedAt,
    ResetCredits? ResetCredits = null,
    ExtraUsage? ExtraUsage = null);

public sealed record ExtraUsage(
    string Currency,
    double? Used,
    double? Limit,
    double? Balance,
    bool IsEnabled,
    bool Unlimited)
{
    [JsonIgnore]
    public double? RemainingPercent => Used is >= 0 && Limit is > 0
        ? Math.Clamp(100 * (1 - Used.Value / Limit.Value), 0, 100)
        : null;

}

/// <summary>Saved rate-limit resets available from the account's provider; read-only.</summary>
public sealed record ResetCredits(
    int AvailableCount,
    ResetCredit[]? Credits)
{
    public int AvailableAt(DateTimeOffset now) => Math.Max(0, AvailableCount -
        (Credits ?? []).Count(credit => credit.ExpiresAt is DateTimeOffset expiry && expiry <= now));
}

public sealed record ResetCredit(
    DateTimeOffset? ExpiresAt,
    string? Title);

public sealed record RateWindow(
    string Id,
    string Label,
    double UsedPercent,
    int? WindowMinutes,
    DateTimeOffset? ResetsAt)
{
    [JsonIgnore]
    public double RemainingPercent => Math.Clamp(100 - UsedPercent, 0, 100);
}

public sealed record ProviderIdentity(
    string? AccountEmail,
    string? Plan);

public sealed record ProviderFailure(
    string Provider,
    string Code,
    string Message);
