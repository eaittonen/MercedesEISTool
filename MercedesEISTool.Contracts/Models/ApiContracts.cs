namespace MercedesEISTool.Contracts.Models;

public enum FieldValueStatus
{
    Present,
    NotPresent,
    NotMapped,
    Invalid,
    UnsupportedFormat,
    AnalysisFailed
}

public sealed class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public sealed class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public sealed class CurrentUserResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsAdministrator { get; set; }
}

public sealed class AdminUserListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsEnabled { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}

public sealed class AdminUserListResponseDto
{
    public List<AdminUserListItemDto> Items { get; set; } = new();
}

public sealed class AdminUserActionResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class OrganizationSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public DateTimeOffset? LicenseExpirationUtc { get; set; }
    public int MaxUsers { get; set; }
    public int UserCount { get; set; }
}

public sealed class OrganizationListResponseDto
{
    public List<OrganizationSummaryDto> Items { get; set; } = new();
}

public sealed class OrganizationDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public DateTimeOffset? LicenseExpirationUtc { get; set; }
    public int MaxUsers { get; set; }
    public int UserCount { get; set; }
}

public sealed class CreateOrganizationRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string LicenseType { get; set; } = "Standard";
    public DateTimeOffset? LicenseExpirationUtc { get; set; }
    public int MaxUsers { get; set; } = 4;
}

public sealed class UpdateOrganizationRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public DateTimeOffset? LicenseExpirationUtc { get; set; }
    public int MaxUsers { get; set; }
}

public sealed class CreateOrUpdateUserRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public bool MustChangePassword { get; set; }
}

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    string OrganizationId,
    IReadOnlyList<string> Roles,
    bool IsEnabled,
    bool MustChangePassword);

public sealed record UpdateUserRequest(
    string Email,
    string DisplayName,
    string OrganizationId,
    IReadOnlyList<string> Roles,
    bool IsEnabled,
    bool MustChangePassword);

public sealed record OrganizationOptionDto(
    string Id,
    string Name);

public sealed class RoleOptionDto
{
    public RoleOptionDto()
    {
    }

    public RoleOptionDto(string name, bool canAssign)
    {
        Name = name;
        CanAssign = canAssign;
    }

    public string Name { get; set; } = string.Empty;
    public bool CanAssign { get; set; }
}

public sealed class ResetPasswordRequestDto
{
    public string NewPassword { get; set; } = string.Empty;
    public bool ForcePasswordChange { get; set; }
}

public sealed class ForcePasswordChangeRequestDto
{
    public bool RequirePasswordChange { get; set; }
}

