using ExtFsViewer.Models;
using ExtFsViewer.Services;

string imagePath = args.Length > 0 ? args[0] : "test_ext4.img";

Console.WriteLine($"=== EXT FS Engine Test ===");
Console.WriteLine($"Image: {imagePath}");
Console.WriteLine();

Console.WriteLine("1. Opening filesystem (read-only):");
var source = new ExtSource
{
    DisplayName = "Test EXT4 Image",
    Type = SourceType.ImageFile,
    BackingPath = Path.GetFullPath(imagePath),
    Offset = 0,
    Size = new FileInfo(imagePath).Length,
    FileSystem = FileSystemType.Ext4,
};

using var ext = new ExtFileSystemService();
try
{
    ext.Open(source);
    Console.WriteLine("   Opened successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"   FAILED: {ex.Message}");
    return 1;
}
Console.WriteLine();

Console.WriteLine("2. Listing root directory:");
List<ExtDirEntry> entries;
try
{
    entries = ext.ListRoot();
    foreach (var e in entries)
    {
        Console.WriteLine($"   [{e.FileType}] {e.Name} (inode {e.Inode})");
    }
    Console.WriteLine($"   Total: {entries.Count} entries");
}
catch (Exception ex)
{
    Console.WriteLine($"   FAILED: {ex.Message}");
    return 1;
}
Console.WriteLine();

Console.WriteLine("3. Getting inode info for hello.txt:");
try
{
    uint ino = ext.LookupPath(ExtFileSystemService.RootInode, "hello.txt");
    var info = ext.GetInode(ino);
    Console.WriteLine($"   Inode: {info.Inode}");
    Console.WriteLine($"   Size: {info.Size} bytes");
    Console.WriteLine($"   Mode: {info.Permissions}");
    Console.WriteLine($"   Type: {info.FileType}");
    Console.WriteLine($"   Modified: {info.ModifyTime}");
}
catch (Exception ex)
{
    Console.WriteLine($"   FAILED: {ex.Message}");
    return 1;
}
Console.WriteLine();

Console.WriteLine("4. Reading hello.txt content:");
try
{
    uint ino = ext.LookupPath(ExtFileSystemService.RootInode, "hello.txt");
    byte[] content = ext.ReadFile(ino);
    string text = System.Text.Encoding.UTF8.GetString(content);
    Console.WriteLine($"   Content: {text.Trim()}");
    Console.WriteLine($"   Bytes: {content.Length}");
}
catch (Exception ex)
{
    Console.WriteLine($"   FAILED: {ex.Message}");
    return 1;
}
Console.WriteLine();

Console.WriteLine("5. Listing subdir/ contents:");
try
{
    uint subdirIno = ext.LookupPath(ExtFileSystemService.RootInode, "subdir");
    var subEntries = ext.ListDirectory(subdirIno);
    foreach (var e in subEntries)
    {
        Console.WriteLine($"   [{e.FileType}] {e.Name} (inode {e.Inode})");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"   FAILED: {ex.Message}");
    return 1;
}
Console.WriteLine();

Console.WriteLine("6. Reading subdir/nested.txt:");
try
{
    uint subdirIno = ext.LookupPath(ExtFileSystemService.RootInode, "subdir");
    uint nestedIno = ext.LookupPath(subdirIno, "nested.txt");
    byte[] content = ext.ReadFile(nestedIno);
    string text = System.Text.Encoding.UTF8.GetString(content);
    Console.WriteLine($"   Content: {text.Trim()}");
    Console.WriteLine($"   Bytes: {content.Length}");
}
catch (Exception ex)
{
    Console.WriteLine($"   FAILED: {ex.Message}");
    return 1;
}
Console.WriteLine();

Console.WriteLine("=== ALL TESTS PASSED ===");
return 0;
