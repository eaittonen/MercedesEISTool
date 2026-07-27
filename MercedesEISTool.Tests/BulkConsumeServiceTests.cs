using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public sealed class BulkConsumeServiceTests
{
    [Fact]
    public async Task PreviewAsync_ReportsSupportedDumpAndSkipsUnsupportedFiles()
    {
        using var tempRoot = new TempDirectory();
        var dumpPath = Path.Combine(tempRoot.Path, "dump.bin");
        await File.WriteAllBytesAsync(dumpPath, CreateValidEisDumpBytes());
        var unsupportedPath = Path.Combine(tempRoot.Path, "notes.txt");
        await File.WriteAllTextAsync(unsupportedPath, "not an eis dump");

        var service = new BulkConsumeService(
            new FakeUploadedDumpStore(),
            new EisAnalysisService(),
            new KeyFileAnalysisService(),
            NullLogger<BulkConsumeService>.Instance);

        var response = await service.PreviewAsync(tempRoot.Path, includeSubdirectories: false);

        Assert.Equal(tempRoot.Path, response.SourceFolderPath);
        Assert.Equal(2, response.Items.Count);
        Assert.Contains(response.Items, item => item.FileName == "dump.bin" && item.Action == "Import" && item.Classification == "EIS dump" && item.IsSelected);
        Assert.Contains(response.Items, item => item.FileName == "notes.txt" && item.Action == "Skip" && item.Classification == "Unsupported" && !item.IsSelected);
    }

    [Fact]
    public async Task PreviewAsync_ResolvesFileUriPaths()
    {
        using var tempRoot = new TempDirectory();
        var dumpPath = Path.Combine(tempRoot.Path, "dump.bin");
        await File.WriteAllBytesAsync(dumpPath, CreateValidEisDumpBytes());

        var service = new BulkConsumeService(
            new FakeUploadedDumpStore(),
            new EisAnalysisService(),
            new KeyFileAnalysisService(),
            NullLogger<BulkConsumeService>.Instance);

        var uriPath = new Uri(tempRoot.Path).AbsoluteUri;
        var response = await service.PreviewAsync(uriPath, includeSubdirectories: false);

        Assert.Single(response.Items);
        Assert.Equal("EIS dump", response.Items[0].Classification);
    }

    [Fact]
    public async Task PreviewAsync_GroupsFilesBySourceFolderAndIncludesChildRows()
    {
        using var tempRoot = new TempDirectory();
        var vehicleOnePath = Path.Combine(tempRoot.Path, "vehicle-1");
        var vehicleTwoPath = Path.Combine(tempRoot.Path, "vehicle-2");
        Directory.CreateDirectory(vehicleOnePath);
        Directory.CreateDirectory(vehicleTwoPath);

        await File.WriteAllBytesAsync(Path.Combine(vehicleOnePath, "dump.bin"), CreateValidEisDumpBytes());
        await File.WriteAllTextAsync(Path.Combine(vehicleOnePath, "notes.txt"), "ignore me");
        await File.WriteAllBytesAsync(Path.Combine(vehicleTwoPath, "key.bin"), CreateValidKeyFileBytes());

        var service = new BulkConsumeService(
            new FakeUploadedDumpStore(),
            new EisAnalysisService(),
            new KeyFileAnalysisService(),
            NullLogger<BulkConsumeService>.Instance);

        var response = await service.PreviewAsync(tempRoot.Path, includeSubdirectories: true);

        Assert.Equal(2, response.Groups.Count);
        Assert.Contains(response.Groups, group => group.DisplayName.Contains("vehicle-1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Groups, group => group.DisplayName.Contains("vehicle-2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Groups.SelectMany(group => group.Children), child => child.Classification == "EIS dump");
        Assert.Contains(response.Groups.SelectMany(group => group.Children), child => child.Classification == "CGMB key file");
    }

    [Fact]
    public async Task PreviewAsync_KeepsUnknownFilesVisibleAsUnsupported()
    {
        using var tempRoot = new TempDirectory();
        var unknownPath = Path.Combine(tempRoot.Path, "unknown.bin");
        await File.WriteAllBytesAsync(unknownPath, new byte[32]);

        var service = new BulkConsumeService(
            new FakeUploadedDumpStore(),
            new EisAnalysisService(),
            new KeyFileAnalysisService(),
            NullLogger<BulkConsumeService>.Instance);

        var response = await service.PreviewAsync(tempRoot.Path, includeSubdirectories: false);

        Assert.Single(response.Items);
        Assert.Equal("Unsupported", response.Items[0].Classification);
        Assert.Equal(1, response.TotalFiles);
    }

    [Fact]
    public async Task ImportAsync_ContinuesAfterPerFileFailures()
    {
        using var tempRoot = new TempDirectory();
        var dumpPath = Path.Combine(tempRoot.Path, "dump.bin");
        await File.WriteAllBytesAsync(dumpPath, CreateValidEisDumpBytes());

        var service = new BulkConsumeService(
            new FakeUploadedDumpStore(),
            new EisAnalysisService(),
            new KeyFileAnalysisService(),
            NullLogger<BulkConsumeService>.Instance);

        var request = new BulkConsumeImportRequest
        {
            Items = new List<BulkConsumeImportItemRequest>
            {
                new() { SourcePath = dumpPath, FileName = "dump.bin", Classification = "EIS dump" },
                new() { SourcePath = Path.Combine(tempRoot.Path, "missing.bin"), FileName = "missing.bin", Classification = "Unsupported" }
            }
        };

        var response = await service.ImportAsync(request);

        Assert.Equal(2, response.Results.Count);
        Assert.Equal(1, response.ImportedCount);
        Assert.Equal("Imported", response.Results[0].Status);
        Assert.Equal("Failed", response.Results[1].Status);
    }

    [Fact]
    public void DetectorRegistry_DetectsKeyAndDumpFilesFromContent()
    {
        var registry = new BulkConsumeFileDetectorRegistry();
        registry.Register(new AnalysisBasedBulkConsumeDetector(new EisAnalysisService(), new KeyFileAnalysisService()));

        var dumpResult = registry.Detect(CreateValidEisDumpBytes(), "dump.bin");
        var keyResult = registry.Detect(CreateValidKeyFileBytes(), "key.bin");

        Assert.Equal("EIS dump", dumpResult.DetectedFormat);
        Assert.True(dumpResult.Confidence > 0.5);
        Assert.Equal("CGMB key file", keyResult.DetectedFormat);
        Assert.True(keyResult.Confidence > 0.5);
    }

    private static byte[] CreateValidEisDumpBytes()
    {
        var data = new byte[256];
        var vin = "WVWZZZ1JZ3W12345";
        var vinBytes = Encoding.ASCII.GetBytes(vin);
        Array.Copy(vinBytes, data, vinBytes.Length);
        return data;
    }

    private static byte[] CreateValidKeyFileBytes()
    {
        var data = new byte[160];
        data[0x00] = 0x01;
        data[0x09] = 0x00;
        data[0x0A] = 0xAA;
        data[0x0B] = 0xBB;
        data[0x0C] = 0xCC;
        data[0x8C] = 0xAA;
        data[0x8D] = 0xBB;
        data[0x8E] = 0xCC;
        data[0x8F] = 0xDD;
        return data;
    }

    private sealed class FakeUploadedDumpStore : IUploadedDumpStore
    {
        public Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation, IEisAnalysisService? analysisService = null, ICurrentUser? currentUser = null, FileCategory fileCategory = FileCategory.Unknown, string? customerName = null, string? additionalInformation = null)
            => Task.FromResult(new UploadedDumpRecord());

        public Task<List<UploadedDumpRecord>> ListAsync(ICurrentUser? currentUser = null, string? search = null, int page = 1, int pageSize = 50)
            => Task.FromResult(new List<UploadedDumpRecord>());

        public Task<StoredFileAnalysisSnapshot?> GetLatestAnalysisAsync(Guid storedFileId)
            => Task.FromResult<StoredFileAnalysisSnapshot?>(null);

        public Task<StoredFileAnalysisSnapshot?> AnalyzeAndStoreAsync(Guid storedFileId, IEisAnalysisService analysisService)
            => Task.FromResult<StoredFileAnalysisSnapshot?>(null);

        public Task<CgmbKeyFileAnalysisDto?> AnalyzeAndStoreKeyFileAsync(Guid storedFileId, IKeyFileAnalysisService analysisService, ICurrentUser? currentUser = null)
            => Task.FromResult<CgmbKeyFileAnalysisDto?>(null);

        public Task<byte[]> ReadStoredFileAsync(Guid storedFileId, ICurrentUser? currentUser = null)
            => Task.FromResult(Array.Empty<byte>());

        public Task<UploadedDumpRecord?> GetByIdAsync(Guid storedFileId, ICurrentUser? currentUser = null)
            => Task.FromResult<UploadedDumpRecord?>(null);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bulk-consume-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
