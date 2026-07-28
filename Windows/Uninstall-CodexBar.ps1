#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch] $RemoveSettings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Uninstall-CodexBar.ps1 must run on Windows."
}

$installDirectory = [System.IO.Path]::GetFullPath($PSScriptRoot)
$programsDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Programs)
$shortcutPaths = @(
    (Join-Path $programsDirectory "CodexBar"),
    (Join-Path (
        [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
    ) "CodexBar.lnk"),
    (Join-Path (
        [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
    ) "CodexBar.lnk")
)

Get-Process -Name "CodexBar.Windows.Tray" -ErrorAction SilentlyContinue |
    Stop-Process -Force

foreach ($shortcutPath in $shortcutPaths) {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Recurse -Force
    }
}

if ($RemoveSettings -and -not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    $settingsDirectory = Join-Path $env:LOCALAPPDATA "CodexBar"
    if (Test-Path -LiteralPath $settingsDirectory) {
        Remove-Item -LiteralPath $settingsDirectory -Recurse -Force
    }
}

$escapedInstallDirectory = $installDirectory.Replace("'", "''")
$cleanupScript = Join-Path ([System.IO.Path]::GetTempPath()) `
    "Uninstall-CodexBar-$PID.ps1"
$cleanupContents = @"
Start-Sleep -Milliseconds 750
if (Test-Path -LiteralPath '$escapedInstallDirectory') {
    Remove-Item -LiteralPath '$escapedInstallDirectory' -Recurse -Force
}
Remove-Item -LiteralPath `$PSCommandPath -Force
"@
Set-Content -LiteralPath $cleanupScript -Value $cleanupContents -Encoding UTF8

$powershellPath = Join-Path $env:SystemRoot `
    "System32\WindowsPowerShell\v1.0\powershell.exe"
$cleanupArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$cleanupScript`""
Start-Process -FilePath $powershellPath `
    -ArgumentList $cleanupArguments `
    -WindowStyle Hidden | Out-Null

Write-Host "CodexBar was uninstalled."
if ($RemoveSettings) {
    Write-Host "CodexBar presentation settings were removed."
}
else {
    Write-Host "Presentation settings were preserved."
}
Write-Host "Codex and Claude credentials were not changed."
