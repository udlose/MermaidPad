<#
.SYNOPSIS
    Builds and publishes MermaidPad for the current platform.

.DESCRIPTION
    This script builds and publishes MermaidPad for local development and testing.
    It mirrors the CI/CD workflow in .github/workflows/build-and-release.yml to ensure
    local builds produce artifacts identical to CI builds.

    The script:
    1. Detects the current OS and architecture
    2. Restores NuGet packages
    3. Publishes the application (which triggers asset hash generation)
    4. Optionally creates a zip artifact matching CI naming convention

.PARAMETER Version
    The version string to embed in the build (e.g., "1.2.3").
    Defaults to "1.0.0-localdev" for local development builds.

.PARAMETER Configuration
    Build configuration ("Debug" or "Release"). Defaults to "Debug".

.PARAMETER Clean
    If specified, removes previous build artifacts before building.

.PARAMETER SkipZip
    If specified, skips creating the zip artifact (faster for iteration).

.PARAMETER OutputDirectory
    The directory for build artifacts. Defaults to "./artifacts".

.EXAMPLE
    ./build.ps1
    Builds for the current platform with Debug configuration and version "1.0.0-localdev".

.EXAMPLE
    ./build.ps1 -Configuration Release
    Builds for the current platform using Release configuration.

.EXAMPLE
    ./build.ps1 -Version 1.2.3 -Configuration Release -Clean
    Cleans previous artifacts and builds with version 1.2.3 using Release configuration.

.EXAMPLE
    ./build.ps1 -Version 1.2.3 -SkipZip -Verbose
    Builds without creating a zip, with verbose output.

.NOTES
    Author: MermaidPad Contributors
    Repository: https://github.com/udlose/MermaidPad

    Prerequisites:
    - .NET 10.0 SDK or later
    - PowerShell 7.0 or later (recommended) or Windows PowerShell 5.1

    This script is designed for local development and testing.
    For releases, use the GitHub Actions workflow.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$Version = "1.0.0-localdev",

    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$SkipZip,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = "./artifacts"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[bool]$script:IsWindowsOs = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows
)

#region Helper Functions

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Error "✗ $Message" -ErrorAction Continue
}

function Get-RuntimeIdentifier {
    <#
    .SYNOPSIS
        Detects the current platform's Runtime Identifier (RID).
    #>
    [string]$os = ""
    [string]$arch = ""

    # Detect OS (compatible with Windows PowerShell 5.1 and PowerShell 6+)
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        $os = "win"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
        $os = "linux"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
        $os = "osx"
    }
    else {
        throw "Unable to detect operating system. Supported: Windows, Linux, macOS."
    }

    # Detect architecture
    # Note: [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture is more reliable
    [string]$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()

    switch ($osArch) {
        "x64" { $arch = "x64" }
        "arm64" { $arch = "arm64" }
        default {
            throw "Unsupported architecture: $osArch. Supported: x64, arm64."
        }
    }

    # Validate against supported RIDs in Directory.Build.props
    [string]$rid = "$os-$arch"
    [string[]]$supportedRids = @("win-x64", "win-arm64", "linux-x64", "osx-x64", "osx-arm64")

    if ($rid -notin $supportedRids) {
        throw "Unsupported Runtime Identifier: $rid. Supported RIDs: $($supportedRids -join ', ')"
    }

    return $rid
}

function Get-ExecutableExtension {
    param([string]$RuntimeIdentifier)

    if ($RuntimeIdentifier.StartsWith("win")) {
        return ".exe"
    }

    return ""
}

function Test-DotNetSdk {
    <#
    .SYNOPSIS
        Validates that the .NET SDK is installed and meets minimum version requirements.
    #>
    try {
        $dotnetVersion = & dotnet --version 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw ".NET SDK not found"
        }

        Write-Verbose "Detected .NET SDK version: $dotnetVersion"

        # Parse major version - handle preview versions like "10.0.100-preview.1"
        [string]$majorVersionString = $dotnetVersion.Split('.')[0]
        [int]$majorVersion = 0

        if (-not [int]::TryParse($majorVersionString, [ref]$majorVersion)) {
            throw "Unable to parse .NET SDK version from 'dotnet --version' output: '$dotnetVersion'"
        }

        if ($majorVersion -lt 10) {
            throw ".NET 10.0 SDK or later is required. Found: $dotnetVersion"
        }

        Write-Success ".NET SDK $dotnetVersion detected"
    }
    catch {
        Write-ErrorMessage "Failed to detect .NET SDK: $_"
        Write-Host ""
        Write-Host "Please install the .NET 10.0 SDK from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
        throw
    }
}

function Get-GitCommitSha {
    <#
    .SYNOPSIS
        Gets the current Git commit SHA, or a placeholder if not in a Git repository.
    #>
    try {
        [string]$sha = & git rev-parse HEAD 2>&1
        if ($LASTEXITCODE -eq 0 -and $sha.Length -ge 7) {
            return $sha.Trim()
        }
    }
    catch {
        Write-Verbose "Git not available or not in a repository: $_"
    }

    return "local-build"
}

