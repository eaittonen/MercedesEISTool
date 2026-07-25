namespace MercedesEISTool.Server.Models;

public sealed class ProductionCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductionCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId => _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "production";

    public string DisplayName => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Production";
}
