#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $PackageDirectory = $PSScriptRoot,
    [switch] $Launch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$PackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
$checksumPath = Join-Path $PackageDirectory "SHA256SUMS.txt"
$enginePath = Join-Path $PackageDirectory "CodexBarWindowsEngine.exe"
$trayPath = Join-Path $PackageDirectory "CodexBar.Windows.Tray.exe"

foreach ($requiredPath in @($checksumPath, $enginePath, $trayPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required package file is missing: $(Split-Path -Leaf $requiredPath)"
    }
}

$verifiedCount = 0
$verifiedPaths = @{}
foreach ($line in Get-Content -LiteralPath $checksumPath) {
    if ($line -notmatch "^([0-9a-fA-F]{64})  (.+)$") {
        throw "SHA256SUMS.txt contains an invalid line."
    }

    $expectedHash = $Matches[1].ToLowerInvariant()
    $relativePath = $Matches[2]
    $candidatePath = [System.IO.Path]::GetFullPath((Join-Path $PackageDirectory $relativePath))
    $trimCharacters = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $packagePrefix = $PackageDirectory.TrimEnd($trimCharacters) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidatePath.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA256SUMS.txt contains a path outside the package."
    }
    if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
        throw "Package file is missing: $relativePath"
    }

    $actualHash = (Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Package integrity check failed: $relativePath"
    }
    $verifiedPaths[$candidatePath] = $true
    $verifiedCount += 1
}

foreach ($packagedFile in Get-ChildItem -LiteralPath $PackageDirectory -File -Recurse) {
    if ($packagedFile.FullName -eq $checksumPath) {
        continue
    }
    if (-not $verifiedPaths.ContainsKey($packagedFile.FullName)) {
        throw "Package contains an unlisted file: $($packagedFile.Name)"
    }
}

$engineVersion = (& $enginePath --version | Select-Object -Last 1).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "The packaged usage engine failed to start."
}

Write-Host "Integrity verified for $verifiedCount files."
Write-Host "Engine verified: $engineVersion"

if ($Launch) {
    $process = Start-Process -FilePath $trayPath -WorkingDirectory $PackageDirectory -PassThru
    Start-Sleep -Seconds 3
    if ($process.HasExited) {
        throw "CodexBar closed during startup with exit code $($process.ExitCode)."
    }
    Write-Host "CodexBar is running in the notification area (process $($process.Id))."
}
else {
    Write-Host "Run this script again with -Launch to start the tray application."
}
