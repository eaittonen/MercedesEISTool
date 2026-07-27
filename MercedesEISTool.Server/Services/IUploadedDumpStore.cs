using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

public interface IUploadedDumpStore
{
    Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation, IEisAnalysisService? analysisService = null, ICurrentUser? currentUser = null, FileCategory fileCategory = FileCategory.Unknown, string? customerName = null, string? additionalInformation = null);
    Task<List<UploadedDumpRecord>> ListAsync(ICurrentUser? currentUser = null, string? search = null, int page = 1, int pageSize = 50);
    Task<StoredFileAnalysisSnapshot?> GetLatestAnalysisAsync(Guid storedFileId);
    Task<StoredFileAnalysisSnapshot?> AnalyzeAndStoreAsync(Guid storedFileId, IEisAnalysisService analysisService);
    Task<CgmbKeyFileAnalysisDto?> AnalyzeAndStoreKeyFileAsync(Guid storedFileId, IKeyFileAnalysisService analysisService, ICurrentUser? currentUser = null);
    Task<byte[]> ReadStoredFileAsync(Guid storedFileId, ICurrentUser? currentUser = null);
    Task<UploadedDumpRecord?> GetByIdAsync(Guid storedFileId, ICurrentUser? currentUser = null);
}

public enum FileCategory
{
    Unknown,
    EisDump,
    KeyFile
}

public class UploadedDumpRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string VehicleIdentifier { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? AdditionalInformation { get; set; }
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public long SizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = "development";
    public FileCategory FileCategory { get; set; } = FileCategory.Unknown;
    public StoredFileAnalysisSnapshot? LatestAnalysis { get; set; }
    public List<StoredFileAnalysisSnapshot> AnalysisHistory { get; set; } = new();
    public CgmbKeyFileAnalysisDto? KeyFileAnalysis { get; set; }
    public string LockGroupKey { get; set; } = string.Empty;
    public int MetadataCompletenessScore { get; set; }
    public bool HasEisPassword { get; set; }
    public bool IsPreferredVersion { get; set; }
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
