using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MercedesEISTool.Server.Data;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

public sealed class DevelopmentBootstrapOptions
{
    public bool Enabled { get; set; } = false;
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPassword { get; set; }
}

public sealed class DevelopmentBootstrapService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IOptions<DevelopmentBootstrapOptions> _options;
    private readonly ILogger<DevelopmentBootstrapService> _logger;

    public DevelopmentBootstrapService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext dbContext,
        IOptions<DevelopmentBootstrapOptions> options,
        ILogger<DevelopmentBootstrapService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_options.Value.Enabled is not true)
        {
            return;
        }

        await EnsureRoleAsync("SystemAdministrator");
        await EnsureRoleAsync("CompanyAdministrator");
        await EnsureRoleAsync("Administrator");
        await EnsureRoleAsync("Technician");
        await EnsureRoleAsync("Research");
        await EnsureRoleAsync("ReadOnly");
        await EnsureRoleAsync("User");

        var defaultOrganization = await EnsureDefaultOrganizationAsync();

        var adminEmail = !string.IsNullOrWhiteSpace(_options.Value.AdminEmail) ? _options.Value.AdminEmail : "admin@example.local";
        var adminPassword = !string.IsNullOrWhiteSpace(_options.Value.AdminPassword) ? _options.Value.AdminPassword : "development-only-password";
        var userEmail = !string.IsNullOrWhiteSpace(_options.Value.UserEmail) ? _options.Value.UserEmail : "user@example.local";
        var userPassword = !string.IsNullOrWhiteSpace(_options.Value.UserPassword) ? _options.Value.UserPassword : "development-only-password";

        await EnsureUserAsync(adminEmail, adminPassword, new[] { "SystemAdministrator", "Administrator" }, true, "Administrator", defaultOrganization.Id);
        await EnsureUserAsync(userEmail, userPassword, new[] { "ReadOnly", "User" }, false, "User", defaultOrganization.Id);

        _logger.LogInformation("Development bootstrap completed.");
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private async Task<Organization> EnsureDefaultOrganizationAsync()
    {
        var existing = await _dbContext.Organizations.FirstOrDefaultAsync(organization => organization.Name == "Default Organization");
        if (existing is not null)
        {
            return existing;
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Default Organization",
            ContactEmail = "admin@example.local",
            Country = "Finland",
            IsActive = true,
            LicenseType = "Standard",
            MaxUsers = 4,
            LicenseExpirationUtc = DateTimeOffset.UtcNow.AddYears(1)
        };

        _dbContext.Organizations.Add(organization);
        await _dbContext.SaveChangesAsync();
        return organization;
    }

    private async Task EnsureUserAsync(string email, string password, IReadOnlyCollection<string> roles, bool isAdmin, string displayName, string organizationId)
    {
        ApplicationUser? existing;
        try
        {
            existing = await _userManager.FindByEmailAsync(email);
        }
        catch (Exception ex) when (ex.Message.Contains("NULL") || ex.Message.Contains("ordinal"))
        {
            _logger.LogWarning(ex, "Bootstrap user lookup encountered a legacy SQLite nullability issue for {Email}; continuing with compatibility recovery.", email);
            existing = null;
        }

        if (existing is not null)
        {
            existing.IsEnabled = true;
            existing.EmailConfirmed = true;
            existing.DisplayName = displayName;
            existing.OrganizationId = organizationId;
            await _userManager.UpdateAsync(existing);

            var hasPassword = !string.IsNullOrWhiteSpace(existing.PasswordHash);
            var passwordMatchesLegacyBootstrap = await _userManager.CheckPasswordAsync(existing, "Admin123!");
            if (!hasPassword || passwordMatchesLegacyBootstrap)
            {
                var addPasswordResult = await _userManager.RemovePasswordAsync(existing);
                if (addPasswordResult.Succeeded)
                {
                    addPasswordResult = await _userManager.AddPasswordAsync(existing, password);
                }

                if (!addPasswordResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(", ", addPasswordResult.Errors.Select(error => error.Description)));
                }
            }

            foreach (var role in roles)
            {
                if (!await _userManager.IsInRoleAsync(existing, role))
                {
                    await _userManager.AddToRoleAsync(existing, role);
                }
            }

            if (isAdmin)
            {
                _logger.LogWarning("Initial administrator bootstrap completed for {Email}. Please change the default password immediately.", email);
            }

            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            IsEnabled = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            OrganizationId = organizationId,
            MustChangePassword = false
        };

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user, password);
        }
        catch (Exception ex) when (ex.Message.Contains("NULL") || ex.Message.Contains("ordinal"))
        {
            _logger.LogWarning(ex, "Bootstrap user creation encountered a legacy SQLite nullability issue for {Email}; attempting to recover by creating a minimal user record.", email);
            user.LockoutEnabled = true;
            user.AccessFailedCount = 0;
            user.PhoneNumberConfirmed = false;
            user.TwoFactorEnabled = false;
            user.MustChangePassword = false;
            result = await _userManager.CreateAsync(user, password);
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        foreach (var role in roles)
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        if (isAdmin)
        {
            _logger.LogWarning("Initial administrator bootstrap completed for {Email}. Please change the default password immediately.", email);
        }
    }

}
