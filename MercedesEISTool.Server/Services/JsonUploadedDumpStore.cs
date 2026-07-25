using System.Text.Json;
using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.Server.Services;

public class JsonUploadedDumpStore : IUploadedDumpStore
{
    private readonly string _rootPath;
    private readonly string _indexPath;
    private readonly string _uploadsPath;

    public JsonUploadedDumpStore(string? rootPath = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads") : rootPath;
        _uploadsPath = Path.Combine(_rootPath, "uploads");
        _indexPath = Path.Combine(_uploadsPath, "index.json");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation, IEisAnalysisService? analysisService = null)
    {
        if (string.IsNullOrWhiteSpace(vehicleIdentifier) && string.IsNullOrWhiteSpace(registrationNumber))
        {
            throw new ArgumentException("At least one identifier is required for uploads.", nameof(vehicleIdentifier));
        }

        var record = new UploadedDumpRecord
        {
            FileName = Path.GetFileName(fileName),
            VehicleIdentifier = vehicleIdentifier.Trim(),
            RegistrationNumber = registrationNumber.Trim(),
            Operation = operation,
            SizeBytes = data.Length
        };

        var fileNameSafe = SanitizeFileName(record.FileName);
        var storedFilePath = Path.Combine(_uploadsPath, $"{record.Id:N}-{fileNameSafe}");
        await File.WriteAllBytesAsync(storedFilePath, data);

        record.StoredFilePath = storedFilePath;

        var records = await LoadRecordsAsync();
        records.Add(record);
        await SaveRecordsAsync(records);

        if (analysisService is not null)
        {
            await AnalyzeAndStoreAsync(record.Id, analysisService);
        }

        return record;
    }

    public async Task<List<UploadedDumpRecord>> ListAsync()
    {
        return await LoadRecordsAsync();
    }

    public async Task<StoredFileAnalysisSnapshot?> GetLatestAnalysisAsync(Guid storedFileId)
    {
        var records = await LoadRecordsAsync();
        var record = records.FirstOrDefault(item => item.Id == storedFileId);
        return record?.LatestAnalysis;
    }

    public async Task<StoredFileAnalysisSnapshot?> AnalyzeAndStoreAsync(Guid storedFileId, IEisAnalysisService analysisService)
    {
        var records = await LoadRecordsAsync();
        var record = records.FirstOrDefault(item => item.Id == storedFileId);
        if (record is null)
        {
            return null;
        }

        var rawBytes = await File.ReadAllBytesAsync(record.StoredFilePath);
        var analysis = analysisService.Analyze(rawBytes, record.FileName);
        var snapshot = new StoredFileAnalysisSnapshot
        {
            StoredFileId = record.Id,
            ParserVersion = analysis.ParserVersion,
            DetectedFormat = analysis.DetectedFormat,
            DetectedVin = analysis.DetectedVin,
            VinStatus = analysis.VinStatus,
            EisType = analysis.EisType,
            EisTypeStatus = analysis.EisTypeStatus,
            McuType = analysis.McuType,
            McuTypeStatus = analysis.McuTypeStatus,
            KeyCount = analysis.KeyCount,
            KeyCountStatus = analysis.KeyCountStatus,
            EisPassword = analysis.EisPassword,
            Ssid = analysis.Ssid,
            Keys = analysis.Keys,
            AdditionalFields = analysis.AdditionalFields,
            AnalyzedAtUtc = analysis.AnalyzedAtUtc,
            AnalysisSucceeded = true
        };

        record.LatestAnalysis = snapshot;
        record.AnalysisHistory.Add(snapshot);
        await SaveRecordsAsync(records);
        return snapshot;
    }

    private async Task<List<UploadedDumpRecord>> LoadRecordsAsync()
    {
        if (!File.Exists(_indexPath))
        {
            return new List<UploadedDumpRecord>();
        }

        await using var stream = File.OpenRead(_indexPath);
        var records = await JsonSerializer.DeserializeAsync<List<UploadedDumpRecord>>(stream);
        return records ?? new List<UploadedDumpRecord>();
    }

    private async Task SaveRecordsAsync(List<UploadedDumpRecord> records)
    {
        Directory.CreateDirectory(_uploadsPath);
        await using var stream = File.Create(_indexPath);
        await JsonSerializer.SerializeAsync(stream, records, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "upload.bin" : sanitized;
    }
}
