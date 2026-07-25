using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace MercedesEISTool.GUI.Views;

public partial class MainWindow : Window
{
    private readonly string _stateFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MercedesEISTool", "window-state.json");

    public MainWindow()
    {
        InitializeComponent();
        Width = 1280;
        Height = 860;
        MinWidth = 1024;
        MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;
        StateChanged += OnStateChanged;
        LoadWindowState();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SaveWindowState()
    {
        try
        {
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new WindowStateSettings
            {
                Width = Width,
                Height = Height,
                X = Position.X,
                Y = Position.Y,
                IsMaximized = WindowState == WindowState.Maximized
            };

            File.WriteAllText(_stateFilePath, System.Text.Json.JsonSerializer.Serialize(state));
        }
        catch
        {
            // Best-effort persistence for window state.
        }
    }

    private void LoadWindowState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_stateFilePath);
            var state = System.Text.Json.JsonSerializer.Deserialize<WindowStateSettings>(json);
            if (state is null)
            {
                return;
            }

            if (state.Width > 0)
            {
                Width = state.Width;
            }

            if (state.Height > 0)
            {
                Height = state.Height;
            }

            if (state.X != 0 || state.Y != 0)
            {
                Position = new PixelPoint((int)state.X, (int)state.Y);
            }

            if (state.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
        catch
        {
            // Best-effort restore; fall back to defaults.
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        SaveWindowState();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SaveWindowState();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        SaveWindowState();
    }

    private sealed class WindowStateSettings
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsMaximized { get; set; }
    }
}