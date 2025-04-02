using System.Configuration;
using System.Data;
using System.Windows;
using IndustrialControlApp.ViewModels;

namespace IndustrialControlApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow { DataContext = new MainViewModel() }.Show();
        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel()
        };
        mainWindow.Show();
    }
}