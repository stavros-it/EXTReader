<#
.SYNOPSIS
    Build script for EXTReader — produces a portable single-file exe.
.DESCRIPTION
    Cleans, restores, builds, publishes (single-file self-contained win-x64),
    copies native DLLs, and zips the output.
.EXAMPLE
    .\scripts\build.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDir = "$PSScriptRoot\..\publish",
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot\.."
$project = "$repoRoot\src\EXTReader\EXTReader.csproj"

Write-Host "=== EXTReader Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Output:        $OutputDir"
Write-Host "Project:       $project"
Write-Host ""

# 1. Clean
Write-Host "[1/6] Cleaning previous build…" -ForegroundColor Yellow
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
dotnet clean $project -c $Configuration --nologo 2>&1 | Out-Null
Write-Host "      Done." -ForegroundColor Green

# 2. Restore
Write-Host "[2/6] Restoring NuGet packages…" -ForegroundColor Yellow
dotnet restore $project -r win-x64 --nologo 2>&1 | Out-Null
Write-Host "      Done." -ForegroundColor Green

# 3. Build (verify zero warnings/errors)
Write-Host "[3/6] Building $Configuration…" -ForegroundColor Yellow
dotnet build $project -c $Configuration -r win-x64 --no-restore --nologo 2>&1 | Tee-Object -Variable buildOutput | Out-Null
if ($LASTEXITCODE -ne 0)
{
    Write-Host "      BUILD FAILED!" -ForegroundColor Red
    $buildOutput | ForEach-Object { Write-Host "      $_" }
    exit 1
}
Write-Host "      Build succeeded." -ForegroundColor Green

# 4. Publish (single-file self-contained)
Write-Host "[4/6] Publishing single-file self-contained…" -ForegroundColor Yellow
dotnet publish $project -c $Configuration -r win-x64 --no-build --nologo -o $OutputDir 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0)
{
    Write-Host "      PUBLISH FAILED!" -ForegroundColor Red
    exit 1
}
Write-Host "      Published." -ForegroundColor Green

# 5. Copy native DLLs (libext2fs.dll + libwinpthread-1.dll)
Write-Host "[5/6] Copying native DLLs…" -ForegroundColor Yellow
$nativeSrc = "$repoRoot\src\EXTReader"
Copy-Item "$nativeSrc\libext2fs.dll" -Destination "$OutputDir\libext2fs.dll" -Force
Copy-Item "$nativeSrc\libwinpthread-1.dll" -Destination "$OutputDir\libwinpthread-1.dll" -Force
Write-Host "      libext2fs.dll:     $((Get-Item "$OutputDir\libext2fs.dll").Length) bytes" -ForegroundColor Green
Write-Host "      libwinpthread-1.dll: $((Get-Item "$OutputDir\libwinpthread-1.dll").Length) bytes" -ForegroundColor Green

# 6. Zip output (optional)
if (-not $NoZip)
{
    Write-Host "[6/6] Zipping output…" -ForegroundColor Yellow
    $version = (Get-Item "$OutputDir\EXTReader.exe").VersionInfo.ProductVersion
    if (-not $version) { $version = "1.0.0" }
    $zipName = "EXTReader-$version-win-x64.zip"
    $zipPath = Join-Path (Split-Path $OutputDir) $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "      $zipName ($zipSize MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== BUILD COMPLETE ===" -ForegroundColor Cyan
Write-Host "Output directory: $OutputDir"
$exe = Get-Item "$OutputDir\EXTReader.exe" -ErrorAction SilentlyContinue
if ($exe)
{
    $exeSize = [math]::Round($exe.Length / 1MB, 1)
    Write-Host "EXTReader.exe: $exeSize MB"
}
Write-Host ""
Write-Host "Files in output:"
Get-ChildItem $OutputDir | ForEach-Object { Write-Host "  $($_.Name) ($($_.Length) bytes)" }
