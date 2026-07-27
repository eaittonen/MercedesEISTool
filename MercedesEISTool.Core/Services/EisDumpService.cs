using System.Text;
using System.Text.Json;
using MercedesEISTool.Core.Models;

namespace MercedesEISTool.Core.Services;

public class EisDumpService
{
    private const string VvdiSignature = "VVDIMBDATA";
    private readonly Dictionary<string, Dictionary<string, FieldDefinition>> _fieldMaps;
    private readonly List<IEisParser> _parsers = new();

    public EisDumpService()
    {
        var configPath = ResolveConfigPath();
        var json = File.ReadAllText(configPath);
        _fieldMaps = LoadFieldMaps(json);
        _parsers.Add(new CgdiEisParser());
        _parsers.Add(new VvdiMercedesEisParser());
    }

    private static Dictionary<string, Dictionary<string, FieldDefinition>> LoadFieldMaps(string json)
    {
        var result = new Dictionary<string, Dictionary<string, FieldDefinition>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var formatProperty in document.RootElement.EnumerateObject())
        {
            if (formatProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fieldMap = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldProperty in formatProperty.Value.EnumerateObject())
            {
                if (fieldProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var definition = new FieldDefinition();
                if (TryReadInt(fieldProperty.Value, "offset", out var offset))
                {
                    definition.Offset = offset;
                }

                if (TryReadInt(fieldProperty.Value, "length", out var length))
                {
                    definition.Length = length;
                }

                fieldMap[fieldProperty.Name] = definition;
            }

            result[formatProperty.Name] = fieldMap;
        }

        return result;
    }

    private static bool TryReadInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (element.TryGetProperty(propertyName, out var child))
        {
            if (child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out var parsed))
            {
                value = parsed;
                return true;
            }

