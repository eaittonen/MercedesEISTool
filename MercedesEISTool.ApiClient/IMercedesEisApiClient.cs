using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.ApiClient;

public interface IMercedesEisApiClient
{
    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<AnalyzeDumpResponse> AnalyzeDumpAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
    Task<CompareDumpsResponse> CompareDumpsAsync(byte[] left, byte[] right, string leftFileName, string rightFileName, CancellationToken cancellationToken = default);
}
