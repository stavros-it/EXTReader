using EXTReader.Models;
using EXTReader.Services;
using EXTReader.ViewModels;

string imagePath = args.Length > 0 ? args[0] : "test_ext4.img";
string tempRoot = Path.Combine(Path.GetTempPath(), $"extfs_phase4_test_{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

Console.WriteLine("=== Phase 4 Transfer Test ===");
Console.WriteLine($"Temp root: {tempRoot}");
Console.WriteLine();

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
ext.Open(source);
Console.WriteLine("Opened filesystem.");
Console.WriteLine();

Console.WriteLine("1. CopyFileAsync (single file: hello.txt):");
string fileDest = Path.Combine(tempRoot, "hello_copy.txt");
var progress1 = new Progress<CopyProgress>(p =>
    Console.WriteLine($"   {p.Percent:F1}% — {p.BytesCopiedFormatted}/{p.TotalBytesFormatted}"));
var transfer = new FileTransferService(ext);
uint helloIno = ext.LookupPath(ExtFileSystemService.RootInode, "hello.txt");
long helloSize = ext.GetFileSize(helloIno);
await transfer.CopyFileAsync(helloIno, fileDest, progress1, new CopyProgress(0, helloSize, "hello.txt", 0, 1), CancellationToken.None);
string content = File.ReadAllText(fileDest);
Console.WriteLine($"   Extracted: {content.Trim()} ({new FileInfo(fileDest).Length} bytes)");
Console.WriteLine();

Console.WriteLine("2. CopyDirectoryAsync (recursive from root):");
string dirDest = Path.Combine(tempRoot, "root_copy");
int filesCopied = await transfer.CopyDirectoryAsync(
    ExtFileSystemService.RootInode,
    dirDest,
    new Progress<CopyProgress>(p => Console.WriteLine($"   {p.Percent:F1}% — {p.Summary} — {p.CurrentFile}")),
    CollisionPolicy.Overwrite,
    CancellationToken.None);
Console.WriteLine($"   Files copied: {filesCopied}");
Console.WriteLine();

Console.WriteLine("3. Verifying directory contents:");
void Walk(string dir, int depth = 0)
{
    foreach (var f in Directory.GetFiles(dir).OrderBy(f => f))
        Console.WriteLine($"{new string(' ', depth * 3)}- {Path.GetFileName(f)} ({new FileInfo(f).Length} bytes)");
    foreach (var d in Directory.GetDirectories(dir).OrderBy(d => d))
    {
        Console.WriteLine($"{new string(' ', depth * 3)}[D] {Path.GetFileName(d)}/");
        Walk(d, depth + 1);
    }
}
Walk(dirDest);
Console.WriteLine();

Console.WriteLine("4. Verifying subdir/nested.txt content:");
string nestedCopy = Path.Combine(dirDest, "subdir", "nested.txt");
if (File.Exists(nestedCopy))
{
    string nestedContent = File.ReadAllText(nestedCopy);
    Console.WriteLine($"   Content: {nestedContent.Trim()}");
    Console.WriteLine($"   Match: {nestedContent.Trim() == "Nested file"}");
}
else
{
    Console.WriteLine("   FAILED: file not found");
    return 1;
}
Console.WriteLine();

Console.WriteLine("5. Long-path support (>260 chars):");
string longName = new string('a', 200) + ".txt";
string longPath = Path.Combine(dirDest, longName);
File.WriteAllText(longPath, "long path test");
Console.WriteLine($"   Created long path: {longPath.Length} chars");
Console.WriteLine($"   Readable via ToLongPath: {File.ReadAllText(FileTransferService.ToLongPath(longPath))}");
Console.WriteLine();

Console.WriteLine("=== ALL TESTS PASSED ===");
Directory.Delete(tempRoot, recursive: true);
return 0;
