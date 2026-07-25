using Microsoft.AspNetCore.Identity;

namespace MercedesEISTool.Server.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
