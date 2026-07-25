using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class AnalysisServiceTests
{
    [Fact]
    public void Analyze_ReturnsNotMappedForUnverifiedSensitiveFields()
    {
        var service = new EisAnalysisService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000001");

        var result = service.Analyze(bytes, "sample.bin");

        Assert.Equal("CGDI MB", result.DetectedFormat);
        Assert.Equal("WVWZZZ1JZ3C000001", result.DetectedVin);
        Assert.Equal("Present", result.VinStatus);
        Assert.Equal(FieldValueStatus.NotMapped, result.EisPassword.Status);
        Assert.Null(result.EisPassword.Value);
        Assert.Equal(FieldValueStatus.NotMapped, result.Ssid.Status);
        Assert.Null(result.Ssid.Value);
        Assert.Equal(FieldValueStatus.NotMapped, result.KeyCountStatus);
        Assert.Null(result.KeyCount);
        Assert.Equal("1.1.0", result.ParserVersion);
    }

    [Fact]
    public async Task PersistingAnalysisStoresLatestSnapshot()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "MercedesEISToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);
        try
        {
            var service = new EisAnalysisService();
            var store = new JsonUploadedDumpStore(tempPath);
            var bytes = CreateCgdiDump("WVWZZZ1JZ3C000002");

            var record = await store.PersistAsync(bytes, "sample.bin", "WVWZZZ1JZ3C000002", "ABC-123", "upload", service);
            var latest = await store.GetLatestAnalysisAsync(record.Id);

            Assert.NotNull(latest);
            Assert.Equal("1.1.0", latest.ParserVersion);
            Assert.Equal("CGDI MB", latest.DetectedFormat);
            Assert.Equal("WVWZZZ1JZ3C000002", latest.DetectedVin);
            Assert.Equal(FieldValueStatus.NotMapped, latest.EisPassword.Status);
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    private static byte[] CreateCgdiDump(string vin)
    {
        var bytes = new byte[256];
        Array.Fill(bytes, (byte)0x00);
        var vinBytes = System.Text.Encoding.ASCII.GetBytes(vin.PadRight(17, '\0'));
        Array.Copy(vinBytes, 0, bytes, 0, vinBytes.Length);
        return bytes;
    }
}
