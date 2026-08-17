<#
.SYNOPSIS
    Regenerates tests/test_ext4.img — a 10 MB EXT4 fixture used by the
    SmokeTest, BrowserTest and TransferTest projects.
.DESCRIPTION
    Uses WSL2 (Ubuntu) to invoke mkfs.ext4 on a sparse file, then populates
    it with a known set of files so the assertions in the test programs hold:
        /hello.txt        -> "Hello EXT4!"
        /test.txt         -> (small text)
        /subdir/nested.txt-> "Nested file"
        /lost+found/      -> (created by mkfs.ext4)
    Requires WSL2 with an Ubuntu distribution installed and mkfs.ext4 available
    inside it (usually provided by the e2fsprogs package).
.EXAMPLE
    .\tests\make-test-image.ps1
#>
[CmdletBinding()]
param(
    [string]$OutPath = (Resolve-Path "$PSScriptRoot").Path + "\test_ext4.img",
    [int]$SizeMB = 10,
    [string]$Distribution = "Ubuntu"
)

$ErrorActionPreference = 'Stop'

Write-Host "=== EXT FS Viewer — test image generator ===" -ForegroundColor Cyan
Write-Host "Output:       $OutPath"
Write-Host "Size:         $SizeMB MB"
Write-Host "WSL distro:   $Distribution"
Write-Host ""

# Verify WSL is available
$wslExe = Get-Command wsl.exe -ErrorAction SilentlyContinue
if (-not $wslExe) {
    throw "wsl.exe not found. Install WSL2 with 'wsl --install' and an Ubuntu distribution."
}

# Convert Windows path to a WSL path (e.g. C:\foo\bar -> /mnt/c/foo/bar)
$winRoot = (Split-Path -Qualifier $OutPath).TrimEnd('\').ToLower()
$driveLetter = $winRoot[0]
$wslPath = "/mnt/$driveLetter" + (Split-Path -NoQualifier $OutPath).Replace('\','/')

Write-Host "[1/4] Creating $SizeMB MB file at $wslPath ..." -ForegroundColor Yellow
wsl -d $Distribution -e bash -lc "dd if=/dev/zero of=`"$wslPath`" bs=1M count=$SizeMB status=none"
if ($LASTEXITCODE -ne 0) { throw "dd failed (exit $LASTEXITCODE)." }

Write-Host "[2/4] Formatting as EXT4 ..." -ForegroundColor Yellow
wsl -d $Distribution -e bash -lc "mkfs.ext4 -F -L testvol `"$wslPath`" 2>&1" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "mkfs.ext4 failed (exit $LASTEXITCODE)." }

Write-Host "[3/4] Mounting and populating ..." -ForegroundColor Yellow
$mnt = "/tmp/extfs_test_$$"
wsl -d $Distribution -e bash -lc @"
set -e
mkdir -p $mnt
sudo mount -o loop `"$wslPath`" $mnt 2>/dev/null || sudo mount `"$wslPath`" $mnt
echo 'Hello EXT4!' | sudo tee $mnt/hello.txt > /dev/null
echo 'A second test file.' | sudo tee $mnt/test.txt > /dev/null
sudo mkdir -p $mnt/subdir
echo 'Nested file' | sudo tee $mnt/subdir/nested.txt > /dev/null
sudo umount $mnt
sudo rmdir $mnt
"@
if ($LASTEXITCODE -ne 0) { throw "Mount/populate failed (exit $LASTEXITCODE)." }

Write-Host "[4/4] Done." -ForegroundColor Green
Write-Host ""
Write-Host "Created: $OutPath ($((Get-Item $OutPath).Length) bytes)" -ForegroundColor Green
Write-Host ""
Write-Host "Test programs look for 'test_ext4.img' next to their csproj or in the bin"
Write-Host "output directory. Rebuild the test projects after regenerating."
