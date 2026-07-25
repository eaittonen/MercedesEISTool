using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.GUI;
using MercedesEISTool.GUI.Configuration;

namespace MercedesEISTool.GUI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IConfiguration? _configuration;
    private readonly EnvironmentSettings _environmentSettings;
    private IMercedesEisApiClient _apiClient;

    public LoginViewModel(IConfiguration? configuration = null)
    {
        _configuration = configuration;
        _environmentSettings = EnvironmentSettings.Load();
        _apiClient = CreateApiClient();
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
    private string _selectedServerDisplay = "https://tool.mestariverkko.fi";

    public string SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (SetProperty(ref _selectedEnvironment, value))
            {
                UpdateServerSelection();
            }
        }
    }

    public string SelectedServerDisplay
    {
        get => _selectedServerDisplay;
        set => SetProperty(ref _selectedServerDisplay, value);
    }

    [RelayCommand]
    private void UpdateServerSelection()
    {
        var options = GetOptionsForEnvironment(_selectedEnvironment);
        SelectedServerDisplay = options.BaseUrl;
        _apiClient = CreateApiClient(options.BaseUrl);
        _environmentSettings.SelectedEnvironment = _selectedEnvironment;
        _environmentSettings.Save();
    }

    private ApiOptions GetOptionsForEnvironment(string environmentName)
    {
        var section = _configuration?.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
        var environmentSettings = _configuration?.GetSection("Environments").GetSection(environmentName).Get<ApiOptions>() ?? new ApiOptions();
        return string.IsNullOrWhiteSpace(environmentSettings.BaseUrl) ? section : environmentSettings;
    }

    private IMercedesEisApiClient CreateApiClient(string? baseUrl = null)
    {
        var selectedBaseUrl = baseUrl ?? GetOptionsForEnvironment(_selectedEnvironment).BaseUrl;
        return new MercedesEisApiClient(new HttpClient
        {
            BaseAddress = new Uri(selectedBaseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        });
    }

    private async Task Login()
    {
        try
        {
            Status = "Signing in...";
            _apiClient = CreateApiClient(GetOptionsForEnvironment(_selectedEnvironment).BaseUrl);
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
}
