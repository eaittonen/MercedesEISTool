using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public sealed class StoredFileLockGroupingTests
{
    [Fact]
    public void BuildGroups_GroupsVersionsByStableIdentityAndChoosesPreferredVersion()
    {
        var first = CreateRecord(
            id: Guid.NewGuid(),
            vehicleIdentifier: "VIN123456789012345",
            registrationNumber: "ABC123",
            ssid: "SSID-1",
            password: "secret",
            hasPassword: true);

        var second = CreateRecord(
            id: Guid.NewGuid(),
            vehicleIdentifier: "VIN123456789012345",
            registrationNumber: "ABC123",
            ssid: "SSID-1",
            password: null,
            hasPassword: false);

        var groups = StoredFileLockGroupResolver.BuildGroups(new[] { second, first });

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Versions.Count);
        Assert.Equal(first.Id, group.PreferredVersionId);
        Assert.True(group.Versions.Single(item => item.Id == first.Id).IsPreferredVersion);
        Assert.True(group.HasEisPassword);
        Assert.True(group.MetadataCompletenessScore >= 2);
    }

    private static UploadedDumpRecord CreateRecord(Guid id, string vehicleIdentifier, string registrationNumber, string? ssid, string? password, bool hasPassword)
    {
        var analysis = new StoredFileAnalysisSnapshot
        {
            StoredFileId = id,
            DetectedVin = vehicleIdentifier,
            VinStatus = "Present",
            EisPassword = new SensitiveFieldDto { Name = "EIS password", Value = password, Status = hasPassword ? FieldValueStatus.Present : FieldValueStatus.NotPresent },
            Ssid = new SensitiveFieldDto { Name = "SSID", Value = ssid, Status = string.IsNullOrWhiteSpace(ssid) ? FieldValueStatus.NotPresent : FieldValueStatus.Present },
            McuType = "MEDC17",
            EisType = "CGMB",
            KeyCount = 4
        };

        return new UploadedDumpRecord
        {
            Id = id,
            FileName = $"dump-{id:N}.bin",
            VehicleIdentifier = vehicleIdentifier,
            RegistrationNumber = registrationNumber,
            LatestAnalysis = analysis,
            AnalysisHistory = new List<StoredFileAnalysisSnapshot> { analysis },
            LockGroupKey = StoredFileLockGroupResolver.BuildLockGroupKey(vehicleIdentifier, registrationNumber, analysis.DetectedVin, analysis.Ssid?.Value),
            MetadataCompletenessScore = StoredFileLockGroupResolver.ComputeMetadataCompletenessScore(analysis, vehicleIdentifier, registrationNumber),
            HasEisPassword = hasPassword,
            IsPreferredVersion = false
        };
    }
}
