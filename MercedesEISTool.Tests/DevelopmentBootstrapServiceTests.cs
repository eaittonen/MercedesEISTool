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
        var dbContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var userStore = new UserStore<ApplicationUser>(dbContext);
        var roleStore = new RoleStore<IdentityRole>(dbContext);
        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null,
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<ApplicationUser>>.Instance);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var service = new DevelopmentBootstrapService(userManager, roleManager, dbContext, options, NullLogger<DevelopmentBootstrapService>.Instance);

        await service.SeedAsync();

        var createdUser = await userManager.FindByEmailAsync("admin@example.local");
        Assert.NotNull(createdUser);
        Assert.True(createdUser!.EmailConfirmed);
        Assert.True(createdUser.IsEnabled);
        Assert.True(await userManager.IsInRoleAsync(createdUser, "SystemAdministrator"));
    }
}
