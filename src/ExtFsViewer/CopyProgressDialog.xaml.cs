using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ExtFsViewer.Models;
using ExtFsViewer.Services;
using Wpf.Ui.Controls;

namespace ExtFsViewer;

public partial class CopyProgressDialog : FluentWindow
{
    private readonly CancellationTokenSource _cts = new();
    private readonly CopyDialogViewModel _vm = new();

    private readonly FileTransferService _transfer;
    private readonly ExtFileSystemService _ext;
    private readonly uint _rootIno;
    private readonly string _destDir;
    private readonly CollisionPolicy _policy;
    private readonly bool _isDirectory;

    public CopyProgressDialog(ExtFileSystemService ext, uint ino, string destPath, bool isDirectory, CollisionPolicy policy)
    {
        InitializeComponent();
        DataContext = _vm;
        _ext = ext;
        _transfer = new FileTransferService(ext);
        _rootIno = ino;
        _destDir = destPath;
        _isDirectory = isDirectory;
        _policy = policy;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDirectory)
            {
                _vm.CurrentFile = "Scanning directory…";
                await Task.Run(() => _transfer.CopyDirectoryAsync(_rootIno, _destDir, new Progress<CopyProgress>(OnProgress), _policy, _cts.Token));
            }
            else
            {
                string fileName = System.IO.Path.GetFileName(_destDir);
                long size = _ext.GetFileSize(_rootIno);
                await Task.Run(() => _transfer.CopyFileAsync(_rootIno, _destDir, new Progress<CopyProgress>(OnProgress), new CopyProgress(0, size, fileName, 0, 1), _cts.Token));
            }
            _vm.CurrentFile = "Done.";
            _vm.Percent = 100;
            await Task.Delay(500);
            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            _vm.CurrentFile = "Cancelled.";
            DialogResult = false;
            Close();
        }
        catch (Exception ex)
        {
            _vm.CurrentFile = $"Error: {ex.Message}";
            DialogResult = false;
            Close();
        }
    }

    private void OnProgress(CopyProgress p)
    {
        _vm.CurrentFile = p.CurrentFile;
        _vm.Percent = p.Percent;
        _vm.Summary = p.Summary;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Dispose();
        base.OnClosed(e);
    }
}

public partial class CopyDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private string _summary = string.Empty;
}
