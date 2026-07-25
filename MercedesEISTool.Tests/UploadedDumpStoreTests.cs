using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class UploadedDumpStoreTests
{
    [Fact]
    public async Task PersistAsync_RequiresVehicleIdentifierAndRegistration()
    {
        var root = Path.Combine(Path.GetTempPath(), "MercedesEISToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var store = new JsonUploadedDumpStore(root);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => store.PersistAsync(new byte[] { 1, 2, 3 }, "example.bin", string.Empty, string.Empty, "analyze"));
            Assert.Contains("identifier", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PersistAsync_StoresFileAndMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "MercedesEISToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var store = new JsonUploadedDumpStore(root);
            var entry = await store.PersistAsync(new byte[] { 1, 2, 3 }, "example.bin", "VIN12345678901234", "ABC-123", "analyze");

            Assert.Equal("VIN12345678901234", entry.VehicleIdentifier);
            Assert.Equal("ABC-123", entry.RegistrationNumber);
            Assert.Equal("example.bin", entry.FileName);
            Assert.Equal("analyze", entry.Operation);
            Assert.True(File.Exists(entry.StoredFilePath));
            Assert.True(File.Exists(Path.Combine(root, "uploads", "index.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PersistAsync_AllowsVinOnlyOrRegistrationOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "MercedesEISToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var store = new JsonUploadedDumpStore(root);
            var vinOnly = await store.PersistAsync(new byte[] { 1, 2, 3 }, "vin.bin", "VIN12345678901234", string.Empty, "upload");
            var registrationOnly = await store.PersistAsync(new byte[] { 4, 5, 6 }, "registration.bin", string.Empty, "ABC-123", "upload");

            Assert.Equal("VIN12345678901234", vinOnly.VehicleIdentifier);
            Assert.Equal(string.Empty, vinOnly.RegistrationNumber);
            Assert.Equal("ABC-123", registrationOnly.RegistrationNumber);
            Assert.Equal(string.Empty, registrationOnly.VehicleIdentifier);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsPersistedUploads()
    {
        var root = Path.Combine(Path.GetTempPath(), "MercedesEISToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var store = new JsonUploadedDumpStore(root);
            await store.PersistAsync(new byte[] { 1, 2, 3 }, "first.bin", "VIN12345678901234", "ABC-123", "analyze");
            await store.PersistAsync(new byte[] { 4, 5, 6 }, "second.bin", "VIN12345678901234", "ABC-123", "compare");

            var records = await store.ListAsync();

            Assert.Equal(2, records.Count);
            Assert.Contains(records, record => record.FileName == "first.bin");
            Assert.Contains(records, record => record.FileName == "second.bin");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
