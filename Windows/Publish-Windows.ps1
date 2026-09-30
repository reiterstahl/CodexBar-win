#Requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateSet("Release")]
    [string] $Configuration = "Release",
    [ValidateSet("win-x64")]
    [string] $RuntimeIdentifier = "win-x64",
    [string] $OutputDirectory,
    [string] $ArchivePath,
    [string] $Version,
    [switch] $SkipTests,
    [switch] $ArchiveOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot "artifacts\windows"
$packageName = "CodexBar-$RuntimeIdentifier"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactRoot $packageName
}
if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $ArchivePath = Join-Path $artifactRoot "$packageName.zip"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$ArchivePath = [System.IO.Path]::GetFullPath($ArchivePath)
if ($ArchiveOnly) {
    $trimCharacters = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $outputPrefix = $OutputDirectory.TrimEnd($trimCharacters) +
        [System.IO.Path]::DirectorySeparatorChar
    if ($ArchivePath.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "ArchivePath cannot be inside OutputDirectory when ArchiveOnly is used."
    }
}

function Invoke-CheckedCommand {
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

function Add-RuntimeCandidates {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]] $Candidates,
        [Parameter(Mandatory = $true)]
        [string] $RuntimeRoot
    )

    if (-not (Test-Path -LiteralPath $RuntimeRoot -PathType Container)) {
        return
    }

    Get-ChildItem -LiteralPath $RuntimeRoot -Directory |
        Sort-Object -Property LastWriteTimeUtc -Descending |
        ForEach-Object {
            $Candidates.Add((Join-Path $_.FullName "usr\bin"))
        }
}

function Find-SwiftExecutable {
    $command = Get-Command "swift.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return [System.IO.Path]::GetFullPath($command.Source)
    }

    $toolchainRoots = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $toolchainRoots.Add((Join-Path $env:LOCALAPPDATA "Programs\Swift\Toolchains"))
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $toolchainRoots.Add((Join-Path $env:ProgramFiles "Swift\Toolchains"))
    }

    foreach ($toolchainRoot in $toolchainRoots) {
        if (-not (Test-Path -LiteralPath $toolchainRoot -PathType Container)) {
            continue
        }

        $candidate = Get-ChildItem -LiteralPath $toolchainRoot -Directory |
            Sort-Object -Property LastWriteTimeUtc -Descending |
            ForEach-Object { Join-Path $_.FullName "usr\bin\swift.exe" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw @"
Swift could not be found. Install it with:
  winget install --id Swift.Toolchain -e --source winget
Then open a new PowerShell window and retry.
"@
}

function Find-SwiftRuntimeDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SwiftExecutable,
        [string] $SwiftVersion
    )

    $candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($env:SWIFT_RUNTIME_BIN)) {
        $candidates.Add($env:SWIFT_RUNTIME_BIN)
    }

    $swiftDirectory = Split-Path -Parent $SwiftExecutable
    $cursor = Get-Item -LiteralPath $swiftDirectory
    while ($null -ne $cursor) {
        Add-RuntimeCandidates -Candidates $candidates -RuntimeRoot (Join-Path $cursor.FullName "Runtimes")
        $cursor = $cursor.Parent
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        Add-RuntimeCandidates -Candidates $candidates `
            -RuntimeRoot (Join-Path $env:LOCALAPPDATA "Programs\Swift\Runtimes")
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        Add-RuntimeCandidates -Candidates $candidates `
            -RuntimeRoot (Join-Path $env:ProgramFiles "Swift\Runtimes")
    }

    $candidates.Add($swiftDirectory)
    $preferredCandidates = @()
    $fallbackCandidates = @()
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        $versionDirectory = Split-Path -Parent (Split-Path -Parent $candidate)
        $candidateVersion = Split-Path -Leaf $versionDirectory
        if (-not [string]::IsNullOrWhiteSpace($SwiftVersion) -and
            $candidateVersion -like "$SwiftVersion*") {
            $preferredCandidates += $candidate
        }
        else {
            $fallbackCandidates += $candidate
        }
    }

    foreach ($candidate in @($preferredCandidates + $fallbackCandidates)) {
        if (-not (Test-Path -LiteralPath (Join-Path $candidate "swiftCore.dll") -PathType Leaf)) {
            continue
        }
        if ($null -eq (Get-ChildItem -LiteralPath $candidate -Filter "Foundation*.dll" -File |
                Select-Object -First 1)) {
            continue
        }
        return [System.IO.Path]::GetFullPath($candidate)
    }

    throw @"
