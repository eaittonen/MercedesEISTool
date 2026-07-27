using MercedesEISTool.Core.Services;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public sealed class VvdiParserTests
{
    [Fact]
    public void DetectFormat_ReturnsVvdiForHeadered256ByteDump()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();

        Assert.Equal("VVDI MB Tool", service.DetectFormat(data));
    }

    [Fact]
    public void DetectFormat_RejectsWrongHeader()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();
        data[0] = (byte)'X';

        Assert.Equal("Unknown", service.DetectFormat(data));
    }

    [Fact]
    public void DetectFormat_RejectsWrongSize()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();
        Array.Resize(ref data, 255);

        Assert.Equal("Unknown", service.DetectFormat(data));
    }

    [Fact]
    public void ParseResult_ExtractsSsidPasswordVinAndPartNumber()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();
        var ssidBytes = ConvertHexStringToBytes("F010366A");
        Array.Copy(ssidBytes, 0, data, 0x10, ssidBytes.Length);

        var passwordBytes = ConvertHexStringToBytes("11C65502512DC766");
        Array.Copy(passwordBytes, 0, data, 0x70, passwordBytes.Length);

        var vin = "WDD2040081A022323";
        WriteAscii(data, 0x90, vin);

        var partNumber = "2045450908";
        WriteAscii(data, 0xE0, partNumber);

        var result = service.ParseResult(data);

        Assert.Equal("VVDI MB Tool", result.Format);
        Assert.Equal(vin, result.Vin);
        Assert.Equal("F010366A", result.Ssid);
        Assert.Equal(partNumber, result.EisPartNumber);
        Assert.Equal("11C65502512DC766", result.EisPassword);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseResult_TreatsZeroPasswordAsNotStored()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();
        Array.Clear(data, 0x70, 8);

        var result = service.ParseResult(data);

        Assert.Null(result.EisPassword);
        Assert.Contains(result.Warnings, warning => warning.Contains("Password"));
    }

    [Fact]
    public void ParseResult_EmitsWarningForInvalidVin()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();
        WriteAscii(data, 0x90, "INVALIDVIN");

        var result = service.ParseResult(data);

        Assert.Contains(result.Warnings, warning => warning.Contains("VIN"));
    }

    [Fact]
    public void ParseResult_EmitsWarningForInvalidPartNumber()
    {
        var service = new EisDumpService();
        var data = CreateVvdiDump();
        WriteAscii(data, 0xE0, "@@@"");

        var result = service.ParseResult(data);

        Assert.Contains(result.Warnings, warning => warning.Contains("part number"));
    }

    [Fact]
    public void EisAnalysisService_UsesParserForVvdiDump()
    {
        var service = new EisAnalysisService();
        var data = CreateVvdiDump();
        var vin = "WDD2193221A138153";
        WriteAscii(data, 0x90, vin);

        var result = service.Analyze(data, "jonecls.bin");

        Assert.Equal("VVDI MB Tool", result.DetectedFormat);
        Assert.Equal(vin, result.DetectedVin);
        Assert.Equal("Present", result.VinStatus);
        Assert.Equal("Present", result.Ssid.Status.ToString());
    }

    private static byte[] CreateVvdiDump()
    {
        var data = new byte[256];
        var header = "VVDIMBDATA";
        for (var i = 0; i < header.Length; i++)
        {
            data[i] = (byte)header[i];
        }

        return data;
    }

    private static void WriteAscii(byte[] data, int offset, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, data, offset, Math.Min(bytes.Length, 17));
    }

    private static byte[] ConvertHexStringToBytes(string value)
    {
        return Enumerable.Range(0, value.Length / 2)
            .Select(index => Convert.ToByte(value.Substring(index * 2, 2), 16))
            .ToArray();
    }
}
