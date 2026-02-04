function Get-FilesWithBom {
<#
.SYNOPSIS
Lists files that start with a Unicode byte order mark (BOM).

.DESCRIPTION
Recursively scans files under a root folder and returns objects for files that
start with a known BOM (UTF-8 / UTF-16 LE+BE / UTF-32 LE+BE). [web:120]

Certain directories are intentionally excluded from scanning, along with all
their subfolders and files, to avoid build outputs, IDE metadata, and dependency
trees (for example: .git, bin, obj, node_modules).

.PARAMETER Root
Root directory to scan. Defaults to current directory.

.PARAMETER Include
One or more wildcard patterns passed to Get-ChildItem -Include (for example: *.cs, *.md). [web:119]

.EXAMPLE
Get-FilesWithBom -Root . -Include @("*.cs","*.csproj","*.html","*.*js","*.sln","*.md","*.yml") | Sort-Object Bom, Path

Lists BOM-bearing files in typical .NET source formats.

.EXAMPLE
# Find any BOM files anywhere
Get-FilesWithBom -Root . -Include @("*") | Format-Table -AutoSize

Finds BOMs across the entire repo (can be slower on large repos).
#>
    param(
        [string] $Root = ".",
        [string[]] $Include = @(
            "*.axaml",
            "*.cs",
            "*.csproj",
            "*.*config",
            "*.*settings",
            "*.gitignore",
            "*.html",
            "*.ic*",
            "*.*js*",
            "*.manifest",
            "*.*md",
            "*.plist",
            "*.png",
            "*.props",
            "*.ps1",
            "*.sln*",
            "*.targets",
            "*.txt",
            "*.yaml",
            "*.yml") #@("*")
    )

    $excludedDirectories = @(
        '.git',
        '.vs',
        '.vscode',
        'bin',
        'obj',
        'node_modules'
    )

    $boms = @(
        @{ Name = "UTF-8 BOM";     Bytes = [byte[]](0xEF,0xBB,0xBF) },
        @{ Name = "UTF-16 LE BOM"; Bytes = [byte[]](0xFF,0xFE) },
        @{ Name = "UTF-16 BE BOM"; Bytes = [byte[]](0xFE,0xFF) },
        @{ Name = "UTF-32 LE BOM"; Bytes = [byte[]](0xFF,0xFE,0x00,0x00) },
        @{ Name = "UTF-32 BE BOM"; Bytes = [byte[]](0x00,0x00,0xFE,0xFF) }
    )

    foreach ($pattern in $Include) {
        Get-ChildItem -Path $Root -Recurse -File -Include $pattern -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $fullPath = $_.FullName
            -not ($excludedDirectories | Where-Object {
                $fullPath -match "(^|[\\/])$([regex]::Escape($_))([\\/]|$)"
            })
        } |
        ForEach-Object {
            $path = $_.FullName

            $fs = [System.IO.File]::OpenRead($path)
            try {
                $head = New-Object byte[] 4
                $n = $fs.Read($head, 0, 4)
            }
            finally { $fs.Dispose() }

            foreach ($bom in $boms) {
                $sig = $bom.Bytes
                if ($n -ge $sig.Length) {
                    $match = $true
                    for ($i = 0; $i -lt $sig.Length; $i++) {
                        if ($head[$i] -ne $sig[$i]) { $match = $false; break }
                    }
                    if ($match) {
                        [pscustomobject]@{ Path = $path; Bom = $bom.Name }
                        break
                    }
                }
            }
        }
    }
}

function Remove-Utf8BomRecursively {
<#
.SYNOPSIS
Removes UTF-8 BOMs from files under a directory (recursively).

.DESCRIPTION
Finds files that begin with the UTF-8 BOM byte sequence (EF BB BF) and rewrites
each file with those first 3 bytes removed, leaving the rest of the file intact. [web:120]

This function intentionally removes only UTF-8 BOMs; UTF-16/UTF-32 BOM files are
reported by Get-FilesWithBom but not modified. [web:120]

Directory exclusions applied by Get-FilesWithBom (for example: .git, bin, obj,
node_modules) also apply here, since this function operates on its results.

The function supports -WhatIf (dry run) and -Confirm via SupportsShouldProcess. [web:99]

.PARAMETER Root
Root directory to scan. Defaults to current directory.

.PARAMETER Include
One or more wildcard patterns to search (examples: *.cs, *.csproj, *.md).

.OUTPUTS
Writes a table of targeted UTF-8 BOM files to the host, and performs edits unless
-WhatIf is specified.

.EXAMPLE
# Dry run: list what would change (no writes)
Remove-Utf8BomRecursively -Root . -Include @("*.cs","*.csproj","*.html","*.*js","*.sln","*.md","*.yml") -WhatIf

Shows the files that would be rewritten and prints "What if:" messages. [web:99]

.EXAMPLE
# Real run: remove UTF-8 BOMs
Remove-Utf8BomRecursively -Root . -Include @("*.cs","*.csproj","*.html","*.*js","*.sln","*.md","*.yml")

Afterwards, review changes with git diff and commit the normalization.

.EXAMPLE
# Get full help (after dot-sourcing or importing the script/module)
Get-Help Remove-Utf8BomRecursively -Full

Displays this documentation in the standard Get-Help format
#>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string] $Root = ".",
        [string[]] $Include = @(
            "*.axaml",
            "*.cs",
            "*.csproj",
            "*.*config",
            "*.*settings",
            "*.gitignore",
            "*.html",
            "*.ic*",
            "*.*js*",
            "*.manifest",
            "*.*md",
            "*.plist",
            "*.png",
            "*.props",
            "*.ps1",
            "*.sln*",
            "*.targets",
            "*.txt",
            "*.yaml",
            "*.yml") #@("*")
    )

    $utf8BomFiles =
        Get-FilesWithBom -Root $Root -Include $Include |
        Where-Object { $_.Bom -eq "UTF-8 BOM" } |
        Sort-Object Path

    # Always show what would be targeted
    $utf8BomFiles | Format-Table -AutoSize

    foreach ($item in $utf8BomFiles) {
        $path = $item.Path

        if ($PSCmdlet.ShouldProcess($path, "Remove UTF-8 BOM (strip first 3 bytes)")) {
            $bytes = [System.IO.File]::ReadAllBytes($path)
            if ($bytes.Length -ge 3) {
                [System.IO.File]::WriteAllBytes($path, $bytes[3..($bytes.Length-1)])
            }
        }
    }
}
