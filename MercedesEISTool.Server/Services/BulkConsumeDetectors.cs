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

public sealed class AnalysisBasedBulkConsumeDetector : IBulkConsumeDetector
{
    private readonly IEisAnalysisService _analysisService;
    private readonly IKeyFileAnalysisService _keyFileAnalysisService;

    public AnalysisBasedBulkConsumeDetector(IEisAnalysisService analysisService, IKeyFileAnalysisService keyFileAnalysisService)
    {
        _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        _keyFileAnalysisService = keyFileAnalysisService ?? throw new ArgumentNullException(nameof(keyFileAnalysisService));
    }

    public string Name => "analysis-based";

    public BulkConsumeDetectorResult Detect(byte[] data, string fileName)
    {
        if (data is null || data.Length == 0)
        {
            return new BulkConsumeDetectorResult { DetectedFormat = "Unsupported", Confidence = 0d };
        }

        var keyAnalysis = _keyFileAnalysisService.Analyze(data, fileName);
        if (string.Equals(keyAnalysis.DetectedFormat, "CGMB key file", StringComparison.OrdinalIgnoreCase)
            && string.Equals(keyAnalysis.DetectionConfidence, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            return new BulkConsumeDetectorResult
            {
                DetectedFormat = "CGMB key file",
                Confidence = 1d,
                Metadata = new Dictionary<string, object?>
                {
                    ["reason"] = "verified-key-file",
                    ["confidence"] = keyAnalysis.DetectionConfidence
                }
            };
        }

        var analysis = _analysisService.Analyze(data, fileName);
        if (string.Equals(analysis.DetectedFormat, "CGDI MB", StringComparison.OrdinalIgnoreCase)
            || string.Equals(analysis.DetectedFormat, "VVDI MB Tool", StringComparison.OrdinalIgnoreCase))
        {
            return new BulkConsumeDetectorResult
            {
                DetectedFormat = "EIS dump",
                Confidence = 0.82,
                Metadata = new Dictionary<string, object?>
                {
                    ["reason"] = "verified-eis-analysis",
                    ["detectedFormat"] = analysis.DetectedFormat
                }
            };
        }

        if (data.Length == 256)
        {
            return new BulkConsumeDetectorResult
            {
                DetectedFormat = "EIS dump",
                Confidence = 0.6,
                Metadata = new Dictionary<string, object?>
                {
                    ["reason"] = "size-match"
                }
            };
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
