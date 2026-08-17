namespace ExtFsViewer.Models;

public sealed class ExtSource
{
    public string DisplayName { get; init; } = string.Empty;
    public SourceType Type { get; init; }
    public string BackingPath { get; init; } = string.Empty;
    public long Offset { get; init; }
    public long Size { get; init; }
    public FileSystemType FileSystem { get; init; }

    public string SizeFormatted =>
        Size switch
        {
            >= 1L << 40 => $"{Size / (double)(1L << 40):F2} TB",
            >= 1L << 30 => $"{Size / (double)(1L << 30):F2} GB",
            >= 1L << 20 => $"{Size / (double)(1L << 20):F2} MB",
            >= 1L << 10 => $"{Size / (double)(1L << 10):F2} KB",
            _ => $"{Size} B",
        };
}
