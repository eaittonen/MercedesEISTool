namespace MercedesEISTool.Server.Services;

public interface IUploadedDumpStore
{
    Task<UploadedDumpRecord> PersistAsync(byte[] data, string fileName, string vehicleIdentifier, string registrationNumber, string operation);
    Task<List<UploadedDumpRecord>> ListAsync();
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
}
