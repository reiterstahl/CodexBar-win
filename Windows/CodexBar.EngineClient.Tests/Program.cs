using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CodexBar.EngineClient;
using CodexBar.Windows.Tray;

var tests = new (string Name, Action Run)[]
{
    ("parses snapshot contract", ParsesSnapshotContract),
    ("rejects unsupported schema", RejectsUnsupportedSchema),
    ("rejects unknown provider", RejectsUnknownProvider),
    ("resolves explicit engine path", ResolvesExplicitEnginePath),
    ("discovers isolated provider profiles", DiscoversIsolatedProviderProfiles),
    ("parses and formats hex colors", ParsesAndFormatsHexColors),
    ("lifts custom accents to readable contrast", LiftsCustomAccentsToReadableContrast),
    ("picks readable ink for accent fills", PicksReadableInkForAccentFills),
    ("projects pace until reset", ProjectsPaceUntilReset),
    ("formats countdown durations", FormatsCountdownDurations),
    ("parses stored appearance options", ParsesStoredAppearanceOptions),
    ("translates every string into both languages", TranslatesEveryString),
    ("defines every key used by the XAML views", DefinesEveryXamlKey),
    ("resolves the automatic language from Windows", ResolvesAutomaticLanguage),
    ("picks the next free account profile", PicksNextFreeAccountProfile),
    ("builds sign-in commands for new accounts", BuildsSignInCommands),
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

Console.WriteLine($"{tests.Length} engine client and appearance tests passed.");
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
              "updatedAt": "2026-07-27T18:00:00Z",
              "resetCredits": {
                "availableCount": 2,
                "credits": [
                  { "expiresAt": "2026-08-01T00:00:00Z", "title": "Reset" },
                  { "expiresAt": null, "title": null }
                ]
              }
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
    Assert(snapshot.Providers[0].ResetCredits?.AvailableCount == 2, "Expected two reset credits.");
    Assert(snapshot.Providers[0].ResetCredits?.Credits?[1].ExpiresAt is null, "Expected a credit without expiry.");
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

static void DiscoversIsolatedProviderProfiles()
{
    string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"codexbar-profile-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporaryDirectory);
    try
    {
        string secondCodex = Path.Combine(temporaryDirectory, ".codex-work");
        string secondClaude = Path.Combine(temporaryDirectory, ".claude-personal");
        string incompleteCodex = Path.Combine(temporaryDirectory, ".codex-incomplete");
        Directory.CreateDirectory(secondCodex);
        Directory.CreateDirectory(secondClaude);
        Directory.CreateDirectory(incompleteCodex);
        File.WriteAllText(Path.Combine(secondCodex, "auth.json"), "{}");
        File.WriteAllText(Path.Combine(secondClaude, ".credentials.json"), "{}");

        IReadOnlyList<ProviderProfile> profiles = ProviderProfileDiscovery.Discover(
            temporaryDirectory,
            new Dictionary<string, string?>());

        Assert(profiles.Count == 4, "Expected two Codex and two Claude profiles.");
        Assert(
            profiles.Select(profile => profile.Label).SequenceEqual(
                ["Codex", "Codex (work)", "Claude Code", "Claude Code (personal)"]),
            "Expected stable provider profile labels.");
        Assert(
            profiles.All(profile => Path.IsPathFullyQualified(profile.ConfigDirectory)),
            "Expected absolute profile directories.");
    }
    finally
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }
}

static void ParsesAndFormatsHexColors()
{
    Assert(RgbColor.TryParse("#d9d900", out RgbColor accent), "Expected a six-digit color.");
    Assert(accent.ToHex() == "#D9D900", "Expected uppercase round-trip.");
    Assert(RgbColor.TryParse("fff", out RgbColor white) && white == RgbColor.White, "Expected shorthand expansion.");
    Assert(!RgbColor.TryParse("#12345", out _), "Expected invalid length to be rejected.");
    Assert(!RgbColor.TryParse("#GGGGGG", out _), "Expected non-hex digits to be rejected.");
}

static void LiftsCustomAccentsToReadableContrast()
{
    RgbColor yellow = RgbColor.Parse("#D9D900");
    RgbColor white = RgbColor.White;
    RgbColor fill = yellow.EnsureContrast(white, 3);
    RgbColor text = yellow.EnsureContrast(white, 4.5);
    Assert(RgbColor.Contrast(fill, white) >= 3, "Expected graphics contrast on a light theme.");
    Assert(RgbColor.Contrast(text, white) >= 4.5, "Expected text contrast on a light theme.");

    RgbColor dark = RgbColor.Parse("#0F1115");
    Assert(yellow.EnsureContrast(dark, 3) == yellow, "Expected a readable accent to stay untouched.");
    RgbColor navy = RgbColor.Parse("#101060");
    Assert(RgbColor.Contrast(navy.EnsureContrast(dark, 3), dark) >= 3, "Expected dark accents to be lightened.");
}

