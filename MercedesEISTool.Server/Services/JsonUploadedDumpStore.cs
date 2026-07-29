using System.Security.Cryptography;
using System.Text.Json;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

public class JsonUploadedDumpStore : IUploadedDumpStore
{
    private readonly string _rootPath;
    private readonly string _indexPath;

    public JsonUploadedDumpStore(string? rootPath = null)
    {
        _rootPath = ResolveStorageRoot(rootPath);
        _indexPath = Path.Combine(_rootPath, "index.json");
        Directory.CreateDirectory(_rootPath);
        MigrateLegacyStorageIfNeeded();
    }

    public static string ResolveStorageRoot(string? rootPath = null)
    {
        var configuredRoot = rootPath
            ?? Environment.GetEnvironmentVariable("MERCEDES_EIS_UPLOAD_ROOT")
            ?? Environment.GetEnvironmentVariable("UPLOAD_STORAGE_ROOT");

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var trimmed = configuredRoot.Trim();
            if (trimmed.EndsWith("uploads", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith("uploads/", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(trimmed);
            }

            return Path.GetFullPath(Path.Combine(trimmed, "uploads"));
        }

        if (OperatingSystem.IsWindows())
        {
            var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.GetFullPath(Path.Combine(appDataRoot, "MercedesEISTool", "uploads"));
        }

        return Path.GetFullPath(Path.Combine("/var/lib", "mercedes-eis-tool", "uploads"));
    }

    public async Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation, IEisAnalysisService? analysisService = null, ICurrentUser? currentUser = null, FileCategory fileCategory = FileCategory.Unknown, string? customerName = null, string? additionalInformation = null, bool allowMissingIdentifiers = false)
    {
        if (!allowMissingIdentifiers && string.IsNullOrWhiteSpace(vehicleIdentifier) && string.IsNullOrWhiteSpace(registrationNumber))
        {
            throw new ArgumentException("At least one identifier is required for uploads.", nameof(vehicleIdentifier));
        }

        var record = new UploadedDumpRecord
        {
            FileName = Path.GetFileName(fileName),
            VehicleIdentifier = vehicleIdentifier.Trim(),
            RegistrationNumber = registrationNumber.Trim(),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            AdditionalInformation = string.IsNullOrWhiteSpace(additionalInformation) ? null : additionalInformation.Trim(),
            Operation = operation,
            SizeBytes = data.Length,
            UploadedByUserId = currentUser?.UserId ?? "development",
            FileCategory = fileCategory
        };

        var fileNameSafe = SanitizeFileName(record.FileName);
        var storedFilePath = Path.Combine(_rootPath, $"{record.Id:N}-{fileNameSafe}");
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

    public async Task<List<UploadedDumpRecord>> ListAsync(ICurrentUser? currentUser = null, string? search = null, int page = 1, int pageSize = 50)
    {
        var records = await LoadRecordsAsync();
        var filtered = records.Where(record => CanAccessRecord(record, currentUser)).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var query = search.Trim();
            filtered = filtered.Where(record =>
                record.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || record.VehicleIdentifier.Contains(query, StringComparison.OrdinalIgnoreCase)
                || record.RegistrationNumber.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (record.CustomerName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || record.Operation.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return filtered
            .OrderByDescending(record => record.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
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

    public async Task<CgmbKeyFileAnalysisDto?> AnalyzeAndStoreKeyFileAsync(Guid storedFileId, IKeyFileAnalysisService analysisService, ICurrentUser? currentUser = null)
    {
        var records = await LoadRecordsAsync();
        var record = records.FirstOrDefault(item => item.Id == storedFileId);
        if (record is null || !CanAccessRecord(record, currentUser))
        {
            return null;
        }

        var rawBytes = await File.ReadAllBytesAsync(record.StoredFilePath);
        var analysis = analysisService.Analyze(rawBytes, record.FileName);
        record.KeyFileAnalysis = analysis;
        record.FileCategory = FileCategory.KeyFile;
        await SaveRecordsAsync(records);
        return analysis;
    }

    public async Task<byte[]> ReadStoredFileAsync(Guid storedFileId, ICurrentUser? currentUser = null)
    {
        var records = await LoadRecordsAsync();
        var record = records.FirstOrDefault(item => item.Id == storedFileId);
        if (record is null || !CanAccessRecord(record, currentUser))
        {
            throw new FileNotFoundException("Stored file was not found.", storedFileId.ToString());
        }

        return await File.ReadAllBytesAsync(record.StoredFilePath);
    }

    public async Task<UploadedDumpRecord?> GetByIdAsync(Guid storedFileId, ICurrentUser? currentUser = null)
    {
        var records = await LoadRecordsAsync();
        var record = records.FirstOrDefault(item => item.Id == storedFileId);
        return record is not null && CanAccessRecord(record, currentUser) ? record : null;
    }

    private static bool CanAccessRecord(UploadedDumpRecord record, ICurrentUser? currentUser)
    {
        if (currentUser is null)
        {
            return true;
        }

        var currentUserId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return true;
        }

        var isDevelopmentUser = string.Equals(currentUserId, "development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentUser.DisplayName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.UploadedByUserId, "development", StringComparison.OrdinalIgnoreCase);

        return isDevelopmentUser
            || string.Equals(record.UploadedByUserId, currentUserId, StringComparison.OrdinalIgnoreCase);
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
        Directory.CreateDirectory(_rootPath);
        await using var stream = File.Create(_indexPath);
        await JsonSerializer.SerializeAsync(stream, records, new JsonSerializerOptions { WriteIndented = true });
    }

    private void MigrateLegacyStorageIfNeeded()
    {
        var existingFiles = Directory.EnumerateFiles(_rootPath)
            .Where(path => !string.Equals(Path.GetFileName(path), ".write-test", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (File.Exists(_indexPath) || existingFiles.Count > 0)
        {
            return;
        }

        var legacyRootCandidates = GetLegacyRootCandidates().ToList();
        foreach (var legacyRoot in legacyRootCandidates)
        {
            var legacyIndexPath = Path.Combine(legacyRoot, "index.json");
            if (!File.Exists(legacyIndexPath))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(_rootPath);
                using var stream = File.OpenRead(legacyIndexPath);
                var records = JsonSerializer.Deserialize<List<UploadedDumpRecord>>(stream) ?? new List<UploadedDumpRecord>();
                foreach (var record in records)
                {
                    if (string.IsNullOrWhiteSpace(record.StoredFilePath))
                    {
                        continue;
                    }

                    var sourcePath = record.StoredFilePath;
                    if (!File.Exists(sourcePath))
                    {
                        var legacySourcePath = Path.Combine(legacyRoot, Path.GetFileName(sourcePath));
                        if (File.Exists(legacySourcePath))
                        {
                            sourcePath = legacySourcePath;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var destinationPath = Path.Combine(_rootPath, Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                    record.StoredFilePath = destinationPath;
                }

                File.WriteAllText(_indexPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            catch
            {
                // Best-effort migration. The next startup will retry if the directory is still empty.
            }
        }
    }

    private IEnumerable<string> GetLegacyRootCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var searchRoots = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var searchRoot in searchRoots)
        {
            AddCandidate(candidates, searchRoot);
            AddCandidate(candidates, Path.Combine(searchRoot, "App_Data", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "App_Data", "uploads", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "Configuration", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "Configuration", "uploads", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "bin", "Debug", "net8.0", "App_Data", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "bin", "Debug", "net8.0", "App_Data", "uploads", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "bin", "Release", "net8.0", "App_Data", "uploads"));
            AddCandidate(candidates, Path.Combine(searchRoot, "bin", "Release", "net8.0", "App_Data", "uploads", "uploads"));

            var parent = Directory.GetParent(searchRoot);
            while (parent is not null)
            {
                AddCandidate(candidates, Path.Combine(parent.FullName, "App_Data", "uploads"));
                AddCandidate(candidates, Path.Combine(parent.FullName, "App_Data", "uploads", "uploads"));
                AddCandidate(candidates, Path.Combine(parent.FullName, "Configuration", "uploads"));
                AddCandidate(candidates, Path.Combine(parent.FullName, "Configuration", "uploads", "uploads"));
                parent = parent.Parent;
            }
        }

        return candidates.OrderBy(candidate => candidate.Length);
    }

    private static void AddCandidate(HashSet<string> candidates, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        candidates.Add(Path.GetFullPath(candidate));
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "upload.bin" : sanitized;
    }
}
