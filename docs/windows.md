---
summary: "Windows port architecture and phase-one portable engine for Codex and Claude Code."
read_when:
  - "Building or testing CodexBar on Windows"
  - "Changing the Codex or Claude Code portable OAuth engine"
  - "Planning the Windows tray application"
---

# CodexBar for Windows

The Windows port starts with a deliberately small engine that supports two providers:

- Codex, using the OAuth credentials owned by the Codex CLI.
- Claude Code, using the OAuth credentials owned by the Claude CLI.

The existing macOS application and full `CodexBarCore` remain unchanged. Windows uses
`CodexBarPortableCore` and `CodexBarWindowsEngine`, which depend only on Foundation and
FoundationNetworking. They do not import AppKit, SwiftUI, Security, WebKit, SweetCookieKit,
SQLite, POSIX process APIs, or browser-cookie code.

The native tray shell lives under `Windows/`. It is a dependency-free WPF application targeting
.NET 10 LTS. `CodexBar.EngineClient` owns process execution and schema validation, while
`CodexBar.Windows.Tray` only owns presentation, refresh scheduling, and tray interactions.

## Security boundary

The engine reads provider-owned credential files:

```text
%USERPROFILE%\.codex\auth.json
%USERPROFILE%\.claude\.credentials.json
```

`CODEX_HOME` remains supported for non-default Codex profiles, and `CLAUDE_CONFIG_DIR` can
override Claude Code's configuration directory. The engine never copies access tokens, refresh
tokens, or API keys into CodexBar configuration and does not print them in its JSON output.

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

The ZIP does not require Swift or .NET to be installed on the destination machine. Swift's
Windows toolchain depends on the Microsoft Visual C++ runtime; the package intentionally does
not copy potentially stale Microsoft DLLs from the toolchain. If the destination does not
already have it, install the
[current x64 Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe).

After extracting the ZIP, verify it and launch the tray application:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Test-CodexBar.ps1 -Launch
```

This validation checks packaged hashes and starts the engine only with `--version`; it does not
probe provider accounts. Launching the tray performs the normal provider refresh.

CI creates the same ZIP in the `CodexBar-win-x64` workflow artifact. This development preview is
not signed, so Windows SmartScreen can display a warning.

## Tray process boundary

The tray launches the engine as a short-lived child process every five minutes or after a manual
refresh. The process uses `UseShellExecute=false`, an argument list rather than a shell command,
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
4. Sign the Windows binaries and package after desktop validation.
5. Build an MSIX or MSI installer from the validated ZIP layout.
6. Add startup registration with an explicit user-controlled setting.
7. Add fixture parity tests against the mature macOS/Linux Codex and Claude mappings.

Browser cookies, WebView2, ConPTY, SQLite, and providers other than Codex and Claude Code are
outside the current scope.

## Provider references

- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference#configtoml)
- [Claude Code authentication and credential storage](https://code.claude.com/docs/en/authentication)
- [.NET deployment overview](https://learn.microsoft.com/dotnet/core/deploying/)
