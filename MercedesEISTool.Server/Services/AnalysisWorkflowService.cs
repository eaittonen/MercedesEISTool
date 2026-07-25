using System.Text;
using MercedesEISTool.Core.Services;

namespace MercedesEISTool.Server.Services;

public enum AnalysisVinStatus
{
    Present,
    NotPresent,
    Invalid,
    UnsupportedFormat,
    NotMapped
}

public class AnalysisWorkflowResult
{
    public bool AnalysisSucceeded { get; set; }
    public string DetectedFormat { get; set; } = string.Empty;
    public string DetectedVin { get; set; } = string.Empty;
    public AnalysisVinStatus VinStatus { get; set; }
    public string VinSource { get; set; } = string.Empty;
    public string EisType { get; set; } = string.Empty;
    public string McuType { get; set; } = string.Empty;
    public string KeyCount { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}

public class AnalysisWorkflowService
{
    public AnalysisWorkflowResult Analyze(byte[] data, string fileName)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
        var result = new AnalysisWorkflowResult
        {
            AnalysisSucceeded = true,
            Sha256 = sha,
            FileSizeBytes = data.Length,
            Message = "Analyzed successfully"
        };

        if (data.Length != 256)
        {
            result.AnalysisSucceeded = false;
            result.Message = "Mercedes EIS dumps must be exactly 256 bytes.";
            return result;
        }

        var service = new EisDumpService();
        var format = service.DetectFormat(data);
        result.DetectedFormat = format;

        if (string.Equals(format, "CGDI MB", StringComparison.OrdinalIgnoreCase))
        {
            result.DetectedVin = ReadVinAtOffset(data, 0);
            result.VinSource = "DumpOffset0";
        }
        else if (string.Equals(format, "VVDI MB Tool", StringComparison.OrdinalIgnoreCase))
        {
            if (HasVvdiSignature(data))
            {
                result.DetectedVin = ReadVinAtOffset(data, 0x90);
                result.VinSource = "DumpOffset0x90";
            }
            else
            {
                result.VinStatus = AnalysisVinStatus.UnsupportedFormat;
                result.VinSource = "None";
                result.Message = "Unsupported format.";
                return result;
            }
        }
        else
        {
            result.VinStatus = AnalysisVinStatus.UnsupportedFormat;
            result.VinSource = "None";
            result.Message = "Unsupported format.";
            return result;
        }

        result.VinStatus = DetermineVinStatus(result.DetectedVin);
        return result;
    }

    public UploadValidationResult ValidateUpload(byte[] data, string? userProvidedVin, string? userProvidedRegistrationNumber, bool vehicleIdentifierConfirmed)
    {
        if (!vehicleIdentifierConfirmed)
        {
            return UploadValidationResult.Invalid("Confirmation is required before upload.");
        }

        if (string.IsNullOrWhiteSpace(userProvidedVin) && string.IsNullOrWhiteSpace(userProvidedRegistrationNumber))
        {
            return UploadValidationResult.Invalid("Provide either a VIN or registration number and confirm it before uploading.");
        }

        if (!string.IsNullOrWhiteSpace(userProvidedVin) && !IsValidVin(userProvidedVin))
        {
            return UploadValidationResult.Invalid("The supplied VIN is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(userProvidedRegistrationNumber) && !IsValidRegistrationNumber(userProvidedRegistrationNumber))
        {
            return UploadValidationResult.Invalid("The supplied registration number is invalid.");
        }

        var analysis = Analyze(data, string.Empty);
        if (!analysis.AnalysisSucceeded)
        {
            return UploadValidationResult.Invalid(analysis.Message);
        }

        if (!string.IsNullOrWhiteSpace(analysis.DetectedVin) && !string.IsNullOrWhiteSpace(userProvidedVin) && !string.Equals(analysis.DetectedVin, userProvidedVin, StringComparison.OrdinalIgnoreCase))
        {
            return UploadValidationResult.Invalid("The confirmed VIN does not match the VIN detected from the dump.");
        }

        return UploadValidationResult.Valid();
    }

    private static AnalysisVinStatus DetermineVinStatus(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return AnalysisVinStatus.NotPresent;
        }

        return IsValidVin(vin) ? AnalysisVinStatus.Present : AnalysisVinStatus.Invalid;
    }

    private static string ReadVinAtOffset(byte[] data, int offset)
    {
        if (offset < 0 || offset + 17 > data.Length)
        {
            return string.Empty;
        }

        var bytes = data.Skip(offset).Take(17).ToArray();
        var value = Encoding.ASCII.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
        return IsValidVin(value) ? value : string.Empty;
    }

    private static bool HasVvdiSignature(byte[] data)
    {
        var signature = "VVDIMBDATA";
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

    private static bool IsValidRegistrationNumber(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 2;
    }
}

public class UploadValidationResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;

    public static UploadValidationResult Valid() => new() { IsValid = true };
    public static UploadValidationResult Invalid(string message) => new() { IsValid = false, Message = message };
}
