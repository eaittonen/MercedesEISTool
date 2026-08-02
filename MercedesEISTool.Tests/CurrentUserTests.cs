using Microsoft.AspNetCore.Http;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class CurrentUserTests
{
    [Fact]
    public void ProductionCurrentUser_DoesNotUseDevelopmentFallbackWhenNoClaimsArePresent()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var currentUser = new ProductionCurrentUser(httpContextAccessor);

        Assert.Equal(string.Empty, currentUser.UserId);
        Assert.Equal(string.Empty, currentUser.DisplayName);
        Assert.Null(currentUser.OrganizationId);
    }
}
