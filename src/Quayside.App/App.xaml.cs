using System.Windows;
using Quayside.App.ViewModels;
using Quayside.Data;

namespace Quayside.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var services = new AppServices(DatabaseOptions.Load());
            var window = new MainWindow { DataContext = new ShellViewModel(services) };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Quayside could not open its database:{Environment.NewLine}{ex.GetBaseException().Message}",
                "Quayside", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
