using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace MercedesEISTool.GUI.Services;

public interface IClipboardService
{
    Task SetTextAsync(string? value);
    Task<string?> GetTextAsync();
}

public sealed class AvaloniaClipboardService : IClipboardService
{
    public Task SetTextAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.CompletedTask;
        }

        var clipboard = TopLevel.GetTopLevel((Window)Application.Current!.ApplicationLifetime!.
            GetType().GetProperty("MainWindow")!.GetValue(Application.Current.ApplicationLifetime)!);

        return clipboard!.Clipboard!.SetTextAsync(value);
    }

    public Task<string?> GetTextAsync()
    {
        return Task.FromResult<string?>(null);
    }
}
