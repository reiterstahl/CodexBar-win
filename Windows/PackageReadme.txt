CodexBar for Windows 11
=======================

This package supports only:

  - OpenAI Codex, using the existing Codex CLI login.
  - Claude Code, using the existing Claude CLI login.

For complete instructions in Spanish, open INSTALLATION.md.

It does not copy, store, or refresh provider credentials. Authenticate with the
official provider CLIs before opening CodexBar.

Install
-------

Open PowerShell in this directory and run:

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-CodexBar.ps1

This verifies the package, installs CodexBar under your local application-data
directory, creates Start menu shortcuts, and launches it. No administrator,
Swift, .NET SDK, Visual Studio, or source repository is required.

For a desktop shortcut and automatic startup:

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-CodexBar.ps1 -DesktopShortcut -StartWithWindows

To run directly from the extracted folder without installing:

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-CodexBar.ps1 -Launch

If Windows reports a missing Microsoft Visual C++ runtime, install the latest
supported x64 Visual C++ Redistributable from Microsoft, then retry:

  https://aka.ms/vs/17/release/vc_redist.x64.exe

This package is not code-signed yet. Windows SmartScreen may ask you to confirm
that you want to run it.

Uninstall
---------

Use "Uninstall CodexBar" in the Start menu. It removes the application but
preserves presentation settings and all provider-owned credentials. To remove
CodexBar presentation settings too, run:

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\CodexBar\Uninstall-CodexBar.ps1" -RemoveSettings

Troubleshooting
---------------

Codex login:

  codex login

Claude Code login:

  claude

Refresh manually from the CodexBar tray menu after authenticating. The app also
refreshes automatically every five minutes.

Multiple accounts
-----------------

CodexBar can discover isolated profiles named .codex-* and .claude-* under
your Windows user profile. To create and authenticate a second account for
each provider, run:

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Add-CodexBarAccounts.ps1 -Login

Codex uses device-code login by default: the terminal prints a URL and
one-time code without opening the default browser. Open the URL in the browser
profile for the secondary account, enter the code, and return to the terminal.
Device-code login may first need to be enabled in ChatGPT security settings.
To use the original browser-opening flow instead, add -CodexBrowserLogin.

Each account is queried in a separate short-lived engine process. The script
does not copy tokens or change your primary Codex and Claude Code sessions.

Window controls
---------------

Click an account name to rename it. Names, compact-summary mode, and the
always-on-top preference are stored in your local CodexBar settings; OAuth
credentials remain in the provider-owned directories.

Each account has a Copy login button. It copies a PowerShell block for that
specific profile without copying any credentials: Codex uses device-code login
and Claude Code uses its auth-login command.

The Aa button opens appearance settings. Use A-/A+ to scale the entire
interface from 80% to 160%; 100% restores the default. The selected scale is
saved automatically, which is useful on high-resolution and 4K monitors.

The Summary button switches to a landscape one-line view. The diamond button
toggles always-on-top. Minimize sends the window to the taskbar; close hides
it to the notification area without exiting CodexBar. Losing focus does not hide or reposition the
window; the last position selected by dragging the header is restored later.
The card layout is condensed so the normal four-account setup requires no
vertical scrollbar. Reset timing is shown as a live countdown, prioritizing
the short session in Summary view, with the full localized reset date beneath
each quota.
