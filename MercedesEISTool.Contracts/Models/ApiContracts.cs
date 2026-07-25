namespace MercedesEISTool.Contracts.Models;

public class AnalyzeDumpRequest
{
    public string FileName { get; set; } = string.Empty;
}

public class AnalyzeDumpResponse
{
    public string FileName { get; set; } = string.Empty;
    public string DetectedFormat { get; set; } = "Unknown";
    public string Vin { get; set; } = string.Empty;
    public Dictionary<string, bool> FieldAvailability { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Sha256 { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = "Analyzed";
}

public class CompareDumpsRequest
{
    public string LeftFileName { get; set; } = string.Empty;
    public string RightFileName { get; set; } = string.Empty;
}

public class CompareDumpsResponse
{
    public int TotalDifferences { get; set; }
    public List<int> DifferingOffsets { get; set; } = new();
}

public class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? RequestId { get; set; }
}

public class HealthResponse
{
    public bool IsHealthy { get; set; } = true;
    public string Status { get; set; } = "Healthy";
    public string ServerVersion { get; set; } = "1.0";
    public string ServiceName { get; set; } = "MercedesEISTool.Server";
}