function Validate-BuildPaths {
    <#
    .SYNOPSIS
        Validates that the Version and OutputDirectory parameters do not contain invalid characters or patterns.
    #>
    param(
        [string]$Version,
        [string]$OutputDirectory
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Version cannot be null, empty, or whitespace."
    }
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        throw "OutputDirectory cannot be null, empty, or whitespace."
    }

    [string]$invalidVersionPattern = '[\\/]|:|(\.\.)'
    [string]$invalidOutputDirectoryPattern = '(\.\.)'
    if ($Version -match $invalidVersionPattern) {
        throw "Invalid Version value '$Version'. Version must not contain '/', '\', ':', or '..'."
    }
    if ($OutputDirectory -match $invalidOutputDirectoryPattern) {
        throw "Invalid OutputDirectory value '$OutputDirectory'. OutputDirectory must not contain '..'."
    }
}

function Get-BuildArtifactPaths {
    <#
    .SYNOPSIS
        Computes and validates the paths for build artifacts based on input parameters.
        Ensures that all computed paths are under the specified output directory to prevent accidental file system modifications.
    #>
    param(
        [string]$Version,
        [string]$Configuration,
        [string]$rid,
        [string]$OutputDirectory
    )

    # Validate that configuration and RID are safe to use as path components
    $invalidFileNameChars = [System.IO.Path]::GetInvalidFileNameChars()
    foreach ($item in @(
        @{ Name = 'Configuration'; Value = $Configuration },
        @{ Name = 'rid';           Value = $rid }
    )) {
        $name = $item.Name
        $value = [string]$item.Value

        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "$name cannot be null, empty, or whitespace."
        }

        if ($value -match '[\\/]' -or $value -like '*..*') {
            throw "$name value '$value' contains invalid path segments or separators."
        }

        if ($value.IndexOfAny($invalidFileNameChars) -ne -1) {
            throw "$name value '$value' contains invalid file name characters."
        }
    }
    Validate-BuildPaths -Version $Version -OutputDirectory $OutputDirectory

    [string]$publishDir = Join-Path -Path $OutputDirectory -ChildPath "MermaidPad-$Version-$Configuration-$rid"
    $publishDir = [System.IO.Path]::GetFullPath($publishDir)

    # Normalize and validate that publishDir is under OutputDirectory using an OS-appropriate comparison
    [string]$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
    [char]$directorySeparator = [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedOutputDirectory.EndsWith([string]$directorySeparator)) {
        $resolvedOutputDirectory += $directorySeparator
    }

    if ($script:IsWindowsOs) {
        $comparison = [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        $comparison = [System.StringComparison]::Ordinal
    }

    if (-not $publishDir.StartsWith($resolvedOutputDirectory, $comparison)) {
        throw "Resolved publish directory '$publishDir' is not under the output directory '$resolvedOutputDirectory'."
    }

    [string]$zipName = "MermaidPad-$Version-$Configuration-$rid.zip"
    [string]$zipPath = Join-Path -Path $OutputDirectory -ChildPath $zipName
    $zipPath = [System.IO.Path]::GetFullPath($zipPath)

    # Defense-in-depth: Ensure $zipPath is under $OutputDirectory
    if (-not $zipPath.StartsWith($resolvedOutputDirectory, $comparison)) {
        throw "Resolved zip path '$zipPath' is not under the output directory '$resolvedOutputDirectory'."
    }

    return @{
        PublishDir = $publishDir
        ZipName    = $zipName
        ZipPath    = $zipPath
    }
}

#endregion

#region Main Script

