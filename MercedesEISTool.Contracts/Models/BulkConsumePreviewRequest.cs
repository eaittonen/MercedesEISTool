namespace MercedesEISTool.Contracts.Models;

public sealed class BulkConsumePreviewRequest
{
    public string SourceFolderPath { get; set; } = string.Empty;
    public bool IncludeSubdirectories { get; set; }
}
