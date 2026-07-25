using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.GUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private IMercedesEisApiClient _apiClient;

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
    private string _serverStatus = "Connecting";

    [ObservableProperty]
    private string _selectedFileName = "No file selected";

    [ObservableProperty]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    private string _analysisSummary = string.Empty;

    [ObservableProperty]
    private string _uploadSummary = string.Empty;

    [ObservableProperty]
    private string _uploadedFilesSummary = string.Empty;

    [ObservableProperty]
    private string _apiBaseUrl = "http://localhost:5080";

    [ObservableProperty]
    private string _selectedTargetFormat = "CGDI MB";

    [ObservableProperty]
    private string _vehicleIdentifier = string.Empty;

    [ObservableProperty]
    private string _registrationNumber = string.Empty;

    [ObservableProperty]
    private bool _vinConfirmedByUser;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canUpload;

    [ObservableProperty]
    private string _lastChecked = string.Empty;

    [ObservableProperty]
    private string _connectionReason = string.Empty;

    [ObservableProperty]
    private string _connectionUrl = "http://localhost:5080";

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

    private ResearchFolderAnalysis? _lastResearchAnalysis;
    private List<ResearchMatchRecord> _lastResearchMatches = new();

    [ObservableProperty]
    private string _sequenceStartOffset = string.Empty;

    [ObservableProperty]
    private string _sequenceLength = "1";

    [ObservableProperty]
    private string _sequenceSearchText = string.Empty;

    [ObservableProperty]
    private string _sequenceResults = string.Empty;

    [ObservableProperty]
    private string _researchEisPath = string.Empty;

    [ObservableProperty]
    private string _researchKeyPath = string.Empty;

    [ObservableProperty]
    private string _researchComparePath = string.Empty;

    [ObservableProperty]
    private string _researchFolderPath = string.Empty;

    [ObservableProperty]
    private string _researchStartOffset = string.Empty;

    [ObservableProperty]
    private string _researchLength = "8";

    [ObservableProperty]
    private string _researchSelectedSourceFile = "EIS dump";

    [ObservableProperty]
    private string _researchSelectedMode = "Exact";

    [ObservableProperty]
    private string _researchXorValue = "0";

    [ObservableProperty]
    private string _researchSearchResults = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationName = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationFileFormat = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationOffset = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationLength = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationByteOrder = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationNotes = string.Empty;

    [ObservableProperty]
    private string _researchSelectedConfidence = "Unknown";

    [ObservableProperty]
    private string _researchAnnotationsText = string.Empty;

    [ObservableProperty]
    private string _researchFolderAnalysisText = string.Empty;

    [ObservableProperty]
    private string _researchReportPath = string.Empty;

    [ObservableProperty]
    private bool _canConvert = false;

    [ObservableProperty]
    private bool _canSave = false;

    public MainViewModel()
    {
        _apiClient = CreateApiClient(ApiBaseUrl);
        ConnectionUrl = ApiBaseUrl;
        _ = RefreshServerStatusAsync();
    }

    partial void OnApiBaseUrlChanged(string value)
    {
        _apiClient = CreateApiClient(value);
        ConnectionUrl = value;
        _ = RefreshServerStatusAsync();
    }

    partial void OnVehicleIdentifierChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnRegistrationNumberChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnVinConfirmedByUserChanged(bool value)
    {
        UpdateUploadAvailability();
    }

    partial void OnServerStatusChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnSelectedFileNameChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateUploadAvailability();
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "VVDI MB Tool", "CGDI MB" };
    public ObservableCollection<string> ResearchSourceFiles { get; } = new() { "EIS dump", "Key file", "Compare dump" };
    public ObservableCollection<string> ResearchSearchModes { get; } = new() { "Exact", "Reversed", "BytePairSwapped", "FourByteWordReversed", "Xor" };
    public ObservableCollection<string> ResearchConfidenceValues { get; } = new() { "Unknown", "Suspected", "Probable", "Verified", "Low" };

    [RelayCommand]
    private async Task OpenDump()
    {
        var result = await PickFileAsync("Open Mercedes EIS dump");
        if (result is null)
        {
            return;
        }

        try
        {
            var bytes = LoadLocalFile(result.Value.Path);
            if (bytes.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return;
            }

            SelectedFileName = Path.GetFileName(result.Value.Path);
            SelectedFilePath = result.Value.Path;
            RawHexText = BuildRawHexText(bytes);
            AnalysisSummary = "File loaded locally. Use Analyze to send it to the server.";
            UploadSummary = string.Empty;
            Status = $"Loaded {SelectedFileName}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AnalyzeDump()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            Status = "Open a dump file before analyzing it.";
            return;
        }

        try
        {
            IsBusy = true;
            var bytes = LoadLocalFile(SelectedFilePath);
            var response = await _apiClient.AnalyzeDumpAsync(bytes, SelectedFileName);
            var details = response.AnalysisDetails;
            Vin = DisplayValue(response.DetectedVin);
            DetectedFormat = DisplayValue(response.DetectedFormat);
            EisType = details?.EisType ?? "Not mapped";
            Mcu = details?.McuType ?? "Not mapped";
            KeyCount = details?.KeyCount?.ToString() ?? "Not mapped";
            RawHexText = BuildRawHexText(bytes);
            AnalysisSummary = response.Message;
            UploadSummary = string.Empty;
            ValidationMessage = string.Empty;
            if (!string.IsNullOrWhiteSpace(response.DetectedVin))
            {
                VehicleIdentifier = response.DetectedVin;
                ValidationMessage = "Detected from dump";
                VinConfirmedByUser = false;
            }
            else
            {
                VehicleIdentifier = string.Empty;
                ValidationMessage = "No VIN detected from the dump.";
                VinConfirmedByUser = false;
            }
            Status = response.Status;
            CanConvert = false;
            CanSave = false;
        }
        catch (Exception ex)
        {
            AnalysisSummary = $"Analysis failed: {ex.Message}";
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateUploadAvailability()
    {
        CanUpload = !IsBusy && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(SelectedFileName) && !SelectedFileName.Equals("No file selected", StringComparison.OrdinalIgnoreCase) && VinConfirmedByUser && (!string.IsNullOrWhiteSpace(VehicleIdentifier) || !string.IsNullOrWhiteSpace(RegistrationNumber));
    }

    [RelayCommand]
    private async Task UploadDump()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            Status = "Open a dump file before uploading it.";
            return;
        }

        if (!VinConfirmedByUser)
        {
            Status = "Provide either a VIN or registration number and confirm it before uploading.";
            return;
        }

        if (string.IsNullOrWhiteSpace(VehicleIdentifier) && string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            Status = "Provide either a VIN or registration number and confirm it before uploading.";
            return;
        }

        try
        {
            IsBusy = true;
            var bytes = LoadLocalFile(SelectedFilePath);
            var response = await _apiClient.UploadDumpAsync(bytes, SelectedFileName, VehicleIdentifier, RegistrationNumber, VinConfirmedByUser);
            var details = response.AnalysisDetails;
            UploadSummary = $"Uploaded to server: {response.Status} | {response.Message}";
            if (details is not null)
            {
                UploadSummary += $"{Environment.NewLine}EIS type: {details.EisType ?? "Not mapped"}; Key count: {details.KeyCount?.ToString() ?? "Not mapped"}";
            }
            Status = response.Status;
            await RefreshUploadedFilesAsync();
        }
        catch (Exception ex)
        {
            UploadSummary = $"Upload failed: {ex.Message}";
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshUploadedFiles()
    {
        await RefreshUploadedFilesAsync();
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
    private async Task LoadResearchDump()
    {
        var result = await PickFileAsync("Open research EIS dump");
        if (result is null)
        {
            return;
        }

        try
        {
            var bytes = LoadLocalFile(result.Value.Path);
            if (bytes.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return;
            }

            ResearchEisPath = result.Value.Path;
            Status = $"Loaded EIS dump {Path.GetFileName(result.Value.Path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadResearchKeyFile()
    {
        var result = await PickFileAsync("Open research key file");
        if (result is null)
        {
            return;
        }

        try
        {
            _ = LoadLocalFile(result.Value.Path);
            ResearchKeyPath = result.Value.Path;
            Status = $"Loaded key file {Path.GetFileName(result.Value.Path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadResearchCompareDump()
    {
        var result = await PickFileAsync("Open comparison EIS dump");
        if (result is null)
        {
            return;
        }

        try
        {
            var bytes = LoadLocalFile(result.Value.Path);
            if (bytes.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return;
            }

            ResearchComparePath = result.Value.Path;
            Status = $"Loaded comparison dump {Path.GetFileName(result.Value.Path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AnalyzeResearchFolder()
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return;
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder for research analysis"
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        try
        {
            ResearchFolderPath = folder.Path.LocalPath;
            var analysis = AnalyzeResearchFolderLocally(folder.Path.LocalPath);
            _lastResearchAnalysis = analysis;

            var lines = new List<string>
            {
                "Folder analysis",
                "---------------"
            };
            foreach (var file in analysis.Files)
            {
                lines.Add($"{file.RelativePath} | size={file.Size} | sha256={file.Sha256} | type={file.DetectedType} | format={file.SourceFormat} | vin={file.VIN} | group={file.DuplicateGroup}");
            }

            if (analysis.DuplicateGroups.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"Duplicate groups: {string.Join(", ", analysis.DuplicateGroups)}");
            }

            ResearchFolderAnalysisText = string.Join(Environment.NewLine, lines);
            Status = $"Analyzed {analysis.Files.Count} files";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void ResearchSearch()
    {
        if (string.IsNullOrWhiteSpace(ResearchEisPath) && string.IsNullOrWhiteSpace(ResearchKeyPath) && string.IsNullOrWhiteSpace(ResearchComparePath))
        {
            ResearchSearchResults = "Load at least one research file before searching.";
            return;
        }

        var sourceBytes = BuildResearchSourceBytes();
        if (sourceBytes is null)
        {
            ResearchSearchResults = "Select a loaded source file first.";
            return;
        }

        if (!TryParseOffset(ResearchStartOffset, out var startOffset))
        {
            ResearchSearchResults = "Enter a valid start offset in decimal or 0x-prefixed hex.";
            return;
        }

        if (!int.TryParse(ResearchLength, out var length) || length < 1 || length > 64)
        {
            ResearchSearchResults = "Sequence length must be between 1 and 64.";
            return;
        }

        if (!TryParseMode(ResearchSelectedMode, ResearchXorValue, out var mode, out var xorValue))
        {
            ResearchSearchResults = "Select a valid search mode and XOR value.";
            return;
        }

        var targets = GetLoadedResearchTargets();
        if (targets.Count == 0)
        {
            ResearchSearchResults = "Load at least one target file before searching.";
            return;
        }

        var formatted = new List<string>();
        var allMatches = new List<ResearchMatchRecord>();
        foreach (var target in targets)
        {
            var matches = SearchResearchSequenceLocally(sourceBytes, target.Bytes, startOffset, length, mode, xorValue);
            allMatches.AddRange(matches);
            foreach (var match in matches)
            {
                formatted.Add($"{target.Name}: offset 0x{match.Offset:X} ({match.Mode}) {Convert.ToHexString(match.MatchedBytes)}");
            }
        }

        _lastResearchMatches = allMatches;
        ResearchSearchResults = string.Join(Environment.NewLine, formatted.Any() ? formatted : new[] { "No matches found." });
    }

    [RelayCommand]
    private void SaveResearchAnnotation()
    {
        if (string.IsNullOrWhiteSpace(ResearchAnnotationName))
        {
            Status = "Provide an annotation name before saving.";
            return;
        }

        if (!int.TryParse(ResearchAnnotationOffset, out var offset))
        {
            Status = "Provide a numeric offset.";
            return;
        }

        if (!int.TryParse(ResearchAnnotationLength, out var length) || length < 1)
        {
            Status = "Provide a positive length.";
            return;
        }

        var annotation = new ResearchAnnotation
        {
            Name = ResearchAnnotationName,
            FileFormat = ResearchAnnotationFileFormat,
            Offset = offset,
            Length = length,
            ByteOrder = ResearchAnnotationByteOrder,
            Notes = ResearchAnnotationNotes,
            Confidence = ParseConfidence(ResearchSelectedConfidence)
        };

        var path = Path.Combine(AppContext.BaseDirectory, "ResearchData", "annotations.json");
        var existing = LoadAnnotations(path);
        existing.Add(annotation);
        SaveAnnotations(path, existing);
        ResearchAnnotationsText = string.Join(Environment.NewLine, existing.Select(item => $"{item.Name} [{item.Confidence}]"));
        Status = "Annotation saved.";
    }

    [RelayCommand]
    private void ExportResearchReport()
    {
        var analysis = _lastResearchAnalysis ?? new ResearchFolderAnalysis();
        var annotationsPath = Path.Combine(AppContext.BaseDirectory, "ResearchData", "annotations.json");
        var annotations = LoadAnnotations(annotationsPath);
        var reportPath = Path.Combine(AppContext.BaseDirectory, "ResearchData", "research-report.txt");
        var report = BuildResearchReport(analysis, annotations, _lastResearchMatches);

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, report, System.Text.Encoding.UTF8);
        ResearchReportPath = reportPath;
        Status = $"Exported research report to {Path.GetFileName(reportPath)}";
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

        SequenceResults = "Server unavailable. Analysis requires an active server connection.";
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
            var data = File.ReadAllBytes(file.Path.LocalPath);
            if (data.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return null;
            }

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

        if (string.IsNullOrWhiteSpace(VehicleIdentifier) || string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            CompareText = "Provide a vehicle identifier and registration number before comparing.";
            CompareSummary = "Provide a vehicle identifier and registration number before comparing.";
            CompareOffsets = string.Empty;
            return;
        }

        CompareText = "Server unavailable. Analysis requires an active server connection.";
        CompareSummary = "Server unavailable. Analysis requires an active server connection.";
        CompareOffsets = string.Empty;
        _ = CompareDumpsAsync(_compareABytes, _compareBBytes);
    }

    private async Task<(string Path, byte[]? Data)?> PickFileAsync(string title)
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        return (file.Path.LocalPath, File.ReadAllBytes(file.Path.LocalPath));
    }

    private byte[]? BuildResearchSourceBytes()
    {
        if (ResearchSelectedSourceFile == "EIS dump" && File.Exists(ResearchEisPath))
        {
            return File.ReadAllBytes(ResearchEisPath);
        }

        if (ResearchSelectedSourceFile == "Key file" && File.Exists(ResearchKeyPath))
        {
            return File.ReadAllBytes(ResearchKeyPath);
        }

        if (ResearchSelectedSourceFile == "Compare dump" && File.Exists(ResearchComparePath))
        {
            return File.ReadAllBytes(ResearchComparePath);
        }

        return null;
    }

    private List<(string Name, byte[] Bytes)> GetLoadedResearchTargets()
    {
        var targets = new List<(string Name, byte[] Bytes)>();
        if (File.Exists(ResearchEisPath))
        {
            targets.Add(("EIS dump", File.ReadAllBytes(ResearchEisPath)));
        }

        if (File.Exists(ResearchKeyPath))
        {
            targets.Add(("Key file", File.ReadAllBytes(ResearchKeyPath)));
        }

        if (File.Exists(ResearchComparePath))
        {
            targets.Add(("Compare dump", File.ReadAllBytes(ResearchComparePath)));
        }

        return targets;
    }

    private static bool TryParseMode(string? value, string? xorValueText, out ResearchSearchMode mode, out byte xorValue)
    {
        mode = ResearchSearchMode.Exact;
        xorValue = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Equals("Xor", StringComparison.OrdinalIgnoreCase))
        {
            mode = ResearchSearchMode.Xor;
            if (byte.TryParse(xorValueText, NumberStyles.HexNumber, null, out var parsedHex))
            {
                xorValue = parsedHex;
                return true;
            }

            return byte.TryParse(xorValueText, out xorValue);
        }

        return Enum.TryParse(value, true, out mode);
    }

    private static ResearchConfidence ParseConfidence(string value)
    {
        return Enum.TryParse<ResearchConfidence>(value, true, out var confidence) ? confidence : ResearchConfidence.Unknown;
    }

    private static byte[] LoadLocalFile(string path)
    {
        return File.ReadAllBytes(path);
    }

    private static void SaveAnnotations(string path, IEnumerable<ResearchAnnotation> annotations)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(annotations.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static List<ResearchAnnotation> LoadAnnotations(string path)
    {
        if (!File.Exists(path))
        {
            return new List<ResearchAnnotation>();
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        return string.IsNullOrWhiteSpace(json) ? new List<ResearchAnnotation>() : System.Text.Json.JsonSerializer.Deserialize<List<ResearchAnnotation>>(json) ?? new List<ResearchAnnotation>();
    }

    private static string BuildResearchReport(ResearchFolderAnalysis analysis, IEnumerable<ResearchAnnotation> annotations, IEnumerable<ResearchMatchRecord> matches)
    {
        var lines = new List<string> { "Research report", "===============" };
        lines.Add($"Files analyzed: {analysis.Files.Count}");
        if (analysis.DuplicateGroups.Count > 0)
        {
            lines.Add($"Duplicate groups: {string.Join(", ", analysis.DuplicateGroups)}");
        }

        lines.Add(string.Empty);
        lines.Add("Files:");
        foreach (var file in analysis.Files)
        {
            lines.Add($"- {file.RelativePath} | size={file.Size} | sha256={file.Sha256} | type={file.DetectedType} | format={file.SourceFormat} | vin={file.VIN} | group={file.DuplicateGroup}");
        }

        lines.Add(string.Empty);
        lines.Add("Annotations:");
        foreach (var annotation in annotations)
        {
            lines.Add($"- {annotation.Name} | confidence={annotation.Confidence} | offset=0x{annotation.Offset:X} | length={annotation.Length}");
        }

        lines.Add(string.Empty);
        lines.Add("Matches:");
        foreach (var match in matches)
        {
            lines.Add($"- offset=0x{match.Offset:X} | mode={match.Mode} | bytes={Convert.ToHexString(match.MatchedBytes)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ResearchFolderAnalysis AnalyzeResearchFolderLocally(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The selected folder does not exist.");
        }

        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => File.Exists(path))
            .Select(path => new FileInfo(path))
            .Where(info => info.Length > 0)
            .OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var analysis = new ResearchFolderAnalysis();
        var shaGroups = new Dictionary<string, List<ResearchFolderFile>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInfo in files)
        {
            var bytes = File.ReadAllBytes(fileInfo.FullName);
            var sha = ComputeSha256(bytes);
            var file = new ResearchFolderFile
            {
                FileName = fileInfo.Name,
                RelativePath = fileInfo.FullName.Replace(folderPath, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Size = fileInfo.Length,
                Sha256 = sha,
                DetectedType = bytes.Length == 256 ? "EIS dump" : "Possible key file",
                SourceFormat = bytes.Length == 256 ? "Unknown" : string.Empty,
                VIN = string.Empty,
                Bytes = bytes
            };

            if (shaGroups.TryGetValue(sha, out var group))
            {
                group.Add(file);
            }
            else
            {
                shaGroups[sha] = new List<ResearchFolderFile> { file };
            }

            analysis.Files.Add(file);
        }

        foreach (var group in shaGroups.Where(group => group.Value.Count > 1))
        {
            for (var index = 0; index < group.Value.Count; index++)
            {
                group.Value[index].DuplicateGroup = index + 1;
            }
        }

        analysis.DuplicateGroups = analysis.Files.Where(file => file.DuplicateGroup > 0).Select(file => file.DuplicateGroup).Distinct().OrderBy(value => value).ToList();
        return analysis;
    }

    private static List<ResearchMatchRecord> SearchResearchSequenceLocally(byte[] source, byte[] target, int startOffset, int length, ResearchSearchMode mode, byte xorValue)
    {
        var sequence = source.Skip(startOffset).Take(length).ToArray();
        var results = new List<ResearchMatchRecord>();
        for (var index = 0; index <= target.Length - length; index++)
        {
            var candidate = target.Skip(index).Take(length).ToArray();
            if (MatchesResearchSequence(sequence, candidate, mode, xorValue))
            {
                results.Add(new ResearchMatchRecord { Offset = index, Mode = mode, MatchedBytes = candidate.ToArray() });
            }
        }

        return results;
    }

    private static bool MatchesResearchSequence(byte[] sequence, byte[] candidate, ResearchSearchMode mode, byte xorValue)
    {
        return mode switch
        {
            ResearchSearchMode.Reversed => candidate.SequenceEqual(sequence.Reverse().ToArray()),
            ResearchSearchMode.BytePairSwapped => BytePairSwappedMatches(sequence, candidate),
            ResearchSearchMode.FourByteWordReversed => FourByteWordReversedMatches(sequence, candidate),
            ResearchSearchMode.Xor => candidate.SequenceEqual(sequence.Select(value => (byte)(value ^ xorValue)).ToArray()),
            _ => candidate.SequenceEqual(sequence)
        };
    }

    private static bool BytePairSwappedMatches(byte[] sequence, byte[] candidate)
    {
        if (sequence.Length % 2 != 0 || candidate.Length % 2 != 0)
        {
            return false;
        }

        var expected = new byte[sequence.Length];
        for (var i = 0; i < sequence.Length; i += 2)
        {
            expected[i] = sequence[i + 1];
            expected[i + 1] = sequence[i];
        }

        return candidate.SequenceEqual(expected);
    }

    private static bool FourByteWordReversedMatches(byte[] sequence, byte[] candidate)
    {
        if (sequence.Length < 4 || candidate.Length < 4)
        {
            return false;
        }

        var expected = new byte[sequence.Length];
        Array.Copy(sequence, expected, sequence.Length);

        for (var i = 0; i + 3 < expected.Length; i += 4)
        {
            Array.Reverse(expected, i, 4);
        }

        return candidate.SequenceEqual(expected);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static IMercedesEisApiClient CreateApiClient(string baseUrl)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
        return new MercedesEisApiClient(httpClient);
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

    private async Task AnalyzeDumpAsync(byte[] data, string fileName)
    {
        try
        {
            ServerStatus = "Connecting";
            var response = await _apiClient.AnalyzeDumpAsync(data, fileName);
            var details = response.AnalysisDetails;
            Vin = DisplayValue(response.DetectedVin);
            DetectedFormat = DisplayValue(response.DetectedFormat);
            EisType = details?.EisType ?? "Not mapped";
            Mcu = details?.McuType ?? "Not mapped";
            KeyCount = details?.KeyCount?.ToString() ?? "Not mapped";
            RawHexText = BuildRawHexText(data);
            ServerStatus = "Connected";
            Status = response.Status;
            CanConvert = false;
            CanSave = false;
        }
        catch (Exception)
        {
            ServerStatus = "Offline";
            Vin = "Unknown";
            DetectedFormat = "Unknown";
            EisType = "Unknown";
            Mcu = "Unknown";
            KeyCount = "0";
            RawHexText = BuildRawHexText(data);
            Status = "Server unavailable. Analysis requires an active server connection.";
        }
    }

    private async Task RefreshUploadedFilesAsync()
    {
        try
        {
            var response = await _apiClient.GetUploadedDumpsAsync();
            var lines = response.Uploads.Select(upload => $"{upload.FileName} | {upload.UserProvidedVin ?? string.Empty} | {upload.UserProvidedRegistrationNumber ?? string.Empty} | {upload.Operation} | {upload.SizeBytes} bytes").ToList();
            UploadedFilesSummary = lines.Any() ? string.Join(Environment.NewLine, lines) : "No uploaded files yet.";
        }
        catch (Exception ex)
        {
            UploadedFilesSummary = $"Unable to load uploads: {ex.Message}";
        }
    }

    private async Task CompareDumpsAsync(byte[] left, byte[] right)
    {
        try
        {
            ServerStatus = "Connecting";
            var response = await _apiClient.CompareDumpsAsync(left, right, "left.bin", "right.bin", VehicleIdentifier, RegistrationNumber);
            CompareText = $"Total differing bytes: {response.TotalDifferences}{Environment.NewLine}Offsets: {string.Join(", ", response.DifferingOffsets)}";
            CompareSummary = "Compared via server";
            CompareOffsets = $"Differing bytes: {response.TotalDifferences} | Offsets: {FormatOffsets(response.DifferingOffsets)}";
            ServerStatus = "Connected";
        }
        catch (Exception)
        {
            ServerStatus = "Offline";
            CompareText = "Server unavailable. Analysis requires an active server connection.";
            CompareSummary = "Server unavailable. Analysis requires an active server connection.";
            CompareOffsets = string.Empty;
        }
    }

    private async Task RefreshServerStatusAsync()
    {
        try
        {
            ServerStatus = "Connecting";
            var response = await _apiClient.GetHealthAsync();
            ServerStatus = response.IsHealthy ? "Connected" : "Offline";
            ConnectionReason = response.Status;
            LastChecked = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            ServerStatus = "Offline";
            ConnectionReason = ex.Message;
            LastChecked = DateTime.Now.ToString("HH:mm:ss");
        }
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

    private static string BuildComparisonText(byte[] left, byte[] right, CompareDumpsResponse comparison)
    {
        if (left is null || right is null || left.Length != 256 || right.Length != 256)
        {
            return "Load two 256-byte dumps to compare them.";
        }

        var lines = new List<string>
        {
            $"Total differing bytes: {comparison.TotalDifferences}",
            $"Offsets: {string.Join(", ", comparison.DifferingOffsets)}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private sealed class ResearchFolderAnalysis
    {
        public List<ResearchFolderFile> Files { get; } = new();
        public List<int> DuplicateGroups { get; set; } = new();
    }

    private sealed class ResearchFolderFile
    {
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string DetectedType { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string VIN { get; set; } = string.Empty;
        public int DuplicateGroup { get; set; }
        public byte[]? Bytes { get; set; }
    }

    private sealed class ResearchMatchRecord
    {
        public int Offset { get; set; }
        public ResearchSearchMode Mode { get; set; }
        public byte[] MatchedBytes { get; set; } = Array.Empty<byte>();
    }

    private sealed class ResearchAnnotation
    {
        public string Name { get; set; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Length { get; set; }
        public string ByteOrder { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public ResearchConfidence Confidence { get; set; } = ResearchConfidence.Unknown;
    }

    private enum ResearchSearchMode
    {
        Exact,
        Reversed,
        BytePairSwapped,
        FourByteWordReversed,
        Xor
    }

    private enum ResearchConfidence
    {
        Unknown,
        Suspected,
        Probable,
        Verified,
        Low
    }
}
