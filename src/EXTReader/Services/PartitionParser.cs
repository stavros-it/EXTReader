using System.Buffers.Binary;
using System.IO;
using EXTReader.Models;

namespace EXTReader.Services;

public static class PartitionParser
{
    private static readonly Guid LinuxFilesystemGuid = new("0FC63DAF-8483-4772-8E79-3D69D8477DE4");

    private const byte MbrPartitionTypeLinux = 0x83;
    private const byte MbrPartitionTypeGptProtective = 0xEE;

    public static PartitionTableType DetectTableType(byte[] firstSector)
    {
        if (!IsMbrValid(firstSector))
            return PartitionTableType.None;

        if (HasGptProtectiveMbr(firstSector))
            return PartitionTableType.Gpt;

        return PartitionTableType.Mbr;
    }

    public static List<PartitionInfo> Parse(byte[] firstSector, Stream stream, uint sectorSize)
    {
        if (!IsMbrValid(firstSector))
        {
            return new List<PartitionInfo>();
        }

        if (HasGptProtectiveMbr(firstSector))
        {
            return ParseGpt(stream, sectorSize);
        }

        return ParseMbr(firstSector, sectorSize);
    }

    private static bool IsMbrValid(byte[] sector)
    {
        return sector.Length >= 512
            && sector[510] == 0x55
            && sector[511] == 0xAA;
    }

    private static bool HasGptProtectiveMbr(byte[] sector)
    {
        for (int i = 0; i < 4; i++)
        {
            int offset = 446 + (i * 16);
            if (offset + 16 > sector.Length) break;
            byte type = sector[offset + 4];
            if (type == MbrPartitionTypeGptProtective)
                return true;
        }
        return false;
    }

    private static List<PartitionInfo> ParseMbr(byte[] sector, uint sectorSize)
    {
        var partitions = new List<PartitionInfo>();

        for (int i = 0; i < 4; i++)
        {
            int entryOffset = 446 + (i * 16);
            byte partitionType = sector[entryOffset + 4];

            if (partitionType == 0) continue;

            uint startLba = BinaryPrimitives.ReadUInt32LittleEndian(
                sector.AsSpan(entryOffset + 8, 4));
            uint sectorCount = BinaryPrimitives.ReadUInt32LittleEndian(
                sector.AsSpan(entryOffset + 12, 4));

            if (sectorCount == 0) continue;

            partitions.Add(new PartitionInfo
            {
                Index = i + 1,
                StartOffset = (long)startLba * sectorSize,
                Size = (long)sectorCount * sectorSize,
                MbrPartitionType = partitionType,
                TypeDescription = GetMbrTypeDescription(partitionType),
            });
        }

        return partitions;
    }

    private static List<PartitionInfo> ParseGpt(Stream stream, uint sectorSize)
    {
        var partitions = new List<PartitionInfo>();

        Span<byte> headerBuf = stackalloc byte[92];
        stream.Seek(sectorSize, SeekOrigin.Begin);
        if (stream.Read(headerBuf) < 92) return partitions;

        ReadOnlySpan<byte> sig = "EFI PART"u8;
        if (!headerBuf.Slice(0, 8).SequenceEqual(sig)) return partitions;

        ulong partitionEntryLba = BinaryPrimitives.ReadUInt64LittleEndian(headerBuf.Slice(72, 8));
        uint numEntries = BinaryPrimitives.ReadUInt32LittleEndian(headerBuf.Slice(80, 4));
        uint entrySize = BinaryPrimitives.ReadUInt32LittleEndian(headerBuf.Slice(84, 4));

        if (entrySize == 0 || entrySize > 512) return partitions;
        if (numEntries == 0 || numEntries > 256) return partitions;

        long entriesOffset = (long)partitionEntryLba * sectorSize;
        stream.Seek(entriesOffset, SeekOrigin.Begin);

        byte[] entryBuffer = new byte[entrySize];

        for (int i = 0; i < numEntries; i++)
        {
            stream.Seek(entriesOffset + (i * entrySize), SeekOrigin.Begin);
            if (stream.Read(entryBuffer, 0, (int)entrySize) < (int)entrySize) break;

            bool allZero = true;
            for (int b = 0; b < 16; b++)
            {
                if (entryBuffer[b] != 0) { allZero = false; break; }
            }
            if (allZero) continue;

            Guid typeGuid = new Guid(entryBuffer.AsSpan(0, 16).ToArray());
            ulong startLba = BinaryPrimitives.ReadUInt64LittleEndian(entryBuffer.AsSpan(32, 8));
            ulong endLba = BinaryPrimitives.ReadUInt64LittleEndian(entryBuffer.AsSpan(40, 8));

            if (endLba < startLba) continue;

            partitions.Add(new PartitionInfo
            {
                Index = i + 1,
                StartOffset = (long)startLba * sectorSize,
                Size = (long)(endLba - startLba + 1) * sectorSize,
                GptTypeGuid = typeGuid,
                TypeDescription = GetGptTypeDescription(typeGuid),
            });
        }

        return partitions;
    }

    private static string GetMbrTypeDescription(byte type)
    {
        return type switch
        {
            0x83 => "Linux filesystem",
            0x07 => "NTFS/exFAT",
            0x0B or 0x0C => "FAT32",
            0x05 or 0x0F => "Extended",
            0xEE => "GPT protective",
            _ => $"0x{type:X2}",
        };
    }

    private static string GetGptTypeDescription(Guid typeGuid)
    {
        if (typeGuid == LinuxFilesystemGuid)
            return "Linux filesystem";
        return typeGuid.ToString().ToUpperInvariant();
    }
}
