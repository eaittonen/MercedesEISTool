using System.Security.Claims;

namespace MercedesEISTool.Server.Models;

public sealed class ProductionCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductionCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value
        ?? string.Empty;

    public string DisplayName => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.GivenName)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name
        ?? string.Empty;

    public string? OrganizationId => _httpContextAccessor.HttpContext?.User?.FindFirst("OrganizationId")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("organizationId")?.Value;

    public IReadOnlyCollection<string> Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
