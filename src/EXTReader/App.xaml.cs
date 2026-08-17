using System.Windows;
using EXTReader.Services;

namespace EXTReader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var result = SafetySelfCheck.Run();
        if (!result.Passed)
        {
            MessageBox.Show(result.Summary, "Safety Check Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        new MainWindow().Show();
    }
}
