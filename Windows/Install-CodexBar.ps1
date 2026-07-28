#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $InstallDirectory,
    [switch] $StartWithWindows,
    [switch] $DesktopShortcut,
    [switch] $NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Install-CodexBar.ps1 must run on Windows."
}
if (-not [Environment]::Is64BitOperatingSystem) {
    throw "CodexBar requires 64-bit Windows."
}
if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    throw "The local application-data directory could not be resolved."
}

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = Join-Path $env:LOCALAPPDATA "Programs\CodexBar"
}
$InstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$programInstallRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "Programs"))
$trimCharacters = [char[]]@(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar
)
$programInstallPrefix = $programInstallRoot.TrimEnd($trimCharacters) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $InstallDirectory.StartsWith(
        $programInstallPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "InstallDirectory must be a folder under '$programInstallRoot'."
}

$sourceDirectory = [System.IO.Path]::GetFullPath($PSScriptRoot)
$verifyScript = Join-Path $sourceDirectory "Test-CodexBar.ps1"
$sourceExecutable = Join-Path $sourceDirectory "CodexBar.Windows.Tray.exe"
foreach ($requiredPath in @($verifyScript, $sourceExecutable)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "The installer package is incomplete: $(Split-Path -Leaf $requiredPath)"
    }
}

Write-Host "Verifying the CodexBar package..."
& $verifyScript -PackageDirectory $sourceDirectory -Quiet

$processes = @(Get-Process -Name "CodexBar.Windows.Tray" -ErrorAction SilentlyContinue)
if ($processes.Count -gt 0) {
    Write-Host "Closing the existing CodexBar process..."
    $processes | Stop-Process -Force
    $processes | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

$sameDirectory = $sourceDirectory.Equals(
    $InstallDirectory,
    [StringComparison]::OrdinalIgnoreCase)
if (-not $sameDirectory) {
    $installParent = Split-Path -Parent $InstallDirectory
    New-Item -ItemType Directory -Path $installParent -Force | Out-Null

    $stagingDirectory = "$InstallDirectory.installing-$PID"
    $backupDirectory = "$InstallDirectory.previous-$PID"
    foreach ($temporaryDirectory in @($stagingDirectory, $backupDirectory)) {
        if (Test-Path -LiteralPath $temporaryDirectory) {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
        }
    }

    try {
        New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
        Copy-Item -Path (Join-Path $sourceDirectory "*") `
            -Destination $stagingDirectory -Recurse -Force

        if (Test-Path -LiteralPath $InstallDirectory) {
            Move-Item -LiteralPath $InstallDirectory -Destination $backupDirectory
        }
        Move-Item -LiteralPath $stagingDirectory -Destination $InstallDirectory
        if (Test-Path -LiteralPath $backupDirectory) {
            Remove-Item -LiteralPath $backupDirectory -Recurse -Force `
                -ErrorAction SilentlyContinue
        }
    }
    catch {
        if (-not (Test-Path -LiteralPath $InstallDirectory) -and
            (Test-Path -LiteralPath $backupDirectory)) {
            Move-Item -LiteralPath $backupDirectory -Destination $InstallDirectory
        }
        throw
    }
    finally {
        if (Test-Path -LiteralPath $stagingDirectory) {
            Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
        }
    }
}

function New-CodexBarShortcut {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ShortcutPath,
        [Parameter(Mandatory = $true)]
        [string] $TargetPath,
        [string] $Arguments = "",
        [string] $WorkingDirectory = $InstallDirectory
    )

    $shortcutParent = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Path $shortcutParent -Force | Out-Null
    $shell = $null
    $shortcut = $null
    try {
        $shell = New-Object -ComObject "WScript.Shell"
        $shortcut = $shell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $TargetPath
        $shortcut.Arguments = $Arguments
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.IconLocation = "$(Join-Path $InstallDirectory 'CodexBar.Windows.Tray.exe'),0"
        $shortcut.Description = "CodexBar usage monitor"
        $shortcut.Save()
    }
    finally {
        if ($null -ne $shortcut) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
        }
        if ($null -ne $shell) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
        }
    }
}

$installedExecutable = Join-Path $InstallDirectory "CodexBar.Windows.Tray.exe"
$programsDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Programs)
$startMenuDirectory = Join-Path $programsDirectory "CodexBar"
New-CodexBarShortcut `
    -ShortcutPath (Join-Path $startMenuDirectory "CodexBar.lnk") `
    -TargetPath $installedExecutable

$powershellPath = Join-Path $env:SystemRoot `
    "System32\WindowsPowerShell\v1.0\powershell.exe"
$uninstallPath = Join-Path $InstallDirectory "Uninstall-CodexBar.ps1"
$uninstallArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$uninstallPath`""
New-CodexBarShortcut `
    -ShortcutPath (Join-Path $startMenuDirectory "Uninstall CodexBar.lnk") `
    -TargetPath $powershellPath `
    -Arguments $uninstallArguments

$startupShortcut = Join-Path (
    [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
) "CodexBar.lnk"
if ($StartWithWindows) {
    New-CodexBarShortcut -ShortcutPath $startupShortcut -TargetPath $installedExecutable
}
elseif (Test-Path -LiteralPath $startupShortcut) {
    Remove-Item -LiteralPath $startupShortcut -Force
}

$desktopShortcutPath = Join-Path (
    [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
) "CodexBar.lnk"
if ($DesktopShortcut) {
    New-CodexBarShortcut -ShortcutPath $desktopShortcutPath -TargetPath $installedExecutable
}
elseif (Test-Path -LiteralPath $desktopShortcutPath) {
    Remove-Item -LiteralPath $desktopShortcutPath -Force
}

Write-Host ""
Write-Host "CodexBar installed successfully:"
Write-Host "  $InstallDirectory"
Write-Host "  Start menu: CodexBar"
Write-Host "  Starts with Windows: $([bool]$StartWithWindows)"
Write-Host "  Desktop shortcut: $([bool]$DesktopShortcut)"

if (-not $NoLaunch) {
    Start-Process -FilePath $installedExecutable -WorkingDirectory $InstallDirectory
    Write-Host "CodexBar is now running in the notification area."
}
