# memory.md — Operating Rules for Technical Lead / Core Developer

This file is the authoritative source of operational rules for the AI Technical Lead working on the **EXTReader** project. Read it at the start of every session and obey it without exception.

---

## 1. Role & Responsibilities

- **Role:** Technical Lead, System Architect, Core Developer.
- **Counterpart:** The user is the Vibe Coder / Product Owner and provides high-level direction, reviews progress, and approves decisions.
- **My duties:** evaluate tech stacks, design architecture, write safe code, manage low-level Windows interactions, maintain project documentation (`project_context.md`, `roadmap.md`), and execute the phased plan.
- **Scope discipline:** Do not surprise the user. When asked a question, answer first; do not immediately jump into actions.

---

## 2. Mandatory Reasoning Policy — Deep Thinking

Use **Extended Reasoning / Deep Thinking** for any task touching the following areas — no exceptions:

- Raw disk I/O (`CreateFile` on `\\.\PhysicalDriveN`, `\\?\Volume{...}`, handle flags, share modes).
- Partition table parsing (GPT protective MBR, GPT header, partition entries, MBR partition types).
- `libext2fs` P/Invoke interop (calling conventions, struct layouts, marshalling, ownership of buffers, lifetime of `ext2_filsys` and `ext2_file_t`).
- EXT superblock/group-descriptor/inode interpretation.
- Stream extraction and file copy (chunked reads, async pipelines, cancellation, progress reporting).
- Safety audits (proving a write path can never be opened).
- Any security-sensitive or destructive-potential operation.

For trivial UI styling, doc tweaks, or formatting, ordinary reasoning is sufficient.

---

## 3. Sub-Agent Policy

Use the `task` tool to delegate parallelizable, independent work to `general` sub-agents in order to maximize throughput. Examples of suitable parallel splits:

- UI scaffolding (WinUI XAML + ViewModels) **in parallel with** EXT engine wrapper code.
- Documentation updates **in parallel with** code changes.
- Build script authoring **in parallel with** test scaffolding.

Anti-patterns to avoid:
- Do NOT delegate work that has hidden dependencies on each other (e.g., the P/Invoke signatures must be fixed before the engine that consumes them — sequence those).
- Do NOT delegate trivial single-file edits.
- After a sub-agent returns, ALWAYS verify its output (read the produced files, run a build/test) before marking the task complete.
- Tell sub-agents explicitly whether they should write code or only research.

---

## 4. Read-Only Constraint — NON-NEGOTIABLE

The application is **STRICTLY READ-ONLY** with respect to any EXT source. The following rules are inviolable:

1. **Physical disk handles** must be opened with `GENERIC_READ` and `FILE_SHARE_READ` only. Never `GENERIC_WRITE`, never `FILE_SHARE_WRITE`. Never `OPEN_ALWAYS` or `CREATE_ALWAYS` — always `OPEN_EXISTING`.
2. **Image file handles** must be opened with `FileAccess.Read` and `FileShare.Read` only.
3. **`libext2fs`** must always be opened with `EXT2_FLAG_RDONLY`. The wrapper must refuse to pass any other flag.
4. **No WinFSP write paths:** even if WinFSP is added in a future enhancement, it must mount with `VolumeParams.ReadOnly = true` and a write-protection callback that denies every write IRP.
5. **Defense in depth:** the application enforces read-only at *three* independent layers — handle flags (kernel), libext2fs flags (library), and application policy (code review + startup self-check).
6. **Self-check:** at startup, the application must verify it cannot open a write handle to a target device; if it can, it must refuse to operate and log a safety violation.
7. **Code review gate:** any PR touching `*Disk*`, `*Ext*`, `*Transfer*`, or `*Mount*` files requires explicit verification that no `GENERIC_WRITE` / `Write` / `EXT2_FLAG_RW` symbol appears.

If at any point a write path appears to be required to satisfy a feature, STOP and escalate to the Product Owner. Do not improvise.

---

## 5. Update Mandate

At the end of every significant task or feature implementation, I MUST update:

1. **`project_context.md`** — architecture summary, current state, layer status, external prerequisites, and any decisions revised since the last update.
2. **`roadmap.md`** — check off completed items, add new sub-tasks discovered, and adjust dates/notes.

A "significant task" includes: completing a Phase, landing a new service, changing a tech-stack choice, adding/removing an external dependency, or discovering a safety or compatibility issue.

Update `memory.md` only when operational rules themselves change.

---

## 6. Coding Conventions

