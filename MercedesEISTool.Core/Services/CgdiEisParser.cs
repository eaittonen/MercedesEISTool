using System.Text;

namespace MercedesEISTool.Core.Services;

public sealed class CgdiEisParser : IEisParser
{
    public string Format => "CGDI MB";

    public bool CanHandle(byte[] data)
    {
        if (data is null || data.Length != 256)
        {
            return false;
        }

        var vin = ExtractAscii(data, 0, 17);
        return !string.IsNullOrWhiteSpace(vin) && IsValidVin(vin);
    }

    public EisParserResult Parse(byte[] data)
    {
        if (data is null || data.Length != 256)
        {
            return new EisParserResult { Format = "Unknown", DetectionConfidence = "Rejected" };
        }

        var vin = ExtractAscii(data, 0, 17);
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(vin) && !IsValidVin(vin))
        {
            warnings.Add("VIN does not match the expected format.");
        }

        var ssid = ExtractReversedHex(data, 0x28, 4);
        var password = ExtractReversedHex(data, 0x38, 8);

        return new EisParserResult
        {
            Format = Format,
            Vin = string.IsNullOrWhiteSpace(vin) ? null : vin,
            Ssid = string.IsNullOrWhiteSpace(ssid) ? null : ssid,
            EisPartNumber = null,
            EisPassword = string.IsNullOrWhiteSpace(password) ? null : password,
            DetectionConfidence = "Verified",
            Warnings = warnings
        };
    }

    private static string ExtractAscii(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString(data.Skip(offset).Take(length).ToArray()).Trim('\0', ' ', '\r', '\n', '\t');
    }

    private static string ExtractReversedHex(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            return string.Empty;
        }

        var bytes = data.Skip(offset).Take(length).Reverse().ToArray();
        if (bytes.All(value => value == 0x00) || bytes.All(value => value == 0xFF))
        {
            return string.Empty;
        }

        return string.Join(" ", bytes.Select(value => value.ToString("X2")));
    }

    private static bool IsValidVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin) || (vin.Length != 16 && vin.Length != 17))
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
