using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.Tests;

public sealed class BulkConsumeMetadataTests
{
    [Fact]
    public void Metadata_properties_expose_confidence_values()
    {
        var metadata = new BulkConsumeMetadata
        {
            DetectedVin = "WVWZZZ1JZ3C000001",
            RegistrationNumber = "ABC123",
            CustomerName = "Jane Doe",
            CustomerIdentifier = "CUST-001",
            FolderIdentifier = "FolderA",
            VinConfidence = MetadataConfidence.High,
            RegistrationConfidence = MetadataConfidence.Medium,
            CustomerConfidence = MetadataConfidence.Low,
            FolderIdentifierConfidence = MetadataConfidence.Unknown,
            MetadataConfidence = MetadataConfidence.High
        };

        Assert.Equal("WVWZZZ1JZ3C000001", metadata.DetectedVin);
        Assert.Equal("ABC123", metadata.RegistrationNumber);
        Assert.Equal("Jane Doe", metadata.CustomerName);
        Assert.Equal("CUST-001", metadata.CustomerIdentifier);
        Assert.Equal("FolderA", metadata.FolderIdentifier);
        Assert.Equal(MetadataConfidence.High, metadata.VinConfidence);
        Assert.Equal(MetadataConfidence.Medium, metadata.RegistrationConfidence);
        Assert.Equal(MetadataConfidence.Low, metadata.CustomerConfidence);
        Assert.Equal(MetadataConfidence.Unknown, metadata.FolderIdentifierConfidence);
        Assert.Equal(MetadataConfidence.High, metadata.MetadataConfidence);
    }
}
