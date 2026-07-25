using System.Text;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Server.Services;

public interface IEisAnalysisService
{
    EisAnalysisDetailsDto Analyze(byte[] data, string fileName);
}

public sealed class EisAnalysisService : IEisAnalysisService
{
    private const string ParserVersionValue = "1.1.0";
    private readonly EisDumpService _dumpService = new();

    public EisAnalysisDetailsDto Analyze(byte[] data, string fileName)
    {
        ArgumentNullException.ThrowIfNull(data);

        var details = new EisAnalysisDetailsDto
        {
            ParserVersion = ParserVersionValue,
            AnalyzedAtUtc = DateTimeOffset.UtcNow
        };

        if (data.Length != 256)
        {
            details.DetectedFormat = "Unknown";
            details.VinStatus = "Invalid";
            details.EisTypeStatus = FieldValueStatus.UnsupportedFormat;
            details.McuTypeStatus = FieldValueStatus.UnsupportedFormat;
            details.KeyCountStatus = FieldValueStatus.UnsupportedFormat;
            details.EisPassword.Status = FieldValueStatus.UnsupportedFormat;
            details.Ssid.Status = FieldValueStatus.UnsupportedFormat;
            details.AdditionalFields.Add(new SensitiveFieldDto
            {
                Name = "FileSize",
                Value = data.Length.ToString(),
                Status = FieldValueStatus.UnsupportedFormat,
                Confidence = "Verified"
            });
            details.AdditionalFields.Add(new SensitiveFieldDto
            {
                Name = "FileName",
                Value = fileName,
                Status = FieldValueStatus.Present,
                Confidence = "Verified"
            });
            return details;
        }

        details.DetectedFormat = _dumpService.DetectFormat(data);
        details.EisTypeStatus = FieldValueStatus.NotMapped;
        details.McuTypeStatus = FieldValueStatus.NotMapped;
        details.KeyCountStatus = FieldValueStatus.NotMapped;
        details.EisPassword = CreateNotMappedField("EIS password");
        details.Ssid = CreateNotMappedField("SSID");

        if (string.Equals(details.DetectedFormat, "CGDI MB", StringComparison.OrdinalIgnoreCase))
        {
            var vin = ReadAscii(data, 0, 17);
            details.DetectedVin = vin;
            details.VinStatus = DetermineVinStatus(vin);
        }
        else if (string.Equals(details.DetectedFormat, "VVDI MB Tool", StringComparison.OrdinalIgnoreCase))
        {
            if (HasVvdiSignature(data))
            {
                var vin = ReadAscii(data, 0x90, 17);
                details.DetectedVin = vin;
                details.VinStatus = DetermineVinStatus(vin);
            }
            else
            {
                details.VinStatus = "UnsupportedFormat";
            }
        }
        else
        {
            details.VinStatus = "NotMapped";
        }

        details.AdditionalFields.Add(new SensitiveFieldDto
        {
            Name = "FileName",
            Value = fileName,
            Status = FieldValueStatus.Present,
            Confidence = "Verified"
        });

        return details;
    }

    private static SensitiveFieldDto CreateNotMappedField(string name)
    {
        return new SensitiveFieldDto
        {
            Name = name,
            Status = FieldValueStatus.NotMapped,
            Confidence = "Unknown"
        };
    }

    private static string DetermineVinStatus(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return "NotPresent";
        }

        return IsValidVin(vin) ? "Present" : "Invalid";
    }

    private static string ReadAscii(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            return string.Empty;
        }

        var bytes = data.Skip(offset).Take(length).ToArray();
        return Encoding.ASCII.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
    }

    private static bool HasVvdiSignature(byte[] data)
    {
        const string signature = "VVDIMBDATA";
        if (data.Length < signature.Length)
        {
            return false;
        }

        for (var index = 0; index < signature.Length; index++)
        {
            if (data[index] != signature[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return false;
        }

        if (vin.Length != 17)
        {
            return false;
        }

        foreach (var ch in vin)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                return false;
            }

            if (ch is 'I' or 'O' or 'Q' or 'i' or 'o' or 'q')
            {
                return false;
            }
        }

        return true;
    }
}
