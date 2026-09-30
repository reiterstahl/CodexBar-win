---
summary: "Windows port architecture and phase-one portable engine for Codex and Claude Code."
read_when:
  - "Building or testing CodexBar on Windows"
  - "Changing the Codex or Claude Code portable OAuth engine"
  - "Planning the Windows tray application"
---

# CodexBar for Windows

For an end-to-end Spanish setup guide for a new Windows 11 PC, see
[Instalación de CodexBar en una PC nueva](windows-installation.es.md).

The Windows port starts with a deliberately small engine that supports two providers:

- Codex, using the OAuth credentials owned by the Codex CLI.
- Claude Code, using the OAuth credentials owned by the Claude CLI.

This repository keeps only the Windows port; the macOS app and the full `CodexBarCore` live in the
[original project](https://github.com/steipete/CodexBar). Windows uses
`CodexBarPortableCore` and `CodexBarWindowsEngine`, which depend only on Foundation and
FoundationNetworking. They do not import AppKit, SwiftUI, Security, WebKit, SweetCookieKit,
SQLite, POSIX process APIs, or browser-cookie code.

The native tray shell lives under `Windows/`. It is a WPF application targeting
.NET 10 LTS. `CodexBar.EngineClient` owns process execution and schema validation, while
`CodexBar.Windows.Tray` only owns presentation, refresh scheduling, and tray interactions.

The tray offers Cards, Summary, and Mini views and a **Personalizar** window with five themes
(Grafito is the default, with `#D9D900` accents), any accent color, five chart types (bars,
rings, gauge, blocks, numbers), accent/level/provider chart colors, available-or-used
percentages, density, 80%–160% interface scaling, a pace marker, Windows notifications when a
quota returns, and a dynamic notification-area icon. Theme brushes are application resources
replaced at runtime (`AppearancePalette`), and user-chosen colors are nudged until they reach
WCAG contrast on the active theme. Account names and all of these preferences are stored in
`%LOCALAPPDATA%\CodexBar\settings.json`; older `compactView` settings migrate to the Summary
view. This file contains presentation preferences and profile directory identifiers only; it
never contains OAuth credentials. The window restores the last position selected by dragging it.
Reset timing is presented as a minute-level live countdown with a short reset date, and the full
localized weekday/date in each quota's tooltip. Countdown labels update every minute without launching provider processes; provider
usage still refreshes every five minutes.

## Security boundary

The engine reads provider-owned credential files:

```text
%USERPROFILE%\.codex\auth.json
%USERPROFILE%\.claude\.credentials.json
```

`CODEX_HOME` remains supported for non-default Codex profiles, and `CLAUDE_CONFIG_DIR` can
override Claude Code's configuration directory. The engine never copies access tokens, refresh
tokens, or API keys into CodexBar configuration and does not print them in its JSON output.

The Windows tray discovers additional isolated accounts in `%USERPROFILE%\.codex-*` and
`%USERPROFILE%\.claude-*` when their provider-owned credential files exist. It launches one
short-lived engine process per account, passing only that profile's `CODEX_HOME` or
`CLAUDE_CONFIG_DIR`. The default `.codex` and `.claude` sessions remain unchanged.

Phase one does not refresh or persist rotated OAuth tokens. When a provider rejects or expires a
credential, the engine reports a structured failure and asks the user to authenticate through the
provider CLI. This avoids writing provider credentials on Windows before the Credential Manager
and ACL implementation is available.

## Build

Install the [current Swift toolchain for Windows](https://www.swift.org/install/windows/), then:

```powershell
swift build -c release --product CodexBarWindowsEngine
swift test --filter Portable
```

Fetch both providers:

```powershell
$engineDirectory = swift build -c release --product CodexBarWindowsEngine --show-bin-path
& "$engineDirectory\CodexBarWindowsEngine.exe" --pretty
```

Fetch one provider:

```powershell
& "$engineDirectory\CodexBarWindowsEngine.exe" --provider codex --pretty
& "$engineDirectory\CodexBarWindowsEngine.exe" --provider claude --pretty
```

Provider failures are part of the snapshot rather than process-fatal, so the tray UI can
continue displaying one provider when the other provider is signed out or unavailable.

Build the Windows tray application:

```powershell
cd Windows
dotnet build .\CodexBar.Windows.Tray\CodexBar.Windows.Tray.csproj -c Release
dotnet run --project .\CodexBar.EngineClient.Tests\CodexBar.EngineClient.Tests.csproj -c Release
```

Place `CodexBarWindowsEngine.exe` beside `CodexBar.Windows.Tray.exe`, or set
`CODEXBAR_ENGINE_PATH` to its absolute path during development.

## Publish a Windows 11 preview

Run the packaging script from a PowerShell terminal on an x64 Windows machine:

```powershell
.\Windows\Publish-Windows.ps1
```

To produce only a transferable ZIP on the desktop, without retaining the expanded artifact and
publish-staging directories:

```powershell
.\Windows\Publish-Windows.ps1 -SkipTests -ArchiveOnly `
  -ArchivePath "$env:USERPROFILE\Desktop\CodexBar-Windows.zip"
```

The script:

1. Runs the portable Swift and engine-client tests.
2. Builds the Swift engine in release mode.
3. Publishes the WPF application for `win-x64`, self-contained and without trimming.
4. Copies the Swift runtime DLLs while deliberately excluding Microsoft C/C++ runtime DLLs
   from the Swift toolchain.
5. Smoke-tests the packaged engine with a minimal `PATH`.
6. Produces per-file and archive SHA-256 checksums.

The output is:

```text
artifacts\windows\CodexBar-win-x64\
artifacts\windows\CodexBar-win-x64.zip
artifacts\windows\CodexBar-win-x64.zip.sha256
```

Only `CodexBar-win-x64.zip` needs to be transferred to another PC. The ZIP does not require
Swift, .NET, Visual Studio, Git, or the source repository on the destination machine. Swift's
Windows toolchain depends on the Microsoft Visual C++ runtime; the package intentionally does not
copy potentially stale Microsoft DLLs from the toolchain. If the destination does not already
have it, install the
[current x64 Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe).

After extracting the ZIP, install and launch the tray application:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Install-CodexBar.ps1 -DesktopShortcut -StartWithWindows
```

The installer verifies every packaged hash, smoke-tests the engine without probing accounts,
installs to `%LOCALAPPDATA%\Programs\CodexBar`, and creates Start menu shortcuts. The optional
switches create a desktop shortcut and a current-user Startup shortcut. Installation requires no
administrator privileges. To remain fully portable, run `Test-CodexBar.ps1 -Launch` instead.

Use **Uninstall CodexBar** from the Start menu to remove the application. Presentation settings
are preserved unless `Uninstall-CodexBar.ps1 -RemoveSettings` is used. Uninstallation never
removes provider-owned Codex or Claude credentials.

To create and authenticate a second isolated account for each provider:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Add-CodexBarAccounts.ps1 -Login
```

The Codex step defaults to `codex login --device-auth`. It prints a URL and one-time code instead
of opening the default browser, so the URL can be completed in the browser profile for the intended
account. Device-code authentication may first need to be enabled in ChatGPT security settings.
Pass `-CodexBrowserLogin` to opt back into the standard browser-opening flow.

The default profile name is `2`, producing `%USERPROFILE%\.codex-2` and
`%USERPROFILE%\.claude-2`. Pass `-ProfileName work` (or another short name) to create more
profiles. Restart CodexBar or refresh after authentication.

CI can create the same ZIP in the `CodexBar-win-x64` workflow artifact, but CI is not required:
`Publish-Windows.ps1` creates it entirely on the build PC. The package is not signed, so Windows
SmartScreen can display a warning.

## Installer and updates

Public releases are built by `.github/workflows/release-windows.yml` when a `windows-vX.Y.Z` tag is
pushed. The workflow runs the portable Swift and engine-client tests, calls
`Windows/Package-Release.ps1`, and publishes the output of `vpk upload github`:

```text
CodexBarWindows-win-Setup.exe      per-user installer (no administrator rights)
CodexBarWindows-win-Portable.zip   portable folder without automatic updates
CodexBarWindows-X.Y.Z-full.nupkg   full update package (plus -delta packages)
releases.win.json, RELEASES        update feed consumed by installed copies
```

[Velopack](https://velopack.io) installs the application under `%LOCALAPPDATA%\CodexBarWindows`
(pack id `CodexBarWindows`, deliberately distinct from the `%LOCALAPPDATA%\CodexBar` settings
folder so uninstalling never deletes preferences) and bootstraps the Visual C++ 2022 x64
redistributable required by the Swift runtime. `Program.Main` runs `VelopackApp` before WPF starts.
Installed copies (`UpdateManager.IsInstalled`) check the GitHub Releases feed 30 seconds after
startup and every six hours, download updates in the background, and offer a restart; pending
updates are otherwise applied on the next launch. Portable and development builds never update.
Updates require the repository to be public, since the app queries GitHub without a token.

"Iniciar con Windows" writes a per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value
pointing at the stable `current\CodexBar.Windows.Tray.exe` path; the uninstall hook removes it.

To package locally on Windows:

```powershell
dotnet tool install -g vpk --version 1.2.161
.\Windows\Package-Release.ps1 -Version 1.0.0
```

## Tray process boundary

The tray launches the engine as a short-lived child process every five minutes or after a manual
refresh. Multi-account profiles each receive an independent child process. Each process uses
`UseShellExecute=false`, an argument list rather than a shell command,
redirected standard output, a 45-second timeout, and a one-million-character response limit.

Only schema-versioned JSON crosses this boundary. Standard error and malformed output are never
shown verbatim, reducing the chance that an accidental provider diagnostic exposes credential
material. The tray rejects unknown schema versions and providers.

## Snapshot contract

The engine emits:

- `schemaVersion`
- `generatedAt`
- successful `providers`
- independent `failures`
- session, weekly, model-specific windows, reset timestamps, and remaining percentages
- non-secret identity fields when available

The WPF tray application consumes this contract without loading or parsing credential files
itself.

## Remaining Windows work

1. Validate the native Windows job and tray behavior on a real Windows desktop.
2. Add Windows Credential Manager support before CodexBar owns any secrets.
3. Add provider-CLI version detection without PTY requirements.
4. Sign the Windows binaries and Setup.exe (for example with SignPath or Azure Trusted Signing).
5. Publish a winget manifest that points at the GitHub Release installer.
6. Add fixture parity tests against the mature macOS/Linux Codex and Claude mappings.

Browser cookies, WebView2, ConPTY, SQLite, and providers other than Codex and Claude Code are
outside the current scope.

## Provider references

- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference#configtoml)
- [Claude Code authentication and credential storage](https://code.claude.com/docs/en/authentication)
- [.NET deployment overview](https://learn.microsoft.com/dotnet/core/deploying/)