            if (child.ValueKind == JsonValueKind.String && int.TryParse(child.GetString(), out parsed))
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    private static string ResolveConfigPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Configuration", "FieldMaps.json"),
            Path.Combine(AppContext.BaseDirectory, "FieldMaps.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "FieldMaps.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "FieldMaps.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var repoCandidates = new[]
            {
                Path.Combine(current.FullName, "MercedesEISTool.Core", "Configuration", "FieldMaps.json"),
                Path.Combine(current.FullName, "Configuration", "FieldMaps.json")
            };

            foreach (var candidate in repoCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Unable to locate FieldMaps.json for Mercedes EIS parser configuration.");
    }

    public EisDump ParseDump(byte[] data)
    {
        if (data.Length != 256)
        {
            throw new InvalidOperationException("Mercedes EIS dumps must be exactly 256 bytes.");
        }

        var format = DetectFormat(data);
        var dump = new EisDump
        {
            RawData = (byte[])data.Clone(),
            Format = format,
            VIN = ReadMappedString(data, format, "VIN"),
            EisType = string.Empty,
            MCU = string.Empty,
            SSID = string.Empty,
            Keys = new List<KeyInfo>()
        };

        return dump;
    }

    public EisParserResult ParseResult(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        foreach (var parser in _parsers)
        {
            if (parser.CanHandle(data))
            {
                return parser.Parse(data);
            }
        }

        return new EisParserResult { Format = "Unknown", DetectionConfidence = "Rejected" };
    }

    public EisDump ConvertDump(EisDump dump, string targetFormat)
    {
        var converted = new EisDump
        {
            RawData = (byte[])dump.RawData.Clone(),
            Format = targetFormat,
            VIN = dump.VIN,
            EisType = dump.EisType,
            MCU = dump.MCU,
            SSID = dump.SSID,
            Keys = dump.Keys.ToList()
        };

        return converted;
    }

    public DumpValidationResult ValidateDump(byte[] data)
    {
        if (data.Length != 256)
        {
            return DumpValidationResult.Invalid("Expected a 256-byte dump.");
        }

        return DumpValidationResult.Valid();
    }

    public string DetectFormat(byte[] data)
    {
        if (data is null || data.Length != 256)
        {
            return "Unknown";
        }

        foreach (var parser in _parsers)
        {
            if (parser.CanHandle(data))
            {
                return parser.Format;
            }
        }

        return "Unknown";
    }

    private static bool HasVvdiSignature(byte[] data)
    {
        if (data.Length < VvdiSignature.Length)
        {
            return false;
        }

        for (var i = 0; i < VvdiSignature.Length; i++)
        {
            if (data[i] != (byte)VvdiSignature[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeCgdiVin(byte[] data)
    {
        if (data.Length != 256)
        {
            return false;
        }

        var vin = Encoding.ASCII.GetString(data.Take(17).ToArray()).Trim('\0', ' ', '\r', '\n', '\t');
        return (vin.Length == 16 || vin.Length == 17) && IsValidVinCharacters(vin);
    }

    private static bool IsValidVinCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (ch is 'I' or 'O' or 'Q' or 'i' or 'o' or 'q')
                {
                    return false;
                }

                continue;
            }

            return false;
        }

        return true;
    }

    private string ReadMappedString(byte[] data, string format, string fieldName)
    {
        if (!_fieldMaps.TryGetValue(format, out var map) || !map.TryGetValue(fieldName, out var field))
        {
            return string.Empty;
        }

        if (field.Offset < 0 || field.Length <= 0 || field.Offset + field.Length > data.Length)
        {
            return string.Empty;
        }

        if (fieldName == "VIN")
        {
            var candidate = SafeDecodeAscii(data.Skip(field.Offset).Take(field.Length).ToArray());
            return IsValidVinCharacters(candidate) ? candidate : string.Empty;
        }

        var bytes = data.Skip(field.Offset).Take(field.Length).ToArray();
        return SafeDecodeAscii(bytes);
    }

    public DumpCompareResult CompareDumps(byte[] left, byte[] right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Length != 256 || right.Length != 256)
        {
            throw new InvalidOperationException("Both dumps must be exactly 256 bytes.");
        }

        var rows = new List<DumpCompareRow>();
        var differingOffsets = new List<int>();
        var totalDifferences = 0;

        for (var offset = 0; offset < 256; offset += 16)
        {
            var rowBytesLeft = left.Skip(offset).Take(16).ToArray();
            var rowBytesRight = right.Skip(offset).Take(16).ToArray();
            var differences = rowBytesLeft.Zip(rowBytesRight, (a, b) => a != b).Count(isDifferent => isDifferent);
            totalDifferences += differences;
            var rowHasDifferences = differences > 0;
            if (rowHasDifferences)
            {
                for (var index = offset; index < offset + 16; index++)
                {
                    if (left[index] != right[index])
                    {
                        differingOffsets.Add(index);
                    }
                }
            }

            rows.Add(new DumpCompareRow
            {
                Offset = offset,
                RowBytesLeft = rowBytesLeft,
                RowBytesRight = rowBytesRight,
                HasDifferences = rowHasDifferences
            });
        }

        return new DumpCompareResult
        {
            Rows = rows,
            TotalDifferences = totalDifferences,
            DifferingOffsets = differingOffsets.Distinct().OrderBy(x => x).ToList()
        };
    }

    public SequenceSearchResult SearchSequence(byte[] source, byte[] target, int startOffset, int length)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (startOffset < 0 || startOffset >= source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        if (length < 1 || length > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (startOffset + length > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        var sequence = source.Skip(startOffset).Take(length).ToArray();
        var exactMatches = new List<int>();
        var reversedMatches = new List<int>();

        for (var index = 0; index <= target.Length - length; index++)
        {
            var candidate = target.Skip(index).Take(length).ToArray();
            if (candidate.SequenceEqual(sequence))
            {
                exactMatches.Add(index);
            }

            var reversed = candidate.Reverse().ToArray();
            if (reversed.SequenceEqual(sequence))
            {
                reversedMatches.Add(index);
            }
        }

        return new SequenceSearchResult
        {
            ExactMatches = exactMatches,
            ReversedMatches = reversedMatches
        };
    }

    private static string SafeDecodeAscii(byte[] bytes)
    {
        try
        {
            var encoding = Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            var text = encoding.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
            return text.Length > 0 ? text : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public class FieldDefinition
    {
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}

public class DumpValidationResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;

    public static DumpValidationResult Valid() => new() { IsValid = true };
    public static DumpValidationResult Invalid(string message) => new() { IsValid = false, Message = message };
}

public class DumpCompareRow
{
    public int Offset { get; set; }
    public byte[] RowBytesLeft { get; set; } = Array.Empty<byte>();
    public byte[] RowBytesRight { get; set; } = Array.Empty<byte>();
    public bool HasDifferences { get; set; }
}

public class DumpCompareResult
{
    public List<DumpCompareRow> Rows { get; set; } = new();
    public int TotalDifferences { get; set; }
    public List<int> DifferingOffsets { get; set; } = new();
}

public class SequenceSearchResult
{
    public List<int> ExactMatches { get; set; } = new();
    public List<int> ReversedMatches { get; set; } = new();
}
