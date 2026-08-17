using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExtFsViewer.Models;
using ExtFsViewer.Services;

namespace ExtFsViewer.ViewModels;

public partial class BrowserViewModel : ObservableObject, IDisposable
{
    private readonly ExtFileSystemService _ext = new();
    private readonly Stack<(uint ino, string name)> _navStack = new();
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isNavigating;

    [ObservableProperty]
    private string _currentPath = "/";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private FileItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _sourceName = string.Empty;

    public ObservableCollection<FileItemViewModel> Entries { get; } = new();

    public bool IsOpen => _ext.IsOpen;

    [RelayCommand]
    public async Task OpenAsync(ExtSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsNavigating = true;
        try
        {
            _ext.Open(source);
            SourceName = source.DisplayName;
            _navStack.Clear();
            _navStack.Push((ExtFileSystemService.RootInode, "/"));
            CurrentPath = "/";
            await LoadCurrentAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Open failed: {ex.Message}";
            System.Windows.MessageBox.Show($"Failed to open filesystem:\n\n{ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsNavigating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    public async Task EnterAsync(FileItemViewModel item)
    {
        if (!item.IsDirectory)
        {
            SelectedItem = item;
            return;
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        IsNavigating = true;
        try
        {
            _navStack.Push((item.Inode, item.Name));
            CurrentPath = BuildPath();
            await LoadCurrentAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Enter failed: {ex.Message}";
        }
        finally
        {
            IsNavigating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public async Task BackAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_navStack.Count <= 1) return;

        _navStack.Pop();
        CurrentPath = BuildPath();
        IsNavigating = true;
        try { await LoadCurrentAsync(); }
        finally { IsNavigating = false; }
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    public async Task RefreshAsync()
    {
        IsNavigating = true;
        try { await LoadCurrentAsync(); }
        finally { IsNavigating = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExtract))]
    public async Task ExtractAsync()
    {
        if (SelectedItem == null) return;
        var item = SelectedItem;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = item.Name,
            Title = "Extract file to…",
            Filter = "All files|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        await ExtractToAsync(item.Inode, item.Name, dialog.FileName);
    }

    public async Task ExtractToAsync(uint inode, string displayName, string destPath)
    {
        StatusMessage = $"Extracting {displayName}…";
        try
        {
            var progress = new Progress<(long copied, long total)>(p =>
            {
                StatusMessage = $"Extracting {displayName}… {p.copied}/{p.total} bytes";
            });

            await Task.Run(() => _ext.CopyFileAsync(inode, destPath, progress as IProgress<(long, long)>, CancellationToken.None));
            StatusMessage = $"Extracted {displayName} → {destPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Extract failed: {ex.Message}";
            throw;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyDir))]
    public void ExtractDirectory()
    {
        if (SelectedItem == null) return;
        var item = SelectedItem;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select destination folder…",
        };

        if (dialog.ShowDialog() != true) return;

        string dest = System.IO.Path.Combine(dialog.FolderName, item.Name);
        var progressDlg = new CopyProgressDialog(_ext, item.Inode, dest, isDirectory: true, CollisionPolicy.Skip)
        {
            Owner = App.Current.MainWindow,
        };
        progressDlg.ShowDialog();

        StatusMessage = progressDlg.DialogResult == true
            ? $"Extracted directory {item.Name} → {dest}"
            : $"Directory extraction cancelled/failed.";
    }

    private bool CanCopyDir() => !IsNavigating && SelectedItem != null && SelectedItem.IsDirectory;

    private async Task LoadCurrentAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_ext.IsOpen) return;

        var (ino, _) = _navStack.Peek();
        Entries.Clear();
        SelectedItem = null;
        StatusMessage = "Loading…";

        try
        {
            var items = await Task.Run(() =>
            {
                var entries = _ext.ListDirectory(ino);
                var ordered = entries
                    .OrderBy(e => e.FileType != ExtFileType.Directory)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

                var result = new List<FileItemViewModel>();
                foreach (var e in ordered)
                {
                    ExtInodeInfo? info = null;
                    try { info = _ext.GetInode(e.Inode); }
                    catch { }
                    result.Add(FileItemViewModel.FromEntry(e, info));
                }
                return result;
            });

            foreach (var item in items)
                Entries.Add(item);

            StatusMessage = $"{Entries.Count} item(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"List failed: {ex.Message}";
        }
    }

    private string BuildPath()
    {
        if (_navStack.Count == 0) return "/";
        var parts = _navStack.Reverse().Select(s => s.name).Where(n => n != "/");
        var joined = string.Join("/", parts);
        return "/" + joined;
    }

    private bool CanNavigate() => !IsNavigating && _ext.IsOpen;
    private bool CanGoBack() => !IsNavigating && _navStack.Count > 1;
    private bool CanExtract() => !IsNavigating && SelectedItem != null && !SelectedItem.IsDirectory;

    partial void OnSelectedItemChanged(FileItemViewModel? value)
        => ExtractCommand.NotifyCanExecuteChanged();

    public void Dispose()
    {
        if (_disposed) return;
        _ext.Dispose();
        _disposed = true;
    }
}
