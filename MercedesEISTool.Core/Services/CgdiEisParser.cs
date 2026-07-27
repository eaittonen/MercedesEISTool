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

        return new EisParserResult
        {
            Format = Format,
            Vin = string.IsNullOrWhiteSpace(vin) ? null : vin,
            Ssid = null,
            EisPartNumber = null,
            EisPassword = null,
            DetectionConfidence = "Supported",
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

    private static bool IsValidVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
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
