Write-Host "Locating solution or project to build..."
[string] $buildTarget = [string]::Empty
[string] $repoRoot = (Join-Path -Path $PSScriptRoot -ChildPath '..\..' -Resolve)
Write-Host "Repository root: $repoRoot"

# Prefer a solution at repository root
[System.IO.FileInfo] $sln = Get-ChildItem -Path $repoRoot -Filter '*.sln' -File -Depth 0 -ErrorAction SilentlyContinue |
    Select-Object -First 1

if ($null -ne $sln) {
    $buildTarget = $sln.FullName
    Write-Host "Found solution: $buildTarget"
}
else {
    # Prefer the known project file anywhere in the repo
    [System.IO.FileInfo] $proj = Get-ChildItem -Path $repoRoot -Filter 'MermaidPad.csproj' -File -Recurse -Depth 6 -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($null -ne $proj) {
        $buildTarget = $proj.FullName
        Write-Host "Found project: $buildTarget"
    }
    else {
        # Fallback to any .csproj in the repo
        [System.IO.FileInfo] $anyProj = Get-ChildItem -Path $repoRoot -Filter '*.csproj' -File -Recurse -Depth 6 -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($null -ne $anyProj) {
            $buildTarget = $anyProj.FullName
            Write-Host "Using discovered project: $buildTarget"
        }
        else {
            Write-Error "No .sln or .csproj found. Set the path explicitly or ensure project files are checked out."
            exit 1
        }
    }
}

Write-Host "Restoring packages..."
dotnet restore "$buildTarget"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building (Release)..."
dotnet build "$buildTarget" --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
