using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MercedesEISTool.Core.Models;

namespace MercedesEISTool.Core.Services;

public enum ResearchSearchMode
{
    Exact,
    Reversed,
    BytePairSwapped,
    FourByteWordReversed,
    Xor
}

public enum ResearchConfidence
{
    Unknown,
    Suspected,
    Probable,
    Verified,
    Low
}

public class ResearchMatch
{
    public int Offset { get; set; }
    public ResearchSearchMode Mode { get; set; }
    public byte[] MatchedBytes { get; set; } = Array.Empty<byte>();
    public string SourceFileName { get; set; } = string.Empty;
    public string TargetFileName { get; set; } = string.Empty;
}

public class ResearchAnnotation
{
    public string Name { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int Length { get; set; }
    public string ByteOrder { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public ResearchConfidence Confidence { get; set; } = ResearchConfidence.Unknown;
}

public class ResearchFolderFile
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string DetectedType { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public string VIN { get; set; } = string.Empty;
    public int DuplicateGroup { get; set; }
    public string SuggestedRelatedFiles { get; set; } = string.Empty;
    public byte[]? Bytes { get; set; }
}

public class ResearchFolderAnalysis
{
    public List<ResearchFolderFile> Files { get; set; } = new();
    public List<int> DuplicateGroups { get; set; } = new();
}

public class ResearchRelatedGroup
{
    public string Name { get; set; } = string.Empty;
    public ResearchConfidence Confidence { get; set; }
    public List<ResearchFolderFile> Files { get; set; } = new();
}

public class ResearchService
{
    private readonly EisDumpService _dumpService = new();
    private readonly KeyFileLoader _keyFileLoader = new();

    public List<ResearchMatch> SearchSequence(byte[] source, byte[] target, int startOffset, int length, ResearchSearchMode mode, byte xorValue = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (startOffset < 0 || startOffset >= source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        if (length < 1 || length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (startOffset + length > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        var sequence = source.Skip(startOffset).Take(length).ToArray();
        var results = new List<ResearchMatch>();

        for (var index = 0; index <= target.Length - length; index++)
        {
            var candidate = target.Skip(index).Take(length).ToArray();
            if (Matches(sequence, candidate, mode, xorValue))
            {
                results.Add(new ResearchMatch
                {
                    Offset = index,
                    Mode = mode,
                    MatchedBytes = candidate.ToArray(),
                    SourceFileName = string.Empty,
                    TargetFileName = string.Empty
                });
            }
        }

        return results;
    }

    public void SaveAnnotations(string path, IEnumerable<ResearchAnnotation> annotations)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(annotations.ToList(), options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public List<ResearchAnnotation> LoadAnnotations(string path)
    {
        if (!File.Exists(path))
        {
            return new List<ResearchAnnotation>();
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ResearchAnnotation>();
        }

        return JsonSerializer.Deserialize<List<ResearchAnnotation>>(json) ?? new List<ResearchAnnotation>();
    }

    public ResearchFolderAnalysis AnalyzeFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The selected folder does not exist.");
        }

        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => File.Exists(path))
            .Select(path => new FileInfo(path))
            .Where(info => info.Length > 0)
            .OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var analysis = new ResearchFolderAnalysis();
        var shaGroups = new Dictionary<string, List<ResearchFolderFile>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileInfo in files)
        {
            var bytes = File.ReadAllBytes(fileInfo.FullName);
            var sha = ComputeSha256(bytes);
            var file = new ResearchFolderFile
            {
                FileName = fileInfo.Name,
                RelativePath = MakeRelativePath(folderPath, fileInfo.FullName),
                Size = fileInfo.Length,
                Sha256 = sha,
                DetectedType = bytes.Length == 256 ? "EIS dump" : "Possible key file",
                SourceFormat = bytes.Length == 256 ? DetectSourceFormat(bytes) : string.Empty,
                VIN = ExtractVin(bytes),
                Bytes = bytes
            };

            if (shaGroups.TryGetValue(sha, out var group))
            {
                group.Add(file);
            }
            else
            {
                shaGroups[sha] = new List<ResearchFolderFile> { file };
            }

            analysis.Files.Add(file);
        }

        foreach (var group in shaGroups.Where(group => group.Value.Count > 1))
        {
            for (var index = 0; index < group.Value.Count; index++)
            {
                group.Value[index].DuplicateGroup = index + 1;
            }
        }

        analysis.DuplicateGroups = analysis.Files
            .Where(file => file.DuplicateGroup > 0)
            .Select(file => file.DuplicateGroup)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        return analysis;
    }

    public List<ResearchRelatedGroup> FindRelatedGroups(IReadOnlyList<ResearchFolderFile> files)
    {
        var groups = new List<ResearchRelatedGroup>();

        var verifiedVinGroups = files
            .Where(file => !string.IsNullOrWhiteSpace(file.VIN))
            .GroupBy(file => file.VIN, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new ResearchRelatedGroup
            {
                Name = $"VIN: {group.Key}",
                Confidence = ResearchConfidence.Verified,
                Files = group.ToList()
            });

        groups.AddRange(verifiedVinGroups);

        var filenameGroups = files
            .Where(file => !string.IsNullOrWhiteSpace(file.FileName))
            .GroupBy(file => GetBaseName(file.FileName), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new ResearchRelatedGroup
            {
                Name = $"Filename base: {group.Key}",
                Confidence = ResearchConfidence.Low,
                Files = group.ToList()
            });

        groups.AddRange(filenameGroups);

        return groups;
    }

    public string BuildReport(ResearchFolderAnalysis analysis, IEnumerable<ResearchAnnotation> annotations, IEnumerable<ResearchMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(matches);

        var lines = new List<string>
        {
            "Research report",
            "==============="
        };

        lines.Add($"Files analyzed: {analysis.Files.Count}");
        if (analysis.DuplicateGroups.Count > 0)
        {
            lines.Add($"Duplicate groups: {string.Join(", ", analysis.DuplicateGroups)}");
        }

        lines.Add(string.Empty);
        lines.Add("Files:");
        foreach (var file in analysis.Files)
        {
            lines.Add($"- {file.RelativePath} | size={file.Size} | sha256={file.Sha256} | type={file.DetectedType} | format={file.SourceFormat} | vin={file.VIN} | group={file.DuplicateGroup}");
        }

        lines.Add(string.Empty);
        lines.Add("Annotations:");
        foreach (var annotation in annotations)
        {
            lines.Add($"- {annotation.Name} | confidence={annotation.Confidence} | offset=0x{annotation.Offset:X} | length={annotation.Length}");
        }

        lines.Add(string.Empty);
        lines.Add("Matches:");
        foreach (var match in matches)
        {
            lines.Add($"- offset=0x{match.Offset:X} | mode={match.Mode} | bytes={Convert.ToHexString(match.MatchedBytes)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetBaseName(string value)
    {
        var fileName = Path.GetFileNameWithoutExtension(value);
        var markerIndex = fileName.IndexOfAny(new[] { '-', '_', '.', ' ' });
        return markerIndex >= 0 ? fileName[..markerIndex] : fileName;
    }

    private static string MakeRelativePath(string root, string fullPath)
    {
        var rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private static string DetectSourceFormat(byte[] bytes)
    {
        var service = new EisDumpService();
        return service.DetectFormat(bytes);
    }

    private static string ExtractVin(byte[] bytes)
    {
        if (bytes.Length != 256)
        {
            return string.Empty;
        }

        var service = new EisDumpService();
        var dump = service.ParseDump(bytes);
        return dump.VIN;
    }

    private static bool Matches(byte[] sequence, byte[] candidate, ResearchSearchMode mode, byte xorValue)
    {
        return mode switch
        {
            ResearchSearchMode.Reversed => candidate.SequenceEqual(sequence.Reverse().ToArray()),
            ResearchSearchMode.BytePairSwapped => BytePairSwappedMatches(sequence, candidate),
            ResearchSearchMode.FourByteWordReversed => FourByteWordReversedMatches(sequence, candidate),
            ResearchSearchMode.Xor => XorMatches(sequence, candidate, xorValue),
            _ => candidate.SequenceEqual(sequence)
        };
    }

    private static bool BytePairSwappedMatches(byte[] sequence, byte[] candidate)
    {
        if (sequence.Length % 2 != 0 || candidate.Length % 2 != 0)
        {
            return false;
        }

        var expected = new byte[sequence.Length];
        for (var i = 0; i < sequence.Length; i += 2)
        {
            expected[i] = sequence[i + 1];
            expected[i + 1] = sequence[i];
        }

        return candidate.SequenceEqual(expected);
    }

    private static bool FourByteWordReversedMatches(byte[] sequence, byte[] candidate)
    {
        if (sequence.Length < 4 || candidate.Length < 4)
        {
            return false;
        }

        var expected = new byte[sequence.Length];
        Array.Copy(sequence, expected, sequence.Length);

        for (var i = 0; i + 3 < expected.Length; i += 4)
        {
            Array.Reverse(expected, i, 4);
        }

        return candidate.SequenceEqual(expected);
    }

    private static bool XorMatches(byte[] sequence, byte[] candidate, byte xorValue)
    {
        var transformed = sequence.Select(value => (byte)(value ^ xorValue)).ToArray();
        return candidate.SequenceEqual(transformed);
    }
}
