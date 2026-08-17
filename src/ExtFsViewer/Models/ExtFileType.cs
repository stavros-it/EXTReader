namespace ExtFsViewer.Models;

public enum ExtFileType
{
    Unknown = 0,
    Regular = 1,
    Directory = 2,
    Character = 3,
    Block = 4,
    Fifo = 5,
    Socket = 6,
    Symlink = 7,
}
