using EXTReader.Models;
using EXTReader.Services;

string[] cmdArgs = Environment.GetCommandLineArgs();
int driveIndex = cmdArgs.Length > 1 ? int.Parse(cmdArgs[1]) : 5;

Console.WriteLine($"=== SanDisk EXT Browser Test (PhysicalDrive{driveIndex}) ===");
Console.WriteLine($"Running as: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");
Console.WriteLine();

try
{
    var driveService = new DriveDiscoveryService();
    Console.WriteLine("Discovering drives...");
    var drives = driveService.DiscoverDrives();
    var drive = drives.FirstOrDefault(d => d.Index == driveIndex);

    if (drive == null)
    {
        Console.WriteLine($"ERROR: Drive {driveIndex} not found.");
        return 1;
    }

    Console.WriteLine($"Found: {drive.Model} ({drive.Size / 1e9:F1} GB), {drive.Partitions.Count} partition(s)");
    var extPart = drive.Partitions.FirstOrDefault(p => p.IsExt);
    if (extPart == null)
    {
        Console.WriteLine("ERROR: No EXT partition found on this drive.");
        return 1;
    }

    Console.WriteLine($"EXT partition: index={extPart.Index}, offset={extPart.StartOffset}, size={extPart.Size / 1e9:F1} GB, FS={extPart.FileSystem}");
    Console.WriteLine();

    var source = new ExtSource
    {
        DisplayName = $"PhysicalDrive{driveIndex} Partition {extPart.Index}",
        Type = SourceType.PhysicalDisk,
        BackingPath = drive.DevicePath,
        Offset = extPart.StartOffset,
        Size = extPart.Size,
        FileSystem = extPart.FileSystem,
    };

    Console.WriteLine("Opening EXT filesystem via libext2fs...");
    Console.WriteLine($"  AppContext.BaseDirectory: {AppContext.BaseDirectory}");
    Console.WriteLine($"  libext2fs.dll exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "libext2fs.dll"))}");
    Console.WriteLine($"  libwinpthread-1.dll exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "libwinpthread-1.dll"))}");
    using var ext = new ExtFileSystemService();
    ext.Open(source);
    Console.WriteLine("Opened successfully!");
    Console.WriteLine();

    Console.WriteLine("Listing root directory:");
    var entries = ext.ListRoot();
    foreach (var e in entries)
    {
        Console.WriteLine($"  [{e.FileType}] {e.Name} (inode {e.Inode})");
    }
    Console.WriteLine($"  Total: {entries.Count} entries");
    Console.WriteLine();

    if (entries.Count > 0)
    {
        var firstFile = entries.FirstOrDefault(e => e.IsRegular);
        if (firstFile != null)
        {
            Console.WriteLine($"Reading first file: {firstFile.Name}");
            var info = ext.GetInode(firstFile.Inode);
            Console.WriteLine($"  Size: {info.Size} bytes, Mode: {info.Permissions}");
            byte[] content = ext.ReadFile(firstFile.Inode);
            string text = System.Text.Encoding.UTF8.GetString(content);
            string preview = text.Length > 200 ? text[..200] + "..." : text;
            Console.WriteLine($"  Content preview: {preview.Trim()}");
        }

        var firstDir = entries.FirstOrDefault(e => e.IsDirectory);
        if (firstDir != null)
        {
            Console.WriteLine();
            Console.WriteLine($"Listing subdirectory: {firstDir.Name}/");
            var subEntries = ext.ListDirectory(firstDir.Inode);
            foreach (var e in subEntries)
            {
                Console.WriteLine($"  [{e.FileType}] {e.Name} (inode {e.Inode})");
            }
            Console.WriteLine($"  Total: {subEntries.Count} entries");
        }
    }

    ext.Close();
    Console.WriteLine();
    Console.WriteLine("=== TEST PASSED ===");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    return 1;
}
