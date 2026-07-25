using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class ArchitectureTests
{
    [Fact]
    public void LicenseService_AllowsAnalyzeAndCompareInDevelopment()
    {
        var service = new DevelopmentLicenseService();

        var analyze = service.CheckFeature(FeatureName.AnalyzeDump);
        var compare = service.CheckFeature(FeatureName.CompareDumps);
        var convert = service.CheckFeature(FeatureName.ConvertDump);

        Assert.True(analyze.IsGranted);
        Assert.True(compare.IsGranted);
        Assert.False(convert.IsGranted);
    }

    [Fact]
    public void Contracts_ExposeExpectedApiModels()
    {
        var response = new AnalyzeDumpResponse();
        var compare = new CompareDumpsResponse();
        var health = new HealthResponse();
        var error = new ApiErrorResponse();

        Assert.NotNull(response);
        Assert.NotNull(compare);
        Assert.NotNull(health);
        Assert.NotNull(error);
    }

    [Fact]
    public void ApiClient_ImplementsExpectedInterface()
    {
        var client = new HttpClient();
        var apiClient = new MercedesEisApiClient(client);
        Assert.IsAssignableFrom<IMercedesEisApiClient>(apiClient);
    }
}
