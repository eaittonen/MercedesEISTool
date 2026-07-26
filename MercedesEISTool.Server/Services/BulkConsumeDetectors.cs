using System.Security.Cryptography;

namespace MercedesEISTool.Server.Services;

public sealed class BulkConsumeDetectorResult
{
    public string DetectedFormat { get; init; } = "Unsupported";
    public double Confidence { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public interface IBulkConsumeDetector
{
    string Name { get; }
    BulkConsumeDetectorResult Detect(byte[] data, string fileName);
}

public sealed class BulkConsumeFileDetectorRegistry
{
    private readonly List<IBulkConsumeDetector> _detectors = new();

    public void Register(IBulkConsumeDetector detector)
    {
        if (detector is null)
        {
            throw new ArgumentNullException(nameof(detector));
        }

        _detectors.Add(detector);
    }

    public BulkConsumeDetectorResult Detect(byte[] data, string fileName)
    {
        foreach (var detector in _detectors)
        {
            var result = detector.Detect(data, fileName);
            if (!string.Equals(result.DetectedFormat, "Unsupported", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        return new BulkConsumeDetectorResult { DetectedFormat = "Unsupported", Confidence = 0d };
    }
}

public sealed class SizeBasedBulkConsumeDetector : IBulkConsumeDetector
{
    public string Name => "size-based";

    public BulkConsumeDetectorResult Detect(byte[] data, string fileName)
    {
        var size = data.LongLength;
        if (size == 256)
        {
            return new BulkConsumeDetectorResult
            {
                DetectedFormat = "EIS dump",
                Confidence = 0.98,
                Metadata = new Dictionary<string, object?>
                {
                    ["sizeBytes"] = size,
                    ["reason"] = "size-match"
                }
            };
        }

        if (size == 160 && string.Equals(Path.GetExtension(fileName), ".bin", StringComparison.OrdinalIgnoreCase))
        {
            return new BulkConsumeDetectorResult
            {
                DetectedFormat = "CGMB key file",
                Confidence = 0.95,
                Metadata = new Dictionary<string, object?>
                {
                    ["sizeBytes"] = size,
                    ["reason"] = "size-and-extension-match"
                }
            };
        }

        return new BulkConsumeDetectorResult { DetectedFormat = "Unsupported", Confidence = 0d };
    }
}