public sealed class StorageDiagnosticsResponseDto
{
    public string StorageRoot { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string TimestampUtc { get; set; } = string.Empty;
    public bool IsWritable { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class SensitiveFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public FieldValueStatus Status { get; set; }
    public string? SourceDescription { get; set; }
    public int? SourceOffset { get; set; }
    public int? Length { get; set; }
    public string Confidence { get; set; } = "Unknown";
}

public sealed class KeySlotDto
{
    public int SlotNumber { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Password { get; set; }
    public FieldValueStatus PasswordStatus { get; set; }
    public string? Hash { get; set; }
    public FieldValueStatus HashStatus { get; set; }
    public string? Notes { get; set; }
}

public sealed class EisAnalysisDetailsDto
{
    public string DetectedFormat { get; set; } = "Unknown";
    public string? DetectedVin { get; set; }
    public string VinStatus { get; set; } = "NotMapped";
    public string? EisType { get; set; }
    public FieldValueStatus EisTypeStatus { get; set; }
    public string? McuType { get; set; }
    public FieldValueStatus McuTypeStatus { get; set; }
    public int? KeyCount { get; set; }
    public FieldValueStatus KeyCountStatus { get; set; }
    public SensitiveFieldDto EisPassword { get; set; } = new();
    public SensitiveFieldDto Ssid { get; set; } = new();
    public bool? Initialized { get; set; }
    public bool? Personalized { get; set; }
    public bool? TpCleared { get; set; }
    public bool? Activated { get; set; }
    public bool? DealerEis { get; set; }
    public bool? Fbs4 { get; set; }
    public List<KeySlotDto> Keys { get; set; } = new();
    public List<SensitiveFieldDto> AdditionalFields { get; set; } = new();
    public DateTimeOffset AnalyzedAtUtc { get; set; }
    public string ParserVersion { get; set; } = string.Empty;
}

public sealed class StoredFileListItemDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; }
    public string? UserProvidedVin { get; set; }
    public string? DetectedVin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? AdditionalInformation { get; set; }
    public string? OrganizationName { get; set; }
    public string? Warnings { get; set; }
    public string? Reason { get; set; }
    public string DetectedFormat { get; set; } = "Unknown";
    public string? EisType { get; set; }
    public string? McuType { get; set; }
    public int? KeyCount { get; set; }
    public string KeyCountStatus { get; set; } = "NotMapped";
    public string? EisPassword { get; set; }
    public string EisPasswordStatus { get; set; } = "NotMapped";
    public string? Ssid { get; set; }
    public string SsidStatus { get; set; } = "NotMapped";
    public bool? Initialized { get; set; }
    public bool? Personalized { get; set; }
    public bool? TpCleared { get; set; }
    public bool? Activated { get; set; }
    public bool? DealerEis { get; set; }
    public bool? Fbs4 { get; set; }
    public int KeyPasswordsFound { get; set; }
    public string AnalysisStatus { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool CanViewSensitiveFields { get; set; }
    public string LockGroupKey { get; set; } = string.Empty;
    public int MetadataCompletenessScore { get; set; }
    public bool HasEisPassword { get; set; }
    public bool IsPreferredVersion { get; set; }
    public int VersionCount { get; set; }
}

public sealed class StoredFileListResponse
{
    public List<StoredFileListItemDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public sealed class StoredFileDetailsDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; }
    public string? UserProvidedVin { get; set; }
    public string? DetectedVin { get; set; }
    public string VinStatus { get; set; } = "NotMapped";
    public string? RegistrationNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? AdditionalInformation { get; set; }
    public string? OrganizationName { get; set; }
    public string? Warnings { get; set; }
    public string? Reason { get; set; }
    public string DetectedFormat { get; set; } = "Unknown";
    public string? EisType { get; set; }
    public string EisTypeStatus { get; set; } = "NotMapped";
    public string? McuType { get; set; }
    public string McuTypeStatus { get; set; } = "NotMapped";
    public int? KeyCount { get; set; }
    public string KeyCountStatus { get; set; } = "NotMapped";
    public string? EisPassword { get; set; }
    public string EisPasswordStatus { get; set; } = "NotMapped";
    public string? Ssid { get; set; }
    public string SsidStatus { get; set; } = "NotMapped";
    public bool? Initialized { get; set; }
    public bool? Personalized { get; set; }
    public bool? TpCleared { get; set; }
    public bool? Activated { get; set; }
    public bool? DealerEis { get; set; }
    public bool? Fbs4 { get; set; }
    public List<KeySlotDto> Keys { get; set; } = new();
    public string ParserVersion { get; set; } = string.Empty;
    public DateTimeOffset? AnalyzedAtUtc { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool CanViewSensitiveFields { get; set; }
}

public sealed class VehicleInfoDto
{
    public string? Registration { get; set; }
    public string? Vin { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Type { get; set; }
    public int? Year { get; set; }
    public string? Fuel { get; set; }
    public string? Power { get; set; }
    public string? Engine { get; set; }
    public string? EngineCode { get; set; }
    public string? Transmission { get; set; }
    public string? DriveType { get; set; }
    public string? FirstRegistration { get; set; }
    public string? Color { get; set; }
    public string? Mass { get; set; }
    public string? BodyType { get; set; }
    public string? InspectionDate { get; set; }
    public Dictionary<string, object?> AdditionalFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StoredFileDownloadResult
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? Sha256 { get; set; }
}

public class AnalyzeDumpRequest
{
    public string FileName { get; set; } = string.Empty;
    public string VehicleIdentifier { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
}

public class AnalyzeDumpResponse
{
    public string FileName { get; set; } = string.Empty;
    public string DetectedFormat { get; set; } = "Unknown";
    public string? DetectedVin { get; set; }
    public string VinStatus { get; set; } = "NotPresent";
    public string VinSource { get; set; } = "None";
    public string EisType { get; set; } = string.Empty;
    public string McuType { get; set; } = string.Empty;
    public string KeyCount { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool AnalysisSucceeded { get; set; }
    public string Message { get; set; } = "Analyzed";
    public string Status { get; set; } = "Analyzed";
    public EisAnalysisDetailsDto? AnalysisDetails { get; set; }
}

public class CompareDumpsRequest
{
    public string LeftFileName { get; set; } = string.Empty;
    public string RightFileName { get; set; } = string.Empty;
    public string VehicleIdentifier { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
}

public class CompareDumpsResponse
{
    public int TotalDifferences { get; set; }
    public List<int> DifferingOffsets { get; set; } = new();
    public string VehicleIdentifier { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
}

public class UploadDumpRequest
{
    public string FileName { get; set; } = string.Empty;
    public string VehicleIdentifier { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}

public class UploadDumpResponse
{
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = "Uploaded";
    public string Sha256 { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long FileSizeBytes { get; set; }
    public Guid UploadId { get; set; }
    public string? DetectedVin { get; set; }
    public string VinStatus { get; set; } = "NotPresent";
    public string Message { get; set; } = string.Empty;
    public EisAnalysisDetailsDto? AnalysisDetails { get; set; }
}

public class UploadedDumpSummary
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long SizeBytes { get; set; }
    public string? DetectedVin { get; set; }
    public string VinStatus { get; set; } = "NotPresent";
    public string? UserProvidedVin { get; set; }
    public string? UserProvidedRegistrationNumber { get; set; }
    public string? CustomerName { get; set; }
}

public class UploadedDumpListResponse
{
    public List<UploadedDumpSummary> Uploads { get; set; } = new();
}

public sealed class BulkConsumePreviewItemDto
{
    public string SourcePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string DetectedFormat { get; set; } = string.Empty;
    public string? DetectedVin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? OriginalSourceFolderName { get; set; }
    public string? OriginalRelativePath { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerIdentifier { get; set; }
    public string? FolderIdentifier { get; set; }
    public string? AdditionalInformation { get; set; }
    public MetadataConfidence? VinConfidence { get; set; }
    public MetadataConfidence? RegistrationConfidence { get; set; }
    public MetadataConfidence? CustomerConfidence { get; set; }
    public MetadataConfidence? FolderIdentifierConfidence { get; set; }
    public MetadataConfidence? AdditionalInformationConfidence { get; set; }
    public MetadataConfidence? MetadataConfidence { get; set; }
    public string? Password { get; set; }
    public string? Score { get; set; }
    public string? Reason { get; set; }
    public string? EditedVin { get; set; }
    public string? EditedRegistrationNumber { get; set; }
    public string? EditedCustomerName { get; set; }
    public string? EditedAdditionalInformation { get; set; }
    public string? GroupingKey { get; set; }
    public string? GroupingLabel { get; set; }
    public string Action { get; set; } = "Import";
    public string Notes { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
    public bool IsImportable { get; set; } = true;
    public bool IsIgnored { get; set; }
    public bool HasPassword { get; set; }
}

public sealed class BulkConsumePreviewGroupDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public List<BulkConsumePreviewItemDto> Children { get; set; } = new();
}

public sealed class BulkConsumePreviewResponse
{
    public string SourceFolderPath { get; set; } = string.Empty;
    public bool IncludeSubdirectories { get; set; }
    public List<BulkConsumePreviewItemDto> Items { get; set; } = new();
    public List<BulkConsumePreviewGroupDto> Groups { get; set; } = new();
    public int TotalFiles { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class BulkConsumeImportItemRequest
{
    public string SourcePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string? VehicleIdentifier { get; set; }
    public string? DetectedVin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? FolderIdentifier { get; set; }
    public string? AdditionalInformation { get; set; }
    public MetadataConfidence? MetadataConfidence { get; set; }
}

public sealed class BulkConsumeImportRequest
{
    public string SourceFolderPath { get; set; } = string.Empty;
    public bool IncludeSubdirectories { get; set; }
    public List<BulkConsumeImportItemRequest> Items { get; set; } = new();
}

public sealed class BulkConsumeImportResultDto
{
    public string SourcePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public Guid StoredFileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class BulkConsumeImportResponse
{
    public Guid BatchId { get; set; }
    public int ImportedCount { get; set; }
    public List<BulkConsumeImportResultDto> Results { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? RequestId { get; set; }
}

public class HealthResponse
{
    public bool IsHealthy { get; set; } = true;
    public string Status { get; set; } = "Healthy";
    public string ServerVersion { get; set; } = "1.0";
    public string ServiceName { get; set; } = "MercedesEISTool.Server";
}
