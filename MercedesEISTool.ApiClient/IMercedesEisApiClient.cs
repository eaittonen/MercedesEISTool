using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.ApiClient;

public interface IMercedesEisApiClient
{
    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<CurrentUserResponseDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<AdminUserListResponseDto> GetAdminUsersAsync(CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> DisableUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> EnableUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<OrganizationListResponseDto> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<List<OrganizationOptionDto>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default);
    Task<List<RoleOptionDto>> GetRoleOptionsAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDetailDto> CreateOrganizationAsync(CreateOrganizationRequestDto request, CancellationToken cancellationToken = default);
    Task<OrganizationDetailDto> UpdateOrganizationAsync(string organizationId, UpdateOrganizationRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> DeleteOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> CreateUserAsync(CreateOrUpdateUserRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> UpdateUserAsync(string userId, CreateOrUpdateUserRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> ResetUserPasswordAsync(string userId, ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminUserActionResponseDto> SetUserPasswordChangeRequirementAsync(string userId, ForcePasswordChangeRequestDto request, CancellationToken cancellationToken = default);
    Task<StorageDiagnosticsResponseDto> GetStorageDiagnosticsAsync(CancellationToken cancellationToken = default);
    void SetAccessToken(string? accessToken);
    Task<AnalyzeDumpResponse> AnalyzeDumpAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
    Task<UploadDumpResponse> UploadDumpAsync(byte[] data, string fileName, string? userProvidedVin, string? userProvidedRegistrationNumber, bool vehicleIdentifierConfirmed, string? customerName, CancellationToken cancellationToken = default);
    Task<UploadedDumpListResponse> GetUploadedDumpsAsync(CancellationToken cancellationToken = default);
    Task<StoredFileListResponse> GetStoredFilesAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<StoredFileDetailsDto> GetStoredFileDetailsAsync(Guid storedFileId, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadStoredFileAsync(Guid storedFileId, CancellationToken cancellationToken = default);
    Task<StoredFileDetailsDto> ReanalyzeStoredFileAsync(Guid storedFileId, CancellationToken cancellationToken = default);
    Task<CompareDumpsResponse> CompareDumpsAsync(byte[] left, byte[] right, string leftFileName, string rightFileName, string vehicleIdentifier, string registrationNumber, CancellationToken cancellationToken = default);
    Task<BulkConsumePreviewResponse> PreviewBulkConsumeAsync(string sourceFolderPath, bool includeSubdirectories, CancellationToken cancellationToken = default);
    Task<BulkConsumeImportResponse> ImportBulkConsumeAsync(BulkConsumeImportRequest request, CancellationToken cancellationToken = default);
}
