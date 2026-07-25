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
    public List<string> Roles { get; set; } = new();
    public bool IsEnabled { get; set; }
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
    public string DetectedFormat { get; set; } = "Unknown";
    public string? EisType { get; set; }
    public string? McuType { get; set; }
    public int? KeyCount { get; set; }
    public string KeyCountStatus { get; set; } = "NotMapped";
    public string? EisPassword { get; set; }
    public string EisPasswordStatus { get; set; } = "NotMapped";
    public string? Ssid { get; set; }
    public string SsidStatus { get; set; } = "NotMapped";
    public int KeyPasswordsFound { get; set; }
    public string AnalysisStatus { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool CanViewSensitiveFields { get; set; }
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
    public List<KeySlotDto> Keys { get; set; } = new();
    public string ParserVersion { get; set; } = string.Empty;
    public DateTimeOffset? AnalyzedAtUtc { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool CanViewSensitiveFields { get; set; }
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
}

public class UploadDumpResponse
{
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = "Uploaded";
    public string Sha256 { get; set; } = string.Empty;
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
}

public class UploadedDumpListResponse
{
    public List<UploadedDumpSummary> Uploads { get; set; } = new();
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