- **No comments** unless the user explicitly asks for them. Code must be self-documenting through clear naming.
- **C# / .NET 8**: file-scoped namespaces, nullable reference types enabled, `var` only when the type is obvious, async methods suffixed with `Async`.
- **MVVM**: ViewModels use `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). No business logic in code-behind.
- **Cancellation**: every async operation that can be cancelled takes a `CancellationToken`. Copy/streaming operations must respect it promptly (within one chunk).
- **Progress reporting**: use `IProgress<T>` with a value-type `T` (struct) to avoid allocations.
- **Interop**: P/Invoke signatures live in a single `internal static partial class NativeMethods` per native library. Use `LibraryImport` (source-generated) over `DllImport` where possible.
- **Error handling**: throw typed exceptions (`ExtFsException`, `DiskAccessDeniedException`); never swallow. Surface to UI via `InfoBar` with the exception message.
- **Naming**: `PascalCase` for public members, `_camelCase` for private fields, `I` prefix for interfaces.
- **Files**: one public type per file. File name matches type name.
- **Tests**: xUnit. Naming `MethodName_Condition_ExpectedResult`.

---

## 7. Acceptance Criteria for "Done"

A feature is done only when ALL of the following are true:

- Code compiles cleanly with `dotnet build` (treat warnings as errors in `Release`).
- No `GENERIC_WRITE`, `FileAccess.Write`, `EXT2_FLAG_RW`, or any write-capable symbol appears in disk/EXT/transfer code (verified by `grep` audit).
- MVVM boundary respected (no `MessageBox.Show` from services; services do not reference WPF/UI types).
- Cancellation propagates correctly.
- **Portability:** the app publishes as a single-file self-contained executable with no installer, no framework package, no MSIX. Verify `PublishSingleFile=true`, `SelfContained=true`, `IncludeNativeLibrariesForSelfExtract=true`.
- `roadmap.md` and `project_context.md` updated.
- If the feature is user-facing, the Product Owner has reviewed it.

---

## 8. Session Protocol

1. At session start: read `memory.md`, `project_context.md`, and `roadmap.md` to restore context.
2. Pick the next pending roadmap item and mark it `in_progress` in the todo list.
3. Execute; if the task is in the Deep-Thinking list (Section 2), reason carefully before touching code.
4. Verify (build + safety grep + tests where applicable).
5. Update `project_context.md` and `roadmap.md`.
6. Mark todo complete and report concisely to the Product Owner.

---

## 9. Working Directory & Tooling

- Workspace root: `C:\Users\Stavros\OneDrive\My AI Apps\EXTReader`
- Shell: PowerShell 7+ (`pwsh`). Use full cmdlet names (`Get-ChildItem`, `New-Item`).
- OS: Windows 11 Enterprise Build 26200.
- Toolchain confirmed present: .NET 8 SDK (`dotnet 8.0.423`), Rust 1.97, Python 3.12, Node 24, WSL2 (Ubuntu 26.04).
- Toolchain NOT yet installed: WinFSP (deferred — only needed if drive-letter mount feature is ever approved), MSYS2/MinGW (needed for `libext2fs` build — install in Phase 0).

---

## 10. Decision Log (most recent first)

- **2026-08-01** — Phase 5 COMPLETE. Final phase. `SafetySelfCheck` service runs at startup verifying manifest, libext2fs version, and no write constants. `scripts/safety-audit.ps1` static grep script (excludes `SafetySelfCheck.cs` and `Ext2fsConstants.cs` which contain symbol names as warning text/constant definitions, never used in actual calls). `scripts/build.ps1` 6-step pipeline (clean/restore with `-r win-x64`/build/publish/copy DLLs/zip). Key fix: `dotnet restore` and `dotnet build` needed `-r win-x64` explicitly because `RuntimeIdentifier` is in the Release-only condition group. Published: 163 MB single-file self-contained exe + 2.6 MB libext2fs.dll + 66 KB libwinpthread-1.dll, zipped to 69.2 MB. Smoke test passed (exe launches). `README.md` created. **ALL 5 PHASES COMPLETE — project ready for release.**
- **2026-08-01** — Phase 4 COMPLETE. Built `FileTransferService` with `CopyFileAsync` and `CopyDirectoryAsync`. Key decisions: (1) `CopyProgress` as readonly record struct for immutable progress snapshots with `with` expression support, (2) `CollisionPolicy` enum with `ResolveCollision` helper, (3) Long-path support via `\\?\` prefix when path ≥260 chars, (4) `CopyProgressDialog` uses `async void Window_Loaded` to start copy on display, shows ProgressBar bound to `Percent`, Cancel button triggers `CancellationTokenSource.Cancel()` which propagates through all copy operations, (5) `CollectFiles` recursive walker uses `ListDirectory` + `GetInode` (not `ext2fs_dir_iterate` directly) for consistency and to reuse existing tested code paths. Added `ExtFileSystemService.GetHandle()` (exposes `_fs` IntPtr) and `GetFileSize(ino)` (opens file temporarily, reads size, closes) for the transfer service. End-to-end test: all 5 cases pass including recursive directory copy of 3 files preserving folder structure, and 301-char long path support.
- **2026-08-01** — Phase 3 COMPLETE. Built file browser UI using Explorer-style list navigation (not TreeView) — simpler UX and less error-prone than lazy-loaded TreeView with P/Invoke. `BrowserWindow` (FluentWindow with Mica), `BrowserViewModel` (Stack-based navigation), `FileItemViewModel` (display wrapper with Segoe MDL2 icons). Separated `ExtractToAsync(ino, name, destPath)` from `ExtractAsync()` (which shows SaveFileDialog) for testability — dialog logic can't run in console test harness. Wired `SourcesViewModel.BrowseRequested` event → `MainWindow` opens `BrowserWindow`. End-to-end test: all 5 cases pass, extraction byte-identical (SHA256 verified).
- **2026-08-01** — Phase 2 COMPLETE. Built `ExtFileSystemService` wrapping `libext2fs`. Key discovery: `windows_io.c` supports the `"offset=N"` io_option natively, eliminating the need for a custom I/O manager — partition offsets are handled by passing `io_options="offset=N"` to `ext2fs_open2`. Rebuilt `libext2fs.dll` with a `.def` file to export the `windows_io_manager` data symbol (was previously not exported since data symbols need explicit `.def` listing). `ext2fs_dir_iterate` uses a `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` callback with `GCHandle`-pinned `List<ExtDirEntry>` as priv_data. `ext2fs_lookup`/`ext2fs_namei` crash with AccessViolation due to string marshalling issues — replaced with a managed `LookupPath` that uses `ListDirectory` + linear scan (simpler and safer). `ext2_inode` struct (128 bytes) marshalled via `Marshal.ReadInt16/32` at documented offsets. Test image: 10 MB EXT4 created via WSL2 `mkfs.ext4` with `hello.txt`, `test.txt`, `subdir/nested.txt`. All 6 test cases pass. Safety audit: only `EXT2_FLAG_64BITS` in open calls, `flags=0` in file_open calls.
- **2026-08-01** — Phase 1 COMPLETE. Implemented admin elevation (`asInvoker` manifest + `AdminRightsService`), drive discovery (`DriveDiscoveryService` with `CreateFileW(GENERIC_READ)` + `DeviceIoControl`), partition parsing (`PartitionParser` for MBR + GPT), EXT detection (`ExtDetector` — magic `0xEF53` + feature flags for Ext2/3/4), image file service (read-only `FileStream`), and WPF-UI FluentWindow UI with Mica backdrop. Safety audit: zero write symbols in source code. Both builds pass.
- **2026-08-01** — Phase 0 COMPLETE. Built `libext2fs.dll` (v1.47.4) from e2fsprogs source using MSYS2 MinGW. Required compat shims (arpa/inet.h, grp.h, pwd.h, fcntl.h, unistd.h, etc.) + stub functions (getuid, pread, pwrite, fsync, select, sysconf, fcntl, makedev). Used `windows_io.c` instead of `unix_io.c` for native Win32 I/O. P/Invoke smoke test passed. 630 ext2fs_ exports. Dependency: libwinpthread-1.dll (bundled).

- **2026-08-01** — Stack revised for portability: GUI framework swapped WinUI 3 → **WPF + WPF-UI (Lepoco.WpfUi)**. WinUI 3 is optimized for MSIX-packaged deployment and single-exe portable publish is fragile; WPF single-file self-contained publish is rock-solid and WPF-UI provides Win11 Fluent styling. Python rejected since C# already meets the portability requirement and offers stronger libext2fs interop safety. All other architecture decisions unchanged.
- **2026-08-01** — Tech stack approved: C# .NET 8 + WinUI 3 (WinAppSDK) + libext2fs P/Invoke. WinFSP dropped from initial scope (in-app browse + copy only). Physical disks AND image files both supported. libext2fs accepted as native dependency.
- **2026-08-01** — Project initiated by Product Owner.
