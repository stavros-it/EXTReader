using CommunityToolkit.Mvvm.ComponentModel;
using ExtFsViewer.Models;

namespace ExtFsViewer.ViewModels;

public partial class FileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private string _sizeFormatted = string.Empty;

    [ObservableProperty]
    private DateTime? _modified;

    [ObservableProperty]
    private string _modifiedFormatted = string.Empty;

    [ObservableProperty]
    private string _permissions = string.Empty;

    [ObservableProperty]
    private uint _inode;

    [ObservableProperty]
    private ExtFileType _fileType;

    [ObservableProperty]
    private ushort _mode;

    [ObservableProperty]
    private ushort _uid;

    [ObservableProperty]
    private ushort _gid;

    [ObservableProperty]
    private uint _blockCount;

    [ObservableProperty]
    private uint _flags;

    [ObservableProperty]
    private ushort _linksCount;

    [ObservableProperty]
    private DateTime? _accessTime;

    [ObservableProperty]
    private DateTime? _changeTime;

    [ObservableProperty]
    private DateTime? _deleteTime;

    public bool IsDirectory => FileType == ExtFileType.Directory;
    public bool IsRegular => FileType == ExtFileType.Regular;
    public bool IsSymlink => FileType == ExtFileType.Symlink;

    public string IconGlyph => FileType switch
    {
        ExtFileType.Directory => "\uE8B7",
        ExtFileType.Symlink => "\uE71D",
        ExtFileType.Regular => "\uE8A5",
        ExtFileType.Fifo => "\uE968",
        ExtFileType.Socket => "\uF158",
        ExtFileType.Block => "\uE7C1",
        ExtFileType.Character => "\uE7C0",
        _ => "\uE7C3",
    };

    public string TypeLabel => FileType switch
    {
        ExtFileType.Directory => "Folder",
        ExtFileType.Regular => "File",
        ExtFileType.Symlink => "Symlink",
        ExtFileType.Fifo => "FIFO",
        ExtFileType.Socket => "Socket",
        ExtFileType.Block => "Block device",
        ExtFileType.Character => "Char device",
        _ => "Unknown",
    };

    public static FileItemViewModel FromEntry(ExtDirEntry entry, ExtInodeInfo? inode)
    {
        ExtFileType fileType = entry.FileType;

        if (fileType == ExtFileType.Unknown && inode != null)
        {
            fileType = (inode.Mode & 0xF000) switch
            {
                0x4000 => ExtFileType.Directory,
                0x8000 => ExtFileType.Regular,
                0xA000 => ExtFileType.Symlink,
                0x2000 => ExtFileType.Character,
                0x6000 => ExtFileType.Block,
                0x1000 => ExtFileType.Fifo,
                0xC000 => ExtFileType.Socket,
                _ => ExtFileType.Unknown,
            };
        }

        var vm = new FileItemViewModel
        {
            Name = entry.Name,
            Inode = entry.Inode,
            FileType = fileType,
        };

        if (inode != null)
        {
            vm.Size = inode.Size;
            vm.SizeFormatted = inode.Size >= 0 ? FormatSize(inode.Size) : "—";
            vm.Modified = inode.ModifyTime;
            vm.ModifiedFormatted = inode.ModifyTime?.ToString("yyyy-MM-dd HH:mm") ?? "—";
            vm.Permissions = inode.Permissions;
            vm.Mode = inode.Mode;
            vm.Uid = inode.Uid;
            vm.Gid = inode.Gid;
            vm.BlockCount = inode.Blocks;
            vm.Flags = inode.Flags;
            vm.LinksCount = inode.LinksCount;
            vm.AccessTime = inode.AccessTime;
            vm.ChangeTime = inode.ChangeTime;
            vm.DeleteTime = inode.DeleteTime;
        }

        return vm;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
