using EXTReader.Models;
using EXTReader.ViewModels;

string imagePath = args.Length > 0 ? args[0] : "test_ext4.img";

Console.WriteLine("=== Phase 3 Browser Test ===");

var source = new ExtSource
{
    DisplayName = "Test EXT4 Image",
    Type = SourceType.ImageFile,
    BackingPath = Path.GetFullPath(imagePath),
    Offset = 0,
    Size = new FileInfo(imagePath).Length,
    FileSystem = FileSystemType.Ext4,
};

using var vm = new BrowserViewModel();
vm.Open(source);

Console.WriteLine($"1. Opened: {vm.SourceName}");
Console.WriteLine($"   Path: {vm.CurrentPath}");
Console.WriteLine($"   Items: {vm.Entries.Count}");
foreach (var item in vm.Entries)
{
    Console.WriteLine($"   [{item.TypeLabel}] {item.Name} ({item.Permissions}, {item.SizeFormatted})");
}

Console.WriteLine();
Console.WriteLine("2. Entering subdir…");
var subdir = vm.Entries.First(e => e.Name == "subdir");
vm.EnterCommand.Execute(subdir);
Console.WriteLine($"   Path: {vm.CurrentPath}");
Console.WriteLine($"   Items: {vm.Entries.Count}");
foreach (var item in vm.Entries)
{
    Console.WriteLine($"   [{item.TypeLabel}] {item.Name} (inode {item.Inode})");
}

Console.WriteLine();
Console.WriteLine("3. Going back…");
vm.BackCommand.Execute(null);
Console.WriteLine($"   Path: {vm.CurrentPath}");
Console.WriteLine($"   Items: {vm.Entries.Count}");

Console.WriteLine();
Console.WriteLine("4. Selecting hello.txt and viewing properties…");
var hello = vm.Entries.First(e => e.Name == "hello.txt");
vm.SelectedItem = hello;
Console.WriteLine($"   Name: {vm.SelectedItem.Name}");
Console.WriteLine($"   Inode: {vm.SelectedItem.Inode}");
Console.WriteLine($"   Size: {vm.SelectedItem.SizeFormatted}");
Console.WriteLine($"   Permissions: {vm.SelectedItem.Permissions}");
Console.WriteLine($"   Type: {vm.SelectedItem.TypeLabel}");
Console.WriteLine($"   UID/GID: {vm.SelectedItem.Uid}/{vm.SelectedItem.Gid}");
Console.WriteLine($"   Links: {vm.SelectedItem.LinksCount}");
Console.WriteLine($"   Blocks: {vm.SelectedItem.BlockCount}");
Console.WriteLine($"   Can extract: {vm.ExtractCommand.CanExecute(null)}");

Console.WriteLine();
Console.WriteLine("5. Extracting hello.txt…");
string destPath = Path.Combine(Path.GetTempPath(), "extfs_extracted_hello.txt");
if (File.Exists(destPath)) File.Delete(destPath);
vm.ExtractToAsync(hello.Inode, hello.Name, destPath).Wait();
if (File.Exists(destPath))
{
    string content = File.ReadAllText(destPath);
    Console.WriteLine($"   Extracted to: {destPath}");
    Console.WriteLine($"   Content: {content.Trim()}");
    Console.WriteLine($"   Size: {new FileInfo(destPath).Length} bytes");
}
else
{
    Console.WriteLine("   FAILED: file not created");
    return 1;
}

Console.WriteLine();
Console.WriteLine("=== ALL TESTS PASSED ===");
return 0;
