using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MercedesEISTool.ApiClient;
using MercedesEISTool.GUI.ViewModels;
using MercedesEISTool.GUI.Views;
using System.Runtime.ExceptionServices;

namespace MercedesEISTool.GUI;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IMercedesEisApiClient>(_ =>
            new MercedesEisApiClient(new HttpClient
            {
                BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "https://tool.mestariverkko.fi"),
                Timeout = TimeSpan.FromSeconds(5)
            }));

        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loginWindow = new LoginWindow(configuration);
            desktop.MainWindow = loginWindow;
            loginWindow.Show();

            if (IsUpdateCheckEnabled(configuration))
            {
                _ = RunUpdateCheckAsync(configuration);
            }
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
        var configuration = _serviceProvider?.GetService<IConfiguration>();
        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(apiClient, configuration)
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        loginWindow?.Close();
    }

    private static bool IsUpdateCheckEnabled(IConfiguration configuration)
    {
        return configuration.GetValue("Updates:Enabled", true);
    }

    private static async Task<GitHubRelease?> CheckForUpdates(IConfiguration configuration)
    {
        var owner = configuration["Updates:RepositoryOwner"] ?? "eaittonen";
        var repo = configuration["Updates:RepositoryName"] ?? "MercedesEISTool";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MercedesEISTool-Client");

        var requestUri = new Uri($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
        var response = await client.GetAsync(requestUri);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new GitHubRelease();
        var currentVersion = typeof(App).Assembly.GetName().Version;
        var latestVersion = ParseVersion(release.TagName);

        if (currentVersion is null || latestVersion is null || latestVersion <= currentVersion)
        {
            return null;
        }

        return release;
    }

    private static async Task RunUpdateCheckAsync(IConfiguration configuration)
    {
        try
        {
            var update = await CheckForUpdates(configuration);
            if (update is null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await ShowUpdateDialog(update, configuration);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? value[1..] : value;
        return Version.TryParse(normalized, out var parsed) ? parsed : null;
    }

    private static async Task ShowUpdateDialog(GitHubRelease release, IConfiguration configuration)
    {
        var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
        var latestVersion = release.TagName ?? "unknown";
        var releaseNotes = string.IsNullOrWhiteSpace(release.Body) ? "A new version is available. Download it to update the application." : release.Body;

        var window = new Window
        {
            Title = "Update available",
            Width = 520,
            Height = 320,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12
        };

        stack.Children.Add(new TextBlock
        {
            Text = $"A newer version is available: {currentVersion} -> {latestVersion}",
            FontWeight = FontWeight.Bold
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Release notes:",
            FontWeight = FontWeight.Bold
        });

        stack.Children.Add(new TextBlock
        {
            Text = releaseNotes,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 160
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var skipButton = new Button { Content = "Skip" };
        skipButton.Click += (_, _) => window.Close();

        var updateButton = new Button { Content = "Download and update" };
        updateButton.Click += async (_, _) =>
        {
            await DownloadAndInstallAsync(release, configuration);
            window.Close();
        };

        buttons.Children.Add(skipButton);
        buttons.Children.Add(updateButton);
        stack.Children.Add(buttons);

        window.Content = stack;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
        {
            await window.ShowDialog(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }

    private static async Task DownloadAndInstallAsync(GitHubRelease release, IConfiguration configuration)
    {
        var assetName = configuration["Updates:AssetName"];
        var asset = release.Assets?.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, assetName, StringComparison.OrdinalIgnoreCase))
            ?? release.Assets?.FirstOrDefault(candidate => candidate.Name?.Contains("win-x64", StringComparison.OrdinalIgnoreCase) == true)
            ?? release.Assets?.FirstOrDefault();

        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            return;
        }

        using var client = new HttpClient();
        var data = await client.GetByteArrayAsync(asset.BrowserDownloadUrl);
        var destinationPath = Path.Combine(Path.GetTempPath(), asset.Name ?? "MercedesEISTool-update.bin");
        await File.WriteAllBytesAsync(destinationPath, data);

        var processStartInfo = new ProcessStartInfo(destinationPath)
        {
            UseShellExecute = true
        };

        Process.Start(processStartInfo);
        Environment.Exit(0);
    }

    private sealed class GitHubRelease
    {
        public string? TagName { get; set; }
        public string? Body { get; set; }
        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
    }
}