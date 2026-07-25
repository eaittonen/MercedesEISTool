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
    private string _compareAPath = string.Empty;

    [ObservableProperty]
    private string _compareBPath = string.Empty;

    [ObservableProperty]
    private string _compareText = string.Empty;

    [ObservableProperty]
    private string _compareSummary = string.Empty;

    [ObservableProperty]
    private string _compareOffsets = string.Empty;

    private byte[]? _compareABytes;

    private byte[]? _compareBBytes;

    [ObservableProperty]
    private string _sequenceStartOffset = string.Empty;

    [ObservableProperty]
    private string _sequenceLength = "1";

    [ObservableProperty]
    private string _sequenceSearchText = string.Empty;

    [ObservableProperty]
    private string _sequenceResults = string.Empty;

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

    [RelayCommand]
    private async Task OpenFileA()
    {
        var result = await OpenCompareFileAsync();
        if (result is null)
        {
            return;
        }

        _compareABytes = result.Value.Data;
        CompareAPath = result.Value.Path;
        UpdateComparisonUi();
    }

    [RelayCommand]
    private async Task OpenFileB()
    {
        var result = await OpenCompareFileAsync();
        if (result is null)
        {
            return;
        }

        _compareBBytes = result.Value.Data;
        CompareBPath = result.Value.Path;
        UpdateComparisonUi();
    }

    [RelayCommand]
    private void SearchSequence()
    {
        if (_compareABytes is null || _compareBBytes is null)
        {
            SequenceResults = "Load file A and file B before searching.";
            return;
        }

        if (!TryParseOffset(SequenceStartOffset, out var startOffset))
        {
            SequenceResults = "Enter a valid start offset in decimal or 0x-prefixed hex.";
            return;
        }

        if (!int.TryParse(SequenceLength, out var length) || length < 1 || length > 32)
        {
            SequenceResults = "Sequence length must be between 1 and 32.";
            return;
        }

        if (startOffset < 0 || startOffset + length > _compareABytes.Length)
        {
            SequenceResults = "The requested range is outside file A.";
            return;
        }

        var result = _service.SearchSequence(_compareABytes, _compareBBytes, startOffset, length);
        var exact = FormatOffsets(result.ExactMatches);
        var reversed = FormatOffsets(result.ReversedMatches);
        SequenceResults = $"Exact matches: {exact}{Environment.NewLine}Reversed matches: {reversed}";
    }

    private async Task<(string Path, byte[]? Data)?> OpenCompareFileAsync()
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Mercedes EIS dump",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        try
        {
            var data = _binaryLoader.LoadBinFile(file.Path.LocalPath);
            return (file.Path.LocalPath, data);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return null;
        }
    }

    private void UpdateComparisonUi()
    {
        if (_compareABytes is null || _compareBBytes is null)
        {
            CompareText = "Load file A and file B to compare them.";
            CompareSummary = string.Empty;
            CompareOffsets = string.Empty;
            return;
        }

        var dumpA = _service.ParseDump(_compareABytes);
        var dumpB = _service.ParseDump(_compareBBytes);
        var comparison = _service.CompareDumps(_compareABytes, _compareBBytes);
        CompareText = BuildComparisonText(_compareABytes, _compareBBytes, comparison);
        CompareSummary = $"A: {DisplayValue(dumpA.Format)} / VIN: {DisplayValue(dumpA.VIN)} | B: {DisplayValue(dumpB.Format)} / VIN: {DisplayValue(dumpB.VIN)}";
        CompareOffsets = $"Differing bytes: {comparison.TotalDifferences} | Offsets: {FormatOffsets(comparison.DifferingOffsets)}";
    }

    private static string FormatOffsets(IEnumerable<int> offsets)
    {
        var values = offsets.Select(offset => offset.ToString("X2"));
        return values.Any() ? string.Join(", ", values) : "none";
    }

    private static bool TryParseOffset(string? value, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
        }

        return int.TryParse(value, out offset);
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

    private static string BuildComparisonText(byte[] left, byte[] right, DumpCompareResult comparison)
    {
        if (left is null || right is null || left.Length != 256 || right.Length != 256)
        {
            return "Load two 256-byte dumps to compare them.";
        }

        var lines = new List<string>();
        foreach (var row in comparison.Rows)
        {
            var leftHex = string.Join(" ", row.RowBytesLeft.Select(b => b.ToString("X2")));
            var rightHex = string.Join(" ", row.RowBytesRight.Select(b => b.ToString("X2")));
            var asciiA = new string(row.RowBytesLeft.Select(b => b >= 0x20 && b < 0x7F ? (char)b : '.').ToArray());
            var asciiB = new string(row.RowBytesRight.Select(b => b >= 0x20 && b < 0x7F ? (char)b : '.').ToArray());
            var marker = row.HasDifferences ? "*" : "-";
            lines.Add($"{marker} {row.Offset:D4}  {leftHex.PadRight(47)} | {rightHex.PadRight(47)} | {asciiA} | {asciiB} | {row.RowBytesLeft.Zip(row.RowBytesRight, (a, b) => a != b).Count(isDifferent => isDifferent)}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
