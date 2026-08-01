namespace MercedesEISTool.Server.Models;

public class DevelopmentCurrentUser : ICurrentUser
{
    public string UserId => "development";
    public string DisplayName => "Development";
    public string? OrganizationId => "default-org";
    public IReadOnlyCollection<string> Roles { get; } = new[] { "SystemAdministrator" };

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
