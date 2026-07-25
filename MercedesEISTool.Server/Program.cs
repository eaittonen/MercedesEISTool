using System.Security.Cryptography;
using System.Text;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Core.Services;
using MercedesEISTool.Server.Middleware;
using MercedesEISTool.Server.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5080");

builder.Services.AddSingleton<ILicenseService, DevelopmentLicenseService>();
builder.Services.AddSingleton<ICurrentUser, DevelopmentCurrentUser>();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

app.MapGet("/api/health", (ILicenseService licenseService) =>
{
    var status = licenseService.CheckFeature(FeatureName.AnalyzeDump);
    return Results.Ok(new HealthResponse
    {
        IsHealthy = true,
        Status = status.IsGranted ? "Healthy" : "Restricted",
        ServerVersion = "1.0.0",
        ServiceName = "MercedesEISTool.Server"
    });
});

app.MapPost("/api/dumps/analyze", async Task<IResult> (IFormFile? file, ILicenseService licenseService, ILoggerFactory loggerFactory, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A dump file is required.", ErrorCode = "missing_file", RequestId = httpContext.TraceIdentifier });
    }

    if (file.Length > 1_048_576)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "The uploaded dump must be no larger than 1 MB.", ErrorCode = "file_too_large", RequestId = httpContext.TraceIdentifier });
    }

    var license = licenseService.CheckFeature(FeatureName.AnalyzeDump);
    if (!license.IsGranted)
    {
        return Results.Forbid();
    }

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream, cancellationToken);
    var bytes = stream.ToArray();
    var sha = ComputeSha256(bytes);

    if (bytes.Length != 256)
    {
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=analyze requestId={RequestId} success=false fileSize={FileSize} sha256={Sha256} reason=invalid_size", httpContext.TraceIdentifier, bytes.Length, sha);
        return Results.BadRequest(new ApiErrorResponse { Message = "Mercedes EIS dumps must be exactly 256 bytes.", ErrorCode = "invalid_size", RequestId = httpContext.TraceIdentifier });
    }

    var service = new EisDumpService();
    var dump = service.ParseDump(bytes);
    var response = new AnalyzeDumpResponse
    {
        FileName = file.FileName,
        DetectedFormat = dump.Format,
        Vin = dump.VIN,
        FieldAvailability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["VIN"] = !string.IsNullOrWhiteSpace(dump.VIN),
            ["Format"] = !string.IsNullOrWhiteSpace(dump.Format),
            ["EIS type"] = !string.IsNullOrWhiteSpace(dump.EisType),
            ["MCU"] = !string.IsNullOrWhiteSpace(dump.MCU),
            ["Key count"] = dump.Keys.Count > 0
        },
        Sha256 = sha,
        FileSizeBytes = bytes.Length,
        Status = "Analyzed"
    };

    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=analyze requestId={RequestId} success=true fileSize={FileSize} sha256={Sha256}", httpContext.TraceIdentifier, bytes.Length, sha);
    return Results.Ok(response);
});

app.MapPost("/api/dumps/compare", async Task<IResult> (IFormFile? leftFile, IFormFile? rightFile, ILicenseService licenseService, ILoggerFactory loggerFactory, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (leftFile is null || rightFile is null)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Two dump files are required.", ErrorCode = "missing_files", RequestId = httpContext.TraceIdentifier });
    }

    var license = licenseService.CheckFeature(FeatureName.CompareDumps);
    if (!license.IsGranted)
    {
        return Results.Forbid();
    }

    using var leftStream = new MemoryStream();
    using var rightStream = new MemoryStream();
    await leftFile.CopyToAsync(leftStream, cancellationToken);
    await rightFile.CopyToAsync(rightStream, cancellationToken);

    var leftBytes = leftStream.ToArray();
    var rightBytes = rightStream.ToArray();
    var shaLeft = ComputeSha256(leftBytes);
    var shaRight = ComputeSha256(rightBytes);

    if (leftBytes.Length != 256 || rightBytes.Length != 256)
    {
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=compare requestId={RequestId} success=false leftSize={LeftSize} rightSize={RightSize} leftSha={LeftSha} rightSha={RightSha}", httpContext.TraceIdentifier, leftBytes.Length, rightBytes.Length, shaLeft, shaRight);
        return Results.BadRequest(new ApiErrorResponse { Message = "Both dumps must be exactly 256 bytes.", ErrorCode = "invalid_size", RequestId = httpContext.TraceIdentifier });
    }

    var service = new EisDumpService();
    var comparison = service.CompareDumps(leftBytes, rightBytes);
    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=compare requestId={RequestId} success=true leftSize={LeftSize} rightSize={RightSize} leftSha={LeftSha} rightSha={RightSha}", httpContext.TraceIdentifier, leftBytes.Length, rightBytes.Length, shaLeft, shaRight);
    return Results.Ok(new CompareDumpsResponse { TotalDifferences = comparison.TotalDifferences, DifferingOffsets = comparison.DifferingOffsets });
});

app.Run();

static string ComputeSha256(byte[] bytes)
{
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(bytes));
}

public partial class Program
{
}
