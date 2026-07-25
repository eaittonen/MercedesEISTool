using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MercedesEISTool.GUI.ViewModels;

namespace MercedesEISTool.GUI.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
