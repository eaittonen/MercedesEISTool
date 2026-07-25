using System.Text;
using System.Text.Json;
using MercedesEISTool.Core.Models;

namespace MercedesEISTool.Core.Services;

public class EisDumpService
{
    private readonly Dictionary<string, Dictionary<string, FieldDefinition>> _fieldMaps;

    public EisDumpService()
    {
        var configPath = ResolveConfigPath();
        var json = File.ReadAllText(configPath);
        _fieldMaps = LoadFieldMaps(json);
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
            MCU = ReadMappedString(data, format, "MCU"),
            SSID = string.Empty,
            Keys = new List<KeyInfo>()
        };

        return dump;
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
        if (data.Length != 256)
        {
            return "Unknown";
        }

        return data.Length >= 2 && data[0] == (byte)'V' && data[1] == (byte)'V'
            ? "VVDI MB Tool"
            : "Unknown";
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

        var bytes = data.Skip(field.Offset).Take(field.Length).ToArray();
        return SafeDecodeAscii(bytes);
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
