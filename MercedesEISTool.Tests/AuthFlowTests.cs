using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class AuthFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_UsesDevelopmentBootstrapCredentials()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@example.local",
            Password = "development-only-password"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Contains("Administrator", payload.Roles);
    }
}
