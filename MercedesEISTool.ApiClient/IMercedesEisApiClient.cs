using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.ApiClient;

public interface IMercedesEisApiClient
{
    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<CurrentUserResponseDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    void SetAccessToken(string? accessToken);
    Task<AnalyzeDumpResponse> AnalyzeDumpAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
    Task<UploadDumpResponse> UploadDumpAsync(byte[] data, string fileName, string? userProvidedVin, string? userProvidedRegistrationNumber, bool vehicleIdentifierConfirmed, CancellationToken cancellationToken = default);
    Task<UploadedDumpListResponse> GetUploadedDumpsAsync(CancellationToken cancellationToken = default);
    Task<StoredFileListResponse> GetStoredFilesAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<StoredFileDetailsDto> GetStoredFileDetailsAsync(Guid storedFileId, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadStoredFileAsync(Guid storedFileId, CancellationToken cancellationToken = default);
    Task<StoredFileDetailsDto> ReanalyzeStoredFileAsync(Guid storedFileId, CancellationToken cancellationToken = default);
    Task<CompareDumpsResponse> CompareDumpsAsync(byte[] left, byte[] right, string leftFileName, string rightFileName, string vehicleIdentifier, string registrationNumber, CancellationToken cancellationToken = default);
}
