using System.Text.Json;

// Stand-in for CodexBarWindowsEngine that returns fictitious accounts, used to take README
// screenshots without real credentials. Point CODEXBAR_ENGINE_PATH at this executable.
// Profiles whose directory ends in "-2" (".codex-2", ".claude-2") get the second account.

string provider = ReadOption(args, "--provider") ?? "codex";
string profileDirectory = Environment.GetEnvironmentVariable(
    provider == "claude" ? "CLAUDE_CONFIG_DIR" : "CODEX_HOME") ?? string.Empty;
bool secondary = profileDirectory.TrimEnd('\\', '/').EndsWith("-2", StringComparison.Ordinal);
DateTimeOffset now = DateTimeOffset.UtcNow;

(double SessionUsed, int SessionResetMinutes, double WeeklyUsed, int WeeklyResetMinutes, string Plan, string Email) account =
    (provider, secondary) switch
    {
        ("codex", false) => (38, 134, 52, 4020, "plus", "ana@example.com"),
        ("codex", true) => (100, 65, 69, 2890, "pro", "dev@example.com"),
        ("claude", false) => (9, 262, 88, 1335, "max", "ana@example.com"),
        _ => (82, 41, 28, 7640, "pro", "dev@example.com"),
    };

var snapshot = new
{
    schemaVersion = 1,
    generatedAt = now,
    providers = new[]
    {
        new
        {
            provider,
            displayName = provider == "claude" ? "Claude Code" : "Codex",
            source = "demo",
            windows = new[]
            {
                Window("session", "Session", account.SessionUsed, 300, account.SessionResetMinutes),
                Window("weekly", "Weekly", account.WeeklyUsed, 10_080, account.WeeklyResetMinutes),
            },
            identity = new { accountEmail = account.Email, plan = account.Plan },
            updatedAt = now,
            resetCredits = ResetCredits(provider, secondary),
        },
    },
    failures = Array.Empty<object>(),
};

Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
return 0;

object Window(string id, string label, double usedPercent, int windowMinutes, int resetMinutes) => new
{
    id,
    label,
    usedPercent,
    windowMinutes,
    resetsAt = now.AddMinutes(resetMinutes),
};

object? ResetCredits(string providerName, bool isSecondary)
{
    // Codex only: two credits on the personal account, one expiring soon on the work account.
    if (providerName != "codex")
    {
        return null;
    }

    double[] expiryDays = isSecondary ? [2] : [12, 20];
    return new
    {
        availableCount = expiryDays.Length,
        credits = expiryDays
            .Select(days => new { expiresAt = (DateTimeOffset?)now.AddDays(days), title = "Rate limit reset" })
            .ToArray(),
    };
}

static string? ReadOption(string[] arguments, string name)
{
    int index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}
