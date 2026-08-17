using System.Windows;
using System.Windows.Input;
using EXTReader.Models;
using EXTReader.ViewModels;
using Wpf.Ui.Controls;

namespace EXTReader;

public partial class BrowserWindow : FluentWindow
{
    private readonly BrowserViewModel _vm = new();

    public BrowserWindow(ExtSource source)
    {
        InitializeComponent();
        DataContext = _vm;
        _ = _vm.OpenCommand.ExecuteAsync(source);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) { }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedItem is FileItemViewModel item)
            _ = _vm.EnterCommand.ExecuteAsync(item);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Dispose();
        base.OnClosed(e);
    }
}
