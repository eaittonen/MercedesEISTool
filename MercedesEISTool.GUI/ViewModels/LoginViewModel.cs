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
        var savedCredentials = LoadSavedCredentials();
        UpdateSelectedEnvironment(storedEnvironment);
        if (savedCredentials is not null)
        {
            Email = savedCredentials.Email;
            Password = savedCredentials.Password;
            RememberMe = true;
        }
        _apiClient = CreateApiClient(GetBaseUrl(storedEnvironment));
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    private string _selectedEnvironment = "Production";
    public string SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (_selectedEnvironment == value)
            {
                return;
            }

            _selectedEnvironment = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedServerDisplay));
        }
    }

    [ObservableProperty]
    private ObservableCollection<string> _availableEnvironments = new() { "Production", "QA" };

    [ObservableProperty]
    private string _selectedServerDisplay = ProductionBaseUrl;

    [ObservableProperty]
    private bool _rememberMe;

    private void UpdateSelectedEnvironment(string value)
    {
        SelectedEnvironment = value;
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
        var selectedBaseUrl = baseUrl ?? GetBaseUrl(SelectedEnvironment);
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

        var settings = LoadSettings();
        settings.SelectedEnvironment = environmentName;
        SaveSettings(settings);
    }

    private LoginSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            return new LoginSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LoginSettings>(json) ?? new LoginSettings();
        }
        catch
        {
            return new LoginSettings();
        }
    }

    private void SaveSettings(LoginSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private LoginSettings? LoadSavedCredentials()
    {
        var settings = LoadSettings();
        if (!settings.RememberMe || string.IsNullOrWhiteSpace(settings.Email))
        {
            return null;
        }

        return settings;
    }

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            Status = "Signing in...";
            _apiClient = CreateApiClient(GetBaseUrl(SelectedEnvironment));
            var response = await _apiClient.LoginAsync(Email, Password);
            _apiClient.SetAccessToken(response.AccessToken);

            var settings = LoadSettings();
            settings.SelectedEnvironment = SelectedEnvironment;
            settings.Email = RememberMe ? Email : string.Empty;
            settings.Password = RememberMe ? Password : string.Empty;
            settings.RememberMe = RememberMe;
            SaveSettings(settings);

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
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
