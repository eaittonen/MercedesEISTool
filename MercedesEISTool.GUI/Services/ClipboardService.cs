using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace MercedesEISTool.GUI.Services;

public interface IClipboardService
{
    System.Threading.Tasks.Task SetTextAsync(string? value);
    System.Threading.Tasks.Task<string?> GetTextAsync();
}

public sealed class AvaloniaClipboardService : IClipboardService
{
    public System.Threading.Tasks.Task SetTextAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        var clipboard = TopLevel.GetTopLevel((Window)Application.Current!.ApplicationLifetime!.
            GetType().GetProperty("MainWindow")!.GetValue(Application.Current.ApplicationLifetime)!);

        return clipboard!.Clipboard!.SetTextAsync(value);
    }

    public System.Threading.Tasks.Task<string?> GetTextAsync()
    {
        return System.Threading.Tasks.Task.FromResult<string?>(null);
    }
}
