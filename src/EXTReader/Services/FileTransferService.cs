using System.IO;
using System.Runtime.InteropServices;
using EXTReader.Interop;
using EXTReader.Models;

namespace EXTReader.Services;

public sealed class FileTransferService
{
    private readonly ExtFileSystemService _ext;
    private const int ChunkSize = 1 << 20;

    public FileTransferService(ExtFileSystemService ext)
    {
        _ext = ext ?? throw new ArgumentNullException(nameof(ext));
    }

    public async Task CopyFileAsync(uint ino, string destPath, IProgress<CopyProgress>? progress, CopyProgress ctx, CancellationToken ct = default)
    {
        long size = _ext.GetFileSize(ino);

        string longDest = ToLongPath(destPath);
        string? dir = Path.GetDirectoryName(longDest);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        int err = NativeExt2fs.ext2fs_file_open(_ext.GetHandle(), ino, 0, out IntPtr file);
        if (err != 0)
            throw new ExtFsException($"ext2fs_file_open failed for inode {ino} with error {err}.", err);

        try
        {
            byte[] managedBuf = new byte[ChunkSize];
            var pin = GCHandle.Alloc(managedBuf, GCHandleType.Pinned);
            try
            {
                IntPtr bufPtr = pin.AddrOfPinnedObject();
                await using var destStream = new FileStream(longDest, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize, useAsync: true);
                long copied = 0;
                long totalBytes = ctx.TotalBytes;
                long startBytes = ctx.BytesCopied;

                while (copied < size)
                {
                    ct.ThrowIfCancellationRequested();

                    uint toRead = (uint)Math.Min(size - copied, ChunkSize);
                    int readErr = NativeExt2fs.ext2fs_file_read(file, bufPtr, toRead, out uint got);

                    if (readErr != 0)
                        throw new ExtFsException($"ext2fs_file_read failed at offset {copied} with error {readErr}.", readErr);

                    if (got == 0)
                        break;

                    await destStream.WriteAsync(managedBuf.AsMemory(0, (int)got), ct);

                    copied += got;
                    progress?.Report(ctx with { BytesCopied = startBytes + copied, TotalBytes = totalBytes });
                }
            }
            finally
            {
                pin.Free();
            }
        }
        finally
        {
            NativeExt2fs.ext2fs_file_close(file);
        }
    }

    public async Task<int> CopyDirectoryAsync(uint rootIno, string destDir, IProgress<CopyProgress>? progress, CollisionPolicy policy, CancellationToken ct = default)
    {
        var files = new List<(uint ino, string relPath, long size)>();
        CollectFiles(rootIno, "", files, ct);

        long totalBytes = files.Sum(f => f.size);
        long copiedBytes = 0;
        int filesDone = 0;

        foreach (var (ino, relPath, size) in files)
        {
            ct.ThrowIfCancellationRequested();

            string destPath = Path.Combine(destDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            string? parent = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(ToLongPath(parent));

            var resolution = ResolveCollision(destPath, policy);
            if (resolution == CollisionResolution.Skipped)
            {
                filesDone++;
                continue;
            }

            string finalDest = resolution == CollisionResolution.Renamed
                ? GetRenamedPath(destPath)
                : destPath;

            var ctx = new CopyProgress(copiedBytes, totalBytes, relPath, filesDone, files.Count);
            await CopyFileAsync(ino, finalDest, progress, ctx, ct);

            copiedBytes += size;
            filesDone++;
            progress?.Report(new CopyProgress(copiedBytes, totalBytes, relPath, filesDone, files.Count));
        }

        return filesDone;
    }

    private void CollectFiles(uint dirIno, string relPath, List<(uint ino, string relPath, long size)> files, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var entries = _ext.ListDirectory(dirIno);
        foreach (var entry in entries)
        {
            string childRel = string.IsNullOrEmpty(relPath) ? entry.Name : $"{relPath}/{entry.Name}";

            if (entry.IsDirectory)
            {
                CollectFiles(entry.Inode, childRel, files, ct);
            }
            else if (entry.IsRegular)
            {
                long size = 0;
                try { size = _ext.GetFileSize(entry.Inode); }
                catch { }
                files.Add((entry.Inode, childRel, size));
            }
        }
    }

    private static CollisionResolution ResolveCollision(string destPath, CollisionPolicy policy)
    {
        if (!File.Exists(ToLongPath(destPath)))
            return CollisionResolution.Overwrote;

        return policy switch
        {
            CollisionPolicy.Skip => CollisionResolution.Skipped,
            CollisionPolicy.Overwrite => CollisionResolution.Overwrote,
            CollisionPolicy.Rename => CollisionResolution.Renamed,
            _ => CollisionResolution.Skipped,
        };
    }

    private static string GetRenamedPath(string path)
    {
        string dir = Path.GetDirectoryName(path) ?? "";
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int n = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({n}){ext}");
            n++;
        } while (File.Exists(ToLongPath(candidate)));
        return candidate;
    }

    public static string ToLongPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
        if (path.Length < 260) return path;

        string full = Path.GetFullPath(path);
        if (!full.StartsWith(@"\\?\", StringComparison.Ordinal))
            full = @"\\?\" + full;
        return full;
    }
}
