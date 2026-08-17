using System.Buffers.Binary;
using System.IO;
using EXTReader.Models;

namespace EXTReader.Services;

public static class ExtDetector
{
    private const int SuperblockOffset = 1024;
    private const int MagicOffset = 56;
    private const ushort ExtMagic = 0xEF53;

    private const int RevLevelOffset = 76;
    private const int FeatureCompatOffset = 92;
    private const int FeatureIncompatOffset = 96;

    private const int Ext3FeatureCompatHasJournal = 0x0004;
    private const int Ext4FeatureIncompatExtents = 0x0040;
    private const int Ext4FeatureIncompat64Bit = 0x0080;

    public static FileSystemType Detect(Stream stream, long partitionStart)
    {
        long magicPos = partitionStart + SuperblockOffset + MagicOffset;

        stream.Seek(magicPos, SeekOrigin.Begin);
        Span<byte> magicBuf = stackalloc byte[2];
        if (stream.Read(magicBuf) < 2)
            return FileSystemType.Unknown;

        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(magicBuf);
        if (magic != ExtMagic)
            return FileSystemType.Unknown;

        long revPos = partitionStart + SuperblockOffset + RevLevelOffset;
        stream.Seek(revPos, SeekOrigin.Begin);
        Span<byte> revBuf = stackalloc byte[4];
        if (stream.Read(revBuf) < 4)
            return FileSystemType.Unknown;

        uint revLevel = BinaryPrimitives.ReadUInt32LittleEndian(revBuf);
        if (revLevel == 0)
            return FileSystemType.Ext2;

        long compatPos = partitionStart + SuperblockOffset + FeatureCompatOffset;
        stream.Seek(compatPos, SeekOrigin.Begin);
        Span<byte> features = stackalloc byte[8];
        if (stream.Read(features) < 8)
            return FileSystemType.Ext2;

        uint compat = BinaryPrimitives.ReadUInt32LittleEndian(features.Slice(0, 4));
        uint incompat = BinaryPrimitives.ReadUInt32LittleEndian(features.Slice(4, 4));

        bool hasExtents = (incompat & Ext4FeatureIncompatExtents) != 0;
        bool has64Bit = (incompat & Ext4FeatureIncompat64Bit) != 0;
        bool hasJournal = (compat & Ext3FeatureCompatHasJournal) != 0;

        if (hasExtents || has64Bit)
            return FileSystemType.Ext4;

        if (hasJournal)
            return FileSystemType.Ext3;

        return FileSystemType.Ext2;
    }
}
