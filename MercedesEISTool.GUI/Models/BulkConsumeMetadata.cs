namespace MercedesEISTool.GUI.Models;

public sealed class BulkConsumeMetadata
{
    public string? DetectedVin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerIdentifier { get; set; }
    public string? FolderIdentifier { get; set; }
    public string? VinConfidence { get; set; }
    public string? RegistrationConfidence { get; set; }
    public string? CustomerConfidence { get; set; }
    public string? FolderIdentifierConfidence { get; set; }
    public string? MetadataConfidence { get; set; }
    public string? Password { get; set; }
    public string? Score { get; set; }
    public string? Reason { get; set; }
    public string? SourcePath { get; set; }
    public string? FileName { get; set; }
    public bool HasPassword { get; set; }
}
