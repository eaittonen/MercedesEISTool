using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Tests;

public class ResearchServiceTests
{
    [Fact]
    public void KeyFileLoader_LoadsValidKeyFile()
    {
        var loader = new KeyFileLoader();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03, 0x04 });

            var bytes = loader.LoadFile(path);

            Assert.Equal(4, bytes.Length);
            Assert.Equal(0x03, bytes[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFileLoader_RejectsEmptyFile()
    {
        var loader = new KeyFileLoader();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Array.Empty<byte>());

            Assert.Throws<InvalidOperationException>(() => loader.LoadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFileLoader_RejectsFilesAboveMaximumSize()
    {
        var loader = new KeyFileLoader();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[KeyFileLoader.MaxBytes + 1]);

            Assert.Throws<InvalidOperationException>(() => loader.LoadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ResearchService_ExactAndReversedSearchesWork()
    {
        var service = new ResearchService();
        var source = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var target = new byte[] { 0x10, 0x20, 0x30, 0x99, 0x30, 0x20, 0x10 };

        var exact = service.SearchSequence(source, target, 0, 3, ResearchSearchMode.Exact);
        var reversed = service.SearchSequence(source, target, 0, 3, ResearchSearchMode.Reversed);

        Assert.Contains(exact, match => match.Offset == 0);
        Assert.Contains(reversed, match => match.Offset == 4);
    }

    [Fact]
    public void ResearchService_SupportsBytePairSwapAndWordReverseAndXor()
    {
        var service = new ResearchService();
        var source = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var swappedTarget = new byte[] { 0x02, 0x01, 0x04, 0x03, 0x00, 0x00 };
        var reversedTarget = new byte[] { 0x04, 0x03, 0x02, 0x01, 0x00, 0x00 };
        var xorTarget = new byte[] { 0x00, 0x03, 0x02, 0x05, 0x00, 0x00 };

        var swapped = service.SearchSequence(source, swappedTarget, 0, 4, ResearchSearchMode.BytePairSwapped);
        var reversedWords = service.SearchSequence(source, reversedTarget, 0, 4, ResearchSearchMode.FourByteWordReversed);
        var xorMatches = service.SearchSequence(source, xorTarget, 0, 4, ResearchSearchMode.Xor, xorValue: 0x01);

        Assert.Contains(swapped, match => match.Offset == 0);
        Assert.Contains(reversedWords, match => match.Offset == 0);
        Assert.Contains(xorMatches, match => match.Offset == 0);
    }

    [Fact]
    public void ResearchService_SaveAndLoadAnnotations()
    {
        var service = new ResearchService();
        var annotations = new List<ResearchAnnotation>
        {
            new()
            {
                Name = "Key password candidate",
                FileFormat = "Key",
                Offset = 16,
                Length = 8,
                ByteOrder = "LittleEndian",
                Notes = "Research note",
                Confidence = ResearchConfidence.Suspected
            }
        };

        var path = Path.Combine(Path.GetTempPath(), $"research-{Guid.NewGuid():N}.json");
        try
        {
            service.SaveAnnotations(path, annotations);
            var loaded = service.LoadAnnotations(path);

            Assert.Single(loaded);
            Assert.Equal("Key password candidate", loaded[0].Name);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ResearchService_DetectsDuplicateSha256AndGroupsByVerifiedVin()
    {
        var service = new ResearchService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var bytes = new byte[256];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i + 1);
            }

            var vin = "ABC12345678901234";
            for (var i = 0; i < vin.Length; i++)
            {
                bytes[0x90 + i] = (byte)vin[i];
            }

            File.WriteAllBytes(Path.Combine(tempDir, "one.bin"), bytes);
            File.WriteAllBytes(Path.Combine(tempDir, "two.bin"), bytes);

            var result = service.AnalyzeFolder(tempDir);

            Assert.Equal(2, result.Files.Count);
            Assert.Equal(2, result.DuplicateGroups.Count);
            Assert.Contains(result.Files, file => file.VIN == string.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResearchService_FilenameOnlyGroupingRemainsLowConfidence()
    {
        var service = new ResearchService();
        var dir = Path.Combine(Path.GetTempPath(), $"research-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "sample-a.bin"), new byte[256]);
            File.WriteAllBytes(Path.Combine(dir, "sample-b.bin"), new byte[256]);

            var result = service.AnalyzeFolder(dir);
            var groups = service.FindRelatedGroups(result.Files);

            Assert.Contains(groups, group => group.Confidence == ResearchConfidence.Low);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResearchService_BuildsReadableResearchReport()
    {
        var service = new ResearchService();
        var analysis = new ResearchFolderAnalysis
        {
            Files =
            [
                new ResearchFolderFile
                {
                    FileName = "alpha.bin",
                    RelativePath = "alpha.bin",
                    Size = 256,
                    Sha256 = "abc",
                    DetectedType = "EIS dump",
                    SourceFormat = "CGDI MB",
                    VIN = "VIN123",
                    DuplicateGroup = 1
                }
            ],
            DuplicateGroups = [1]
        };

        var annotations = new List<ResearchAnnotation>
        {
            new() { Name = "Candidate A", Confidence = ResearchConfidence.Suspected }
        };

        var matches = new List<ResearchMatch>
        {
            new() { Offset = 4, Mode = ResearchSearchMode.Exact, MatchedBytes = new byte[] { 0x01, 0x02 } }
        };

        var report = service.BuildReport(analysis, annotations, matches);

        Assert.Contains("Research report", report);
        Assert.Contains("alpha.bin", report);
        Assert.Contains("Candidate A", report);
        Assert.Contains("0x4", report);
    }

    [Fact]
    public void ResearchService_LeavesInputBytesUnchanged()
    {
        var service = new ResearchService();
        var source = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        var target = new byte[] { 0x33, 0x22, 0x11, 0x44 };
        var beforeSource = (byte[])source.Clone();
        var beforeTarget = (byte[])target.Clone();

        service.SearchSequence(source, target, 0, 4, ResearchSearchMode.Exact);

        Assert.Equal(beforeSource, source);
        Assert.Equal(beforeTarget, target);
    }
}
