namespace MercedesEISTool.Contracts.Models;

public sealed class AdminDashboardResponseDto
{
    public string ServerVersion { get; set; } = "1.0.0";
    public string Uptime { get; set; } = "0s";
    public string ServerStatus { get; set; } = "Healthy";
    public string DatabaseSize { get; set; } = "0 B";
    public int TotalDumps { get; set; }
    public int TotalOrganizations { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveSessions { get; set; }
    public int ActiveJobs { get; set; }
    public int QueueLength { get; set; }
    public string DiskUsage { get; set; } = "0%";
    public string LastBackup { get; set; } = "Not scheduled";
    public string LatestRelease { get; set; } = "n/a";
    public List<AdminDashboardMetricDto> Metrics { get; set; } = new();
    public List<AdminDashboardSectionDto> DashboardSections { get; set; } = new();
}

public sealed class AdminDashboardMetricDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class AdminDashboardSectionDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class AdminHealthResponseDto
{
    public string CpuUsage { get; set; } = "0%";
    public string RamUsage { get; set; } = "0 MB";
    public string DiskUsage { get; set; } = "0%";
    public string SqliteStatus { get; set; } = "Healthy";
    public int QueueLength { get; set; }
    public List<string> BackgroundServices { get; set; } = new();
    public string AuthenticationStatus { get; set; } = "Enabled";
    public string ApiStatus { get; set; } = "Healthy";
}

public sealed class CreateShareGrantRequestDto
{
    public Guid ResourceId { get; set; }
    public string? TargetOrganizationId { get; set; }
    public string? TargetUserId { get; set; }
    public List<string> Permissions { get; set; } = new();
    public DateTimeOffset? ExpiresUtc { get; set; }
    public bool IncludeFutureResources { get; set; }
    public string? Notes { get; set; }
}

public sealed class AdminShareGrantDto
{
    public string Id { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string SourceOrganization { get; set; } = string.Empty;
    public string? TargetOrganization { get; set; }
    public string? TargetUser { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ExpiresUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public bool IncludeFutureResources { get; set; }
    public string Direction { get; set; } = "Outgoing";
}

public sealed class AdminSharesResponseDto
{
    public List<AdminShareGrantDto> IncomingShares { get; set; } = new();
    public List<AdminShareGrantDto> OutgoingShares { get; set; } = new();
}

public sealed class AdminAuditEntryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string User { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public sealed class AdminAuditLogResponseDto
{
    public List<AdminAuditEntryDto> Items { get; set; } = new();
}

public sealed class AdminSessionDto
{
    public string Id { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class AdminSessionsResponseDto
{
    public List<AdminSessionDto> Items { get; set; } = new();
}

public sealed class CreateReleaseRequestDto
{
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public int DownloadCount { get; set; }
}

public sealed class AdminReleaseDto
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTimeOffset PublishedUtc { get; set; }
    public bool IsMandatory { get; set; }
    public int DownloadCount { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class AdminReleasesResponseDto
{
    public List<AdminReleaseDto> Items { get; set; } = new();
}

public sealed class AdminVehicleCacheEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string Vin { get; set; } = string.Empty;
    public DateTimeOffset CachedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public string Provider { get; set; } = string.Empty;
}

public sealed class AdminVehicleCacheResponseDto
{
    public List<AdminVehicleCacheEntryDto> Items { get; set; } = new();
}

public sealed class CreateNotificationRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Information";
}

public sealed class AdminNotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Information";
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class AdminNotificationsResponseDto
{
    public List<AdminNotificationDto> Items { get; set; } = new();
}

public sealed class AdminFeatureFlagDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class AdminFeatureFlagsResponseDto
{
    public List<AdminFeatureFlagDto> Items { get; set; } = new();
}
