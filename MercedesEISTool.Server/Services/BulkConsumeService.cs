using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

public sealed class BulkConsumeService
{
    private readonly IUploadedDumpStore _uploadedDumpStore;
    private readonly IEisAnalysisService _analysisService;
    private readonly IKeyFileAnalysisService _keyFileAnalysisService;
    private readonly ILogger<BulkConsumeService> _logger;
    private readonly BulkConsumeFileDetectorRegistry _detectorRegistry;

    public BulkConsumeService(
        IUploadedDumpStore uploadedDumpStore,
        IEisAnalysisService analysisService,
        IKeyFileAnalysisService keyFileAnalysisService,
        ILogger<BulkConsumeService> logger)
    {
        _uploadedDumpStore = uploadedDumpStore;
        _analysisService = analysisService;
        _keyFileAnalysisService = keyFileAnalysisService;
        _logger = logger;
        _detectorRegistry = new BulkConsumeFileDetectorRegistry();
        _detectorRegistry.Register(new SizeBasedBulkConsumeDetector());
    }

    public async Task<BulkConsumePreviewResponse> PreviewAsync(string sourceFolderPath, bool includeSubdirectories)
    {
        var resolvedPath = ResolveSourceFolderPath(sourceFolderPath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException("The selected source folder does not exist.");
        }

        var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(resolvedPath, "*", searchOption)
            .Where(path => File.Exists(path))
            .Select(path => new FileInfo(path))
            .OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<BulkConsumePreviewItemDto>();
        var groups = new Dictionary<string, BulkConsumePreviewGroupDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var bytes = await File.ReadAllBytesAsync(file.FullName);
            var detection = _detectorRegistry.Detect(bytes, file.Name);
            var classification = detection.DetectedFormat;
            if (!string.Equals(classification, "EIS dump", StringComparison.OrdinalIgnoreCase) && !string.Equals(classification, "CGMB key file", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var analysis = _analysisService.Analyze(bytes, file.Name);
            var detectedVin = analysis.DetectedVin;
            var detectedFormat = analysis.DetectedFormat;
            var keyFileAnalysis = string.Equals(classification, "CGMB key file", StringComparison.OrdinalIgnoreCase)
                ? _keyFileAnalysisService.Analyze(bytes, file.Name)
                : null;

            var item = new BulkConsumePreviewItemDto
            {
                SourcePath = file.FullName,
                FileName = file.Name,
                SizeBytes = file.Length,
                Sha256 = ComputeSha256(bytes),
                Classification = classification,
                DetectedFormat = detectedFormat,
                DetectedVin = detectedVin,
                RegistrationNumber = ExtractRegistrationNumber(file.DirectoryName ?? string.Empty),
                OriginalSourceFolderName = Path.GetFileName(resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                OriginalRelativePath = Path.GetRelativePath(resolvedPath, file.FullName),
                Action = "Import",
                Notes = keyFileAnalysis is not null ? $"Key analysis confidence: {keyFileAnalysis.DetectionConfidence}" : string.Empty,
                IsSelected = true
            };

            items.Add(item);

            var groupKey = GetGroupKey(resolvedPath, file.FullName);
            if (!groups.TryGetValue(groupKey, out var group))
            {
                var displayName = string.IsNullOrWhiteSpace(groupKey) ? Path.GetFileName(resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : groupKey;
                group = new BulkConsumePreviewGroupDto
                {
                    DisplayName = displayName,
                    GroupKey = groupKey
                };
                groups[groupKey] = group;
            }

            group.Children.Add(item);
        }

        return new BulkConsumePreviewResponse
        {
            SourceFolderPath = resolvedPath,
            IncludeSubdirectories = includeSubdirectories,
            Items = items,
            Groups = groups.Values.ToList(),
            TotalFiles = items.Count,
            Summary = $"{items.Count} supported files ready to import."
        };
    }

    public async Task<BulkConsumeImportResponse> ImportAsync(BulkConsumeImportRequest request, ICurrentUser? currentUser = null)
    {
        if (request is null || request.Items.Count == 0)
        {
            throw new ArgumentException("At least one import item is required.", nameof(request));
        }

        var results = new List<BulkConsumeImportResultDto>();
        foreach (var item in request.Items)
        {
            var bytes = await File.ReadAllBytesAsync(item.SourcePath);
            var uploadedDump = await _uploadedDumpStore.PersistAsync(
                bytes,
                item.FileName,
                item.VehicleIdentifier ?? string.Empty,
                item.RegistrationNumber ?? string.Empty,
                "bulk-consume",
                _analysisService,
                currentUser,
                string.Equals(item.Classification, "CGMB key file", StringComparison.OrdinalIgnoreCase) ? FileCategory.KeyFile : FileCategory.EisDump,
                item.CustomerName);

            results.Add(new BulkConsumeImportResultDto
            {
                SourcePath = item.SourcePath,
                FileName = item.FileName,
                StoredFileId = uploadedDump.Id,
                Status = "Imported",
                Message = $"Imported {item.FileName}"
            });
        }

        return new BulkConsumeImportResponse
        {
            BatchId = Guid.NewGuid(),
            ImportedCount = results.Count,
            Results = results,
            Message = $"Imported {results.Count} file(s)."
        };
    }

    private static string ExtractRegistrationNumber(string path)
    {
        var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        return segments.LastOrDefault() ?? string.Empty;
    }

    private static string ResolveSourceFolderPath(string sourceFolderPath)
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(sourceFolderPath, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeFile || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (uri.IsFile)
            {
                return uri.LocalPath;
            }

            return string.Empty;
        }

        return sourceFolderPath.Trim();
    }

    private static string GetGroupKey(string sourceRootPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(sourceRootPath, filePath);
        var segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return string.IsNullOrWhiteSpace(Path.GetFileName(sourceRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
                ? "Root"
                : Path.GetFileName(sourceRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return segments[0];
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}
