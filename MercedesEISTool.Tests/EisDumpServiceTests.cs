using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Tests;

public class EisDumpServiceTests
{
    [Fact]
    public void Parse_RecognizesVvdiDump_AndExtractsCoreFields()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();

        var signature = "VVDIMBDATA";
        for (var i = 0; i < signature.Length; i++)
        {
            data[i] = (byte)signature[i];
        }

        var vin = "ABC12345678901234";
        for (var i = 0; i < vin.Length; i++)
        {
            data[0x90 + i] = (byte)vin[i];
        }

        var dump = service.ParseDump(data);

        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal("ABC12345678901234", dump.VIN);
        Assert.Equal(string.Empty, dump.MCU);
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
        var signature = "VVDIMBDATA";
        for (var i = 0; i < signature.Length; i++)
        {
            data[i] = (byte)signature[i];
        }

        var vin = "ABC12345678901234";
        for (var i = 0; i < vin.Length; i++)
        {
            data[0x90 + i] = (byte)vin[i];
        }

        var dump = service.ParseDump(data);
        var converted = service.ConvertDump(dump, "CGDI MB");

        Assert.Equal(256, converted.RawData.Length);
        Assert.Equal("CGDI MB", converted.Format);
        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal(data[10], converted.RawData[10]);
        Assert.Equal(data[200], converted.RawData[200]);
    }

    [Fact]
    public void DetectFormat_RequiresFullVvdiSignature()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        data[0] = (byte)'V';
        data[1] = (byte)'V';

        Assert.Equal("Unknown", service.DetectFormat(data));
    }

    [Fact]
    public void Parse_DetectsVvdiSignatureAndVinAtOffset90()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        var signature = "VVDIMBDATA";
        for (var i = 0; i < signature.Length; i++)
        {
            data[i] = (byte)signature[i];
        }

        var vin = "ABC12345678901234";
        for (var i = 0; i < vin.Length; i++)
        {
            data[0x90 + i] = (byte)vin[i];
        }

        var dump = service.ParseDump(data);

        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal(vin, dump.VIN);
    }

    [Fact]
    public void Parse_DetectsCgdiVinAtOffset0()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        var vin = "ABC12345678901234";
        for (var i = 0; i < vin.Length; i++)
        {
            data[i] = (byte)vin[i];
        }

        var dump = service.ParseDump(data);

        Assert.Equal("CGDI MB", dump.Format);
        Assert.Equal(vin, dump.VIN);
    }

    [Fact]
    public void Parse_RejectsInvalidVinCharacters()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        var invalidVin = "ABC12345678901Q345";
        for (var i = 0; i < invalidVin.Length; i++)
        {
            data[i] = (byte)invalidVin[i];
        }

        var dump = service.ParseDump(data);

        Assert.Equal("Unknown", dump.Format);
        Assert.Equal(string.Empty, dump.VIN);
    }

    [Fact]
    public void Parse_DetectsSourceFormatIndependentlyFromConversionTarget()
    {
        var service = new EisDumpService();
        var data = CreateDumpData();
        data[0] = (byte)'V';
        data[1] = (byte)'V';
        data[2] = (byte)'D';
        data[3] = (byte)'I';
        data[4] = (byte)'M';
        data[5] = (byte)'B';
        data[6] = (byte)'D';
        data[7] = (byte)'A';
        data[8] = (byte)'T';
        data[9] = (byte)'A';

        var dump = service.ParseDump(data);
        var converted = service.ConvertDump(dump, "CGDI MB");

        Assert.Equal("VVDI MB Tool", dump.Format);
        Assert.Equal("CGDI MB", converted.Format);
    }

    [Fact]
    public void CompareDumps_ReturnsByteByByteDiffs()
    {
        var service = new EisDumpService();
        var left = CreateDumpData();
        var right = CreateDumpData();
        left[0] = 0xAA;
        right[0] = 0xBB;
        left[16] = 0x01;
        right[16] = 0x02;

        var result = service.CompareDumps(left, right);

        Assert.Equal(2, result.TotalDifferences);
        Assert.Contains(result.DifferingOffsets, offset => offset == 0);
        Assert.Contains(result.DifferingOffsets, offset => offset == 16);
        Assert.Contains(result.Rows, row => row.HasDifferences);
    }

    [Fact]
    public void SearchSequence_FindsExactAndReversedMatches()
    {
        var service = new EisDumpService();
        var source = new byte[] { 0x01, 0x02, 0x03 };
        var target = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x03, 0x02, 0x01 };

        var result = service.SearchSequence(source, target, 0, 3);

        Assert.Equal(new[] { 0 }, result.ExactMatches);
        Assert.Equal(new[] { 4 }, result.ReversedMatches);
    }

    [Fact]
    public void SearchSequence_RejectsInvalidRange()
    {
        var service = new EisDumpService();
        var source = new byte[] { 0x01, 0x02, 0x03 }; 
        var target = new byte[] { 0x01, 0x02, 0x03 };

        Assert.Throws<ArgumentOutOfRangeException>(() => service.SearchSequence(source, target, 2, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.SearchSequence(source, target, 0, 0));
    }

    [Fact]
    public void CompareAndSearch_DoNotModifyInputArrays()
    {
        var service = new EisDumpService();
        var left = CreateDumpData();
        var right = CreateDumpData();
        var beforeLeft = (byte[])left.Clone();
        var beforeRight = (byte[])right.Clone();

        service.CompareDumps(left, right);
        service.SearchSequence(left, right, 0, 2);

        Assert.Equal(beforeLeft, left);
        Assert.Equal(beforeRight, right);
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
