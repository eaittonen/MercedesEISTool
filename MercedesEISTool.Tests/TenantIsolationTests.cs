using MercedesEISTool.Server.Models;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task JsonUploadedDumpStore_DoesNotExposeCrossOrganizationFiles()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"tenant-tests-{Guid.NewGuid():N}");
        var store = new JsonUploadedDumpStore(storageRoot, new ResourceAuthorizationService());

        var ownerUser = new TestCurrentUser("owner-user", "org-a", new[] { "CompanyAdministrator" });
        var otherOrgUser = new TestCurrentUser("other-user", "org-b", new[] { "ReadOnly" });

        var record = await store.PersistAsync(
            [0x01, 0x02, 0x03],
            "owned.bin",
            "VIN12345678901234",
            "ABC-123",
            "upload",
            null,
            ownerUser);

        var listed = await store.ListAsync(otherOrgUser, null, 1, 50);
        var byId = await store.GetByIdAsync(record.Id, otherOrgUser);

        Assert.DoesNotContain(listed, item => item.Id == record.Id);
        Assert.Null(byId);
    }

    [Fact]
    public async Task JsonUploadedDumpStore_AllowsSharedOrganizationToViewMetadata()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"tenant-tests-{Guid.NewGuid():N}");
        var store = new JsonUploadedDumpStore(storageRoot, new ResourceAuthorizationService());

        var ownerUser = new TestCurrentUser("owner-user", "org-a", new[] { "CompanyAdministrator" });
        var sharedOrgUser = new TestCurrentUser("shared-user", "org-b", new[] { "ReadOnly" });

        var record = await store.PersistAsync(
            [0x01, 0x02, 0x03],
            "shared.bin",
            "VIN12345678901234",
            "ABC-123",
            "upload",
            null,
            ownerUser);

        await store.AddAccessGrantAsync(record.Id, new ResourceAccessGrant
        {
            ResourceType = "StoredFile",
            ResourceId = record.Id,
            OwnerOrganizationId = "org-a",
            GrantedToOrganizationId = "org-b",
            Permissions = StoredFilePermission.ViewMetadata,
            CreatedByUserId = ownerUser.UserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            IsActive = true
        }, ownerUser);

        var listed = await store.ListAsync(sharedOrgUser, null, 1, 50);
        var byId = await store.GetByIdAsync(record.Id, sharedOrgUser);

        Assert.Contains(listed, item => item.Id == record.Id);
        Assert.NotNull(byId);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(string userId, string organizationId, IEnumerable<string> roles)
        {
            UserId = userId;
            OrganizationId = organizationId;
            Roles = roles.ToArray();
        }

        public string UserId { get; }
        public string DisplayName { get; set; } = string.Empty;
        public string? OrganizationId { get; }
        public IReadOnlyCollection<string> Roles { get; }

        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
