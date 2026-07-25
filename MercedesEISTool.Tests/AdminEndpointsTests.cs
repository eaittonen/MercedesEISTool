using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class AdminEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NormalUser_CannotListAdminUsers()
    {
        using var client = await CreateAuthenticatedClientAsync("user@example.local", "development-only-password");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_CanListUsers()
    {
        using var client = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Items);
    }

    [Fact]
    public async Task Administrator_CanDisableUser()
    {
        using var adminClient = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");
        var listResponse = await adminClient.GetAsync("/api/admin/users");
        var payload = await listResponse.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        var user = Assert.Single(payload!.Items.Where(item => item.Email == "user@example.local"));

        var disableResponse = await adminClient.PostAsync($"/api/admin/users/{user.Id}/disable", content: null);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        var refreshed = await adminClient.GetAsync("/api/admin/users");
        var refreshedPayload = await refreshed.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        var updatedUser = Assert.Single(refreshedPayload!.Items.Where(item => item.Id == user.Id));
        Assert.False(updatedUser.IsEnabled);
    }

    [Fact]
    public async Task Administrator_CanEnableUser()
    {
        using var adminClient = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");
        var listResponse = await adminClient.GetAsync("/api/admin/users");
        var payload = await listResponse.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        var user = Assert.Single(payload!.Items.Where(item => item.Email == "user@example.local"));

        await adminClient.PostAsync($"/api/admin/users/{user.Id}/disable", content: null);
        var enableResponse = await adminClient.PostAsync($"/api/admin/users/{user.Id}/enable", content: null);
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

        var refreshed = await adminClient.GetAsync("/api/admin/users");
        var refreshedPayload = await refreshed.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        var updatedUser = Assert.Single(refreshedPayload!.Items.Where(item => item.Id == user.Id));
        Assert.True(updatedUser.IsEnabled);
    }

    [Fact]
    public async Task Administrator_CanReadStorageDiagnostics()
    {
        using var client = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");

        var response = await client.GetAsync("/api/admin/storage-diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<StorageDiagnosticsResponseDto>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.StorageRoot));
    }

    [Fact]
    public async Task Administrator_CanExposePasswordChangeRequirementState()
    {
        using var client = await CreateAuthenticatedClientAsync("admin@example.local", "development-only-password");

        var usersResponse = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var usersPayload = await usersResponse.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        var user = Assert.Single(usersPayload!.Items.Where(item => item.Email == "user@example.local"));

        var toggleResponse = await client.PostAsJsonAsync($"/api/admin/users/{user.Id}/force-password-change", new ForcePasswordChangeRequestDto
        {
            RequirePasswordChange = true
        });
        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);

        var refreshedResponse = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);
        var refreshedPayload = await refreshedResponse.Content.ReadFromJsonAsync<AdminUserListResponseDto>();
        var refreshedUser = Assert.Single(refreshedPayload!.Items.Where(item => item.Id == user.Id));
        Assert.True(refreshedUser.MustChangePassword);
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
