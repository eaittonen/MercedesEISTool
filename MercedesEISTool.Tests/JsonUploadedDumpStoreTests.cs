using System.Text;
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
