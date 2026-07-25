using Microsoft.EntityFrameworkCore;
using MercedesEISTool.Server.Data;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class OrganizationModelTests
{
    [Fact]
    public async Task CanPersistOrganizationAndUserRelationship()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);

        var organization = new Organization
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Contoso",
            ContactEmail = "ops@contoso.test",
            Country = "Finland",
            MaxUsers = 6
        };
        context.Organizations.Add(organization);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = "owner@contoso.test",
            Email = "owner@contoso.test",
            DisplayName = "Owner",
            OrganizationId = organization.Id
        };
        context.Users.Add(user);

        await context.SaveChangesAsync();

        var persisted = await context.Organizations.Include(o => o.Users).SingleAsync(o => o.Id == organization.Id);

        Assert.Equal("Contoso", persisted.Name);
        Assert.Single(persisted.Users);
        Assert.Equal(user.Id, persisted.Users.Single().Id);
    }
}
