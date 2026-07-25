using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.GUI;

namespace MercedesEISTool.GUI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const string ProductionBaseUrl = "https://tool.mestariverkko.fi";
    private const string QaBaseUrl = "https://qa.tool.mestariverkko.fi";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MercedesEISTool",
        "login-settings.json");

    private readonly IConfiguration? _configuration;
    private IMercedesEisApiClient _apiClient;

    public LoginViewModel(IConfiguration? configuration = null)
    {
        _configuration = configuration;
        var storedEnvironment = LoadSelectedEnvironment();
        SelectedEnvironment = storedEnvironment;
        SelectedServerDisplay = GetBaseUrl(storedEnvironment);
        _apiClient = CreateApiClient(GetBaseUrl(storedEnvironment));
    }

    [ObservableProperty]
    private string _email = "admin@example.local";

    [ObservableProperty]
    private string _password = "development-only-password";

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _selectedEnvironment = "Production";

    [ObservableProperty]
    private ObservableCollection<string> _availableEnvironments = new() { "Production", "QA" };

    [ObservableProperty]
    private string _selectedServerDisplay = ProductionBaseUrl;

    partial void OnSelectedEnvironmentChanged(string value)
    {
        SelectedServerDisplay = GetBaseUrl(value);
        _apiClient = CreateApiClient(SelectedServerDisplay);
        SaveSelectedEnvironment(value);
    }

    private string GetBaseUrl(string environmentName)
        => string.Equals(environmentName, "QA", StringComparison.OrdinalIgnoreCase)
            ? QaBaseUrl
            : ProductionBaseUrl;

    private IMercedesEisApiClient CreateApiClient(string? baseUrl = null)
    {
        var selectedBaseUrl = baseUrl ?? GetBaseUrl(_selectedEnvironment);
        return new MercedesEisApiClient(new HttpClient
        {
            BaseAddress = new Uri(selectedBaseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        });
    }

    private string LoadSelectedEnvironment()
    {
        if (!File.Exists(SettingsPath))
        {
            return "Production";
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<LoginSettings>(json);
            return string.Equals(settings?.SelectedEnvironment, "QA", StringComparison.OrdinalIgnoreCase)
                ? "QA"
                : "Production";
        }
        catch
        {
            return "Production";
        }
    }

    private void SaveSelectedEnvironment(string environmentName)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new LoginSettings { SelectedEnvironment = environmentName };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            Status = "Signing in...";
            _apiClient = CreateApiClient(GetBaseUrl(_selectedEnvironment));
            var response = await _apiClient.LoginAsync(Email, Password);
            _apiClient.SetAccessToken(response.AccessToken);
            Status = $"Signed in as {response.DisplayName}";
            if (App.Current is App app)
            {
                app.ShowMainWindow(_apiClient);
            }
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private sealed class LoginSettings
    {
        public string SelectedEnvironment { get; set; } = "Production";
    }
}
