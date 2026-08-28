#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $ArchivePath,
    [switch] $RunTests,
    [switch] $NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Update-CodexBar.ps1 must run on Windows."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "Publish-Windows.ps1"
foreach ($requiredPath in @($publishScript, (Join-Path $repositoryRoot ".git"))) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "The CodexBar repository is incomplete: $requiredPath"
    }
}

if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $desktopDirectory = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::Desktop)
    if ([string]::IsNullOrWhiteSpace($desktopDirectory)) {
        $desktopDirectory = Join-Path $env:USERPROFILE "Desktop"
    }
    $ArchivePath = Join-Path $desktopDirectory "CodexBar-Windows.zip"
}
$ArchivePath = [System.IO.Path]::GetFullPath($ArchivePath)

$gitCommand = Get-Command "git.exe" -ErrorAction Stop

function Invoke-CheckedNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Command,
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$Command' exited with code $LASTEXITCODE."
    }
}

Write-Host "[1/4] Checking and updating main..."
$currentBranchOutput = @(Invoke-CheckedNativeCommand -Command $gitCommand.Source `
    -Arguments @("-C", $repositoryRoot, "rev-parse", "--abbrev-ref", "HEAD"))
$currentBranch = ($currentBranchOutput | Select-Object -Last 1).Trim()
if ($currentBranch -ne "main") {
    throw "Switch to the main branch before updating. Current branch: $currentBranch"
}

$workingTreeChanges = @(Invoke-CheckedNativeCommand -Command $gitCommand.Source `
    -Arguments @(
        "-C", $repositoryRoot,
        "status", "--porcelain", "--untracked-files=no"
    ))
if ($workingTreeChanges.Count -gt 0) {
    throw @"
The repository has local changes. Commit or stash them before updating:
$($workingTreeChanges -join [Environment]::NewLine)
"@
}

Invoke-CheckedNativeCommand -Command $gitCommand.Source `
    -Arguments @("-C", $repositoryRoot, "pull", "--ff-only", "origin", "main")

Write-Host ""
Write-Host "[2/4] Building the Windows package..."
$publishParameters = @{
    ArchiveOnly = $true
    ArchivePath = $ArchivePath
    SkipTests = -not $RunTests
}
& $publishScript @publishParameters

if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
    throw "The Windows archive was not created: $ArchivePath"
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) `
    ("CodexBar-Update-" + [guid]::NewGuid().ToString("N"))
try {
    Write-Host ""
    Write-Host "[3/4] Extracting the package temporarily..."
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    Expand-Archive -LiteralPath $ArchivePath `
        -DestinationPath $temporaryDirectory -Force

    $installerScript = Join-Path $temporaryDirectory "Install-CodexBar.ps1"
    if (-not (Test-Path -LiteralPath $installerScript -PathType Leaf)) {
        throw "The generated archive does not contain Install-CodexBar.ps1."
    }

    Write-Host ""
    Write-Host "[4/4] Installing CodexBar..."
    $installerParameters = @{
        DesktopShortcut = $true
        StartWithWindows = $true
        NoLaunch = [bool]$NoLaunch
    }
    & $installerScript @installerParameters
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force `
            -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "CodexBar update completed successfully."
Write-Host "Archive: $ArchivePath"
