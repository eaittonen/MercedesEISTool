using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.GUI;

namespace MercedesEISTool.GUI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IMercedesEisApiClient _apiClient = new MercedesEisApiClient(new HttpClient { BaseAddress = new Uri("http://localhost:5080"), Timeout = TimeSpan.FromSeconds(5) });

    [ObservableProperty]
    private string _email = "admin@example.local";

    [ObservableProperty]
    private string _password = "development-only-password";

    [ObservableProperty]
    private string _status = string.Empty;

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            Status = "Signing in...";
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
