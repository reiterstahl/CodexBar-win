using System.Text.Json;

namespace CodexBar.EngineClient;

public static class EngineSnapshotParser
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static EngineSnapshot Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        EngineSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<EngineSnapshot>(json, Options)
                ?? throw new EngineClientException("The engine returned an empty snapshot.");
        }
        catch (JsonException exception)
        {
            throw new EngineClientException("The engine returned invalid JSON.", exception);
        }

        Validate(snapshot);
        return snapshot;
    }

    private static void Validate(EngineSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != SupportedSchemaVersion)
        {
            throw new EngineClientException(
                $"Unsupported engine schema {snapshot.SchemaVersion}; expected {SupportedSchemaVersion}.");
        }

        if (snapshot.Providers is null || snapshot.Failures is null)
        {
            throw new EngineClientException("The engine snapshot is missing required collections.");
        }

        foreach (ProviderSnapshot provider in snapshot.Providers)
        {
            ValidateProvider(provider.Provider);
            if (provider.Windows is null)
            {
                throw new EngineClientException($"Provider '{provider.Provider}' is missing usage windows.");
            }
        }

        foreach (ProviderFailure failure in snapshot.Failures)
        {
            ValidateProvider(failure.Provider);
        }
    }

    private static void ValidateProvider(string provider)
    {
        if (provider is not ("codex" or "claude"))
        {
            throw new EngineClientException($"Unsupported provider '{provider}'.");
        }
    }
}
