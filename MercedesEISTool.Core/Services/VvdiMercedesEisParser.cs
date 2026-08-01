using System.Text;

namespace MercedesEISTool.Core.Services;

public sealed class VvdiMercedesEisParser : IEisParser
{
    public string Format => "VVDI MB Tool";

    public bool CanHandle(byte[] data)
    {
        if (data is null || data.Length != 256)
        {
            return false;
        }

        return HasVvdiSignature(data);
    }

    public EisParserResult Parse(byte[] data)
    {
        if (!CanHandle(data))
        {
            return new EisParserResult { Format = "Unknown", DetectionConfidence = "Rejected" };
        }

        var warnings = new List<string>();
        var vin = ExtractAscii(data, 0x90, 17);
        var ssid = ExtractHex(data, 0x10, 4);
        var partNumber = ExtractAscii(data, 0xE0, 10);
        var password = ExtractPassword(data, 0x70, 8);
        var stateFlags = data[0x26];

        if (string.IsNullOrWhiteSpace(vin))
        {
            warnings.Add("VIN was not found in the expected VVDI offset.");
        }
        else if (!IsValidVin(vin))
        {
            warnings.Add("VIN does not match the expected format.");
        }

        if (string.IsNullOrWhiteSpace(partNumber))
        {
            warnings.Add("EIS part number was not found in the expected VVDI offset.");
        }
        else if (!IsValidPartNumber(partNumber))
        {
            warnings.Add("EIS part number does not match the expected format.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            warnings.Add("Password not stored in dump.");
        }

        return new EisParserResult
        {
            Format = Format,
            Vin = string.IsNullOrWhiteSpace(vin) ? null : vin,
            Ssid = string.IsNullOrWhiteSpace(ssid) ? null : ssid,
            EisPartNumber = string.IsNullOrWhiteSpace(partNumber) ? null : partNumber,
            EisPassword = string.IsNullOrWhiteSpace(password) ? null : password,
            Initialized = ReadBit(stateFlags, 0),
            Personalized = ReadBit(stateFlags, 1),
            TpCleared = ReadBit(stateFlags, 2),
            Activated = ReadBit(stateFlags, 3),
            DealerEis = ReadBit(stateFlags, 4),
            Fbs4 = ReadBit(stateFlags, 5),
            DetectionConfidence = "Supported",
            Warnings = warnings
        };
    }

    private static bool HasVvdiSignature(byte[] data)
    {
        const string signature = "VVDIMBDATA";
        if (data.Length < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (data[i] != (byte)signature[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string ExtractAscii(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString(data.Skip(offset).Take(length).ToArray()).Trim('\0', ' ', '\r', '\n', '\t', (char)0xFF);
    }

    private static string ExtractHex(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            return string.Empty;
        }

        var bytes = data.Skip(offset).Take(length).ToArray();
        if (bytes.All(value => value == 0x00))
        {
            return string.Empty;
        }

        var hex = new StringBuilder();
        foreach (var value in bytes)
        {
            hex.Append(value.ToString("X2"));
        }

        return hex.ToString();
    }

    private static string? ExtractPassword(byte[] data, int offset, int length)
    {
        var bytes = data.Skip(offset).Take(length).ToArray();
        if (bytes.All(value => value == 0x00))
        {
            return null;
        }

        return ExtractHex(data, offset, length);
    }

    private static bool? ReadBit(byte flags, int bitIndex)
    {
        if (bitIndex < 0 || bitIndex > 7)
        {
            return null;
        }

        return ((flags >> bitIndex) & 0x01) == 0x01;
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

    private static bool IsValidPartNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = value.Where(char.IsDigit).ToArray();
        return digits.Length == value.Length && value.Length > 0;
    }
}