static void PicksReadableInkForAccentFills()
{
    Assert(RgbColor.Parse("#D9D900").ReadableInk() != RgbColor.White, "Expected dark ink on yellow.");
    Assert(RgbColor.Parse("#1E3A8A").ReadableInk() == RgbColor.White, "Expected white ink on navy.");
}

static void ProjectsPaceUntilReset()
{
    var now = new DateTimeOffset(2026, 9, 30, 13, 26, 0, TimeSpan.Zero);

    PaceEstimate enough = UsageMath.EstimatePace(38, 300, now.AddMinutes(134), now);
    Assert(enough.Kind == PaceKind.Enough, "Expected 38% used after 166 of 300 minutes to last.");
    Assert(Math.Abs(enough.ElapsedFraction - (166.0 / 300)) < 0.001, "Expected elapsed fraction.");

    PaceEstimate runsOut = UsageMath.EstimatePace(88, 10_080, now.AddMinutes(1335), now);
    Assert(runsOut.Kind == PaceKind.RunsOut, "Expected 88% used of the week to run out early.");
    Assert(runsOut.MinutesToEmpty is > 1100 and < 1300, "Expected roughly 20 hours to empty.");

    Assert(UsageMath.EstimatePace(100, 300, now.AddMinutes(65), now).Kind == PaceKind.Exhausted,
        "Expected an exhausted window.");
    Assert(UsageMath.EstimatePace(40, null, now.AddMinutes(65), now).Kind == PaceKind.Unknown,
        "Expected unknown pace without a window length.");
    Assert(UsageMath.EstimatePace(0, 300, now.AddMinutes(300), now).Kind == PaceKind.Enough,
        "Expected a fresh window to have enough quota.");
}

static void FormatsCountdownDurations()
{
    Assert(UsageMath.FormatDuration(TimeSpan.FromMinutes(41)) == "41 min", "Expected minutes.");
    Assert(UsageMath.FormatDuration(TimeSpan.FromMinutes(65)) == "1 h 05 min", "Expected padded minutes.");
    Assert(UsageMath.FormatDuration(TimeSpan.FromMinutes(120)) == "2 h", "Expected whole hours.");
    Assert(UsageMath.FormatDuration(TimeSpan.FromMinutes(4020)) == "2 d 19 h", "Expected days and hours.");
    Assert(UsageMath.FormatDuration(TimeSpan.FromSeconds(30)) == "1 min", "Expected partial minutes to round up.");
    Assert(UsageMath.FormatApproximate(1193) == "20 h", "Expected an approximate hour count.");
    Assert(UsageMath.FormatApproximate(45) == "45 min", "Expected approximate minutes.");
}

static void ParsesStoredAppearanceOptions()
{
    Assert(ThemeCatalog.ParseOption("summary", ViewMode.Cards) == ViewMode.Summary, "Expected case-insensitive parsing.");
    Assert(ThemeCatalog.ParseOption("unknown", ChartKind.Bar) == ChartKind.Bar, "Expected fallback for unknown values.");
    Assert(ThemeCatalog.ParseOption("7", Density.Comfortable) == Density.Comfortable, "Expected undefined numbers to fall back.");
    Assert(ThemeCatalog.FormatOption(ChartColorMode.Provider) == "provider", "Expected camel-case storage.");
    Assert(ThemeCatalog.Find("no-such-theme").Id == ThemeCatalog.DefaultThemeId, "Expected the default theme.");
    foreach (ThemeDefinition theme in ThemeCatalog.Themes)
    {
        RgbColor surface = RgbColor.Parse(theme.Surface);
        Assert(RgbColor.Contrast(RgbColor.Parse(theme.Text), surface) >= 7, $"Expected strong text contrast in {theme.Name}.");
        Assert(RgbColor.Contrast(RgbColor.Parse(theme.Muted), surface) >= 4.5, $"Expected readable muted text in {theme.Name}.");
    }
}

static void TranslatesEveryString()
{
    var placeholder = new Regex(@"\{\d+\}");
    foreach ((string key, (string spanish, string english)) in Loc.Strings)
    {
        Assert(spanish.Length > 0 && english.Length > 0, $"Expected both translations for {key}.");
        string[] spanishArguments = placeholder.Matches(spanish).Select(match => match.Value).Order().ToArray();
        string[] englishArguments = placeholder.Matches(english).Select(match => match.Value).Order().ToArray();
        Assert(spanishArguments.SequenceEqual(englishArguments), $"Expected matching placeholders for {key}.");
    }

    foreach (ThemeDefinition theme in ThemeCatalog.Themes)
    {
        string key = "Theme" + char.ToUpperInvariant(theme.Id[0]) + theme.Id[1..];
        Assert(Loc.Strings.ContainsKey(key), $"Expected a translated name for theme {theme.Id}.");
    }

    foreach (AccentPreset accent in ThemeCatalog.Accents)
    {
        Assert(Loc.Strings.ContainsKey(accent.NameKey), $"Expected a translated name for accent {accent.Hex}.");
    }
}

