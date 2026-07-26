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
        await File.WriteAllBytesAsync(dumpPath, new byte[256]);
        var unsupportedPath = Path.Combine(tempRoot.Path, "notes.txt");
        await File.WriteAllTextAsync(unsupportedPath, "not an eis dump");

        var service = new BulkConsumeService(
            new FakeUploadedDumpStore(),
            new EisAnalysisService(),
            new KeyFileAnalysisService(),
            NullLogger<BulkConsumeService>.Instance);

        var response = await service.PreviewAsync(tempRoot.Path, includeSubdirectories: false);

        Assert.Equal(tempRoot.Path, response.SourceFolderPath);
        Assert.Single(response.Items);
        Assert.Equal("Import", response.Items[0].Action);
        Assert.Equal("EIS dump", response.Items[0].Classification);
        Assert.True(response.Items[0].IsSelected);
    }

    [Fact]
    public async Task PreviewAsync_ResolvesFileUriPaths()
    {
        using var tempRoot = new TempDirectory();
        var dumpPath = Path.Combine(tempRoot.Path, "dump.bin");
        await File.WriteAllBytesAsync(dumpPath, new byte[256]);

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

    private sealed class FakeUploadedDumpStore : IUploadedDumpStore
    {
        public Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation, IEisAnalysisService? analysisService = null, ICurrentUser? currentUser = null, FileCategory fileCategory = FileCategory.Unknown, string? customerName = null)
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
