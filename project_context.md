# project_context.md — EXT FS Viewer Architecture Summary

> Living document. Updated by the Technical Lead at the end of every significant task.
> Last updated: 2026-08-01 (Phase 5 — COMPLETE — ALL PHASES DONE).

---

## 1. Project Objective

A native Windows 11 desktop application that reads and browses Linux **EXT2 / EXT3 / EXT4** file systems in **STRICTLY read-only** mode.

**Features:**
- Open EXT-formatted physical disks (USB-attached) and image files (`.img`, `.dd`, `.vhd`).
- Browse the EXT directory tree through a modern Win11 GUI.
- View file/inode properties (mode, uid/gid, timestamps, size, blocks).
- Copy selected files or entire directories to a local Windows folder.
- **Never** modifies, writes, formats, or deletes anything on the EXT source.

---

## 2. Selected Tech Stack

| Layer | Choice | Justification |
|---|---|---|
| Language | **C# / .NET 8** | Strong typing, excellent P/Invoke support for `libext2fs`, SDK present on dev machine. |
| GUI Framework | **WPF + WPF-UI (`Lepoco.WpfUi`)** | **True portability** via single-file self-contained publish. WPF-UI provides Win11 Fluent look (Mica backdrop, NavigationView, rounded corners). |
| MVVM | **CommunityToolkit.Mvvm** | Source-generated `[ObservableProperty]` / `[RelayCommand]`, minimal boilerplate. |
| EXT parsing | **`libext2fs`** (C library) via P/Invoke | Battle-tested EXT2/3/4 reader. `EXT2_FLAG_RDONLY` enforces read-only at the library level. |
| Raw disk I/O | **Win32 `CreateFile` with `GENERIC_READ` only** | Kernel-enforced read-only — a second independent safety layer. |
| Distribution | **Single-file self-contained exe + `libext2fs.dll`** | No installer, no framework package, no WinFSP. Runs from any folder or USB drive. |

### Why WPF instead of WinUI 3?

WinUI 3 / Windows App SDK is optimized for MSIX-packaged deployment and framework-package distribution. Unpackaged single-exe deployment is technically possible but historically fragile (runtime auto-redist, package-aware APIs). WPF's `PublishSingleFile` + `SelfContained` is rock-solid and produces a genuinely portable executable. The **WPF-UI** library bridges the visual gap, providing Fluent/Mica styling indistinguishable from WinUI 3 for the controls we need (window chrome, navigation, tree/list views).

### Why not Python?

Python + PySide6 + PyInstaller IS viable for portability. However, C# P/Invoke to `libext2fs` is more type-safe and robust than Python `ctypes` for low-level struct marshalling (fixed-size buffers, function pointers, opaque handle lifetimes). Since C# .NET 8 achieves true portability via WPF single-file publish, there is no portability penalty to staying with C#, and we gain stronger interop safety.

### Why not Rust + Tauri?

Rust + Tauri produces the smallest single-exe portable binary. Rejected because: (a) UI is WebView2/HTML-CSS (not native Fluent), (b) Rust EXT4 crates are immature compared to `libext2fs`, (c) slower dev iteration, (d) Rust FFI ergonomics are stricter but slower to develop than C# P/Invoke.

---

## 3. High-Level Architecture