try {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║               MermaidPad Build Script                        ║" -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta

    # Determine script and project locations
    [string]$scriptDir = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptDir)) {
        $scriptDir = Get-Location
    }

    [string]$projectFile = Join-Path -Path $scriptDir -ChildPath "MermaidPad.csproj"
    if (-not (Test-Path -LiteralPath $projectFile)) {
        throw "Project file not found: $projectFile. Ensure this script is in the repository root."
    }

    # Resolve output directory to absolute, canonical path
    if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
        $OutputDirectory = Join-Path -Path $scriptDir -ChildPath $OutputDirectory
    }
    $OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

    # Step 1: Validate prerequisites
    Write-Step "Validating Prerequisites"
    Test-DotNetSdk

    # Step 2: Detect platform
    Write-Step "Detecting Platform"
    [string]$rid = Get-RuntimeIdentifier
    [string]$extension = Get-ExecutableExtension -RuntimeIdentifier $rid
    Write-Success "Runtime Identifier: $rid"

    # Step 3: Gather build metadata
    Write-Step "Gathering Build Metadata"
    [string]$buildDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    [string]$commitSha = Get-GitCommitSha
    Write-Info "Version:        $Version"
    Write-Info "Configuration:  $Configuration"
    Write-Info "Build Date:     $buildDate"
    Write-Info "Commit SHA:     $commitSha"

    # Step 4: Clean if requested
    $artifactPaths = Get-BuildArtifactPaths -Version $Version -Configuration $Configuration -rid $rid -OutputDirectory $OutputDirectory
    [string]$publishDir = $artifactPaths.PublishDir
    [string]$zipName = $artifactPaths.ZipName
    [string]$zipPath = $artifactPaths.ZipPath

    if ($Clean) {
        Write-Step "Cleaning Previous Artifacts"

        if (Test-Path -LiteralPath $publishDir) {
            Remove-Item -LiteralPath $publishDir -Recurse -Force
            Write-Success "Removed: $publishDir"
        }

        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
            Write-Success "Removed: $zipPath"
        }

        # Clean bin/obj directories
        [string]$binDir = Join-Path -Path $scriptDir -ChildPath "bin"
        [string]$objDir = Join-Path -Path $scriptDir -ChildPath "obj"

        if (Test-Path -LiteralPath $binDir) {
            try {
                Remove-Item -LiteralPath $binDir -Recurse -Force -ErrorAction Stop
                Write-Success "Removed: bin/"
            }
            catch {
                Write-ErrorMessage "Failed to remove bin/. A process is likely holding files open. Close MermaidPad, dotnet.exe, or any WebView2/msedgewebview2 processes, then retry. Details: $($_.Exception.Message)"
                throw
            }
        }

        if (Test-Path -LiteralPath $objDir) {
            try {
                Remove-Item -LiteralPath $objDir -Recurse -Force -ErrorAction Stop
                Write-Success "Removed: obj/"
            }
            catch {
                Write-ErrorMessage "Failed to remove obj/. A process is likely holding files open. Close MermaidPad, dotnet.exe, or any WebView2/msedgewebview2 processes, then retry. Details: $($_.Exception.Message)"
                throw
            }
        }
    }

    # Step 5: Restore dependencies
    Write-Step "Restoring Dependencies"
    Write-Verbose "Running: dotnet restore $projectFile"

    & dotnet "restore" $projectFile
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }
    Write-Success "Dependencies restored"

    # Step 6: Publish
    Write-Step "Publishing Application"

    # Ensure output directory exists
    if (-not (Test-Path -LiteralPath $OutputDirectory)) {
        New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
    }

    [string[]]$publishArgs = @(
        "publish"
        $projectFile
        "-c", $Configuration
        "-r", $rid
        "-o", $publishDir
        "-p:Version=$Version"
        "-p:MermaidPadBuildDate=$buildDate"
        "-p:MermaidPadCommitSha=$commitSha"
    )

    Write-Verbose "Running: dotnet $($publishArgs -join ' ')"

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
    Write-Success "Application published to: $publishDir"

    # Step 7: Create zip artifact (unless skipped)
    if (-not $SkipZip) {
        Write-Step "Creating Zip Artifact"

        # Use already validated and computed $zipPath from Get-BuildArtifactPaths
        # No need to re-validate $Version or $OutputDirectory here

        # Remove existing zip if present
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        # Create zip from publish directory contents
        Write-Verbose "Creating zip: $zipPath"
        Compress-Archive -Path (Join-Path -Path $publishDir -ChildPath '*') -DestinationPath $zipPath -Force

        Write-Success "Zip artifact created: $zipPath"
    }
    else {
        Write-Info "Skipping zip creation (-SkipZip specified)"
    }

    # Summary
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                    Build Complete!                           ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Artifacts:" -ForegroundColor White
    Write-Host "    Publish Directory: $publishDir" -ForegroundColor Gray

    if (-not $SkipZip) {
        Write-Host "    Zip Artifact:      $zipPath" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "  To run the application:" -ForegroundColor White

    [string]$executableName = "MermaidPad$extension"
    [string]$executablePath = Join-Path -Path $publishDir -ChildPath $executableName

    if ($rid.StartsWith("win")) {
        Write-Host "    $executablePath" -ForegroundColor Gray
    }
    else {
        if (Test-Path -LiteralPath $executablePath) {
            try {
                $chmodCommand = Get-Command chmod -ErrorAction SilentlyContinue
                if ($null -ne $chmodCommand) {
                    & $chmodCommand.Path +x -- $executablePath
                }
            }
            catch {
                Write-Verbose "Failed to set execute permission on '$executablePath': $_"
            }
        }
        Write-Host "    chmod +x `"$executablePath`"" -ForegroundColor Gray
        Write-Host "    `"$executablePath`"" -ForegroundColor Gray
    }

    Write-Host ""
}
catch {
    Write-Host ""
    Write-Error "Build failed: $_" -ErrorAction Continue
    Write-Host ""

    if ($_.Exception.InnerException) {
        Write-Verbose "Inner exception: $($_.Exception.InnerException.Message)"
    }

    exit 1
}

#endregion
