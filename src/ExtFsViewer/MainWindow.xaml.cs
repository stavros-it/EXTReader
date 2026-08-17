using ExtFsViewer.Models;
using ExtFsViewer.ViewModels;
using Wpf.Ui.Controls;

namespace ExtFsViewer;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new SourcesViewModel();
        vm.BrowseRequested += OnBrowseRequested;
        DataContext = vm;
    }

    private void OnBrowseRequested(ExtSource source)
    {
        var browser = new BrowserWindow(source)
        {
            Owner = this,
        };
        browser.Show();
    }
}
