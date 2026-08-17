namespace ExtFsViewer.Models;

public sealed class ExtInodeInfo
{
    public uint Inode { get; init; }
    public ushort Mode { get; init; }
    public ushort Uid { get; init; }
    public ushort Gid { get; init; }
    public long Size { get; init; }
    public uint Blocks { get; init; }
    public uint Flags { get; init; }
    public ushort LinksCount { get; init; }
    public DateTime? AccessTime { get; init; }
    public DateTime? ModifyTime { get; init; }
    public DateTime? ChangeTime { get; init; }
    public DateTime? DeleteTime { get; init; }

    public ExtFileType FileType => (Mode & 0xF000) switch
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

    public string Permissions
    {
        get
        {
            char type = FileType switch
            {
                ExtFileType.Directory => 'd',
                ExtFileType.Regular => '-',
                ExtFileType.Symlink => 'l',
                ExtFileType.Character => 'c',
                ExtFileType.Block => 'b',
                ExtFileType.Fifo => 'p',
                ExtFileType.Socket => 's',
                _ => '?',
            };

            ushort p = (ushort)(Mode & 0x0FFF);
            char uR = (p & 0x100) != 0 ? 'r' : '-';
            char uW = (p & 0x080) != 0 ? 'w' : '-';
            char uX = (p & 0x040) != 0 ? 'x' : '-';
            char gR = (p & 0x020) != 0 ? 'r' : '-';
            char gW = (p & 0x010) != 0 ? 'w' : '-';
            char gX = (p & 0x008) != 0 ? 'x' : '-';
            char oR = (p & 0x004) != 0 ? 'r' : '-';
            char oW = (p & 0x002) != 0 ? 'w' : '-';
            char oX = (p & 0x001) != 0 ? 'x' : '-';
            return $"{type}{uR}{uW}{uX}{gR}{gW}{gX}{oR}{oW}{oX}";
        }
    }
}
