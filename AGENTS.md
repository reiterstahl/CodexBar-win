# Repository Guidelines

CodexBar for Windows is a fork of [steipete/CodexBar](https://github.com/steipete/CodexBar) that
keeps only the Windows port: a Foundation-only Swift usage engine and a native WPF tray app.

## Project Structure
- `Sources/CodexBarPortableCore`: Foundation-only Swift engine for Codex and Claude Code OAuth usage.
  No AppKit, POSIX, browser-cookie, or Keychain dependencies.
- `Sources/CodexBarWindowsEngine`: CLI entry that prints the schema-versioned JSON snapshot.
- `Tests/CodexBarPortableCoreTests`: Swift Testing coverage for the engine.
- `Windows/CodexBar.EngineClient`: .NET library that launches the engine and validates its JSON.
- `Windows/CodexBar.Windows.Tray`: WPF tray app (.NET 10). `Appearance/` holds framework-independent
  color/pace math, `UsageMeter` draws the charts, `AppUpdater` wraps Velopack updates.
- `Windows/CodexBar.EngineClient.Tests`: console test runner, also compiles `Appearance/*.cs`.
- `Windows/*.ps1`: publish, package (Velopack), install/uninstall the portable ZIP, add accounts.
- `docs/windows*.md`: architecture (English) and installation/account guides (Spanish).

## Build and Test
- Engine: `swift build -c release --product CodexBarWindowsEngine` and `swift test --filter Portable`.
- Tray: `dotnet build Windows/CodexBar.Windows.Tray/CodexBar.Windows.Tray.csproj -c Release`
  (on Linux/macOS add `-p:EnableWindowsTargeting=true`; it compiles but cannot run).
- Client and appearance tests: `dotnet run --project Windows/CodexBar.EngineClient.Tests -c Release`.
- Portable package: `.\Windows\Publish-Windows.ps1`; installer: `.\Windows\Package-Release.ps1 -Version X.Y.Z`.
- Warnings are errors in the .NET projects; keep builds warning-free.

## Releases
- Push a `windows-vX.Y.Z` tag; `.github/workflows/release-windows.yml` builds and publishes the
  Velopack release that installed copies update from. Tags with a suffix become pre-releases.
- GitHub skips push-triggered workflows when the tagged commit message contains `[skip ci]`.
- Keep the Velopack NuGet version and `VELOPACK_VERSION` in the workflow in sync.

## Coding Style
- C#: match the existing style (file-scoped namespaces, explicit types for non-obvious values,
  `System.Windows.*` types qualified where WinForms names collide).
- XAML: theme-dependent brushes use `DynamicResource`; colors come from `AppearancePalette`.
- Swift: SwiftFormat/SwiftLint configs are in the repo root; 4-space indent, 120-char lines.
- User-facing strings are Spanish.

## Security
- Never read, copy, log, or persist provider credentials in the tray. Only the engine touches the
  provider-owned credential files, and only schema-validated JSON crosses the process boundary.
- `%LOCALAPPDATA%\CodexBar\settings.json` holds presentation preferences only.
- Tests use fictitious accounts and tokens; never commit real ones.

## Commits
- Short imperative subjects (e.g. "Fix icon dimming"); keep commits scoped.
