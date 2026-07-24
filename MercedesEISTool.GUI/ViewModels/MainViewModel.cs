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

    [ObservableProperty]
    private string _vin = string.Empty;

    [ObservableProperty]
    private string _eisType = string.Empty;

    [ObservableProperty]
    private string _format = string.Empty;

    [ObservableProperty]
    private string _mcu = string.Empty;

    [ObservableProperty]
    private string _keyCount = "0";

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _selectedTargetFormat = "CGDI MB";

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
        // TODO: route BIN loading through BinaryLoader once the integration point is ready.
        var bytes = File.ReadAllBytes(path);
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

    [RelayCommand]
    private void ConvertDump()
    {
        if (string.IsNullOrWhiteSpace(Vin))
        {
            Status = "Open a dump first.";
            return;
        }

        var dump = new EisDump
        {
            RawData = Array.Empty<byte>(),
            VIN = Vin,
            Format = Format,
            EisType = EisType,
            MCU = Mcu,
            SSID = string.Empty,
            Keys = new List<KeyInfo>()
        };

        var converted = _service.ConvertDump(dump, SelectedTargetFormat);
        ApplyDump(converted);
        Status = $"Converted to {SelectedTargetFormat}";
    }

    [RelayCommand]
    private async Task SaveDump()
    {
        if (string.IsNullOrWhiteSpace(Vin))
        {
            Status = "Nothing to save.";
            return;
        }

        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Mercedes EIS dump",
            SuggestedFileName = "dump.bin"
        });

        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        File.WriteAllBytes(path, new byte[256]);
        Status = $"Saved {Path.GetFileName(path)}";
    }

    private void ApplyDump(EisDump dump)
    {
        Vin = dump.VIN;
        EisType = dump.EisType;
        Format = dump.Format;
        Mcu = dump.MCU;
        KeyCount = dump.Keys.Count.ToString();
    }
}
