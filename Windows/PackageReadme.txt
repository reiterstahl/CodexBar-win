CodexBar for Windows 11 — development preview
================================================

This package supports only:

  - OpenAI Codex, using the existing Codex CLI login.
  - Claude Code, using the existing Claude CLI login.

It does not copy, store, or refresh provider credentials. Authenticate with the
official provider CLIs before opening CodexBar.

Quick test
----------

Open PowerShell in this directory and run:

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-CodexBar.ps1 -Launch

The script verifies every packaged file, tests the usage engine without reading
provider credentials, and starts CodexBar in the Windows notification area.

If Windows reports a missing Microsoft Visual C++ runtime, install the latest
supported x64 Visual C++ Redistributable from Microsoft, then retry:

  https://aka.ms/vs/17/release/vc_redist.x64.exe

This development preview is not code-signed yet. Windows SmartScreen may ask you
to confirm that you want to run it.

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

Each account is queried in a separate short-lived engine process. The script
does not copy tokens or change your primary Codex and Claude Code sessions.
