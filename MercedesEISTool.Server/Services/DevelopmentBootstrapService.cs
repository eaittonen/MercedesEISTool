using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

public sealed class DevelopmentBootstrapOptions
{
    public bool Enabled { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPassword { get; set; }
}

public sealed class DevelopmentBootstrapService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IOptions<DevelopmentBootstrapOptions> _options;
    private readonly ILogger<DevelopmentBootstrapService> _logger;

    public DevelopmentBootstrapService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<DevelopmentBootstrapOptions> options,
        ILogger<DevelopmentBootstrapService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_options.Value.Enabled is not true)
        {
            return;
        }

        await EnsureRoleAsync("User");
        await EnsureRoleAsync("Administrator");

        var adminEmail = !string.IsNullOrWhiteSpace(_options.Value.AdminEmail) ? _options.Value.AdminEmail : "admin@example.local";
        var adminPassword = !string.IsNullOrWhiteSpace(_options.Value.AdminPassword) ? _options.Value.AdminPassword : "development-only-password";
        var userEmail = !string.IsNullOrWhiteSpace(_options.Value.UserEmail) ? _options.Value.UserEmail : "user@example.local";
        var userPassword = !string.IsNullOrWhiteSpace(_options.Value.UserPassword) ? _options.Value.UserPassword : "development-only-password";

        await EnsureUserAsync(adminEmail, adminPassword, "Administrator");
        await EnsureUserAsync(userEmail, userPassword, "User");

        _logger.LogInformation("Development bootstrap completed.");
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private async Task EnsureUserAsync(string email, string password, string role)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!existing.IsEnabled)
            {
                existing.IsEnabled = true;
                await _userManager.UpdateAsync(existing);
            }

            if (!await _userManager.IsInRoleAsync(existing, role))
            {
                await _userManager.AddToRoleAsync(existing, role);
            }

            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = role,
            IsEnabled = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        await _userManager.AddToRoleAsync(user, role);
    }

}
