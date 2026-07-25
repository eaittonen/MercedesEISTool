using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MercedesEISTool.Server.Data;
using MercedesEISTool.Server.Models;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class DevelopmentBootstrapServiceTests
{
    [Fact]
    public async Task SeedAsync_CreatesDefaultAdministrator_WhenEnabledOptionIsNotSet()
    {
        var options = Options.Create(new DevelopmentBootstrapOptions());
        var dbContext = CreateDbContext();

        var userManager = CreateUserManager(dbContext);
        var roleManager = CreateRoleManager(dbContext);

        var service = new DevelopmentBootstrapService(userManager, roleManager, dbContext, options, NullLogger<DevelopmentBootstrapService>.Instance);

        await service.SeedAsync();

        var createdUser = await userManager.FindByEmailAsync("admin@example.local");
        Assert.NotNull(createdUser);
        Assert.True(createdUser!.EmailConfirmed);
        Assert.True(createdUser.IsEnabled);
        Assert.True(await userManager.IsInRoleAsync(createdUser, "SystemAdministrator"));
    }

    [Fact]
    public async Task SeedAsync_PreservesExistingUserPassword_WhenBootstrapOptionsChange()
    {
        var options = Options.Create(new DevelopmentBootstrapOptions
        {
            AdminEmail = "admin@example.local",
            AdminPassword = "NewPassword123!"
        });
        var dbContext = CreateDbContext();

        var userManager = CreateUserManager(dbContext);
        var roleManager = CreateRoleManager(dbContext);

        var existingUser = new ApplicationUser
        {
            UserName = "admin@example.local",
            Email = "admin@example.local",
            DisplayName = "Administrator",
            IsEnabled = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            OrganizationId = "existing-org"
        };

        var createResult = await userManager.CreateAsync(existingUser, "old-password");
        Assert.True(createResult.Succeeded);

        var service = new DevelopmentBootstrapService(userManager, roleManager, dbContext, options, NullLogger<DevelopmentBootstrapService>.Instance);

        await service.SeedAsync();

        var updatedUser = await userManager.FindByEmailAsync("admin@example.local");
        Assert.NotNull(updatedUser);
        Assert.True(await userManager.CheckPasswordAsync(updatedUser!, "old-password"));
        Assert.False(await userManager.CheckPasswordAsync(updatedUser!, "NewPassword123!"));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext dbContext)
    {
        var userStore = new UserStore<ApplicationUser>(dbContext);
        return new UserManager<ApplicationUser>(
            userStore,
            null,
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static RoleManager<IdentityRole> CreateRoleManager(ApplicationDbContext dbContext)
    {
        var roleStore = new RoleStore<IdentityRole>(dbContext);
        return new RoleManager<IdentityRole>(
            roleStore,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
    }
}
