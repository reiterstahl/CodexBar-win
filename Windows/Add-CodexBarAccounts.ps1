#Requires -Version 5.1

[CmdletBinding()]
param(
    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$")]
    [string] $ProfileName = "2",
    [switch] $Login,
    [switch] $CodexBrowserLogin
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    throw "The Windows user profile directory could not be resolved."
}

$codexDirectory = Join-Path $env:USERPROFILE ".codex-$ProfileName"
$claudeDirectory = Join-Path $env:USERPROFILE ".claude-$ProfileName"
New-Item -ItemType Directory -Path $codexDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $claudeDirectory -Force | Out-Null

Write-Host "Isolated account directories:"
Write-Host "  Codex:       $codexDirectory"
Write-Host "  Claude Code: $claudeDirectory"

if (-not $Login) {
    Write-Host ""
    Write-Host "Run this script again with -Login to authenticate both secondary accounts."
    exit 0
}

$codexCommand = Get-Command "codex" -ErrorAction Stop
$claudeCommand = Get-Command "claude" -ErrorAction Stop
$previousCodexHome = $env:CODEX_HOME
$previousClaudeConfigDirectory = $env:CLAUDE_CONFIG_DIR

try {
    Write-Host ""
    Write-Host "Signing in to the secondary Codex account..."
    $env:CODEX_HOME = $codexDirectory
    if ($CodexBrowserLogin) {
        Write-Host "Codex will open the standard sign-in flow in your default browser."
        & $codexCommand.Source login
    }
    else {
        Write-Host "Codex will print a URL and one-time code; it will not open a browser."
        Write-Host "Open that URL in the browser profile for this account, enter the code,"
        Write-Host "then return here and wait for confirmation."
        & $codexCommand.Source login --device-auth
    }
    if ($LASTEXITCODE -ne 0) {
        if (-not $CodexBrowserLogin) {
            throw @"
Codex device login exited with code $LASTEXITCODE.
Make sure device code login is enabled in your ChatGPT security settings and
that your Codex CLI supports 'codex login --device-auth'. To use the standard
browser flow instead, rerun this script with -Login -CodexBrowserLogin.
"@
        }
        throw "Codex login exited with code $LASTEXITCODE."
    }

    Write-Host ""
    Write-Host "Signing in to the secondary Claude Code account..."
    Write-Host "Use /login if Claude Code does not open authentication automatically."
    Write-Host "Use /exit after authentication finishes."
    $env:CLAUDE_CONFIG_DIR = $claudeDirectory
    & $claudeCommand.Source
    if ($LASTEXITCODE -ne 0) {
        throw "Claude Code exited with code $LASTEXITCODE."
    }
}
finally {
    if ($null -eq $previousCodexHome) {
        Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue
    }
    else {
        $env:CODEX_HOME = $previousCodexHome
    }

    if ($null -eq $previousClaudeConfigDirectory) {
        Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction SilentlyContinue
    }
    else {
        $env:CLAUDE_CONFIG_DIR = $previousClaudeConfigDirectory
    }
}

$codexCredentialPath = Join-Path $codexDirectory "auth.json"
$claudeCredentialPath = Join-Path $claudeDirectory ".credentials.json"
Write-Host ""
Write-Host "Secondary account status:"
Write-Host "  Codex credentials:       $(Test-Path -LiteralPath $codexCredentialPath -PathType Leaf)"
Write-Host "  Claude Code credentials: $(Test-Path -LiteralPath $claudeCredentialPath -PathType Leaf)"
Write-Host ""
Write-Host "Restart CodexBar or select Refresh to discover the additional accounts."
