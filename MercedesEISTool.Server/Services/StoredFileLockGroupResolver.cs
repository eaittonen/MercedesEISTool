using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Services;

public sealed class StoredFileLockGroup
{
    public string LockGroupKey { get; init; } = string.Empty;
    public List<UploadedDumpRecord> Versions { get; init; } = new();
    public Guid? PreferredVersionId { get; set; }
    public int MetadataCompletenessScore { get; set; }
    public bool HasEisPassword { get; set; }
    public bool HasMultipleVersions => Versions.Count > 1;
}

public static class StoredFileLockGroupResolver
{
    public static List<StoredFileLockGroup> BuildGroups(IEnumerable<UploadedDumpRecord> records)
    {
        var versions = records.ToList();
        var groups = versions
            .GroupBy(record => BuildLockGroupKey(
                record.VehicleIdentifier,
                record.RegistrationNumber,
                record.LatestAnalysis?.DetectedVin,
                record.LatestAnalysis?.Ssid?.Value),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new StoredFileLockGroup
            {
                LockGroupKey = group.Key,
                Versions = group.ToList(),
                PreferredVersionId = null,
                MetadataCompletenessScore = 0,
                HasEisPassword = false
            })
            .ToList();

        foreach (var group in groups)
        {
            foreach (var record in group.Versions)
            {
                record.LockGroupKey = group.LockGroupKey;
                record.MetadataCompletenessScore = ComputeMetadataCompletenessScore(record);
                record.HasEisPassword = HasEisPassword(record);
            }

            var preferredRecord = group.Versions
                .OrderByDescending(item => item.MetadataCompletenessScore)
                .ThenByDescending(item => item.HasEisPassword)
                .ThenByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            if (preferredRecord is not null)
            {
                group.PreferredVersionId = preferredRecord.Id;
                group.MetadataCompletenessScore = preferredRecord.MetadataCompletenessScore;
                group.HasEisPassword = preferredRecord.HasEisPassword;
                foreach (var record in group.Versions)
                {
                    record.IsPreferredVersion = record.Id == preferredRecord.Id;
                }
            }
        }

        return groups;
    }

    public static void ApplyLockMetadata(IEnumerable<UploadedDumpRecord> records)
    {
        foreach (var group in BuildGroups(records))
        {
            // Intentionally left blank; metadata is written to records inside BuildGroups.
        }
    }

    public static string BuildLockGroupKey(string? vehicleIdentifier, string? registrationNumber, string? detectedVin, string? ssid)
    {
        var normalizedVin = NormalizeIdentifier(detectedVin ?? vehicleIdentifier);
        var normalizedRegistration = NormalizeIdentifier(registrationNumber);
        var normalizedSsid = NormalizeIdentifier(ssid);

        if (!string.IsNullOrWhiteSpace(normalizedSsid) && !string.IsNullOrWhiteSpace(normalizedVin))
        {
            return $"ssid:{normalizedSsid}|vin:{normalizedVin}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedVin) && !string.IsNullOrWhiteSpace(normalizedRegistration))
        {
            return $"vin:{normalizedVin}|reg:{normalizedRegistration}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedVin))
        {
            return $"vin:{normalizedVin}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedRegistration))
        {
            return $"reg:{normalizedRegistration}";
        }

        return $"vehicle:{NormalizeIdentifier(vehicleIdentifier)}";
    }

    public static int ComputeMetadataCompletenessScore(UploadedDumpRecord record)
    {
        return ComputeMetadataCompletenessScore(record.LatestAnalysis, record.VehicleIdentifier, record.RegistrationNumber);
    }

    public static int ComputeMetadataCompletenessScore(StoredFileAnalysisSnapshot? analysis, string vehicleIdentifier, string registrationNumber)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(analysis?.DetectedVin) || !string.IsNullOrWhiteSpace(vehicleIdentifier))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(registrationNumber))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(analysis?.Ssid?.Value))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(analysis?.EisPassword?.Value))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(analysis?.McuType))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(analysis?.EisType))
        {
            score += 1;
        }

        if (analysis?.KeyCount is > 0)
        {
            score += 1;
        }

        return score;
    }

    public static bool HasEisPassword(UploadedDumpRecord record)
    {
        return record.LatestAnalysis?.EisPassword?.Status == FieldValueStatus.Present
            && !string.IsNullOrWhiteSpace(record.LatestAnalysis.EisPassword.Value);
    }

    private static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Trim().ToUpperInvariant().Where(character => char.IsLetterOrDigit(character)).ToArray());
    }
}
