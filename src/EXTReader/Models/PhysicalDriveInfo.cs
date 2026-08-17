namespace EXTReader.Models;

public sealed class PhysicalDriveInfo
{
    public int Index { get; init; }
    public string DevicePath { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public long Size { get; init; }
    public uint SectorSize { get; init; }
    public PartitionTableType PartitionTable { get; init; }
    public IReadOnlyList<PartitionInfo> Partitions { get; init; } = Array.Empty<PartitionInfo>();
}
