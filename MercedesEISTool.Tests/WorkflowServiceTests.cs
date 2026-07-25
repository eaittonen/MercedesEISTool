using System.Text.Json;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class WorkflowServiceTests
{
    [Fact]
    public void Analyze_AcceptsFileWithoutIdentifiers()
    {
        var service = new AnalysisWorkflowService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000001");

        var result = service.Analyze(bytes, "sample.bin");

        Assert.True(result.AnalysisSucceeded);
        Assert.Equal("CGDI MB", result.DetectedFormat);
        Assert.Equal("WVWZZZ1JZ3C000001", result.DetectedVin);
        Assert.Equal(AnalysisVinStatus.Present, result.VinStatus);
        Assert.Empty(result.StoragePath);
    }

    [Fact]
    public void Analyze_DetectsVvdiVinWhenPresent()
    {
        var service = new AnalysisWorkflowService();
        var bytes = CreateVvdiDump("WVWZZZ1JZ3C000002");

        var result = service.Analyze(bytes, "sample.bin");

        Assert.True(result.AnalysisSucceeded);
        Assert.Equal("VVDI MB Tool", result.DetectedFormat);
        Assert.Equal("WVWZZZ1JZ3C000002", result.DetectedVin);
        Assert.Equal(AnalysisVinStatus.Present, result.VinStatus);
    }

    [Fact]
    public void Analyze_ReportsNotPresentForVvdiWithoutVin()
    {
        var service = new AnalysisWorkflowService();
        var bytes = CreateVvdiDump(string.Empty);

        var result = service.Analyze(bytes, "sample.bin");

        Assert.True(result.AnalysisSucceeded);
        Assert.Equal("VVDI MB Tool", result.DetectedFormat);
        Assert.Equal(string.Empty, result.DetectedVin);
        Assert.Equal(AnalysisVinStatus.NotPresent, result.VinStatus);
    }

    [Fact]
    public void UploadValidation_AcceptsVinOnlyAndRegistrationOnly()
    {
        var service = new AnalysisWorkflowService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000003");

        var vinOnly = service.ValidateUpload(bytes, "WVWZZZ1JZ3C000003", string.Empty, true);
        var registrationOnly = service.ValidateUpload(bytes, string.Empty, "ABC-123", true);

        Assert.True(vinOnly.IsValid);
        Assert.True(registrationOnly.IsValid);
    }

    [Fact]
    public void UploadValidation_RejectsMissingIdentifiersAndUnconfirmedUpload()
    {
        var service = new AnalysisWorkflowService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000004");

        var missing = service.ValidateUpload(bytes, string.Empty, string.Empty, true);
        var unconfirmed = service.ValidateUpload(bytes, "WVWZZZ1JZ3C000004", string.Empty, false);

        Assert.False(missing.IsValid);
        Assert.False(unconfirmed.IsValid);
    }

    [Fact]
    public void ApiResponses_DoNotExposeStoragePaths()
    {
        var response = new AnalyzeDumpResponse
        {
            FileName = "sample.bin",
            DetectedFormat = "CGDI MB",
            DetectedVin = "WVWZZZ1JZ3C000001",
            Message = "Analyzed"
        };

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("StoredFilePath", json);
        Assert.DoesNotContain("Data/Uploads", json);
        Assert.DoesNotContain("C:", json);
        Assert.DoesNotContain("/", json);
    }

    [Fact]
    public void UploadValidation_RejectsDetectedVinMismatch()
    {
        var service = new AnalysisWorkflowService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000005");

        var result = service.ValidateUpload(bytes, "WVWZZZ1JZ3C000006", string.Empty, true);

        Assert.False(result.IsValid);
        Assert.Equal("The confirmed VIN does not match the VIN detected from the dump.", result.Message);
    }

    private static byte[] CreateCgdiDump(string vin)
    {
        var bytes = new byte[256];
        Array.Fill(bytes, (byte)0x00);
        var vinBytes = System.Text.Encoding.ASCII.GetBytes(vin.PadRight(17, '\0'));
        Array.Copy(vinBytes, 0, bytes, 0, vinBytes.Length);
        return bytes;
    }

    private static byte[] CreateVvdiDump(string vin)
    {
        var bytes = new byte[256];
        Array.Fill(bytes, (byte)0x00);
        var signature = System.Text.Encoding.ASCII.GetBytes("VVDIMBDATA");
        Array.Copy(signature, 0, bytes, 0, signature.Length);
        if (!string.IsNullOrEmpty(vin))
        {
            var vinBytes = System.Text.Encoding.ASCII.GetBytes(vin.PadRight(17, '\0'));
            Array.Copy(vinBytes, 0, bytes, 0x90, vinBytes.Length);
        }
        return bytes;
    }
}
