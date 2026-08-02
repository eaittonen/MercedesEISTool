using System.Net;
using System.Net.Http.Json;
using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.Tests;

public class AdminConsoleApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminConsoleApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_CanLoadDashboardPayload()
    {
        using var client = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminDashboardResponseDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Metrics);
        Assert.NotEmpty(payload.DashboardSections);
    }

    [Fact]
    public async Task Administrator_CanLoadHealthPayload()
    {
        using var client = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");

        var response = await client.GetAsync("/api/admin/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminHealthResponseDto>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.SqliteStatus));
    }

    [Fact]
    public async Task Administrator_CanLoadSharingPayload()
    {
        using var client = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");

        var response = await client.GetAsync("/api/admin/shares");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminSharesResponseDto>();
        Assert.NotNull(payload);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
