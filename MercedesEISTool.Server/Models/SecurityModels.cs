namespace MercedesEISTool.Server.Models;

public enum FeatureName
{
    AnalyzeDump,
    CompareDumps,
    ConvertDump
}

public interface ICurrentUser
{
    string UserId { get; }
    string DisplayName { get; }
    string? OrganizationId { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}

public interface ILicenseService
{
    LicenseStatus CheckFeature(FeatureName featureName, ICurrentUser? user = null);
}

public class LicenseStatus
{
    public bool IsGranted { get; set; }
    public string Message { get; set; } = string.Empty;
    public FeatureName FeatureName { get; set; }
}

public class DevelopmentLicenseService : ILicenseService
{
    public LicenseStatus CheckFeature(FeatureName featureName, ICurrentUser? user = null)
    {
        return featureName switch
        {
            FeatureName.AnalyzeDump or FeatureName.CompareDumps => new LicenseStatus { IsGranted = true, FeatureName = featureName, Message = "Development mode" },
            _ => new LicenseStatus { IsGranted = false, FeatureName = featureName, Message = "Conversion is not available in this build." }
        };
    }
}
