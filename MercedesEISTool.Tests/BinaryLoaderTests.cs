using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Tests;

public class BinaryLoaderTests
{
    [Fact]
    public void LoadBinFile_ReturnsBytes_WhenFileIsExactly256Bytes()
    {
        var loader = new BinaryLoader();
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");

        try
        {
            var expected = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            File.WriteAllBytes(tempPath, expected);

            var loaded = loader.LoadBinFile(tempPath);

            Assert.Equal(expected, loaded);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void LoadBinFile_ThrowsDescriptiveException_ForInvalidSize()
    {
        var loader = new BinaryLoader();
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");

        try
        {
            var invalidData = new byte[255];
            File.WriteAllBytes(tempPath, invalidData);

            var exception = Assert.Throws<InvalidOperationException>(() => loader.LoadBinFile(tempPath));

            Assert.Contains("exactly 256 bytes", exception.Message);
            Assert.Contains("255 bytes", exception.Message);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
