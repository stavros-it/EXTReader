using System.IO;
using System.Runtime.InteropServices;
using ExtFsViewer.Interop;
using ExtFsViewer.Models;

namespace ExtFsViewer.Services;

public sealed class ExtFileSystemService : IDisposable
{
    public const uint RootInode = 2;
    private IntPtr _fs = IntPtr.Zero;
    private IntPtr _ioManager = IntPtr.Zero;
    private IntPtr _libHandle = IntPtr.Zero;
    private bool _disposed;

    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public bool IsOpen => _fs != IntPtr.Zero;

    public IntPtr GetHandle() => _fs;

    public long GetFileSize(uint ino)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        int err = NativeExt2fs.ext2fs_file_open(_fs, ino, 0, out IntPtr file);
        if (err != 0)
            throw new ExtFsException($"ext2fs_file_open failed for inode {ino} with error {err}.", err);

        try
        {
            return NativeExt2fs.ext2fs_file_get_size(file);
        }
        finally
        {
            NativeExt2fs.ext2fs_file_close(file);
        }
    }

    public void Open(ExtSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_fs != IntPtr.Zero)
            throw new InvalidOperationException("Filesystem already open. Close it first.");

        _ioManager = GetWindowsIoManager();

        int flags = Ext2fsConstants.ReadOnlyFlags;

        string ioOptions = source.Offset > 0
            ? $"offset={source.Offset}"
            : string.Empty;

        int err = NativeExt2fs.ext2fs_open2(
            source.BackingPath,
            ioOptions.Length > 0 ? ioOptions : null,
            flags,
            0,
            0,
            _ioManager,
            out _fs);

        if (err != 0)
        {
            _fs = IntPtr.Zero;
            throw new ExtFsException($"ext2fs_open2 failed with error code {err} (0x{err:X8}).", err);
        }

        int checkErr = NativeExt2fs.ext2fs_check_desc(_fs);
        if (checkErr != 0)
        {
            NativeExt2fs.ext2fs_close_free(ref _fs);
            throw new ExtFsException($"ext2fs_check_desc failed: filesystem group descriptors are corrupted. Error {checkErr}.", checkErr);
        }
    }

    public void Close()
    {
        if (_fs != IntPtr.Zero)
        {
            NativeExt2fs.ext2fs_close_free(ref _fs);
            _fs = IntPtr.Zero;
        }
    }

    public List<ExtDirEntry> ListDirectory(uint dirIno)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        var entries = new List<ExtDirEntry>();
        var handle = GCHandle.Alloc(entries);

        try
        {
            var cb = new DirIterateCallback(DirIterateCallbackImpl);
            GCHandle cbHandle = GCHandle.Alloc(cb);

            try
            {
                IntPtr cbPtr = Marshal.GetFunctionPointerForDelegate(cb);
                IntPtr dataPtr = GCHandle.ToIntPtr(handle);

                int err = NativeExt2fs.ext2fs_dir_iterate(
                    _fs, dirIno,
                    Ext2fsConstants.DirIterateBlockbuf,
                    IntPtr.Zero,
                    cbPtr,
                    dataPtr);

                if (err != 0)
                    throw new ExtFsException($"ext2fs_dir_iterate failed with error {err}.", err);
            }
            finally
            {
                cbHandle.Free();
            }
        }
        finally
        {
            handle.Free();
        }

        return entries;
    }

    public List<ExtDirEntry> ListRoot() => ListDirectory(Ext2fsConstants.RootIno);

    private int DirIterateCallbackImpl(IntPtr dirent, int offset, int blocksize, IntPtr buf, IntPtr privData)
    {
        if (dirent == IntPtr.Zero)
            return 0;

        uint inode = (uint)Marshal.ReadInt32(dirent, 0);
        if (inode == 0)
            return 0;

        int nameLen = NativeExt2fs.ext2fs_dirent_name_len(dirent);
        int fileType = NativeExt2fs.ext2fs_dirent_file_type(dirent);

        string name = nameLen > 0
            ? Marshal.PtrToStringAnsi(dirent + 8, nameLen) ?? string.Empty
            : string.Empty;

        if (name is "." or "..")
            return 0;

        var entry = new ExtDirEntry
        {
            Inode = inode,
            Name = name,
            FileType = (ExtFileType)fileType,
        };

        var handle = GCHandle.FromIntPtr(privData);
        if (handle.Target is List<ExtDirEntry> list)
            list.Add(entry);

        return 0;
    }

    public ExtInodeInfo GetInode(uint ino)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        IntPtr buf = Marshal.AllocHGlobal(Ext2fsConstants.InodeSize);
        try
        {
            int err = NativeExt2fs.ext2fs_read_inode_full(_fs, ino, buf, Ext2fsConstants.InodeSize);
            if (err != 0)
                throw new ExtFsException($"ext2fs_read_inode_full failed for inode {ino} with error {err}.", err);

            return ReadInodeFromBuffer(buf, ino);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static ExtInodeInfo ReadInodeFromBuffer(IntPtr buf, uint ino)
    {
        ushort mode = (ushort)Marshal.ReadInt16(buf, 0);
        ushort uid = (ushort)Marshal.ReadInt16(buf, 2);
        uint sizeLow = (uint)Marshal.ReadInt32(buf, 4);
        uint atime = (uint)Marshal.ReadInt32(buf, 8);
        uint ctime = (uint)Marshal.ReadInt32(buf, 12);
        uint mtime = (uint)Marshal.ReadInt32(buf, 16);
        uint dtime = (uint)Marshal.ReadInt32(buf, 20);
        ushort gid = (ushort)Marshal.ReadInt16(buf, 24);
        ushort links = (ushort)Marshal.ReadInt16(buf, 26);
        uint blocks = (uint)Marshal.ReadInt32(buf, 28);
        uint flags = (uint)Marshal.ReadInt32(buf, 32);

        uint sizeHigh = (uint)Marshal.ReadInt32(buf, 108);

        long size = ((long)sizeHigh << 32) | sizeLow;

        return new ExtInodeInfo
        {
            Inode = ino,
            Mode = mode,
            Uid = uid,
            Gid = gid,
            Size = size,
            Blocks = blocks,
            Flags = flags,
            LinksCount = links,
            AccessTime = atime > 0 ? Epoch.AddSeconds(atime).ToLocalTime() : null,
            ModifyTime = mtime > 0 ? Epoch.AddSeconds(mtime).ToLocalTime() : null,
            ChangeTime = ctime > 0 ? Epoch.AddSeconds(ctime).ToLocalTime() : null,
            DeleteTime = dtime > 0 ? Epoch.AddSeconds(dtime).ToLocalTime() : null,
        };
    }

    public byte[] ReadFile(uint ino, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        int err = NativeExt2fs.ext2fs_file_open(_fs, ino, 0, out IntPtr file);
        if (err != 0)
            throw new ExtFsException($"ext2fs_file_open failed for inode {ino} with error {err}.", err);

        try
        {
            long size = NativeExt2fs.ext2fs_file_get_size(file);
            if (size < 0 || size > int.MaxValue)
                throw new ExtFsException($"File size {size} is too large or invalid for inode {ino}.");

            byte[] buffer = new byte[size];
            if (size == 0)
                return buffer;

            IntPtr unmanagedBuf = Marshal.AllocHGlobal((int)size);
            try
            {
                uint remaining = (uint)size;
                uint offset = 0;

                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    uint toRead = Math.Min(remaining, 1u << 20);
                    int readErr = NativeExt2fs.ext2fs_file_read(file, unmanagedBuf + (int)offset, toRead, out uint got);

                    if (readErr != 0)
                        throw new ExtFsException($"ext2fs_file_read failed at offset {offset} with error {readErr}.", readErr);

                    if (got == 0)
                        break;

                    offset += got;
                    remaining -= got;
                }

                Marshal.Copy(unmanagedBuf, buffer, 0, (int)offset);
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedBuf);
            }

            return buffer;
        }
        finally
        {
            NativeExt2fs.ext2fs_file_close(file);
        }
    }

    public async Task CopyFileAsync(uint ino, string destPath, IProgress<(long copied, long total)>? progress, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        int err = NativeExt2fs.ext2fs_file_open(_fs, ino, 0, out IntPtr file);
        if (err != 0)
            throw new ExtFsException($"ext2fs_file_open failed for inode {ino} with error {err}.", err);

        try
        {
            long size = NativeExt2fs.ext2fs_file_get_size(file);
            if (size < 0)
                throw new ExtFsException($"Invalid file size {size} for inode {ino}.");

            const int chunkSize = 1 << 20;
            IntPtr unmanagedBuf = Marshal.AllocHGlobal(chunkSize);

            try
            {
                await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, chunkSize, useAsync: true);
                long copied = 0;

                while (copied < size)
                {
                    ct.ThrowIfCancellationRequested();

                    uint toRead = (uint)Math.Min(size - copied, chunkSize);
                    int readErr = NativeExt2fs.ext2fs_file_read(file, unmanagedBuf, toRead, out uint got);

                    if (readErr != 0)
                        throw new ExtFsException($"ext2fs_file_read failed at offset {copied} with error {readErr}.", readErr);

                    if (got == 0)
                        break;

                    byte[] managedBuf = new byte[got];
                    Marshal.Copy(unmanagedBuf, managedBuf, 0, (int)got);
                    await destStream.WriteAsync(managedBuf, 0, (int)got, ct);

                    copied += got;
                    progress?.Report((copied, size));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedBuf);
            }
        }
        finally
        {
            NativeExt2fs.ext2fs_file_close(file);
        }
    }

    public uint LookupPath(uint dirIno, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        var entries = ListDirectory(dirIno);
        foreach (var entry in entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                return entry.Inode;
        }

        throw new ExtFsException($"Entry '{name}' not found in directory inode {dirIno}.");
    }

    public uint ResolvePath(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fs == IntPtr.Zero)
            throw new InvalidOperationException("Filesystem not open.");

        if (path.StartsWith('/'))
            path = path[1..];

        if (string.IsNullOrEmpty(path))
            return Ext2fsConstants.RootIno;

        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        uint currentIno = Ext2fsConstants.RootIno;

        foreach (var part in parts)
        {
            currentIno = LookupPath(currentIno, part);
        }

        return currentIno;
    }

    private IntPtr GetWindowsIoManager()
    {
        if (_libHandle == IntPtr.Zero)
        {
            string baseDir = AppContext.BaseDirectory;
            string pthreadPath = Path.Combine(baseDir, "libwinpthread-1.dll");
            NativeKernel32.LoadLibrary(pthreadPath);

            string dllPath = Path.Combine(baseDir, "libext2fs.dll");
            _libHandle = NativeKernel32.LoadLibrary(dllPath);
            if (_libHandle == IntPtr.Zero)
                _libHandle = NativeKernel32.LoadLibrary("libext2fs.dll");
            if (_libHandle == IntPtr.Zero)
                throw new ExtFsException("Failed to load libext2fs.dll.");
        }

        IntPtr dataAddr = NativeKernel32.GetProcAddress(_libHandle, "windows_io_manager");
        if (dataAddr == IntPtr.Zero)
            throw new ExtFsException("Failed to find windows_io_manager export in libext2fs.dll.");

        IntPtr managerPtr = Marshal.ReadIntPtr(dataAddr);
        if (managerPtr == IntPtr.Zero)
            throw new ExtFsException("windows_io_manager pointer is null.");

        return managerPtr;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Close();
        _disposed = true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DirIterateCallback(IntPtr dirent, int offset, int blocksize, IntPtr buf, IntPtr privData);
}

public class ExtFsException : Exception
{
    public int ErrorCode { get; }

    public ExtFsException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public ExtFsException(string message) : base(message)
    {
        ErrorCode = 0;
    }
}
