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
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _fieldMaps = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, FieldDefinition>>>(json, options)
            ?? new Dictionary<string, Dictionary<string, FieldDefinition>>();
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
            EisType = "TODO",
            MCU = ReadMappedString(data, format, "MCU"),
            SSID = "TODO",
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
        if (data.Length < 256)
        {
            return "Unknown";
        }

        var signature = Encoding.ASCII.GetString(data.Take(2).ToArray());
        if (signature == "VV")
        {
            return "VVDI MB Tool";
        }

        return "CGDI MB";
    }

    private string ReadMappedString(byte[] data, string format, string fieldName)
    {
        if (!_fieldMaps.TryGetValue(format, out var map) || !map.TryGetValue(fieldName, out var field))
        {
            return string.Empty;
        }

        var start = field.Offset;
        var length = field.Length;
        if (start + length > data.Length)
        {
            return string.Empty;
        }

        var bytes = data.Skip(start).Take(length).ToArray();
        var text = Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        return text;
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
