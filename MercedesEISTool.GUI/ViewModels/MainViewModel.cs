using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MercedesEISTool.Core.Models;
using MercedesEISTool.Core.Services;

namespace MercedesEISTool.GUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly EisDumpService _service = new();
    private readonly BinaryLoader _binaryLoader = new();

    [ObservableProperty]
    private string _vin = "Unknown";

    [ObservableProperty]
    private string _eisType = "Unknown";

    [ObservableProperty]
    private string _detectedFormat = "Unknown";

    [ObservableProperty]
    private string _mcu = "Unknown";

    [ObservableProperty]
    private string _keyCount = "0";

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _selectedTargetFormat = "CGDI MB";

    [ObservableProperty]
    private string _rawHexText = string.Empty;

    [ObservableProperty]
    private bool _canConvert = false;

    [ObservableProperty]
    private bool _canSave = false;

    public ObservableCollection<string> SupportedFormats { get; } = new() { "VVDI MB Tool", "CGDI MB" };

    [RelayCommand]
    private async Task OpenDump()
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Mercedes EIS dump",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        try
        {
            var bytes = _binaryLoader.LoadBinFile(path);
            var validation = _service.ValidateDump(bytes);
            if (!validation.IsValid)
            {
                Status = validation.Message;
                return;
            }

            var dump = _service.ParseDump(bytes);
            ApplyDump(dump);
            Status = $"Loaded {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void ConvertDump()
    {
        Status = "Conversion is not implemented yet.";
    }

    [RelayCommand]
    private Task SaveDump()
    {
        Status = "Saving is disabled until conversion is implemented.";
        return Task.CompletedTask;
    }

    private void ApplyDump(EisDump dump)
    {
        Vin = DisplayValue(dump.VIN);
        EisType = DisplayValue(dump.EisType);
        DetectedFormat = DisplayValue(dump.Format);
        Mcu = DisplayValue(dump.MCU);
        KeyCount = dump.Keys.Count > 0 ? dump.Keys.Count.ToString() : "0";
        RawHexText = BuildRawHexText(dump.RawData);
        CanConvert = false;
        CanSave = false;
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private static string BuildRawHexText(byte[] data)
    {
        if (data is null || data.Length != 256)
        {
            return "No raw dump available.";
        }

        var lines = new List<string>();
        for (var offset = 0; offset < data.Length; offset += 16)
        {
            var chunk = data.Skip(offset).Take(16).ToArray();
            var hex = string.Join(" ", chunk.Select(b => b.ToString("X2")));
            var ascii = new string(chunk.Select(b => b >= 0x20 && b < 0x7F ? (char)b : '.').ToArray());
            lines.Add($"{offset:D4}  {hex.PadRight(47)} {ascii}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
