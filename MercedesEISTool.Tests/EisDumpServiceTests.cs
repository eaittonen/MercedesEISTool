using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Tests;

public class EisDumpServiceTests
{
    [Fact]
    public void Parse_RecognizesVvdiDump_AndExtractsCoreFields()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();

        data[0] = (byte)'V';
        data[1] = (byte)'V';
        data[16] = (byte)'M';
        data[17] = (byte)'C';
        data[18] = (byte)'U';
        data[19] = (byte)'1';
        data[144] = (byte)'V';
        data[145] = (byte)'I';
        data[146] = (byte)'N';
        data[147] = (byte)'1';
        data[148] = (byte)'2';
        data[149] = (byte)'3';
        data[150] = (byte)'4';
        data[151] = (byte)'5';
        data[152] = (byte)'6';
        data[153] = (byte)'7';
        data[154] = (byte)'8';
        data[155] = (byte)'9';
        data[156] = (byte)'0';
        data[157] = (byte)'1';
        data[158] = (byte)'2';
        data[159] = (byte)'3';
        data[160] = (byte)'4';

        var dump = service.ParseDump(data);

        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal("VIN12345678901234", dump.VIN);
        Assert.Equal("MCU1", dump.MCU);
        Assert.Equal(string.Empty, dump.EisType);
        Assert.Equal(string.Empty, dump.SSID);
        Assert.Empty(dump.Keys);
    }

    [Fact]
    public void Parse_ReturnsUnknownFormat_WhenSignatureIsAbsent()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();

        var dump = service.ParseDump(data);

        Assert.Equal("Unknown", dump.Format);
        Assert.Equal(string.Empty, dump.VIN);
        Assert.Equal(string.Empty, dump.MCU);
    }

    [Fact]
    public void Parse_ReturnsEmptyValues_WhenFieldDefinitionsAreMissing()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();

        var dump = service.ParseDump(data);

        Assert.Equal(string.Empty, dump.EisType);
        Assert.Equal(string.Empty, dump.SSID);
    }

    [Fact]
    public void Parse_DoesNotEmitReplacementCharacters_ForUnsafeText()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        data[0] = (byte)'V';
        data[1] = (byte)'V';
        for (var i = 0; i < 17; i++)
        {
            data[144 + i] = 0xFF;
        }

        var dump = service.ParseDump(data);

        Assert.Equal(string.Empty, dump.VIN);
        Assert.DoesNotContain("?", dump.VIN);
    }

    [Fact]
    public void Parse_PreservesRawPayload()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        data[0] = (byte)'V';
        data[1] = (byte)'V';

        var dump = service.ParseDump(data);

        Assert.Equal(data, dump.RawData);
        Assert.Equal(data[10], dump.RawData[10]);
        Assert.Equal(data[200], dump.RawData[200]);
    }

    [Fact]
    public void Convert_ProducesTargetFormatAndPreservesRawPayload()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        data[0] = (byte)'V';
        data[1] = (byte)'V';

        var dump = service.ParseDump(data);
        var converted = service.ConvertDump(dump, "CGDI MB");

        Assert.Equal(256, converted.RawData.Length);
        Assert.Equal("CGDI MB", converted.Format);
        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal(data[10], converted.RawData[10]);
        Assert.Equal(data[200], converted.RawData[200]);
    }

    [Fact]
    public void Validate_ReturnsFalse_ForInvalidLength()
    {
        var service = new EisDumpService();
        var data = new byte[10];

        var result = service.ValidateDump(data);

        Assert.False(result.IsValid);
    }

    private static byte[] CreateDumpData()
    {
        var data = new byte[256];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }
}
