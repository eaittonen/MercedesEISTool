using MercedesEISTool.Core;
using MercedesEISTool.Core.Models;
using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Tests;

public class EisDumpServiceTests
{
    [Fact]
    public void Parse_RecognizesVvdiDump_AndExtractsCoreFields()
    {
        var service = new EisDumpService();
        var data = new byte[256];

        data[0] = 0x56;
        data[1] = 0x56;
        data[16] = 0x4D;
        data[17] = 0x43;
        data[18] = 0x55;
        data[19] = 0x31;
        data[144] = 0x56;
        data[145] = 0x49;
        data[146] = 0x4E;
        data[147] = 0x31;
        data[148] = 0x32;
        data[149] = 0x33;
        data[150] = 0x34;
        data[151] = 0x35;
        data[152] = 0x36;
        data[153] = 0x37;
        data[154] = 0x38;
        data[155] = 0x39;
        data[156] = 0x30;
        data[157] = 0x31;
        data[158] = 0x32;
        data[159] = 0x33;

        var dump = service.ParseDump(data);

        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal("VIN1234567890123", dump.VIN);
        Assert.Equal("TODO", dump.EisType);
        Assert.Equal("TODO", dump.SSID);
        Assert.Equal("MCU1", dump.MCU);
        Assert.Empty(dump.Keys);
    }

    [Fact]
    public void Convert_ProducesTargetFormatAndPreservesRawPayload()
    {
        var service = new EisDumpService();
        var data = new byte[256];
        data[0] = 0x56;
        data[1] = 0x56;
        for (var i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
        data[0] = 0x56;
        data[1] = 0x56;

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
}
