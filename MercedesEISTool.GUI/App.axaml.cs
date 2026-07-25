using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MercedesEISTool.ApiClient;
using MercedesEISTool.GUI.ViewModels;
using MercedesEISTool.GUI.Views;

namespace MercedesEISTool.GUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new LoginWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowMainWindow(IMercedesEisApiClient apiClient)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var loginWindow = desktop.MainWindow;
        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(apiClient)
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        loginWindow?.Close();
    }
}