#Requires -Version 5.1

<#
.SYNOPSIS
Builds the Velopack installer (Setup.exe), portable ZIP and update packages for a release.

.DESCRIPTION
Runs Publish-Windows.ps1 to assemble the self-contained application folder, then packs it with
the Velopack CLI (vpk). Install vpk first, matching the Velopack package version used by the
tray project:

  dotnet tool install -g vpk --version 1.2.161

To publish delta updates, download the previous release into ReleaseDirectory before packing
(the release workflow does this with `vpk download github`).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [string] $ReleaseDirectory,
    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Package-Release.ps1 must run on Windows."
}

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Version must be semantic (for example 1.2.0 or 1.3.0-beta.1): $Version"
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot "artifacts\windows"
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $ReleaseDirectory = Join-Path $artifactRoot "releases"
}
$ReleaseDirectory = [System.IO.Path]::GetFullPath($ReleaseDirectory)
$appDirectory = Join-Path $artifactRoot "CodexBar-win-x64"
$iconPath = Join-Path $PSScriptRoot "CodexBar.Windows.Tray\Assets\CodexBar.ico"

$vpk = Get-Command "vpk" -ErrorAction SilentlyContinue
if ($null -eq $vpk) {
    throw "The Velopack CLI was not found. Install it with: dotnet tool install -g vpk --version 1.2.161"
}

Write-Host "[1/2] Building CodexBar $Version..."
& (Join-Path $PSScriptRoot "Publish-Windows.ps1") `
    -OutputDirectory $appDirectory `
    -Version $Version `
    -SkipTests:$SkipTests

Write-Host ""
Write-Host "[2/2] Creating the installer and update packages..."
New-Item -ItemType Directory -Path $ReleaseDirectory -Force | Out-Null

# The ZIP-only helpers stay out of the installed application. The Swift runtime needs the
# Visual C++ 2022 redistributable, which Setup.exe installs when it is missing.
$excludePattern = '(?i)(\.ps1|SHA256SUMS\.txt|PackageReadme\.txt|INSTALLATION\.md)$'
& $vpk.Source pack `
    --packId "CodexBarWindows" `
    --packVersion $Version `
    --packDir $appDirectory `
    --mainExe "CodexBar.Windows.Tray.exe" `
    --packTitle "CodexBar" `
    --packAuthors "CodexBar for Windows contributors" `
    --icon $iconPath `
    --framework "vcredist143-x64" `
    --exclude $excludePattern `
    --outputDir $ReleaseDirectory
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack exited with code $LASTEXITCODE."
}

Write-Host ""
Write-Host "Release packages are ready in $ReleaseDirectory"
Get-ChildItem -LiteralPath $ReleaseDirectory -File |
    Sort-Object -Property Name |
    ForEach-Object { Write-Host "  $($_.Name)" }