```
┌───────────────────────────────────────────────────────────┐
│  UI Layer  (WPF + WPF-UI, FluentWindow, Mica backdrop)    │
│  MainWindow | NavigationView | TreeView | FileListView   │
│  PropertiesPane | CopyProgressDialog                       │
└───────────────┬───────────────────────────────────────────┘
                │  (ViewModels, IProgress<CopyProgress>, CancellationToken)
┌───────────────▼───────────────────────────────────────────┐
│  Application Services                                     │
│  DriveDiscoveryService   ImageFileService                 │
│  ExtFileSystemService    FileTransferService              │
└───────┬───────────────────────┬───────────────────────────┘
        │                       │
┌───────▼───────────┐   ┌───────▼────────────────────┐
│ Win32 Disk I/O    │   │ libext2fs (P/Invoke)       │
│ CreateFile R/O    │   │ ext2fs_open(FLAG_RDONLY)   │
│ Partition scan    │   │ ext2fs_dir_iterate         │
│ GPT/MBR, magic    │   │ ext2fs_file_open/read      │
└───────────────────┘   └────────────────────────────┘
        │
┌───────▼───────────────────────────────────────────────────┐
│   Physical disk (\\.\PhysicalDriveN)  OR  Image file       │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. Layer Responsibilities

### UI Layer (WPF + WPF-UI)
- `FluentWindow` with Mica backdrop + app icon.
- `NavigationView` (Sources / About).
- `TreeView` for EXT directory hierarchy (lazy-loaded).
- `ListView` / `DataGrid` for files in selected directory.
- `PropertiesPane` for selected inode metadata.
- `CopyProgressDialog` — progress bar, current file, file count, cancel button.

### ViewModels (MVVM)
- `MainViewModel` — top-level navigation state.
- `SourcesViewModel` — physical drive list + image-file picker.
- `FileSystemViewModel` — tree, list, selection, properties.
- `CopyViewModel` — progress reporting + cancellation.

### Application Services
- `DriveDiscoveryService` — enumerate physical drives via `DeviceIoControl`, parse GPT/MBR, detect EXT magic `0xEF53`.
- `ImageFileService` — open `.img` / `.dd` / `.vhd` as readonly `FileStream`.
- `ExtFileSystemService` — `libext2fs` wrapper: open R/O, iterate directories, read inode metadata, read file blocks.
- `FileTransferService` — async streaming copy with `IProgress<T>` + `CancellationToken`.

### Native Interop
- `NativeMethods.Ext2fs` — `ext2fs_open`, `ext2fs_close`, `ext2fs_dir_iterate`, `ext2fs_file_open`, `ext2fs_file_read`, `ext2fs_get_lib_version`.
- `NativeMethods.Kernel32` — `CreateFile`, `DeviceIoControl`, `CloseHandle`.

---

## 5. Safety Mechanics (Defense in Depth)

| # | Layer | Mechanism |
|---|---|---|
| 1 | App manifest | `requireAdministrator` — needed for raw physical disk access. |
| 2 | Physical disk handle | `CreateFile` with `GENERIC_READ` + `FILE_SHARE_READ` only. **Never** `GENERIC_WRITE`, `OPEN_ALWAYS`, or `CREATE_ALWAYS`. Always `OPEN_EXISTING`. |
| 3 | Image file handle | `FileStream` with `FileAccess.Read` + `FileShare.Read` only. |
| 4 | `libext2fs` | Opened with `EXT2_FLAG_RDONLY` exclusively. Wrapper refuses any other flag. |
| 5 | Startup self-check | Verify no write-capable handle to target device; refuse to operate if one is open. |
| 6 | Static audit | `grep` for `GENERIC_WRITE`, `FileAccess.Write`, `EXT2_FLAG_RW` — must return ZERO hits in disk/EXT/transfer code paths. |

If at any point a write path appears required, STOP and escalate to the Product Owner. Do not improvise.

---

## 6. External Prerequisites

| Component | Purpose | Install |
|---|---|---|
| .NET 8 SDK | Build | Already installed (`8.0.423`). |
| MSYS2 | Build environment for `libext2fs.dll` | `winget install MSYS2.MSYS2` |
| `libext2fs` / e2fsprogs | EXT parsing | `pacman -S mingw-w64-x86_64-e2fsprogs` (via MSYS2) |
| WPF-UI (`Lepoco.WpfUi`) | Win11 Fluent UI | NuGet restore. |
| CommunityToolkit.Mvvm | MVVM | NuGet restore. |

**End-user runtime requirements:** Windows 11 (or Windows 10 1809+). No .NET runtime needed (self-contained). No WinFSP needed. No admin install — admin rights are requested at launch only when a physical disk source is selected.

---

## 7. Portability & Distribution

Publish profile (in `.csproj` or `PublishProfile.pubxml`):

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<EnableReadyToRun>true</EnableReadyToRun>
```

