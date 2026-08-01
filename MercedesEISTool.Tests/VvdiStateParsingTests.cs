using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class VvdiStateParsingTests
{
    [Fact]
    public void Analyze_MapsVvdiStateFlags_WhenByte26ContainsFlags()
    {
        var service = new EisAnalysisService();
        var bytes = CreateVvdiDump();
        bytes[0x26] = 0x3F;

        var result = service.Analyze(bytes, "sample.bin");

        Assert.Equal("VVDI MB Tool", result.DetectedFormat);
        Assert.True(result.Initialized);
        Assert.True(result.Personalized);
        Assert.True(result.TpCleared);
        Assert.True(result.Activated);
        Assert.True(result.DealerEis);
        Assert.True(result.Fbs4);
    }

    private static byte[] CreateVvdiDump()
    {
        var bytes = new byte[256];
        Array.Fill(bytes, (byte)0x00);
        var signature = System.Text.Encoding.ASCII.GetBytes("VVDIMBDATA");
        Array.Copy(signature, 0, bytes, 0, signature.Length);
        return bytes;
    }
}
