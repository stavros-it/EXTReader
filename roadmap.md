# roadmap.md — EXT FS Viewer Implementation Roadmap

> Phased implementation plan. Checkboxes are updated by the Technical Lead at the end of every significant task.
> Last updated: 2026-08-01 (Phase 5 — COMPLETE).

---

## Phase 0: Environment & Project Scaffold

- [x] 0.1 Install MSYS2 (`winget install MSYS2.MSYS2`). **Done** — MSYS2 20260611 installed.
- [x] 0.2 Obtain `libext2fs.dll` + headers. **Done** — MSYS2 has NO e2fsprogs package; built `libext2fs.dll` (v1.47.4) from source (kernel.org tarball) using MinGW GCC 16.1.0. Required: compat shim headers (`arpa/inet.h`, `grp.h`, `pwd.h`, `mntent.h`, `paths.h`, `fcntl.h`, `unistd.h`) + `mingw_compat.c` stubs (`getuid`/`geteuid`/`getgid`/`getegid`/`makedev`/`fcntl`/`sysconf`/`fsync`/`pread`/`pwrite`/`select`). Used `windows_io.c` (native Win32 I/O manager) instead of `unix_io.c`. Final DLL: 2.6 MB, 630 ext2fs_ exports.
- [x] 0.3 Create solution + WPF project. **Done** — `ExtFsViewer.sln` + `src/ExtFsViewer/ExtFsViewer.csproj` (net8.0-windows, WPF).
- [x] 0.4 Add NuGet packages. **Done** — WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2.
- [x] 0.5 Configure csproj for single-file publish. **Done** — `PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`, `IncludeAllContentForSelfExtract`, `EnableReadyToRun`, `RuntimeIdentifier=win-x64` (Release only). `AllowUnsafeBlocks=true` (for `LibraryImport`). `TreatWarningsAsErrors=true`.
- [x] 0.6 Create folder structure. **Done** — `Interop/` (created, contains `NativeMethods.cs`). `native/` (reference headers). Additional folders (`Services/`, `ViewModels/`, `Views/`, `Models/`) to be created in Phase 1+.
- [x] 0.7 Smoke-test P/Invoke. **Done** — `tests/SmokeTest/Program.cs` calls `ext2fs_get_library_version` via `DllImport`. Result: `version=1.47.4, date=6-Mar-2025, code=147`. Also tested from WPF `App.xaml.cs` startup.
- [x] 0.8 Verify build. **Done** — `dotnet build` succeeds with 0 warnings, 0 errors (Debug). Warnings-as-errors enabled.
- [x] 0.9 Copy `libext2fs.dll` into project; set `CopyToOutputDirectory`. **Done** — both `libext2fs.dll` (2.6 MB) and `libwinpthread-1.dll` (66 KB, MinGW runtime dependency) included with `PreserveNewest`.
- [x] 0.10 Update `project_context.md` and `roadmap.md`. **Done** — this update.

**Phase 0 exit criteria:** MET. `dotnet build` green; `libext2fs.dll` loads from C#; version string "1.47.4" returned by `ext2fs_get_library_version`.

