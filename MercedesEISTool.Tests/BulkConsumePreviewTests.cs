using MercedesEISTool.Contracts.Models;
using MercedesEISTool.GUI.ViewModels;

namespace MercedesEISTool.Tests;

public class BulkConsumePreviewTests
{
    [Fact]
    public void ClassifyBulkConsumeFile_RecognizesEisDumpAndIgnoredKey()
    {
        var classifier = typeof(MainViewModel).GetMethod("ClassifyBulkConsumeFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(classifier);

        var eisResult = classifier!.Invoke(null, new object?[] { "dump.bin", 256L });
        var ignoredKeyResult = classifier.Invoke(null, new object?[] { "key.bin", 160L });
        var unsupportedResult = classifier.Invoke(null, new object?[] { "notes.txt", 128L });

        Assert.Equal("EIS dump", eisResult);
        Assert.Equal("CGMB key (ignored)", ignoredKeyResult);
        Assert.Equal("Unsupported", unsupportedResult);
    }

    [Fact]
    public void ExtractBulkConsumeMetadata_UsesParserResultForVvdiDump()
    {
        var extractor = typeof(MainViewModel).GetMethod("ExtractBulkConsumeMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(extractor);

        var data = new byte[256];
        var header = "VVDIMBDATA";
        for (var i = 0; i < header.Length; i++)
        {
            data[i] = (byte)header[i];
        }

        var vin = "WDD2193221A138153";
        var vinBytes = System.Text.Encoding.ASCII.GetBytes(vin);
        Array.Copy(vinBytes, 0, data, 0x90, Math.Min(vinBytes.Length, 17));

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var filePath = Path.Combine(tempRoot, "dump.bin");
        File.WriteAllBytes(filePath, data);

        try
        {
            var result = extractor!.Invoke(null, new object?[] { data, new FileInfo(filePath), tempRoot });
            var metadata = Assert.IsType<BulkConsumeMetadata>(result);

            Assert.Equal(vin, metadata.DetectedVin);
            Assert.Equal(MetadataConfidence.High, metadata.VinConfidence);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractBulkConsumeMetadata_DoesNotAcceptRepeatedNumericVinLikeValues()
    {
        var extractor = typeof(MainViewModel).GetMethod("ExtractBulkConsumeMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(extractor);

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var filePath = Path.Combine(tempRoot, "dump.bin");
        File.WriteAllBytes(filePath, new byte[256]);

        try
        {
            var result = extractor!.Invoke(null, new object?[] { new byte[256], new FileInfo(filePath), Path.Combine(tempRoot, "11111111111111111") });
            var metadata = Assert.IsType<BulkConsumeMetadata>(result);

            Assert.True(string.IsNullOrWhiteSpace(metadata.DetectedVin));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractBulkConsumeMetadata_DoesNotAcceptVinEmbeddedInFreeText()
    {
        var extractor = typeof(MainViewModel).GetMethod("ExtractBulkConsumeMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(extractor);

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var filePath = Path.Combine(tempRoot, "Varalukko ei tama auto WDD2042081F179893.bin");
        File.WriteAllBytes(filePath, new byte[256]);

        try
        {
            var result = extractor!.Invoke(null, new object?[] { new byte[256], new FileInfo(filePath), tempRoot });
            var metadata = Assert.IsType<BulkConsumeMetadata>(result);

            Assert.True(string.IsNullOrWhiteSpace(metadata.DetectedVin));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractBulkConsumeMetadata_StoresRegistrationDescriptionInAdditionalInformation()
    {
        var extractor = typeof(MainViewModel).GetMethod("ExtractBulkConsumeMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(extractor);

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var filePath = Path.Combine(tempRoot, "dump.bin");
        File.WriteAllBytes(filePath, new byte[256]);
        var folderPath = Path.Combine(tempRoot, "720-BXP Est");
        Directory.CreateDirectory(folderPath);
        var nestedFilePath = Path.Combine(folderPath, "dump.bin");
        File.WriteAllBytes(nestedFilePath, new byte[256]);

        try
        {
            var result = extractor!.Invoke(null, new object?[] { new byte[256], new FileInfo(nestedFilePath), tempRoot });
            var metadata = Assert.IsType<BulkConsumeMetadata>(result);

            Assert.Equal("720-BXP", metadata.RegistrationNumber);
            Assert.Equal("Est", metadata.AdditionalInformation);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
