using System.Runtime.InteropServices;

namespace ExtFsViewer.Interop;

internal static partial class NativeExt2fs
{
    private const string Lib = "libext2fs.dll";

    [LibraryImport(Lib)]
    public static partial int ext2fs_get_library_version(out IntPtr ver_string, out IntPtr date_string);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int ext2fs_open(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int flags,
        int superblock,
        uint block_size,
        IntPtr manager,
        out IntPtr fs);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int ext2fs_open2(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        [MarshalAs(UnmanagedType.LPStr)] string? io_options,
        int flags,
        int superblock,
        uint block_size,
        IntPtr manager,
        out IntPtr fs);

    [LibraryImport(Lib)]
    public static partial int ext2fs_close(IntPtr fs);

    [LibraryImport(Lib)]
    public static partial int ext2fs_close_free(ref IntPtr fs);

    [LibraryImport(Lib)]
    public static partial int ext2fs_check_desc(IntPtr fs);

    [LibraryImport(Lib)]
    public static partial int ext2fs_read_inode(IntPtr fs, uint ino, IntPtr inode);

    [LibraryImport(Lib)]
    public static partial int ext2fs_read_inode_full(IntPtr fs, uint ino, IntPtr inode, int bufsize);

    [LibraryImport(Lib)]
    public static partial int ext2fs_dir_iterate(
        IntPtr fs,
        uint dir,
        int flags,
        IntPtr block_buf,
        IntPtr func,
        IntPtr priv_data);

    [LibraryImport(Lib)]
    public static partial int ext2fs_file_open(IntPtr fs, uint ino, int flags, out IntPtr file);

    [LibraryImport(Lib)]
    public static partial int ext2fs_file_close(IntPtr file);

    [LibraryImport(Lib)]
    public static partial int ext2fs_file_read(IntPtr file, IntPtr buf, uint wanted, out uint got);

    [LibraryImport(Lib)]
    public static partial int ext2fs_file_lseek(IntPtr file, long offset, int whence, out long ret_pos);

    [LibraryImport(Lib)]
    public static partial long ext2fs_file_get_size(IntPtr file);

    [LibraryImport(Lib)]
    public static partial IntPtr ext2fs_file_get_inode(IntPtr file);

    [LibraryImport(Lib)]
    public static partial uint ext2fs_file_get_inode_num(IntPtr file);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int ext2fs_namei(
        IntPtr fs,
        uint root,
        uint cwd,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        out uint ino);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int ext2fs_lookup(
        IntPtr fs,
        uint dir,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        int namelen,
        out uint ino,
        IntPtr buf);

    [LibraryImport(Lib)]
    public static partial int ext2fs_dirent_name_len(IntPtr dirent);

    [LibraryImport(Lib)]
    public static partial int ext2fs_dirent_file_type(IntPtr dirent);

    [LibraryImport(Lib)]
    public static partial void ext2fs_free(IntPtr fs);
}