static void DefinesEveryXamlKey()
{
    string trayDirectory = Path.Combine(SourceDirectory(), "..", "CodexBar.Windows.Tray");
    string[] views = Directory.GetFiles(trayDirectory, "*.xaml");
    Assert(views.Length > 0, "Expected to find the tray XAML views.");
    var usage = new Regex(@"\{local:Tr (\w+)\}");
    int keys = 0;
    foreach (string view in views)
    {
        foreach (Match match in usage.Matches(File.ReadAllText(view)))
        {
            keys++;
            Assert(Loc.Strings.ContainsKey(match.Groups[1].Value),
                $"{Path.GetFileName(view)} uses the missing key {match.Groups[1].Value}.");
        }
    }
    Assert(keys > 20, "Expected the XAML views to use translated strings.");
}

static void ResolvesAutomaticLanguage()
{
    Assert(Loc.Resolve("auto", CultureInfo.GetCultureInfo("es-CR")) == Loc.Spanish, "Expected Spanish for es-CR.");
    Assert(Loc.Resolve(null, CultureInfo.GetCultureInfo("es-ES")) == Loc.Spanish, "Expected Spanish for es-ES.");
    Assert(Loc.Resolve("auto", CultureInfo.GetCultureInfo("en-US")) == Loc.English, "Expected English for en-US.");
    Assert(Loc.Resolve("auto", CultureInfo.GetCultureInfo("fr-FR")) == Loc.English, "Expected English fallback.");
    Assert(Loc.Resolve("es", CultureInfo.GetCultureInfo("en-US")) == Loc.Spanish, "Expected an explicit choice to win.");
    Assert(Loc.Resolve("en", CultureInfo.GetCultureInfo("es-CR")) == Loc.English, "Expected an explicit choice to win.");
}

static void PicksNextFreeAccountProfile()
{
    string root = Path.Combine(Path.GetTempPath(), $"codexbar-accounts-{Guid.NewGuid():N}");
    try
    {
        Directory.CreateDirectory(root);
        Assert(ProviderProfileDiscovery.NextAdditionalDirectory("codex", root) == Path.Combine(root, ".codex-2"),
            "Expected .codex-2 when no secondary profile exists.");

        Directory.CreateDirectory(Path.Combine(root, ".codex-2"));
        File.WriteAllText(Path.Combine(root, ".codex-2", "auth.json"), "{}");
        Directory.CreateDirectory(Path.Combine(root, ".codex-3"));
        Assert(ProviderProfileDiscovery.NextAdditionalDirectory("codex", root) == Path.Combine(root, ".codex-3"),
            "Expected an unfinished .codex-3 to be reused.");

        Directory.CreateDirectory(Path.Combine(root, ".claude-2"));
        File.WriteAllText(Path.Combine(root, ".claude-2", ".credentials.json"), "{}");
        Assert(ProviderProfileDiscovery.NextAdditionalDirectory("claude", root) == Path.Combine(root, ".claude-3"),
            "Expected .claude-3 after a signed-in .claude-2.");
        AssertThrows(() => ProviderProfileDiscovery.NextAdditionalDirectory("gemini", root),
            "Expected unsupported providers to be rejected.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void BuildsSignInCommands()
{
    string codex = LoginCommandBuilder.Build("codex", @"C:\Users\O'Brien\.codex-3", createDirectory: true);
    Assert(codex.Contains("New-Item -ItemType Directory -Force -Path 'C:\\Users\\O''Brien\\.codex-3'"),
        "Expected the profile folder to be created with an escaped path.");
    Assert(codex.Contains("$env:CODEX_HOME = 'C:\\Users\\O''Brien\\.codex-3'"), "Expected CODEX_HOME for the profile.");
    Assert(codex.Contains("codex login --device-auth") && codex.Contains("Remove-Item Env:CODEX_HOME"),
        "Expected device-code login and cleanup.");

    string claude = LoginCommandBuilder.Build("claude", @"C:\Users\ana\.claude-2", createDirectory: false);
    Assert(!claude.Contains("New-Item"), "Expected existing profiles not to be recreated.");
    Assert(claude.Contains("claude auth login") && claude.Contains("Remove-Item Env:CLAUDE_CONFIG_DIR"),
        "Expected Claude Code login and cleanup.");
    Assert(LoginCommandBuilder.DisplayPath(@"C:\Users\ana\.claude-2", @"C:\Users\ana") == @"%USERPROFILE%\.claude-2",
        "Expected a %USERPROFILE%-relative display path.");
}

static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

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
