using System.IO;
using ExtFsViewer.Models;

namespace ExtFsViewer.Services;

public sealed class ImageFileService
{
    public List<ExtSource> OpenImage(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Image file not found.", path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        byte[] firstSector = new byte[512];
        int read = stream.Read(firstSector, 0, 512);
        if (read < 512)
        {
            var fs = ExtDetector.Detect(stream, 0);
            if (fs != FileSystemType.Unknown)
            {
                return new List<ExtSource>
                {
                    new()
                    {
                        DisplayName = $"{Path.GetFileName(path)} ({fs})",
                        Type = SourceType.ImageFile,
                        BackingPath = path,
                        Offset = 0,
                        Size = stream.Length,
                        FileSystem = fs,
                    },
                };
            }
            return new List<ExtSource>();
        }

        bool hasMbr = firstSector[510] == 0x55 && firstSector[511] == 0xAA;

        if (!hasMbr)
        {
            var rawFs = ExtDetector.Detect(stream, 0);
            if (rawFs != FileSystemType.Unknown)
            {
                return new List<ExtSource>
                {
                    new()
                    {
                        DisplayName = $"{Path.GetFileName(path)} ({rawFs})",
                        Type = SourceType.ImageFile,
                        BackingPath = path,
                        Offset = 0,
                        Size = stream.Length,
                        FileSystem = rawFs,
                    },
                };
            }
            return new List<ExtSource>();
        }

        var partitions = PartitionParser.Parse(firstSector, stream, 512);
        var sources = new List<ExtSource>();

        foreach (var part in partitions)
        {
            if (!part.IsExt) continue;

            sources.Add(new ExtSource
            {
                DisplayName = $"{Path.GetFileName(path)} - Partition {part.Index} ({part.FileSystem})",
                Type = SourceType.ImageFile,
                BackingPath = path,
                Offset = part.StartOffset,
                Size = part.Size,
                FileSystem = part.FileSystem,
            });
        }

        return sources;
    }
}
