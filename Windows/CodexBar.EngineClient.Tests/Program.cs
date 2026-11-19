using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CodexBar.EngineClient;
using CodexBar.Windows.Tray;

var tests = new (string Name, Action Run)[]
{
    ("persists exact reset dates independently across accounts and refreshes", PersistsResetDates),
    ("formats exact reset dates in local time with AM and PM", FormatsExactReset),
    ("keeps extra usage percentages and credit balances honest", MapsExtraUsage),
    ("detects native Claude version output and paths", DetectsClaudeVersion),
    ("expires saved resets without losing count-only inventories", ExpiresSavedResets),
    ("keeps Claude reset inventory on its own account", ParsesClaudeResets),
    ("parses snapshot contract", ParsesSnapshotContract),
    ("rejects unsupported schema", RejectsUnsupportedSchema),
    ("rejects unknown provider", RejectsUnknownProvider),
    ("resolves explicit engine path", ResolvesExplicitEnginePath),
    ("discovers isolated provider profiles", DiscoversIsolatedProviderProfiles),
    ("parses and formats hex colors", ParsesAndFormatsHexColors),
    ("lifts custom accents to readable contrast", LiftsCustomAccentsToReadableContrast),
    ("picks readable ink for accent fills", PicksReadableInkForAccentFills),
    ("projects pace until reset", ProjectsPaceUntilReset),
    ("counts down exhausted quotas without changing usage", CountsDownExhaustedQuotas),
    ("shades reset countdowns continuously with readable contrast", ShadesResetCountdowns),
    ("selects classic usage or reset wait for exhausted meters", SelectsExhaustedMeterModes),
    ("waits for every blocking quota before forecasting recovery", WaitsForEveryBlockingReset),
    ("ranks accounts by both session and weekly quota", OrdersByCombinedQuota),
    ("keeps quota ordering stable and handles missing data", KeepsQuotaOrderingStable),
    ("migrates account order preferences without changing old choices", MigratesAccountOrdering),
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

static void PersistsResetDates()
{
    var choices = new ResetDatePreferences();
    choices.SetExact("claude-main", "session", true);
    choices.SetExact("claude-secondary", "weekly", true);
    string json = System.Text.Json.JsonSerializer.Serialize(choices);
    ResetDatePreferences restored = System.Text.Json.JsonSerializer.Deserialize<ResetDatePreferences>(json)!;
    restored.Normalize();
    Assert(restored.IsExact("CLAUDE-MAIN", "SESSION"), "Restore the selected account/window after restart.");
    Assert(!restored.IsExact("claude-main", "weekly"), "Keep windows independent.");
    Assert(!restored.IsExact("claude-secondary", "session"), "Keep accounts independent.");
    restored.SetExact("claude-main", "session", false);
    Assert(!restored.IsExact("claude-main", "session"), "Toggling back must remove the stored selection.");
    Assert(restored.IsExact("claude-secondary", "weekly"), "Preserve the other account's selection.");
}

static void FormatsExactReset()
{
    var zone = TimeZoneInfo.CreateCustomTimeZone("test-UTC-6", TimeSpan.FromHours(-6), "UTC-6", "UTC-6");
    var es = CultureInfo.GetCultureInfo("es-CR");
    var en = CultureInfo.GetCultureInfo("en-US");
    Assert(UsageMath.FormatExactReset(DateTimeOffset.Parse("2026-11-20T18:00:00Z"), en, zone) == "FRI-20NOV, 12:00PM", "Show the abbreviated weekday.");
    Assert(UsageMath.FormatExactReset(DateTimeOffset.Parse("2026-11-17T18:00:00Z"), es, zone) == "MAR-17NOV, 12:00PM", "Noon must be PM.");
    Assert(UsageMath.FormatExactReset(DateTimeOffset.Parse("2026-11-17T06:00:00Z"), en, zone) == "TUE-17NOV, 12:00AM", "Midnight must be AM.");
    Assert(UsageMath.FormatExactReset(DateTimeOffset.Parse("2026-11-17T01:05:00Z"), es, zone) == "LUN-16NOV, 07:05PM", "Convert the weekday and date as well as the time.");
    Assert(UsageMath.FormatExactReset(DateTimeOffset.Parse("2027-01-02T18:00:00Z"), es, zone) == "SÁB-02ENE, 12:00PM", "Localize weekday and month without dots.");
}

static void MapsExtraUsage()
{
    var claude = new ExtraUsage("USD", 5.25, 20, null, true, false);
    Assert(claude.RemainingPercent == 73.75, "Expected remaining monthly extra usage.");
    Assert(new ExtraUsage("USD", 25, 20, null, true, false).RemainingPercent == 0, "Clamp overspending.");
    Assert(new ExtraUsage("USD", null, 20, null, true, false).RemainingPercent is null, "Unknown spend is not zero.");
    Assert(new ExtraUsage("USD", 5, null, null, true, false).RemainingPercent is null, "No percentage without cap.");
    Assert(new ExtraUsage("credits", null, null, 12, true, false).RemainingPercent is null, "Do not invent Codex percentages.");
    Assert(!new ExtraUsage("credits", null, null, 0, false, false).IsEnabled, "Retain disabled/exhausted credit state.");
    EngineSnapshot snapshot = EngineSnapshotParser.Parse("""
        {"schemaVersion":1,"generatedAt":"2026-11-17T12:00:00Z","failures":[],"providers":[
          {"provider":"claude","displayName":"Claude","source":"oauth","windows":[],
           "updatedAt":"2026-11-17T12:00:00Z",
           "extraUsage":{"currency":"USD","used":5.25,"limit":20,"balance":null,"isEnabled":true,"unlimited":false}}
        ]}
        """);
    Assert(snapshot.Providers[0].ExtraUsage == claude, "Preserve normalized amounts in the engine contract.");
}

static void DetectsClaudeVersion()
{
    Assert(ClaudeVersionDiscovery.ParseVersion("2.1.283 (Claude Code)\r\n") == "2.1.283", "Expected CLI version.");
    Assert(ClaudeVersionDiscovery.ParseVersion("not a version 2.1.283") is null, "Reject unrelated output.");
    Assert(ClaudeVersionDiscovery.ParseVersion("2.1") is null, "Reject incomplete versions.");
    string native = Path.Combine("user", ".local", "bin", "claude.exe");
    string[] candidates = ClaudeVersionDiscovery.CandidatePaths("first" + Path.PathSeparator + "second", "user").ToArray();
    Assert(candidates.SequenceEqual([Path.Combine("first", "claude.exe"), Path.Combine("second", "claude.exe"), native]),
        "Search PATH and the native installation path.");
    if (!OperatingSystem.IsWindows())
    {
        string directory = Path.Combine(Path.GetTempPath(), "codexbar-version-test-" + Guid.NewGuid());
        string? oldPath = Environment.GetEnvironmentVariable("PATH");
        string? oldVersion = Environment.GetEnvironmentVariable("CLAUDE_CODE_VERSION");
        Directory.CreateDirectory(directory);
        try
        {
            string executable = Path.Combine(directory, "claude.exe");
            File.WriteAllText(executable, "#!/bin/sh\nprintf '2.1.283 (Claude Code)\\n'\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("PATH", directory);
            Environment.SetEnvironmentVariable("CLAUDE_CODE_VERSION", null);
            Assert(ClaudeVersionDiscovery.ResolveAsync().GetAwaiter().GetResult() == "2.1.283",
                "Run the discovered executable and read its version.");
            Environment.SetEnvironmentVariable("CLAUDE_CODE_VERSION", "2.1.300");
            Assert(ClaudeVersionDiscovery.ResolveAsync().GetAwaiter().GetResult() == "2.1.300",
                "Honor an explicit version override.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", oldPath);
            Environment.SetEnvironmentVariable("CLAUDE_CODE_VERSION", oldVersion);
            Directory.Delete(directory, recursive: true);
        }
    }
}

static void ExpiresSavedResets()
{
    DateTimeOffset now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
    var credits = new ResetCredits(3, [new(now, null), new(now.AddMinutes(1), null), new(null, null)]);
    Assert(credits.AvailableAt(now.AddSeconds(-1)) == 3, "All resets should still be available.");
    Assert(credits.AvailableAt(now) == 2, "Expiry boundary should remove a reset.");
    Assert(credits.AvailableAt(now.AddMinutes(1)) == 1, "Unbounded reset should remain.");
    Assert(new ResetCredits(2, []).AvailableAt(now) == 2, "Keep count-only Codex inventory.");
    Assert(new ResetCredits(-1, null).AvailableAt(now) == 0, "Negative counts must be clamped.");
}

static void ParsesClaudeResets()
{
    EngineSnapshot snapshot = EngineSnapshotParser.Parse("""
        {
          "schemaVersion":1,"generatedAt":"2026-11-10T12:00:00Z","failures":[],
          "providers":[
            {"provider":"codex","displayName":"Codex","source":"oauth","windows":[],
             "updatedAt":"2026-11-10T12:00:00Z"},
            {"provider":"claude","displayName":"Claude Code","source":"oauth","windows":[],
             "updatedAt":"2026-11-10T12:00:00Z",
             "resetCredits":{"availableCount":2,"credits":[
               {"expiresAt":"2026-11-16T12:00:00Z"},{"expiresAt":null}]}}
          ]
        }
        """);
    Assert(snapshot.Providers[0].ResetCredits is null, "Claude resets must not populate Codex.");
    Assert(snapshot.Providers[1].ResetCredits?.AvailableCount == 2, "Expected two Claude resets.");
}

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

static void CountsDownExhaustedQuotas()
{
    var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    foreach (bool showUsed in new[] { false, true })
    {
        foreach (int minutes in new[] { 300, 10_080, 43_200 })
        {
            foreach (double fraction in new[] { 1.0, 0.75, 0.5, 0.25, 0.1, 0.0 })
            {
                QuotaMeterState meter = WaitMeter(100, showUsed, minutes,
                    now.AddMinutes(minutes * fraction), now);
                Assert(meter.IsResetCountdown && Math.Abs(meter.Percent - (100 * fraction)) < 0.000001,
                    "Measure wait time for session, weekly and supplied monthly windows in either usage mode.");
            }
        }

        Assert(WaitMeter(100, showUsed, 300, now.AddHours(8), now) == new QuotaMeterState(100, true),
            "Clamp waits longer than the supplied window.");
        Assert(WaitMeter(100, showUsed, 300, now.AddMinutes(-1), now) == new QuotaMeterState(0, true),
            "An overdue reset remains exhausted until the provider confirms fresh quota.");
        Assert(WaitMeter(99, showUsed, 300, now.AddMinutes(75), now) ==
            new QuotaMeterState(showUsed ? 99 : 1, false), "Do not replace a quota that still has capacity.");
        Assert(WaitMeter(0, showUsed, 300, now.AddHours(5), now) ==
            new QuotaMeterState(showUsed ? 0 : 100, false), "Restore the usage meter when quota returns.");
        foreach (int? span in new int?[] { null, 0, -1 })
        {
            Assert(!WaitMeter(100, showUsed, span, now.AddHours(5), now).IsResetCountdown,
                "Never invent a window duration, including calendar months.");
        }
        Assert(!WaitMeter(100, showUsed, 300, null, now).IsResetCountdown,
            "Do not fabricate a reset date.");
    }

    DateTimeOffset reset = now.AddMinutes(150);
    Assert(WaitMeter(100, true, 300, reset, now.AddMinutes(75)).Percent == 25,
        "Wait progress advances locally without a new provider snapshot.");
    Assert(WaitMeter(100, true, 300, reset.ToOffset(TimeSpan.FromHours(-6)), now).Percent == 50,
        "Time zones must not change the remaining fraction.");

    static QuotaMeterState WaitMeter(double used, bool showUsed, int? minutes, DateTimeOffset? reset, DateTimeOffset at)
        => UsageMath.CalculateMeter(used, showUsed, minutes, reset, at, ExhaustedMeterMode.ResetWait);
}

static void SelectsExhaustedMeterModes()
{
    var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    foreach (bool showUsed in new[] { false, true })
    {
        QuotaMeterState classic = UsageMath.CalculateMeter(100, showUsed, 300, now.AddMinutes(36), now);
        Assert(classic == new QuotaMeterState(showUsed ? 100 : 0, false),
            "Default to actual exhausted usage, not a small green wait meter.");
        Assert(UsageMath.CalculateMeter(100, showUsed, 300, now.AddMinutes(36), now, ExhaustedMeterMode.ResetWait) ==
            new QuotaMeterState(12, true), "Keep countdown mode available when explicitly chosen.");
        foreach (ExhaustedMeterMode mode in Enum.GetValues<ExhaustedMeterMode>())
        {
            Assert(UsageMath.CalculateMeter(16, showUsed, 300, now.AddHours(5), now, mode) ==
                new QuotaMeterState(showUsed ? 16 : 84, false), "Non-exhausted windows keep actual usage in both modes.");
            Assert(UsageMath.CalculateMeter(100, showUsed, 300, now.AddMinutes(-1), now, mode) ==
                new QuotaMeterState(mode == ExhaustedMeterMode.ResetWait ? 0 : showUsed ? 100 : 0,
                    mode == ExhaustedMeterMode.ResetWait), "An elapsed reset must never pretend quota is recovered.");
        }
    }

    Assert(ThemeCatalog.ParseOption<ExhaustedMeterMode>(null, ExhaustedMeterMode.Usage) == ExhaustedMeterMode.Usage,
        "Old settings without the new choice migrate to the unambiguous classic meter.");
    Assert(ThemeCatalog.ParseOption("invalid", ExhaustedMeterMode.Usage) == ExhaustedMeterMode.Usage,
        "Invalid settings fall back to classic usage.");
    foreach (ExhaustedMeterMode mode in Enum.GetValues<ExhaustedMeterMode>())
    {
        Assert(ThemeCatalog.ParseOption(ThemeCatalog.FormatOption(mode), ExhaustedMeterMode.Usage) == mode,
            "Both selected modes round-trip through stored preferences.");
    }
}

static void WaitsForEveryBlockingReset()
{
    var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    DateTimeOffset sessionReset = now.AddHours(3);
    DateTimeOffset weeklyReset = now.AddDays(1);
    Assert(UsageMath.AccountRecoveryAt([(true, sessionReset), (true, weeklyReset)]) == weeklyReset,
        "The earlier session reset does not unblock an exhausted weekly quota.");
    Assert(UsageMath.AccountRecoveryAt([(false, sessionReset), (true, weeklyReset)]) == weeklyReset,
        "A usable session must not hide the blocking weekly reset.");
    Assert(UsageMath.AccountRecoveryAt([(true, sessionReset), (false, weeklyReset)]) == sessionReset,
        "Ignore a later weekly reset when that quota still has capacity.");
    Assert(UsageMath.AccountRecoveryAt([(true, weeklyReset), (true, sessionReset)]) == weeklyReset,
        "Window enumeration order must not affect the forecast.");
    Assert(UsageMath.AccountRecoveryAt([(true, sessionReset), (true, null)]) is null,
        "Never promise full recovery when one blocking reset is unknown.");
    Assert(UsageMath.AccountRecoveryAt([(false, null), (true, sessionReset)]) == sessionReset,
        "An unknown reset on a non-exhausted window does not block forecasting.");
    Assert(UsageMath.AccountRecoveryAt([(false, sessionReset), (false, weeklyReset)]) is null &&
        UsageMath.AccountRecoveryAt([]) is null, "Do not show an exhausted-account forecast for usable or empty accounts.");
    DateTimeOffset overdue = now.AddMinutes(-1);
    Assert(UsageMath.AccountRecoveryAt([(true, overdue)]) == overdue,
        "Retain exhausted state after the date passes until the provider confirms recovery.");
    Assert(UsageMath.AccountRecoveryAt([(true, weeklyReset.ToOffset(TimeSpan.FromHours(-6))), (true, sessionReset)]) == weeklyReset,
        "Compare actual instants, not local clock labels.");
}

static void ShadesResetCountdowns()
{
    foreach (ThemeDefinition theme in ThemeCatalog.Themes)
    {
        StatusColors status = ThemeCatalog.StatusFor(theme);
        Assert(UsageMath.ResetCountdownColor(100, status) == status.Critical, "Full wait is red.");
        Assert(UsageMath.ResetCountdownColor(50, status) == RgbColor.Parse("#F5D547"), "Half a wait is yellow.");
        Assert(UsageMath.ResetCountdownColor(25, status) == status.Ok, "The last quarter is green.");
        Assert(UsageMath.ResetCountdownColor(-1, status) == UsageMath.ResetCountdownColor(0, status), "Clamp expired waits.");
        Assert(UsageMath.ResetCountdownColor(101, status) == status.Critical, "Clamp long waits.");
        RgbColor background = RgbColor.Parse(theme.Surface).Mix(status.Critical, theme.IsLight ? 0.06 : 0.10);
        RgbColor previous = UsageMath.ResetCountdownColor(0, status);
        for (int percent = 1; percent <= 100; percent++)
        {
            RgbColor color = UsageMath.ResetCountdownColor(percent, status);
            Assert(Math.Abs(color.R - previous.R) <= 10 && Math.Abs(color.G - previous.G) <= 10 &&
                Math.Abs(color.B - previous.B) <= 10, "No abrupt color jumps at either threshold.");
            Assert(RgbColor.Contrast(color.EnsureContrast(background, 3), background) >= 3,
                "Keep countdown graphics readable on the exhausted card in every theme.");
            Assert(RgbColor.Contrast(color.EnsureContrast(background, 4.5), background) >= 4.5,
                "Keep countdown labels readable in every theme.");
            previous = color;
        }
    }
}

static void OrdersByCombinedQuota()
{
    (string Name, double? Session, double? Weekly, bool Error)[] accounts =
    [
        ("Codex RQR", 100, 1, false),
        ("Codex U", 100, 92, false),
        ("Claude Patricio", 100, 56, false),
        ("Claude RQR", 0, 0, false),
        ("Claude U", 100, 0, false),
        ("Unknown", null, null, false),
        ("Signed out", 100, 100, true),
    ];
    var mostQuota = AccountOrdering.Order(accounts, AccountSortOrder.MostQuota,
        account => AccountQuotaRank.FromRemaining(account.Session, account.Weekly, account.Error));
    Assert(mostQuota.Select(account => account.Name).SequenceEqual(
        ["Codex U", "Claude Patricio", "Codex RQR", "Claude U", "Claude RQR", "Unknown", "Signed out"]),
        "Weekly availability must matter even when every usable session is full; exhausted accounts stay last.");

    AccountQuotaRank[] ranks =
    [
        AccountQuotaRank.FromRemaining(100, 1),
        AccountQuotaRank.FromRemaining(40, 40),
        AccountQuotaRank.FromRemaining(90, 40),
        AccountQuotaRank.FromRemaining(40, 80),
        AccountQuotaRank.FromRemaining(10, 100),
    ];
    Assert(AccountOrdering.Order(ranks, AccountSortOrder.MostQuota, rank => rank)
        .SequenceEqual(new[] { ranks[2], ranks[3], ranks[1], ranks[4], ranks[0] }),
        "Prioritize the lower remaining quota, then use the higher quota to break ties.");

    Assert(AccountOrdering.Order(accounts, AccountSortOrder.Original,
        _ => throw new InvalidOperationException("Original order must not score quota.")).SequenceEqual(accounts),
        "Original order must remain unchanged.");
    Assert(AccountOrdering.Order(accounts, AccountSortOrder.AvailableFirst,
        account => AccountQuotaRank.FromRemaining(account.Session, account.Weekly, account.Error))
        .Select(account => account.Name).SequenceEqual(
            ["Codex RQR", "Codex U", "Claude Patricio", "Claude RQR", "Claude U", "Unknown", "Signed out"]),
        "Available first preserves discovery order within availability groups.");
}

static void KeepsQuotaOrderingStable()
{
    AccountQuotaRank tied = AccountQuotaRank.FromRemaining(45, 80);
    var accounts = new[] { (Id: "first", Rank: tied), (Id: "second", Rank: tied),
        (Id: "third", Rank: AccountQuotaRank.FromRemaining(80, 45)) };
    Assert(AccountOrdering.Order(accounts, AccountSortOrder.MostQuota, account => account.Rank).SequenceEqual(accounts),
        "Equal scores must not jump around, including when the limiting windows are reversed.");
    Assert(AccountQuotaRank.FromRemaining(70, null) == new AccountQuotaRank(0, 70, 70),
        "Use the reported quota when only one window exists, not an invented 100% for the missing one.");
    Assert(AccountQuotaRank.FromRemaining(null, 0).Group == 1, "One exhausted window makes the account exhausted.");
    Assert(AccountQuotaRank.FromRemaining(null, null).Group == 2, "Unknown quota must not outrank usable quota.");
    Assert(AccountQuotaRank.FromRemaining(100, 100, true).Group == 3, "Authentication errors belong at the end.");
    Assert(AccountQuotaRank.FromRemaining(double.NaN, double.PositiveInfinity).Group == 2, "Reject non-finite data.");
    Assert(AccountQuotaRank.FromRemaining(-1, 101) == new AccountQuotaRank(1, 0, 100), "Clamp quota bounds.");
    Assert(!AccountOrdering.Order(Array.Empty<AccountQuotaRank>(), AccountSortOrder.MostQuota, rank => rank).Any(),
        "An empty list needs no special-case UI handling.");
    var updated = new[] { (Id: "first", Rank: AccountQuotaRank.FromRemaining(20, 80)),
        (Id: "second", Rank: AccountQuotaRank.FromRemaining(90, 80)) };
    Assert(AccountOrdering.Order(updated, AccountSortOrder.MostQuota, account => account.Rank).First().Id == "second",
        "A new snapshot must immediately change the order when real quota changes.");
}

static void MigratesAccountOrdering()
{
    Assert(AccountOrdering.ResolveOrder(null, true) == AccountSortOrder.AvailableFirst, "Preserve the old default.");
    Assert(AccountOrdering.ResolveOrder(null, false) == AccountSortOrder.Original, "Preserve disabled available-first.");
    Assert(AccountOrdering.ResolveOrder("invalid", false) == AccountSortOrder.Original, "Invalid stored values fall back safely.");
    Assert(AccountOrdering.ResolveOrder("99", true) == AccountSortOrder.AvailableFirst, "Reject unknown numeric enum values.");
    foreach (AccountSortOrder order in Enum.GetValues<AccountSortOrder>())
    {
        string stored = ThemeCatalog.FormatOption(order);
        Assert(AccountOrdering.ResolveOrder(stored, true) == order && AccountOrdering.ResolveOrder(stored, false) == order,
            "An explicit stored choice overrides the legacy flag after saving and reloading.");
        Assert(AccountOrdering.ResolveOrder(stored.ToUpperInvariant(), true) == order, "Parse preferences case-insensitively.");
    }
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
