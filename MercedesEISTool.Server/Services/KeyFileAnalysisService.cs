using System.Text;
using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.Server.Services;

public interface IKeyFileAnalysisService
{
    CgmbKeyFileAnalysisDto Analyze(byte[] data, string fileName);
    CgmbKeyFileAnalysisDto AnalyzeAndAssociate(byte[] keyFileData, byte[] eisDumpData, string fileName);
}

public sealed class KeyFileAnalysisService : IKeyFileAnalysisService
{
    private const string ParserVersionValue = "1.2.0-cgmb";

    public CgmbKeyFileAnalysisDto Analyze(byte[] data, string fileName)
    {
        ArgumentNullException.ThrowIfNull(data);

        var result = new CgmbKeyFileAnalysisDto
        {
            ParserVersion = ParserVersionValue,
            AnalyzedAtUtc = DateTimeOffset.UtcNow
        };

        if (data.Length != 160)
        {
            result.DetectedFormat = "Unknown";
            result.DetectionConfidence = "Invalid";
            result.EisPasswordStatus = "Invalid";
            result.SsidStatus = "Invalid";
            result.KeySlotValueStatus = "Invalid";
            result.KeyPinStatus = "NotMapped";
            result.KeyUsageState = "NotMapped";
            result.KeyDisabledState = "NotMapped";
            return result;
        }

        if (data[0x00] != 0x01)
        {
            result.DetectedFormat = "Unknown";
            result.DetectionConfidence = "NotRecognized";
            result.EisPasswordStatus = "NotMapped";
            result.SsidStatus = "NotMapped";
            result.KeySlotValueStatus = "NotMapped";
            result.KeyPinStatus = "NotMapped";
            result.KeyUsageState = "NotMapped";
            result.KeyDisabledState = "NotMapped";
            return result;
        }

        var keyIndex = data[0x09];
        if (keyIndex > 3)
        {
            result.DetectedFormat = "Unknown";
            result.DetectionConfidence = "Invalid";
            result.EisPasswordStatus = "Invalid";
            result.SsidStatus = "Invalid";
            result.KeySlotValueStatus = "Invalid";
            result.KeyPinStatus = "NotMapped";
            result.KeyUsageState = "NotMapped";
            result.KeyDisabledState = "NotMapped";
            return result;
        }

        var partialSsid = ExtractHex(data, 0x0A, 3);
        var fullSsid = ExtractHex(data, 0x8C, 4);
        var partialMatchesFull = IsConsistentPartialSsid(partialSsid, fullSsid);
        if (!partialMatchesFull)
        {
            result.DetectedFormat = "Unknown";
            result.DetectionConfidence = "Invalid";
            result.EisPasswordStatus = "Invalid";
            result.SsidStatus = "Invalid";
            result.KeySlotValueStatus = "Invalid";
            result.KeyPinStatus = "NotMapped";
            result.KeyUsageState = "NotMapped";
            result.KeyDisabledState = "NotMapped";
            return result;
        }

        result.DetectedFormat = "CGMB key file";
        result.DetectionConfidence = "Verified";
        result.KeyIndex = keyIndex;
        result.SlotNumber = keyIndex + 1;
        result.EisPassword = ExtractHex(data, 0x01, 8);
        result.EisPasswordStatus = "Present";
        result.Ssid = fullSsid;
        result.SsidStatus = "Present";
        result.PartialSsid = partialSsid;
        result.KeySlotRawValue = ExtractHex(data, 0x73, 8);
        result.KeySlotDisplayValue = ExtractReversedHex(data, 0x73, 8);
        result.KeySlotValueStatus = "Present";
        result.KeyPinStatus = "NotMapped";
        result.KeyPin = null;
        result.KeyUsageState = "NotMapped";
        result.KeyDisabledState = "NotMapped";
        result.AssociationStatus = "NotChecked";
        result.ParserVersion = ParserVersionValue;
        result.AnalyzedAtUtc = DateTimeOffset.UtcNow;
        return result;
    }

    public CgmbKeyFileAnalysisDto AnalyzeAndAssociate(byte[] keyFileData, byte[] eisDumpData, string fileName)
    {
        var result = Analyze(keyFileData, fileName);
        if (!string.Equals(result.DetectionConfidence, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            result.AssociationStatus = result.DetectionConfidence.Equals("Invalid", StringComparison.OrdinalIgnoreCase) ? "InvalidKeyFile" : "InvalidKeyFile";
            return result;
        }

        if (eisDumpData is null || eisDumpData.Length != 256)
        {
            result.AssociationStatus = "InvalidEisDump";
            return result;
        }

        var slotOffset = 0x80 + (result.KeyIndex ?? 0) * 8;
        var actualSlotBytes = ExtractByteSlice(eisDumpData, slotOffset, 4);
        var expectedSlotBytes = ExtractByteSlice(keyFileData, 0x73, 4);
        var actualSsidBytes = ExtractByteSlice(eisDumpData, 0x80 + 0x0C, 4);
        var expectedSsidBytes = ExtractByteSlice(keyFileData, 0x0A, 4);

        var slotMatches = expectedSlotBytes.SequenceEqual(actualSlotBytes);
        var ssidMatches = expectedSsidBytes.SequenceEqual(actualSsidBytes);

        if (ssidMatches)
        {
            result.AssociationStatus = slotMatches ? "ExactSlotMatch" : "SsidMatchOnly";
        }
        else
        {
            result.AssociationStatus = slotMatches ? "SsidMatchOnly" : "SlotValueMismatch";
        }

        result.AssociatedEisFileId = null;
        return result;
    }

    private static bool IsConsistentPartialSsid(string partialSsid, string fullSsid)
    {
        if (string.IsNullOrWhiteSpace(partialSsid) || string.IsNullOrWhiteSpace(fullSsid))
        {
            return false;
        }

        var partialParts = partialSsid.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fullParts = fullSsid.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partialParts.Length != 3 || fullParts.Length != 4)
        {
            return false;
        }

        return partialParts[0] == fullParts[0] && partialParts[1] == fullParts[1] && partialParts[2] == fullParts[2];
    }

    private static string ExtractHex(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var bytes = data.Skip(offset).Take(length).ToArray();
        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }

    private static string ExtractReversedHex(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var bytes = data.Skip(offset).Take(length).Reverse().ToArray();
        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }

    private static byte[] ExtractByteSlice(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            return Array.Empty<byte>();
        }

        return data.Skip(offset).Take(length).ToArray();
    }
}
