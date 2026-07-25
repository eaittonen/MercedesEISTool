using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.Server.Services;

public interface IUploadedDumpStore
{
    Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation, IEisAnalysisService? analysisService = null);
    Task<List<UploadedDumpRecord>> ListAsync();
    Task<StoredFileAnalysisSnapshot?> GetLatestAnalysisAsync(Guid storedFileId);
    Task<StoredFileAnalysisSnapshot?> AnalyzeAndStoreAsync(Guid storedFileId, IEisAnalysisService analysisService);
}

public class UploadedDumpRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string VehicleIdentifier { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public long SizeBytes { get; set; }
    public StoredFileAnalysisSnapshot? LatestAnalysis { get; set; }
    public List<StoredFileAnalysisSnapshot> AnalysisHistory { get; set; } = new();
}

public class StoredFileAnalysisSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoredFileId { get; set; }
    public string ParserVersion { get; set; } = string.Empty;
    public string DetectedFormat { get; set; } = "Unknown";
    public string? DetectedVin { get; set; }
    public string VinStatus { get; set; } = "NotMapped";
    public string? EisType { get; set; }
    public FieldValueStatus EisTypeStatus { get; set; }
    public string? McuType { get; set; }
    public FieldValueStatus McuTypeStatus { get; set; }
    public int? KeyCount { get; set; }
    public FieldValueStatus KeyCountStatus { get; set; }
    public SensitiveFieldDto EisPassword { get; set; } = new();
    public SensitiveFieldDto Ssid { get; set; } = new();
    public List<KeySlotDto> Keys { get; set; } = new();
    public List<SensitiveFieldDto> AdditionalFields { get; set; } = new();
    public DateTimeOffset AnalyzedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool AnalysisSucceeded { get; set; } = true;
    public string? FailureMessage { get; set; }
}
