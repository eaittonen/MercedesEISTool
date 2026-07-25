using System.Net;
using System.Net.Http.Json;
using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.ApiClient;

public class MercedesEisApiClient : IMercedesEisApiClient
{
    private readonly HttpClient _httpClient;

    public MercedesEisApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/health", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken: cancellationToken) ?? new HealthResponse();
    }

    public async Task<AnalyzeDumpResponse> AnalyzeDumpAsync(byte[] data, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new ByteArrayContent(data);
        content.Add(streamContent, "file", fileName);

        using var response = await _httpClient.PostAsync("/api/dumps/analyze", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AnalyzeDumpResponse>(cancellationToken: cancellationToken) ?? new AnalyzeDumpResponse();
    }

    public async Task<UploadDumpResponse> UploadDumpAsync(byte[] data, string fileName, string? userProvidedVin, string? userProvidedRegistrationNumber, bool vehicleIdentifierConfirmed, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new ByteArrayContent(data);
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(userProvidedVin ?? string.Empty), "userProvidedVin");
        content.Add(new StringContent(userProvidedRegistrationNumber ?? string.Empty), "userProvidedRegistrationNumber");
        content.Add(new StringContent(vehicleIdentifierConfirmed.ToString()), "vehicleIdentifierConfirmed");

        using var response = await _httpClient.PostAsync("/api/files/upload", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UploadDumpResponse>(cancellationToken: cancellationToken) ?? new UploadDumpResponse();
    }

    public async Task<UploadedDumpListResponse> GetUploadedDumpsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/uploads", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UploadedDumpListResponse>(cancellationToken: cancellationToken) ?? new UploadedDumpListResponse();
    }

    public async Task<CompareDumpsResponse> CompareDumpsAsync(byte[] left, byte[] right, string leftFileName, string rightFileName, string vehicleIdentifier, string registrationNumber, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var leftContent = new ByteArrayContent(left);
        using var rightContent = new ByteArrayContent(right);
        content.Add(leftContent, "leftFile", leftFileName);
        content.Add(rightContent, "rightFile", rightFileName);
        content.Add(new StringContent(vehicleIdentifier), "vehicleIdentifier");
        content.Add(new StringContent(registrationNumber), "registrationNumber");

        using var response = await _httpClient.PostAsync("/api/dumps/compare", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CompareDumpsResponse>(cancellationToken: cancellationToken) ?? new CompareDumpsResponse();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
        throw new HttpRequestException(error?.Message ?? $"Request failed with status {(int)response.StatusCode}.", null, response.StatusCode);
    }
}
