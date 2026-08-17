using EXTReader.Models;
using EXTReader.ViewModels;
using Wpf.Ui.Controls;

namespace EXTReader;

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
