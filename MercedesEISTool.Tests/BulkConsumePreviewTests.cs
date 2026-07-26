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
}
