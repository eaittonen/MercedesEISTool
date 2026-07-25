namespace MercedesEISTool.Server.Models;

public sealed class ProductionLicenseService : ILicenseService
{
    public LicenseStatus CheckFeature(FeatureName featureName, ICurrentUser? user = null)
    {
        return featureName switch
        {
            FeatureName.AnalyzeDump or FeatureName.CompareDumps => new LicenseStatus { IsGranted = true, FeatureName = featureName, Message = "Production mode" },
            _ => new LicenseStatus { IsGranted = false, FeatureName = featureName, Message = "Conversion is not available in this build." }
        };
    }
}
