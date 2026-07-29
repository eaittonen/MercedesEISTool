using System.Text;
using System.Text.Json;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public sealed class JsonUploadedDumpStoreTests : IDisposable
{
    private readonly List<string> _createdPaths = new();
    private readonly Dictionary<string, string?> _originalEnvironmentValues = new();

    public JsonUploadedDumpStoreTests()
    {
        foreach (var variableName in new[] { "MERCEDES_EIS_UPLOAD_ROOT", "UPLOAD_STORAGE_ROOT" })
        {
            _originalEnvironmentValues[variableName] = Environment.GetEnvironmentVariable(variableName);
        }
    }

    [Fact]
    public async Task PersistAsync_UsesUploadRootFromEnvironment_whenNoExplicitPathIsProvided()
    {
        var configuredRoot = CreateTempDirectory();
        Environment.SetEnvironmentVariable("MERCEDES_EIS_UPLOAD_ROOT", configuredRoot);
        Environment.SetEnvironmentVariable("UPLOAD_STORAGE_ROOT", null);

        var store = new JsonUploadedDumpStore();
        var bytes = Encoding.UTF8.GetBytes("test payload");

        var record = await store.PersistAsync(bytes, "sample.bin", "VIN123", "ABC123", "upload");

        var expectedDirectory = Path.Combine(configuredRoot, "uploads");
        Assert.StartsWith(expectedDirectory, record.StoredFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(record.StoredFilePath));
        Assert.True(File.Exists(Path.Combine(expectedDirectory, "index.json")));
    }

    [Fact]
    public async Task PersistAsync_StoresCustomerNameAndSupportsSearch()
    {
        var configuredRoot = CreateTempDirectory();
        Environment.SetEnvironmentVariable("MERCEDES_EIS_UPLOAD_ROOT", configuredRoot);
        Environment.SetEnvironmentVariable("UPLOAD_STORAGE_ROOT", null);

        var store = new JsonUploadedDumpStore();
        var bytes = Encoding.UTF8.GetBytes("customer payload");

        var record = await store.PersistAsync(bytes, "sample.bin", "VIN123", "ABC123", "upload", customerName: "Acme Motors");

        Assert.Equal("Acme Motors", record.CustomerName);

        var results = await store.ListAsync(search: "Acme");
        Assert.Single(results);
        Assert.Equal(record.Id, results[0].Id);
    }

    [Fact]
    public async Task PersistAsync_AllowsMissingIdentifiersWhenExplicitlyRequested()
    {
        var configuredRoot = CreateTempDirectory();
        Environment.SetEnvironmentVariable("MERCEDES_EIS_UPLOAD_ROOT", configuredRoot);
        Environment.SetEnvironmentVariable("UPLOAD_STORAGE_ROOT", null);

        var store = new JsonUploadedDumpStore();
        var bytes = Encoding.UTF8.GetBytes("bulk payload");

        var record = await store.PersistAsync(bytes, "sample.bin", string.Empty, string.Empty, "bulk-consume", allowMissingIdentifiers: true);

        Assert.Equal(string.Empty, record.VehicleIdentifier);
        Assert.Equal(string.Empty, record.RegistrationNumber);
        Assert.Equal("bulk-consume", record.Operation);
    }

    [Fact]
    public async Task Constructor_MigratesLegacyIndexFromAppDataDirectory()
    {
        var runtimeRoot = CreateTempDirectory();
        var legacyRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads", "uploads");
        Directory.CreateDirectory(legacyRoot);

        try
        {
            var legacyFilePath = Path.Combine(legacyRoot, "legacy.bin");
            await File.WriteAllBytesAsync(legacyFilePath, new byte[] { 1, 2, 3, 4 });

            var legacyRecord = new UploadedDumpRecord
            {
                FileName = "legacy.bin",
                StoredFilePath = legacyFilePath,
                VehicleIdentifier = "VIN123",
                RegistrationNumber = "ABC123",
                Operation = "upload"
            };

            var legacyIndexPath = Path.Combine(legacyRoot, "index.json");
            await File.WriteAllTextAsync(legacyIndexPath, JsonSerializer.Serialize(new List<UploadedDumpRecord> { legacyRecord }, new JsonSerializerOptions { WriteIndented = true }));

            var store = new JsonUploadedDumpStore(runtimeRoot);
            var records = await store.ListAsync();

            Assert.Single(records);
            Assert.Equal("legacy.bin", records[0].FileName);
            Assert.Equal(Path.Combine(runtimeRoot, "uploads", "legacy.bin"), records[0].StoredFilePath);
            Assert.True(File.Exists(records[0].StoredFilePath));
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }

            if (Directory.Exists(legacyRoot))
            {
                Directory.Delete(legacyRoot, recursive: true);
            }
        }
    }

    public void Dispose()
    {
        foreach (var variableName in _originalEnvironmentValues.Keys)
        {
            Environment.SetEnvironmentVariable(variableName, _originalEnvironmentValues[variableName]);
        }

        foreach (var path in _createdPaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MercedesEISToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