**Result:** a single `ExtFsViewer.exe` (~70 MB) plus `libext2fs.dll`. Runs from any folder, USB drive, or network share. No install, no framework package, no MSIX.

---

## 8. Current Status

- [x] Tech stack decided (revised: WPF + WPF-UI for portability).
- [x] Architecture documented.
- [x] `memory.md`, `project_context.md`, `roadmap.md` created.
- [x] **Phase 0 COMPLETE**: Solution scaffold, NuGet deps, `libext2fs.dll` built from source (v1.47.4, 630 exports), P/Invoke smoke test passed (`ext2fs_get_library_version` → `1.47.4`).
- [x] **Phase 1 COMPLETE**: Admin elevation handling, physical drive enumeration, GPT/MBR partition parsing, EXT2/3/4 detection (magic `0xEF53` + feature flags), image-file support, WPF-UI FluentWindow with Mica backdrop. Safety audit: zero write symbols.
- [x] **Phase 2 COMPLETE**: EXT engine wrapper. `ExtFileSystemService` with `ext2fs_open2` (using `io_options="offset=N"` for partitions — no custom I/O manager needed), `ext2fs_dir_iterate` (GCHandle-pinned callback), `ext2fs_read_inode_full`, `ext2fs_file_open` (flags=0, read-only), `ext2fs_file_read` (1 MB chunked), `ext2fs_file_get_size`. Rebuilt DLL to export `windows_io_manager` data symbol. Test: all 6 cases pass (open, list root, inode info, read file, list subdir, read nested file).
- [x] **Phase 3 COMPLETE**: File browser UI. `BrowserWindow` (FluentWindow with toolbar/breadcrumbs/list/properties pane), `BrowserViewModel` (navigation stack, Enter/Back/Up/Refresh/Extract commands), `FileItemViewModel` (display wrapper with formatted size/permissions/timestamps, Segoe MDL2 icon glyphs), `BrowseCommand` wired from `SourcesViewModel` to open `BrowserWindow`. Extract uses `SaveFileDialog` → `CopyFileAsync` with progress. End-to-end test: all 5 cases pass (open, navigate into subdir, back, properties view, file extraction with byte-identical content verified by SHA256).
- [x] **Phase 4 COMPLETE**: File transfer engine. `FileTransferService` with `CopyFileAsync` (1 MB chunked, `IProgress<CopyProgress>` reporting) and `CopyDirectoryAsync` (recursive `CollectFiles`, preserves relative folder structure). `CopyProgress` readonly record struct (BytesCopied/TotalBytes/CurrentFile/FilesDone/FilesTotal/Percent/Summary). `CopyProgressDialog` (FluentWindow with ProgressBar, current file, summary, Cancel button → CancellationTokenSource). `CollisionPolicy` enum (Skip/Overwrite/Rename with " (n)" suffix). Long-path support via `ToLongPath()` (`\\?\` prefix when ≥260 chars). `ExtFileSystemService.GetHandle()` + `GetFileSize(ino)` added for transfer service. End-to-end test: all 5 cases pass (single file copy, recursive directory copy of 3 files, structure verified, nested content verified, 301-char long path verified). Safety audit: zero dangerous write symbols, all EXT opens read-only, `FileAccess.Write` only for destination files.
- [x] **Phase 5 COMPLETE**: Testing, safety audit & build scripting. `SafetySelfCheck` runtime check at startup (manifest + libext2fs version + write constants). `scripts/safety-audit.ps1` static grep (PASSED: zero forbidden symbols). `scripts/build.ps1` 6-step pipeline (clean/restore/build/publish/copy DLLs/zip). Published: 163 MB single-file exe + 2.6 MB libext2fs.dll + 66 KB libwinpthread-1.dll, zipped to 69.2 MB. Smoke test: published exe launches successfully. `README.md` with full usage guide. All 3 test suites pass (SmokeTest 6/6, BrowserTest 5/5, TransferTest 5/5).
- [x] **ALL PHASES COMPLETE** — project ready for release.
- [ ] Phase 3: GUI shell.
- [ ] Phase 4: File transfer engine.
- [ ] Phase 5: Testing, safety audit, build script.

### libext2fs Build Details (Phase 0)

- **Source**: e2fsprogs v1.47.4 from `https://kernel.org/pub/linux/kernel/people/tytso/e2fsprogs/v1.47.4/e2fsprogs-1.47.4.tar.xz`.
- **Toolchain**: MSYS2 MinGW-w64 GCC 16.1.0 (`C:\msys64\mingw64\bin\gcc.exe`).
- **Build approach**: Built `lib/et` (com_err) + `lib/ext2fs` as static `.a` archives, then linked into a single `libext2fs.dll` via `gcc -shared -Wl,--whole-archive ... -Wl,--export-all-symbols` with a `.def` file listing 630 `ext2fs_*` exports.
- **Compat layer**: e2fsprogs targets Linux/POSIX. MinGW build required shim headers (`arpa/inet.h`, `grp.h`, `pwd.h`, `mntent.h`, `paths.h`, `fcntl.h`, `unistd.h`) and stub functions (`getuid`/`geteuid`/`getgid`/`getegid`/`makedev`/`fcntl`/`sysconf`/`fsync`/`pread`/`pwrite`/`select`) in `mingw_compat.c`.
- **I/O manager**: Used `windows_io.c` (native Win32 `CreateFile`/`ReadFile` I/O manager) instead of `unix_io.c` (POSIX `open`/`read`). The `ext2_io.h` header conditionally declares `windows_io_manager` on `_WIN32` and `unix_io_manager` elsewhere.
- **Dependencies**: `libext2fs.dll` → `KERNEL32.dll` (system), `msvcrt.dll` (system), `libwinpthread-1.dll` (MinGW runtime, 66 KB, bundled).
- **DLL size**: 2.6 MB.
- **Build artifacts location**: `C:\Users\Stavros\AppData\Local\Temp\opencode\` (build scripts, compat layer, extracted source).
- **Future optimization**: Rebuild with `-static` flag to eliminate `libwinpthread-1.dll` dependency.

---

## 9. Decision Log

- **2026-08-01** — Phase 5 COMPLETE. Final phase: `SafetySelfCheck` runtime verification at startup (manifest/libext2fs/constants). `scripts/safety-audit.ps1` static grep script (excludes `SafetySelfCheck.cs` and `Ext2fsConstants.cs` which contain symbol names as warning text/constant definitions, never used in actual calls). `scripts/build.ps1` 6-step pipeline. Key fix: `dotnet restore` and `dotnet build` needed `-r win-x64` explicitly because `RuntimeIdentifier` is in the Release-only condition group. Published: 163 MB single-file exe (self-contained .NET 8 runtime + WPF + WPF-UI bundled). Zipped to 69.2 MB. All test suites pass. `README.md` created with usage guide, safety explanation, architecture diagram, build instructions. **ALL 5 PHASES COMPLETE — project ready for release.**

- **2026-08-01** — Phase 4 COMPLETE. Built `FileTransferService` with `CopyFileAsync` (1 MB chunked, `IProgress<CopyProgress>`) and `CopyDirectoryAsync` (recursive `CollectFiles` via `ListDirectory`, preserves relative paths, creates destination dirs on demand). `CopyProgress` readonly record struct with computed `Percent`/`Summary`. `CopyProgressDialog` (FluentWindow + Mica, ProgressBar bound to Percent, Cancel button → `CancellationTokenSource.Cancel()`). `CollisionPolicy` enum (Skip/Overwrite/Rename with " (n)" suffix). Long-path support via `ToLongPath()` prepending `\\?\` when ≥260 chars. Added `ExtFileSystemService.GetHandle()` and `GetFileSize(ino)` for transfer service. End-to-end test: all 5 cases pass (single file copy, recursive dir copy of 3 files preserving structure, nested content verified, 301-char long path verified). Safety audit: zero `GENERIC_WRITE`/`EXT2_FLAG_RW`/`CREATE_ALWAYS`/`FileMode.CreateNew/Truncate/Append`; all EXT opens read-only; `FileAccess.Write` only for destination files.

- **2026-08-01** — Phase 3 COMPLETE. Built file browser UI: `BrowserWindow` (FluentWindow with Mica backdrop, toolbar with Back/Up/Refresh/Extract buttons, breadcrumb path bar, ListView with GridView showing Name/Size/Modified/Permissions/Type columns, properties pane with full inode metadata), `BrowserViewModel` (navigation via `Stack<(uint ino, string name)>`, Enter/Back/Up/Refresh/Extract commands with CanExecute guards, `ExtractToAsync` separated from dialog for testability), `FileItemViewModel` (display wrapper with formatted sizes, permissions string, Segoe MDL2 icon glyphs), `BrowseCommand` wired from `SourcesViewModel.BrowseRequested` event to `MainWindow` which opens `BrowserWindow` with the selected source. End-to-end test passes: open image, navigate into/out of directories, view file properties, extract file with byte-identical content (SHA256 verified). Safety audit: EXT source stays read-only, `FileAccess.Write` only for destination files.

- **2026-08-01** — Phase 2 COMPLETE. Built `ExtFileSystemService` wrapping `libext2fs`: `Open` (ext2fs_open2 with `io_options="offset=N"` for partition offsets — eliminates need for custom I/O manager since `windows_io.c` supports the offset option natively), `ListDirectory`/`ListRoot` (ext2fs_dir_iterate with GCHandle-pinned callback), `GetInode` (ext2fs_read_inode_full, manual offset marshalling of 128-byte inode struct), `ReadFile` (ext2fs_file_read in 1 MB chunks), `CopyFileAsync` (streams to local FileStream with IProgress reporting), `LookupPath`/`ResolvePath` (implemented via ListDirectory to avoid ext2fs_lookup marshalling crash). Rebuilt `libext2fs.dll` with `.def` file to export `windows_io_manager` data symbol (was previously not exported). Created 10 MB EXT4 test image via WSL2 mkfs.ext4 with nested files. All 6 test cases pass. Safety audit: only `EXT2_FLAG_64BITS` (no `EXT2_FLAG_RW`) in open calls, `flags=0` in file_open calls. The only `FileAccess.Write` is for destination files in CopyFileAsync.

- **2026-08-01** — Phase 1 COMPLETE. Implemented: `AdminRightsService` (elevation check + restart-elevated), `DriveDiscoveryService` (physical drive enumeration via `CreateFileW(GENERIC_READ)` + `DeviceIoControl`), `PartitionParser` (MBR + GPT), `ExtDetector` (EXT2/3/4 via magic + feature flags), `ImageFileService` (read-only image files), `SourcesViewModel` + WPF-UI `FluentWindow` with Mica backdrop. Manifest: `asInvoker` (not `requireAdministrator`) — elevation only needed for physical disks. Safety audit: ZERO `GENERIC_WRITE`/`FileAccess.Write`/`EXT2_FLAG_RW` symbols in source code. Both Debug and Release builds pass (0 warnings, 0 errors).

- **2026-08-01** — Phase 0 COMPLETE. `libext2fs.dll` (v1.47.4) built from source using MSYS2 MinGW GCC 16.1.0. Required POSIX compat shim headers + stub functions. Used `windows_io.c` for native Win32 I/O. P/Invoke smoke test passed: `ext2fs_get_library_version` returns "1.47.4". DLL has 630 exports. Dependency: `libwinpthread-1.dll` (66 KB, bundled).

- **2026-08-01** — Stack revised: GUI framework swapped from WinUI 3 → **WPF + WPF-UI** to satisfy Product Owner's portability constraint (single-file self-contained exe, no installer). All other architecture decisions unchanged. Python rejected in favor of C# since C# already achieves true portability via WPF and offers stronger `libext2fs` interop safety.
- **2026-08-01** — Stack approved: C# .NET 8 + WinUI 3 + libext2fs P/Invoke. WinFSP dropped (in-app browse + copy only). Physical disks AND image files both supported. libext2fs accepted as native dependency.
- **2026-08-01** — Project initiated by Product Owner.
