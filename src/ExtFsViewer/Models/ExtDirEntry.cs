namespace ExtFsViewer.Models;

public sealed class ExtDirEntry
{
    public uint Inode { get; init; }
    public string Name { get; init; } = string.Empty;
    public ExtFileType FileType { get; init; }
    public bool IsDirectory => FileType == ExtFileType.Directory;
    public bool IsRegular => FileType == ExtFileType.Regular;
    public bool IsSymlink => FileType == ExtFileType.Symlink;
}
