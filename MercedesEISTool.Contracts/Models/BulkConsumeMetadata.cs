namespace MercedesEISTool.Contracts.Models;

public enum MetadataConfidence
{
    Unknown,
    Low,
    Medium,
    High
}

public sealed class BulkConsumeMetadata
{
    public string? DetectedVin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerIdentifier { get; set; }
    public string? FolderIdentifier { get; set; }
    public MetadataConfidence VinConfidence { get; set; } = MetadataConfidence.Unknown;
    public MetadataConfidence RegistrationConfidence { get; set; } = MetadataConfidence.Unknown;
    public MetadataConfidence CustomerConfidence { get; set; } = MetadataConfidence.Unknown;
    public MetadataConfidence FolderIdentifierConfidence { get; set; } = MetadataConfidence.Unknown;
    public MetadataConfidence MetadataConfidence { get; set; } = MetadataConfidence.Unknown;
    public string? Password { get; set; }
    public string? Score { get; set; }
    public string? Reason { get; set; }
    public string? SourcePath { get; set; }
    public string? FileName { get; set; }
    public bool HasPassword { get; set; }
}
