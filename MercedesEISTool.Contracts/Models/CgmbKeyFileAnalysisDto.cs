namespace MercedesEISTool.Contracts.Models;

public sealed class CgmbKeyFileAnalysisDto
{
    public string DetectedFormat { get; set; } = "Unknown";
    public string DetectionConfidence { get; set; } = "Unknown";
    public int? KeyIndex { get; set; }
    public int? SlotNumber { get; set; }
    public string? EisPassword { get; set; }
    public string EisPasswordStatus { get; set; } = "NotMapped";
    public string? Ssid { get; set; }
    public string SsidStatus { get; set; } = "NotMapped";
    public string? PartialSsid { get; set; }
    public string? KeySlotRawValue { get; set; }
    public string? KeySlotDisplayValue { get; set; }
    public string KeySlotValueStatus { get; set; } = "NotMapped";
    public string AssociationStatus { get; set; } = "NotChecked";
    public Guid? AssociatedEisFileId { get; set; }
    public string? AssociatedEisVin { get; set; }
    public string KeyPinStatus { get; set; } = "NotMapped";
    public string? KeyPin { get; set; }
    public string KeyUsageState { get; set; } = "NotMapped";
    public string KeyDisabledState { get; set; } = "NotMapped";
    public string ParserVersion { get; set; } = string.Empty;
    public DateTimeOffset AnalyzedAtUtc { get; set; }
}
