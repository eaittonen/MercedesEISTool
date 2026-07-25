using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MercedesEISTool.ApiClient;
using MercedesEISTool.GUI.Configuration;
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
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<ApiOptions>().Bind(configuration.GetSection(ApiOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IConfiguration>().GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
            return new MercedesEisApiClient(new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl),
                Timeout = TimeSpan.FromSeconds(5)
            });
        });

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new LoginWindow(configuration);
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
        var configuration = (Application as App)?.Services?.GetRequiredService<IConfiguration>();
        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(apiClient, configuration)
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        loginWindow?.Close();
    }
}