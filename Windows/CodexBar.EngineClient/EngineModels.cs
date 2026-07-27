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
    DateTimeOffset UpdatedAt);

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
