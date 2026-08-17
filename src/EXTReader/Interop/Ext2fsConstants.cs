namespace EXTReader.Interop;

internal static class Ext2fsConstants
{
    public const int FlagRw = 0x01;
    public const int Flag64Bits = 0x20000;
    public const int FlagImageFile = 0x2000;
    public const int FlagNoFreeOnError = 0x10000;

    public const int ReadOnlyFlags = Flag64Bits;

    public const uint RootIno = 2;

    public const int DirIterateVoid = 0;
    public const int DirIterateBlockbuf = 1;

    public const ushort SIfDir = 0x4000;
    public const ushort SIfReg = 0x8000;
    public const ushort SIfLnk = 0xA000;
    public const ushort SIfChr = 0x2000;
    public const ushort SIfBlk = 0x6000;
    public const ushort SIfFifo = 0x1000;
    public const ushort SIfSock = 0xC000;

    public const int InodeSize = 128;
    public const int NBlocks = 15;

    public const int IoManagerSize = 184;
    public const int IoChannelSize = 136;

    public const int DirEntryFixedHeaderSize = 8;
    public const int MaxNameLen = 255;

    public const int SeekSet = 0;
    public const int SeekCur = 1;
    public const int SeekEnd = 2;
}
