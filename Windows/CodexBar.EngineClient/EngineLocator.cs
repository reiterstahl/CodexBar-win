namespace CodexBar.EngineClient;

public static class EngineLocator
{
    public const string OverrideEnvironmentKey = "CODEXBAR_ENGINE_PATH";

    public static string Resolve(
        IReadOnlyDictionary<string, string?>? environment = null,
        string? applicationDirectory = null)
    {
        environment ??= ReadEnvironment();
        applicationDirectory ??= AppContext.BaseDirectory;

        if (environment.TryGetValue(OverrideEnvironmentKey, out string? configuredPath)
            && !string.IsNullOrWhiteSpace(configuredPath))
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            if (File.Exists(expandedPath))
            {
                return Path.GetFullPath(expandedPath);
            }

            throw new EngineClientException(
                $"{OverrideEnvironmentKey} points to a missing engine executable.");
        }

        string[] candidates =
        [
            Path.Combine(applicationDirectory, "CodexBarWindowsEngine.exe"),
            Path.Combine(applicationDirectory, "engine", "CodexBarWindowsEngine.exe"),
        ];

        string? resolved = candidates.FirstOrDefault(File.Exists);
        return resolved
            ?? throw new EngineClientException(
                "CodexBarWindowsEngine.exe was not found beside the tray application.");
    }

    private static IReadOnlyDictionary<string, string?> ReadEnvironment()
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [OverrideEnvironmentKey] = Environment.GetEnvironmentVariable(OverrideEnvironmentKey),
        };
    }
}