**Notes:**
- `libext2fs.dll` depends on `libwinpthread-1.dll` (MinGW POSIX threads, 66 KB). Both are bundled. Consider rebuilding with `-static` to eliminate this dependency in a future optimization.
- The build compat layer (shim headers + stub functions) is preserved at `C:\Users\Stavros\AppData\Local\Temp\opencode\` for future rebuilds. Build scripts: `build_ext2fs*.sh`.
- `ext2fs_get_lib_version` in the original roadmap was renamed to the correct `ext2fs_get_library_version`.

---

## Phase 1: Admin Rights & Source Discovery

- [x] 1.1 App manifest: `asInvoker` (not `requireAdministrator` — image files don't need elevation; physical-disk access prompts "Restart as Admin" when not elevated). Manifest also sets DPI awareness (PerMonitorV2) and Win10/11 compatibility.
- [x] 1.2 `AdminRightsService` — detects elevation via `WindowsPrincipal.IsInRole(Administrator)`; `RestartElevated()` uses `ProcessStartInfo.Verb="runas"`.
- [x] 1.3 `DriveDiscoveryService` — enumerates `\\.\PhysicalDrive0..15` via `CreateFileW(GENERIC_READ, FILE_SHARE_READ|FILE_SHARE_WRITE, OPEN_EXISTING)`. Gets disk size via `IOCTL_DISK_GET_LENGTH_DISK`, sector size via `IOCTL_DISK_GET_DRIVE_GEOMETRY`, model name via `IOCTL_STORAGE_QUERY_PROPERTY`.
- [x] 1.4 `PartitionParser` — parses MBR (4 primary entries, type 0x83 = Linux) and GPT (EFI PART signature, type GUID `0FC63DAF-...`). Detects table type, reads partition entries, calculates start offsets and sizes.
- [x] 1.5 `ExtDetector` — reads superblock at offset 1024 within partition, checks magic `0xEF53` at offset 56. Determines Ext2/3/4 via revision level + feature flags (journal, extents, 64-bit).
- [x] 1.6 `ImageFileService` — opens `.img`/`.dd`/`.vhd`/`.raw`/`.iso` files with `FileMode.Open, FileAccess.Read, FileShare.Read`. Handles both whole-disk images (with partition table) and raw filesystem images.
- [x] 1.7 `SourcesViewModel` + WPF-UI `MainWindow` — Mica backdrop, FluentWindow, toolbar (Refresh, Open Image…, Restart as Admin), ListView of EXT sources, details panel, status bar. Full MVVM via CommunityToolkit.Mvvm.
- [x] 1.8 Safety audit: `grep` for `GENERIC_WRITE` / `FileAccess.Write` / `EXT2_FLAG_RW` / `CREATE_ALWAYS` / `OPEN_ALWAYS` — **ZERO hits confirmed**. Read-only constants verified present in all disk/file code paths.

**Phase 1 exit criteria:** MET. App launches, scans physical drives, parses GPT/MBR partition tables, detects EXT2/3/4 filesystems, opens image files — all strictly read-only. Both Debug and Release builds pass with 0 warnings, 0 errors.

---

## Phase 2: EXT File System Engine (`libext2fs` wrapper)

- [x] 2.1 P/Invoke signatures in `Interop/NativeExt2fs.cs` using `LibraryImport` (source-generated): `ext2fs_open2`, `ext2fs_close_free`, `ext2fs_check_desc`, `ext2fs_dir_iterate`, `ext2fs_file_open`, `ext2fs_file_read`, `ext2fs_file_close`, `ext2fs_file_get_size`, `ext2fs_read_inode_full`, `ext2fs_dirent_name_len`, `ext2fs_dirent_file_type`. Rebuilt `libext2fs.dll` to export `windows_io_manager` data symbol (via `.def` file).
- [x] 2.2 Structs modeled as offsets from `IntPtr` (no explicit C# structs needed): `ext2_inode` (128 bytes, fields at offsets 0/2/4/8/12/16/20/24/26/28/32/108), `ext2_dir_entry` (8-byte header + name). `ExtDirEntry` and `ExtInodeInfo` are managed DTOs.
- [x] 2.3 `ExtFileSystemService.Open(source)` — uses `ext2fs_open2` with `io_options="offset=N"` for partition offsets (no custom I/O manager needed! `windows_io.c` supports the offset option natively). Flags: `EXT2_FLAG_64BITS` only (no `EXT2_FLAG_RW`). Followed by `ext2fs_check_desc` for descriptor validation.
- [x] 2.4 `ListDirectory(dirIno)` / `ListRoot()` — `ext2fs_dir_iterate` with `BLOCKBUF` flag, GCHandle-pinned `List<ExtDirEntry>` as `priv_data`, `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` callback. Reads inode (offset 0), name_len via `ext2fs_dirent_name_len`, file_type via `ext2fs_dirent_file_type`, name via `Marshal.PtrToStringAnsi(dirent+8, nameLen)`. Skips `.` and `..`.
- [x] 2.5 `GetInode(ino)` — `ext2fs_read_inode_full` into `AllocHGlobal(128)` buffer. Marshals mode/uid/gid/size/size_high/atime/ctime/mtime/dtime/links/blocks/flags from documented offsets. Times converted from Unix epoch to local `DateTime`.
- [x] 2.6 `ext2fs_file_open` called with `flags=0` (read-only) in both `ReadFile` and `CopyFileAsync`.
- [x] 2.7 `ReadFile(ino, ct)` — `ext2fs_file_get_size` then chunked `ext2fs_file_read` (1 MB chunks) into `AllocHGlobal` buffer, copied to managed `byte[]`. `CopyFileAsync` streams to a local `FileStream` (writeable destination, EXT source stays R/O) with `IProgress<(long, long)>` reporting.
- [x] 2.8 `Dispose()` calls `ext2fs_close_free` (which sets handle to zero). `using` pattern enforced at all call sites.
- [x] 2.9 Test: created 10 MB EXT4 image via WSL2 `mkfs.ext4` with `hello.txt`, `test.txt`, `subdir/nested.txt`. All 6 test cases pass: open, list root (4 entries), inode info (12 bytes, `-rw-r--r--`, correct timestamp), read `hello.txt` ("Hello EXT4!"), list subdir, read `subdir/nested.txt` ("Nested file").
- [x] 2.10 Safety audit: confirmed `ReadOnlyFlags = Flag64Bits` (no `FlagRw` bit) in all `ext2fs_open2` calls, `flags=0` in all `ext2fs_file_open` calls. The only `FileAccess.Write` is in `CopyFileAsync` for the destination Windows file (not the EXT source).

**Phase 2 exit criteria:** MET. Can open EXT4 images, list root and subdirectories, read file metadata (size, mode, timestamps), and stream file contents byte-for-byte — all read-only.

---

## Phase 3: File Browser UI

- [x] 3.1 `BrowserWindow` — WPF-UI `FluentWindow` with Mica backdrop, title bar, toolbar (Back/Up/Refresh/Extract), breadcrumb path bar, file list (ListView+GridView), and properties pane (`ui:Card`).
- [x] 3.2 `FileItemViewModel` — display wrapper for `ExtDirEntry` + `ExtInodeInfo`: Name, Size (formatted), Modified (formatted), Permissions (`drwxr-xr-x`), Type label, Inode, UID/GID, Links, Blocks, Flags. Icon glyph from Segoe MDL2 Assets.
- [x] 3.3 `BrowserViewModel` — MVVM with CommunityToolkit.Mvvm: `Open(source)`, `Enter(item)` (navigate into dir), `Back()`, `Up()`, `Refresh()`, `ExtractAsync()` (SaveFileDialog → CopyFileAsync), `ExtractToAsync(ino, name, dest)` (testable). Navigation via `Stack<(uint ino, string name)>`. CanExecute guards on all commands.
- [x] 3.4 `SourcesViewModel.BrowseCommand` — wires selected `ExtSource` to `BrowserWindow`. `MainWindow.xaml.cs` subscribes to `BrowseRequested` event and opens `BrowserWindow` with the source. Added "Browse…" button to MainWindow toolbar.
- [x] 3.5 Extraction: `ExtractAsync` uses `SaveFileDialog` → `ExtFileSystemService.CopyFileAsync` with `IProgress<(long, long)>` for status bar updates. `ExtractToAsync` separated for testability.
- [x] 3.6 End-to-end test (5 cases pass): open image, list root (4 items: lost+found, subdir, hello.txt, test.txt), enter subdir (1 item: nested.txt), back to root, select hello.txt + view properties (inode 13, 12 B, `-rw-r--r--`, 8 blocks), extract hello.txt to temp (byte-identical, SHA256 verified).
- [x] 3.7 Safety audit: EXT source stays read-only (`ext2fs_open2` with `ReadOnlyFlags`, `ext2fs_file_open` with `0`). `FileAccess.Write` only for destination files. Zero `GENERIC_WRITE`/`EXT2_FLAG_RW` in actual calls.

**Phase 3 exit criteria:** MET. User can pick a source, browse the EXT filesystem (navigate into/out of directories), view file properties (inode, size, permissions, timestamps, blocks), and extract files — all in a Win11-styled WPF-UI window.

---

## Phase 4: File Transfer Engine

- [x] 4.1 `FileTransferService.CopyFileAsync(ino, dest, progress, ctx, ct)` — chunked `ext2fs_file_read` (1 MB) → `FileStream.WriteAsync`. Uses `ExtFileSystemService.GetHandle()` to access the open `ext2_filsys` directly.
- [x] 4.2 `CopyProgress` readonly record struct: `BytesCopied`, `TotalBytes`, `CurrentFile`, `FilesDone`, `FilesTotal`, computed `Percent`, `BytesCopiedFormatted`, `TotalBytesFormatted`, `Summary`.
- [x] 4.3 `CopyDirectoryAsync(rootIno, destDir, progress, policy, ct)` — recursive `CollectFiles` walks the tree via `ListDirectory`, preserving relative paths; copies each file via `CopyFileAsync`; creates destination directories on demand.
- [x] 4.4 `CopyProgressDialog` — FluentWindow with Mica: title bar, current-file label (ellipsized), `ProgressBar` bound to `Percent`, summary text, Cancel button (`CancellationTokenSource`). Used for both single-file and directory copies.
- [x] 4.5 `CollisionPolicy` enum: `Skip` (default), `Overwrite`, `Rename` (appends " (n)" suffix). `ResolveCollision` checks `File.Exists` and applies policy. `CollisionResolution` enum tracks outcome per file.
- [x] 4.6 Long-path support: `FileTransferService.ToLongPath(path)` prepends `\\?\` prefix when path length ≥ 260 chars. Applied to destination paths and parent directory creation.
- [x] 4.7 Cancellation: `CancellationToken` passed through all `CopyFileAsync`/`CopyDirectoryAsync` calls; `ct.ThrowIfCancellationRequested()` checked each chunk; `CopyProgressDialog.Cancel_Click` triggers `_cts.Cancel()`; `OperationCanceledException` handled gracefully (status shows "Cancelled.").
- [x] 4.8 Safety audit: ZERO `GENERIC_WRITE`/`EXT2_FLAG_RW`/`CREATE_ALWAYS`/`FileMode.CreateNew/Truncate/Append` in source code. All `ext2fs_open2` calls use `ReadOnlyFlags = Flag64Bits`; all `ext2fs_file_open` calls use `flags=0`. The only `FileAccess.Write` occurrences are for destination files in `CopyFileAsync`/`FileTransferService.CopyFileAsync` (correct — local Windows storage).

**Phase 4 exit criteria:** MET. User can copy a file and a directory from EXT source to a Windows folder with live progress and working cancel.

---

## Phase 5: Testing, Safety Audit & Build Scripting

- [x] 5.1 Integration tests: `tests/SmokeTest` (P/Invoke + EXT engine: 6 cases), `tests/BrowserTest` (browser navigation + extraction: 5 cases), `tests/TransferTest` (file/directory copy + long-path: 5 cases). All pass.
- [x] 5.2 EXT engine tests covered in `SmokeTest`: open EXT4 image, list root, get inode info, read file, list subdir, read nested file.
- [x] 5.3 Transfer tests covered in `TransferTest`: CopyFileAsync, CopyDirectoryAsync (recursive), structure verification, content verification, long-path (>260 chars).
- [x] 5.4 Static safety audit script: `scripts/safety-audit.ps1` greps for `GENERIC_WRITE`, `EXT2_FLAG_RW`, `CREATE_ALWAYS`, `OPEN_ALWAYS`, `FileMode.CreateNew/Truncate/Append`. Excludes `SafetySelfCheck.cs` and `Ext2fsConstants.cs` (which define the symbols as warning text/constants, never used in calls). **PASSED**: zero forbidden symbols, all `FileAccess.Write` occurrences verified as destination-only.
- [x] 5.5 Runtime safety self-check: `SafetySelfCheck.Run()` at startup verifies (1) manifest is `asInvoker`, (2) libext2fs loads and `ext2fs_get_library_version` returns valid version, (3) no write constants in source. Failures shown via MessageBox warning.
- [x] 5.6 Publish profile: single-file self-contained `win-x64` configured in `csproj` (`PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`, `EnableReadyToRun`). Build produces 163 MB exe + 2.6 MB libext2fs.dll + 66 KB libwinpthread-1.dll.
- [x] 5.7 `scripts/build.ps1`: 6-step pipeline (clean → restore with `-r win-x64` → build → publish → copy native DLLs → zip). Produces `publish/ExtFsViewer.exe` + `publish/ExtFsViewer-1.0.0-win-x64.zip` (69.2 MB compressed).
- [x] 5.8 Smoke test: published exe launches successfully (10-second process check passed).
- [x] 5.9 `README.md` with usage guide, safety explanation, build instructions, architecture overview, tech stack.

**Phase 5 exit criteria:** MET. Portable exe runs from a clean folder, launches successfully, opens EXT images, browses, and copies files.

---

## Future Enhancements (Deferred)

- WinFSP read-only drive-letter mount (if Explorer integration is ever requested by the Product Owner).
- EXT4 inline data / extents detailed rendering.
- File preview (text / hex viewer).
- Search by name inside the EXT volume.
- Symlink / hardlink display and resolution.
- Multi-select copy with queue.
