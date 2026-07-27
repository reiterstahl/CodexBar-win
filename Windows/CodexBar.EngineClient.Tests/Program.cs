using CodexBar.EngineClient;

var tests = new (string Name, Action Run)[]
{
    ("parses snapshot contract", ParsesSnapshotContract),
    ("rejects unsupported schema", RejectsUnsupportedSchema),
    ("rejects unknown provider", RejectsUnknownProvider),
    ("resolves explicit engine path", ResolvesExplicitEnginePath),
};

foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL: {name}: {exception.Message}");
        return 1;
    }
}

Console.WriteLine($"{tests.Length} engine client tests passed.");
return 0;

static void ParsesSnapshotContract()
{
    EngineSnapshot snapshot = EngineSnapshotParser.Parse(
        """
        {
          "schemaVersion": 1,
          "generatedAt": "2026-07-27T18:00:00Z",
          "providers": [
            {
              "provider": "codex",
              "displayName": "Codex",
              "source": "oauth",
              "windows": [
                {
                  "id": "session",
                  "label": "Session",
                  "usedPercent": 25,
                  "windowMinutes": 300,
                  "resetsAt": "2026-07-27T20:00:00Z"
                }
              ],
              "identity": { "accountEmail": null, "plan": "plus" },
              "updatedAt": "2026-07-27T18:00:00Z"
            }
          ],
          "failures": [
            {
              "provider": "claude",
              "code": "credentials_not_found",
              "message": "Claude Code credentials were not found."
            }
          ]
        }
        """);

    Assert(snapshot.Providers.Length == 1, "Expected one provider.");
    Assert(snapshot.Failures.Length == 1, "Expected one independent failure.");
    Assert(snapshot.Providers[0].Windows[0].RemainingPercent == 75, "Expected 75% remaining.");
}

static void RejectsUnsupportedSchema()
{
    AssertThrows(
        () => EngineSnapshotParser.Parse(
            """
            {
              "schemaVersion": 99,
              "generatedAt": "2026-07-27T18:00:00Z",
              "providers": [],
              "failures": []
            }
            """),
        "Expected an unsupported schema failure.");
}

static void RejectsUnknownProvider()
{
    AssertThrows(
        () => EngineSnapshotParser.Parse(
            """
            {
              "schemaVersion": 1,
              "generatedAt": "2026-07-27T18:00:00Z",
              "providers": [],
              "failures": [
                { "provider": "other", "code": "failure", "message": "Nope" }
              ]
            }
            """),
        "Expected an unknown provider failure.");
}

static void ResolvesExplicitEnginePath()
{
    string temporaryFile = Path.GetTempFileName();
    try
    {
        var environment = new Dictionary<string, string?>
        {
            [EngineLocator.OverrideEnvironmentKey] = temporaryFile,
        };
        string resolved = EngineLocator.Resolve(environment, Path.GetTempPath());
        Assert(resolved == Path.GetFullPath(temporaryFile), "Expected the explicit engine path.");
    }
    finally
    {
        File.Delete(temporaryFile);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch (EngineClientException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
