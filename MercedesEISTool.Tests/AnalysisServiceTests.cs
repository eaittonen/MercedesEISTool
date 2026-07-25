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
        Assert.Equal(FieldValueStatus.NotPresent, result.EisPassword.Status);
        Assert.Null(result.EisPassword.Value);
        Assert.Equal(FieldValueStatus.NotPresent, result.Ssid.Status);
        Assert.Null(result.Ssid.Value);
        Assert.Equal(FieldValueStatus.NotMapped, result.KeyCountStatus);
        Assert.Null(result.KeyCount);
        Assert.Equal("1.2.0", result.ParserVersion);
    }

    [Fact]
    public void Analyze_MapsCgdiPasswordAndSsid_WhenBytesArePresent()
    {
        var service = new EisAnalysisService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000003");
        var passwordBytes = new byte[] { 0xE5, 0x71, 0xBF, 0xA5, 0xA7, 0x4A, 0xC1, 0x5F };
        var ssidBytes = new byte[] { 0xFC, 0xB2, 0x4C, 0x00 };
        Array.Copy(passwordBytes, 0, bytes, 0x38, passwordBytes.Length);
        Array.Copy(ssidBytes, 0, bytes, 0x28, ssidBytes.Length);

        var result = service.Analyze(bytes, "sample.bin");

        Assert.Equal(FieldValueStatus.Present, result.EisPassword.Status);
        Assert.Equal("5F C1 4A A7 A5 BF 71 E5", result.EisPassword.Value);
        Assert.Equal(0x38, result.EisPassword.SourceOffset);
        Assert.Equal(8, result.EisPassword.Length);
        Assert.Equal("Verified", result.EisPassword.Confidence);
        Assert.Equal(FieldValueStatus.Present, result.Ssid.Status);
        Assert.Equal("00 4C B2 FC", result.Ssid.Value);
        Assert.Equal(0x28, result.Ssid.SourceOffset);
        Assert.Equal(4, result.Ssid.Length);
        Assert.Equal("Verified", result.Ssid.Confidence);
    }

    [Fact]
    public void Analyze_ReportsNotPresentForZeroedPasswordAndSsidBytes()
    {
        var service = new EisAnalysisService();
        var bytes = CreateCgdiDump("WVWZZZ1JZ3C000004");
        Array.Fill(bytes, (byte)0x00, 0x28, 4);
        Array.Fill(bytes, (byte)0x00, 0x38, 8);

        var result = service.Analyze(bytes, "sample.bin");

        Assert.Equal(FieldValueStatus.NotPresent, result.EisPassword.Status);
        Assert.Null(result.EisPassword.Value);
        Assert.Equal(FieldValueStatus.NotPresent, result.Ssid.Status);
        Assert.Null(result.Ssid.Value);
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
            Assert.Equal("1.2.0", latest.ParserVersion);
            Assert.Equal("CGDI MB", latest.DetectedFormat);
            Assert.Equal("WVWZZZ1JZ3C000002", latest.DetectedVin);
            Assert.Equal(FieldValueStatus.NotPresent, latest.EisPassword.Status);
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
