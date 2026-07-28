namespace CodexBar.EngineClient;

public sealed record ProviderProfile(
    string Provider,
    string Label,
    string ConfigDirectory)
{
    public string EnvironmentVariable => Provider switch
    {
        "codex" => "CODEX_HOME",
        "claude" => "CLAUDE_CONFIG_DIR",
        _ => throw new EngineClientException($"Unsupported provider profile '{Provider}'."),
    };
}

public sealed record ProviderProfileResult(
    ProviderProfile Profile,
    DateTimeOffset GeneratedAt,
    ProviderSnapshot? Snapshot,
    ProviderFailure? Failure);

public static class ProviderProfileDiscovery
{
    public static IReadOnlyList<ProviderProfile> Discover(
        string? userProfile = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        userProfile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new EngineClientException("The Windows user profile directory could not be resolved.");
        }

        environment ??= ReadEnvironment();
        string profileRoot = Path.GetFullPath(userProfile);
        var profiles = new List<ProviderProfile>
        {
            CreatePrimary(
                "codex",
                "Codex",
                environment.GetValueOrDefault("CODEX_HOME"),
                Path.Combine(profileRoot, ".codex")),
        };

        AddAdditionalProfiles(
            profiles,
            profileRoot,
            provider: "codex",
            directoryPattern: ".codex-*",
            credentialFileName: "auth.json",
            labelPrefix: "Codex");
        profiles.Add(
            CreatePrimary(
                "claude",
                "Claude Code",
                environment.GetValueOrDefault("CLAUDE_CONFIG_DIR"),
                Path.Combine(profileRoot, ".claude")));
        AddAdditionalProfiles(
            profiles,
            profileRoot,
            provider: "claude",
            directoryPattern: ".claude-*",
            credentialFileName: ".credentials.json",
            labelPrefix: "Claude Code");

        return profiles;
    }

    private static ProviderProfile CreatePrimary(
        string provider,
        string label,
        string? configuredDirectory,
        string defaultDirectory)
    {
        string directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? defaultDirectory
            : Environment.ExpandEnvironmentVariables(configuredDirectory.Trim());
        return new ProviderProfile(provider, label, Path.GetFullPath(directory));
    }

    private static void AddAdditionalProfiles(
        List<ProviderProfile> profiles,
        string profileRoot,
        string provider,
        string directoryPattern,
        string credentialFileName,
        string labelPrefix)
    {
        if (!Directory.Exists(profileRoot))
        {
            return;
        }

        var existingDirectories = profiles
            .Select(profile => profile.ConfigDirectory)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in Directory
                     .EnumerateDirectories(profileRoot, directoryPattern, SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string fullDirectory = Path.GetFullPath(directory);
            if (existingDirectories.Contains(fullDirectory)
                || !File.Exists(Path.Combine(fullDirectory, credentialFileName)))
            {
                continue;
            }

            string directoryName = Path.GetFileName(fullDirectory);
            int separatorIndex = directoryName.IndexOf('-', StringComparison.Ordinal);
            string suffix = separatorIndex >= 0
                ? directoryName[(separatorIndex + 1)..].Trim()
                : string.Empty;
            string label = string.IsNullOrWhiteSpace(suffix)
                ? $"{labelPrefix} (additional)"
                : $"{labelPrefix} ({suffix})";
            profiles.Add(new ProviderProfile(provider, label, fullDirectory));
            existingDirectories.Add(fullDirectory);
        }
    }

    private static IReadOnlyDictionary<string, string?> ReadEnvironment()
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODEX_HOME"] = Environment.GetEnvironmentVariable("CODEX_HOME"),
            ["CLAUDE_CONFIG_DIR"] = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
        };
    }
}