The Swift runtime directory could not be found. Set SWIFT_RUNTIME_BIN to the
directory containing swiftCore.dll and the Foundation DLLs, then retry.
"@
}

if ($env:OS -ne "Windows_NT") {
    throw "Publish-Windows.ps1 must run on Windows."
}
if (-not [Environment]::Is64BitOperatingSystem) {
    throw "The win-x64 package requires 64-bit Windows."
}

$swiftExecutable = Find-SwiftExecutable
$swiftRuntimeDirectory = Find-SwiftRuntimeDirectory -SwiftExecutable $swiftExecutable
$swiftBinDirectory = Split-Path -Parent $swiftExecutable
$env:Path = "$swiftBinDirectory;$swiftRuntimeDirectory;$env:Path"
Get-Command "dotnet.exe" -ErrorAction Stop | Out-Null
$swiftVersionOutput = @(& $swiftExecutable --version)
if ($LASTEXITCODE -ne 0 -or $swiftVersionOutput.Count -eq 0) {
    throw "The installed Swift toolchain did not report its version."
}
$swiftVersionLine = $swiftVersionOutput[0].Trim()
$swiftVersion = ""
if ($swiftVersionLine -match "Swift version ([0-9]+(?:\.[0-9]+){1,2})") {
    $swiftVersion = $Matches[1]
}

