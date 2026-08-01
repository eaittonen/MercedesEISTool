namespace MercedesEISTool.Core.Services;

public sealed class EisParserResult
{
    public string Format { get; set; } = "Unknown";
    public string? Vin { get; set; }
    public string? Ssid { get; set; }
    public string? EisPartNumber { get; set; }
    public string? EisPassword { get; set; }
    public string? Keys { get; set; }
    public bool? Initialized { get; set; }
    public bool? TpCleared { get; set; }
    public bool? Personalized { get; set; }
    public bool? Activated { get; set; }
    public bool? DealerEis { get; set; }
    public bool? Fbs4 { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string DetectionConfidence { get; set; } = "Unknown";
}
