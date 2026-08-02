using Microsoft.AspNetCore.Identity;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

[Flags]
public enum StoredFilePermission
{
    None = 0,
    ViewMetadata = 1 << 0,
    ViewSensitiveData = 1 << 1,
    DownloadOriginal = 1 << 2,
    EditMetadata = 1 << 3,
    Reanalyze = 1 << 4,
    ShareFurther = 1 << 5
}

public class ResourceAccessGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ResourceType { get; set; } = "StoredFile";
    public Guid ResourceId { get; set; }
    public string OwnerOrganizationId { get; set; } = string.Empty;
    public string? GrantedToOrganizationId { get; set; }
    public string? GrantedToUserId { get; set; }
    public StoredFilePermission Permissions { get; set; }
    public bool IncludeFutureResources { get; set; }
    public string? ScopeDefinitionJson { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public string? RevokedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public interface IResourceAuthorizationService
{
    Task<bool> CanViewAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default);
    Task<bool> CanViewSensitiveDataAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default);
    Task<bool> CanDownloadAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default);
    Task<bool> CanEditAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default);
    Task<bool> CanShareAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ResourceAccessGrant>> GetActiveGrantsAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task AddAccessGrantAsync(Guid resourceId, ResourceAccessGrant grant, ICurrentUser? currentUser, CancellationToken cancellationToken = default);
}

public class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly Dictionary<Guid, List<ResourceAccessGrant>> _grantsByResource = new();

    public Task<bool> CanViewAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HasAccess(resourceId, currentUser, StoredFilePermission.ViewMetadata));
    }

    public Task<bool> CanViewSensitiveDataAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HasAccess(resourceId, currentUser, StoredFilePermission.ViewSensitiveData));
    }

    public Task<bool> CanDownloadAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HasAccess(resourceId, currentUser, StoredFilePermission.DownloadOriginal));
    }

    public Task<bool> CanEditAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HasAccess(resourceId, currentUser, StoredFilePermission.EditMetadata));
    }

    public Task<bool> CanShareAsync(Guid resourceId, ICurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HasAccess(resourceId, currentUser, StoredFilePermission.ShareFurther));
    }

    public Task<IReadOnlyCollection<ResourceAccessGrant>> GetActiveGrantsAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        if (_grantsByResource.TryGetValue(resourceId, out var grants))
        {
            return Task.FromResult<IReadOnlyCollection<ResourceAccessGrant>>(grants.Where(grant => grant.IsActive && !grant.RevokedUtc.HasValue).ToList());
        }

        return Task.FromResult<IReadOnlyCollection<ResourceAccessGrant>>(Array.Empty<ResourceAccessGrant>());
    }

    public Task AddAccessGrantAsync(Guid resourceId, ResourceAccessGrant grant, ICurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        if (!_grantsByResource.TryGetValue(resourceId, out var grants))
        {
            grants = new List<ResourceAccessGrant>();
            _grantsByResource[resourceId] = grants;
        }

        grants.Add(grant);
        return Task.CompletedTask;
    }

    private bool HasAccess(Guid resourceId, ICurrentUser? currentUser, StoredFilePermission requiredPermission)
    {
        if (currentUser is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return false;
        }

        if (currentUser.IsInRole("SystemAdministrator"))
        {
            return true;
        }

        var currentUserId = currentUser.UserId;
        var currentOrganizationId = currentUser.OrganizationId;

        if (_grantsByResource.TryGetValue(resourceId, out var grants))
        {
            foreach (var grant in grants.Where(grant => grant.IsActive && !grant.RevokedUtc.HasValue))
            {
                if (grant.ExpiresUtc.HasValue && grant.ExpiresUtc.Value <= DateTimeOffset.UtcNow)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(grant.GrantedToUserId) && string.Equals(grant.GrantedToUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
                {
                    if ((grant.Permissions & requiredPermission) == requiredPermission)
                    {
                        return true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(grant.GrantedToOrganizationId) && string.Equals(grant.GrantedToOrganizationId, currentOrganizationId, StringComparison.OrdinalIgnoreCase))
                {
                    if ((grant.Permissions & requiredPermission) == requiredPermission)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}

public sealed class TestCurrentUserAdapter : ICurrentUser
{
    private readonly ICurrentUser _inner;

    public TestCurrentUserAdapter(ICurrentUser inner)
    {
        _inner = inner;
    }

    public string UserId => _inner.UserId;
    public string DisplayName => _inner.DisplayName;
    public string? OrganizationId { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

    public bool IsInRole(string role) => Roles.Any(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
}
