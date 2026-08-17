namespace EXTReader.Models;

public readonly record struct CopyProgress(
    long BytesCopied,
    long TotalBytes,
    string CurrentFile,
    int FilesDone,
    int FilesTotal)
{
    public double Percent => TotalBytes > 0 ? (double)BytesCopied / TotalBytes * 100.0 : 0;

    public string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public string BytesCopiedFormatted => FormatBytes(BytesCopied);
    public string TotalBytesFormatted => FormatBytes(TotalBytes);
    public string Summary => $"{FilesDone}/{FilesTotal} files — {BytesCopiedFormatted} / {TotalBytesFormatted}";
}
