<p align="center">
  <img src="src/EXTReader/app_preview.png" alt="EXTReader icon" width="160">
</p>

<h1 align="center">EXTReader</h1>

<p align="center">
  <a href="https://github.com/stavros-it/EXTReader/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/stavros-it/EXTReader/ci.yml?branch=main&label=CI&logo=github" alt="CI"></a>
  <img src="https://img.shields.io/badge/.NET-8.0-512bd4?logo=dotnet" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/C%23-12.0-239120?logo=csharp" alt="C# 12.0">
  <img src="https://img.shields.io/badge/WPF--UI-4.3.0-6750A0" alt="WPF-UI 4.3.0">
  <img src="https://img.shields.io/badge/platform-Windows-0078D4?logo=windows" alt="Windows">
  <img src="https://img.shields.io/badge/tests-16-brightgreen" alt="16 tests">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/stavros-it/EXTReader?color=blue" alt="GPL-2.0"></a>
  <a href="https://github.com/stavros-it/EXTReader/releases/latest"><img src="https://img.shields.io/github/v/release/stavros-it/EXTReader?label=release" alt="Release"></a>
</p>

<p align="center">
  A native Windows 11 desktop application for browsing and extracting files from Linux EXT2/EXT3/EXT4 filesystems — strictly <b>read-only</b>.
</p>

## Features

- **Read-only access** to EXT2/EXT3/EXT4 filesystems via `libext2fs` (e2fsprogs v1.47.4)
- Browse files and directories with an Explorer-style Win11 Fluent UI (Mica backdrop)
- View full inode metadata (permissions, UID/GID, size, timestamps, block count, flags)
- Extract individual files or entire directories to local Windows storage
- Supports both **physical disks** (requires admin) and **image files** (`.img`, `.dd`, `.vhd`, `.raw`, `.iso`)
- MBR and GPT partition table support
- Long-path support (>260 chars) for destination files
- Progress reporting with cancellation support
- Single-file portable executable — no installer required

## Safety

The application enforces read-only access through multiple layers:

1. **`asInvoker` manifest** — no unnecessary elevation
2. **`CreateFileW` with `GENERIC_READ` only** — physical disks opened without write access
3. **`ext2fs_open2` with `EXT2_FLAG_64BITS`** (no `EXT2_FLAG_RW` bit) — libext2fs never opens for writing
4. **`ext2fs_file_open` with `flags=0`** — all file handles are read-only
5. **Static safety audit** — `scripts/safety-audit.ps1` greps for forbidden write symbols
6. **Runtime safety self-check** — verifies libext2fs loads and manifest is correct at startup

## Usage

### Requirements

- Windows 10/11 (x64)
- No installation needed — just extract and run

### Quick start

1. Download `EXTReader-*.zip` from the [latest release](https://github.com/stavros-it/EXTReader/releases/latest) and extract to any folder
2. Run `EXTReader.exe`
3. Click **Refresh** to scan physical drives for EXT partitions (requires admin)
   - Or click **Open Image…** to open a disk image file (no admin needed)
4. Select an EXT source and click **Browse…** to open the file browser
5. Navigate directories, select files, and click **Extract File…** or **Extract Folder…**

### Physical disk access

To read physical disks (`\\.\PhysicalDriveN`), the app must run as Administrator. Click **Restart as Admin** to elevate. Image files (`.img`, `.dd`, `.vhd`) do not require elevation.

## Building from source

### Prerequisites

- .NET 8 SDK
- MSYS2 with MinGW-w64 GCC (only needed to rebuild `libext2fs.dll`)

### Build

```powershell
.\scripts\build.ps1
```

This produces:
- `publish\EXTReader.exe` — single-file self-contained executable
- `publish\libext2fs.dll` — native EXT filesystem library
- `publish\libwinpthread-1.dll` — MinGW runtime dependency
- `publish\EXTReader-*.zip` — zipped portable distribution

### Safety audit

```powershell
.\scripts\safety-audit.ps1
```

### Running tests

```powershell
dotnet run --project tests\SmokeTest    # P/Invoke + EXT engine
dotnet run --project tests\BrowserTest # Browser navigation + extraction
dotnet run --project tests\TransferTest # File/directory copy
```

## Architecture

```
EXTReader.exe (WPF + WPF-UI)
├── Interop/
│   ├── NativeKernel32.cs      — Win32 CreateFileW, DeviceIoControl
│   ├── NativeExt2fs.cs        — libext2fs P/Invoke (LibraryImport)
│   └── Ext2fsConstants.cs     — flag constants
├── Models/
│   ├── ExtSource.cs          — source descriptor
│   ├── ExtDirEntry.cs        — directory entry DTO
│   ├── ExtInodeInfo.cs       — inode metadata DTO
│   ├── CopyProgress.cs       — transfer progress struct
│   └── CollisionPolicy.cs    — skip/overwrite/rename
├── Services/
│   ├── AdminRightsService.cs
│   ├── DriveDiscoveryService.cs
│   ├── PartitionParser.cs    — MBR + GPT
│   ├── ExtDetector.cs        — magic 0xEF53 + feature flags
│   ├── ImageFileService.cs
│   ├── ExtFileSystemService.cs  — libext2fs wrapper (Open, List, Read, GetInode)
│   ├── FileTransferService.cs  — Copy file/directory with progress
│   └── SafetySelfCheck.cs
├── ViewModels/
│   ├── SourcesViewModel.cs
│   ├── BrowserViewModel.cs
│   └── FileItemViewModel.cs
├── MainWindow.xaml(.cs)      — source picker
├── BrowserWindow.xaml(.cs)   — file browser
└── CopyProgressDialog.xaml(.cs) — transfer progress
```

## Tech stack

- C# .NET 8 + WPF
- [WPF-UI](https://github.com/lepoco/wpfui) 4.3.0 (FluentWindow, Mica, Win11 controls)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/Mvvm) 8.4.2
- [e2fsprogs](https://e2fsprogs.sourceforge.net/) v1.47.4 (`libext2fs.dll` built from source via MSYS2 MinGW)

## License

This application is licensed under the [GNU General Public License v2.0](LICENSE) or any later version.

`libext2fs` (e2fsprogs) is licensed under GPL-2.0 / LGPL-2.1; this application links to it dynamically. The bundled `libext2fs.dll` and `libwinpthread-1.dll` binaries in `src/EXTReader/` are redistributed under their respective licenses.
