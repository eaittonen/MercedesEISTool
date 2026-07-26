using Microsoft.AspNetCore.Http;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class CurrentUserTests
{
    [Fact]
    public void ProductionCurrentUser_UsesDevelopmentFallbackWhenNoClaimIsPresent()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var currentUser = new ProductionCurrentUser(httpContextAccessor);

        Assert.Equal("development", currentUser.UserId);
    }
}
