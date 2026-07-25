using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using MercedesEISTool.GUI.ViewModels;

namespace MercedesEISTool.GUI.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(IConfiguration? configuration = null)
    {
        InitializeComponent();
        DataContext = new LoginViewModel(configuration);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
