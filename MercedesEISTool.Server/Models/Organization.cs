namespace MercedesEISTool.Server.Models;

public class Organization
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string LicenseType { get; set; } = "Standard";
    public DateTimeOffset? LicenseExpirationUtc { get; set; }
    public int MaxUsers { get; set; } = 4;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