Push-Location $repositoryRoot
try {
    if (-not $SkipTests) {
        Invoke-CheckedCommand -Command $swiftExecutable -Arguments @("test", "--filter", "Portable")
        Invoke-CheckedCommand -Command "dotnet.exe" -Arguments @(
            "run",
            "--project", "Windows\CodexBar.EngineClient.Tests\CodexBar.EngineClient.Tests.csproj",
            "--configuration", $Configuration
        )
    }

    Invoke-CheckedCommand -Command $swiftExecutable -Arguments @(
        "build", "-c", $Configuration.ToLowerInvariant(), "--product", "CodexBarWindowsEngine"
    )
    $releaseConfiguration = $Configuration.ToLowerInvariant()
    $engineDirectoryOutput = & $swiftExecutable build -c $releaseConfiguration `
        --product CodexBarWindowsEngine --show-bin-path
    if ($LASTEXITCODE -ne 0) {
        throw "'swift build --show-bin-path' exited with code $LASTEXITCODE."
    }
    $engineDirectory = ($engineDirectoryOutput | Select-Object -Last 1).Trim()
    $enginePath = Join-Path $engineDirectory "CodexBarWindowsEngine.exe"
    if (-not (Test-Path -LiteralPath $enginePath -PathType Leaf)) {
        throw "The Windows engine executable was not produced."
    }

    $publishDirectory = Join-Path $artifactRoot ".publish-$RuntimeIdentifier"
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    $publishArguments = @(
        "publish",
        "Windows\CodexBar.Windows.Tray\CodexBar.Windows.Tray.csproj",
        "--configuration", $Configuration,
        "--runtime", $RuntimeIdentifier,
        "--self-contained", "true",
        "--output", $publishDirectory,
        "-p:PublishProfile=win-x64",
        "-p:PublishTrimmed=false"
    )
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $publishArguments += "-p:Version=$Version"
    }
    Invoke-CheckedCommand -Command "dotnet.exe" -Arguments $publishArguments

    if (Test-Path -LiteralPath $OutputDirectory) {
        Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $OutputDirectory -Recurse -Force
    Copy-Item -LiteralPath $enginePath -Destination $OutputDirectory -Force

    $runtimeDirectory = Find-SwiftRuntimeDirectory -SwiftExecutable $swiftExecutable `
        -SwiftVersion $swiftVersion
    $excludedMicrosoftRuntimePatterns = @(
        "api-ms-win-*.dll",
        "concrt*.dll",
        "msvcp*.dll",
        "ucrtbase.dll",
        "vcruntime*.dll"
    )
    $runtimeDlls = @(Get-ChildItem -LiteralPath $runtimeDirectory -Filter "*.dll" -File |
        Where-Object {
            $fileName = $_.Name
            -not ($excludedMicrosoftRuntimePatterns | Where-Object { $fileName -like $_ })
        })
    if ($runtimeDlls.Count -eq 0) {
        throw "No distributable Swift runtime DLLs were found in '$runtimeDirectory'."
    }
    $runtimeDlls | Copy-Item -Destination $OutputDirectory -Force

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Test-WindowsPackage.ps1") `
        -Destination (Join-Path $OutputDirectory "Test-CodexBar.ps1") -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Add-CodexBarAccounts.ps1") `
        -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Install-CodexBar.ps1") `
        -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Uninstall-CodexBar.ps1") `
        -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "PackageReadme.txt") `
        -Destination (Join-Path $OutputDirectory "README.txt") -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\windows-installation.es.md") `
        -Destination (Join-Path $OutputDirectory "INSTALLATION.md") -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "THIRD-PARTY-NOTICES.md") `
        -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") `
        -Destination (Join-Path $OutputDirectory "LICENSE.txt") -Force

    $engineVersion = (& $enginePath --version | Select-Object -Last 1).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "The built engine failed its version smoke test."
    }
    $buildInformation = [ordered]@{
        package = $packageName
        generatedAt = [DateTime]::UtcNow.ToString("o")
        runtimeIdentifier = $RuntimeIdentifier
        dotnetSdk = (& dotnet.exe --version | Select-Object -Last 1).Trim()
        swift = $swiftVersionLine
        engine = $engineVersion
    }
    $buildInformation | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $OutputDirectory "build-info.json") -Encoding UTF8

    $originalPath = $env:Path
    try {
        $env:Path = "$env:SystemRoot\System32;$env:SystemRoot"
        Invoke-CheckedCommand -Command (Join-Path $OutputDirectory "CodexBarWindowsEngine.exe") `
            -Arguments @("--help")
        Invoke-CheckedCommand -Command (Join-Path $OutputDirectory "CodexBarWindowsEngine.exe") `
            -Arguments @("--version")
    }
    finally {
        $env:Path = $originalPath
    }

    $checksumPath = Join-Path $OutputDirectory "SHA256SUMS.txt"
    Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse |
        Where-Object { $_.FullName -ne $checksumPath } |
        Sort-Object -Property FullName |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($OutputDirectory.Length).TrimStart("\")
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relativePath"
        } |
        Set-Content -LiteralPath $checksumPath -Encoding ASCII

    $archiveDirectory = Split-Path -Parent $ArchivePath
    New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $ArchivePath) {
        Remove-Item -LiteralPath $ArchivePath -Force
    }
    Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $ArchivePath `
        -CompressionLevel Optimal

    $archiveHash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$ArchivePath.sha256" `
        -Value "$archiveHash  $(Split-Path -Leaf $ArchivePath)" -Encoding ASCII

    if ($ArchiveOnly) {
        Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    Write-Host ""
    Write-Host "Windows package created:"
    Write-Host "  $ArchivePath"
    Write-Host "  SHA-256: $archiveHash"
}
finally {
    Pop-Location
}
