<#
.SYNOPSIS
    Static safety audit for EXTReader source code.
.DESCRIPTION
    Greps all .cs files for write-capable symbols that must NEVER appear in
    source-side (EXT filesystem) code paths. The only allowed FileAccess.Write
    is for destination files in copy operations.
.EXAMPLE
    .\scripts\safety-audit.ps1
#>
[CmdletBinding()]
param(
    [string]$SourcePath = (Resolve-Path "$PSScriptRoot\..\src\EXTReader").Path
)

$ErrorActionPreference = 'Stop'
Write-Host "=== EXTReader Static Safety Audit ===" -ForegroundColor Cyan
Write-Host "Source path: $SourcePath"
Write-Host ""

# Symbols that must NEVER appear in source-side code
$forbiddenPatterns = @(
    'GENERIC_WRITE',
    'EXT2_FLAG_RW',
    'CREATE_ALWAYS',
    'OPEN_ALWAYS',
    'FileMode\.CreateNew',
    'FileMode\.Truncate',
    'FileMode\.Append'
)

# Symbols allowed only for destination files (not source-side EXT access)
$reviewPatterns = @(
    'FileAccess\.Write',
    'FileMode\.Create\b'
)

$exitCode = 0

Write-Host "--- Forbidden symbols (must be ZERO hits) ---" -ForegroundColor Yellow
$forbiddenHits = @()
foreach ($pattern in $forbiddenPatterns)
{
    $matches = Get-ChildItem -Path $SourcePath -Recurse -Include '*.cs' |
        Where-Object { $_.Name -ne 'SafetySelfCheck.cs' -and $_.Name -ne 'Ext2fsConstants.cs' } |
        Select-String -Pattern $pattern -CaseSensitive
    if ($matches)
    {
        $forbiddenHits += $matches
        foreach ($m in $matches)
        {
            Write-Host "  FAIL  $($m.Filename):$($m.LineNumber): $($m.Line.Trim())" -ForegroundColor Red
        }
    }
}

if ($forbiddenHits.Count -eq 0)
{
    Write-Host "  PASS  Zero forbidden symbols found." -ForegroundColor Green
}
else
{
    $exitCode = 1
}

Write-Host ""
Write-Host "--- Review symbols (allowed only for destination files) ---" -ForegroundColor Yellow
foreach ($pattern in $reviewPatterns)
{
    $matches = Get-ChildItem -Path $SourcePath -Recurse -Include '*.cs' |
        Select-String -Pattern $pattern -CaseSensitive
    if ($matches)
    {
        foreach ($m in $matches)
        {
            $line = $m.Line.Trim()
            if ($line -match 'dest|Dest|destination|output|copy|Copy|extract|Extract')
            {
                Write-Host "  OK    $($m.Filename):$($m.LineNumber): $line" -ForegroundColor Green
            }
            else
            {
                Write-Host "  WARN  $($m.Filename):$($m.LineNumber): $line" -ForegroundColor Yellow
                Write-Host "        ^ Review: is this for a destination file?" -ForegroundColor Yellow
            }
        }
    }
}

Write-Host ""
Write-Host "--- Read-only constants verification ---" -ForegroundColor Yellow
$roMatches = Get-ChildItem -Path $SourcePath -Recurse -Include '*.cs' |
    Select-String -Pattern 'GENERIC_READ|FileAccess\.Read|FileShare\.Read|OPEN_EXISTING|ReadOnlyFlags|Flag64Bits' -CaseSensitive
foreach ($m in $roMatches)
{
    Write-Host "  OK    $($m.Filename):$($m.LineNumber): $($m.Line.Trim())" -ForegroundColor Green
}

Write-Host ""
if ($exitCode -eq 0)
{
    Write-Host "=== AUDIT PASSED ===" -ForegroundColor Green
}
else
{
    Write-Host "=== AUDIT FAILED ===" -ForegroundColor Red
}
exit $exitCode
