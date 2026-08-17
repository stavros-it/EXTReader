namespace ExtFsViewer.Models;

public sealed class PartitionInfo
{
    public int Index { get; init; }
    public long StartOffset { get; init; }
    public long Size { get; init; }
    public byte? MbrPartitionType { get; init; }
    public Guid? GptTypeGuid { get; init; }
    public string TypeDescription { get; init; } = string.Empty;
    public FileSystemType FileSystem { get; set; }
    public bool IsExt => FileSystem != FileSystemType.Unknown;
}
