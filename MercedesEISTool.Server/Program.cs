using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Core.Services;
using MercedesEISTool.Server.Middleware;
using MercedesEISTool.Server.Models;
using MercedesEISTool.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5080");

builder.Services.AddSingleton<ILicenseService, DevelopmentLicenseService>();
builder.Services.AddSingleton<ICurrentUser, DevelopmentCurrentUser>();
builder.Services.AddSingleton<IUploadedDumpStore, JsonUploadedDumpStore>();
builder.Services.AddSingleton<IEisAnalysisService, EisAnalysisService>();
builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAntiforgery();

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

app.MapGet("/api/uploads", async (IUploadedDumpStore uploadedDumpStore, ICurrentUser currentUser) =>
{
    var uploads = await uploadedDumpStore.ListAsync(currentUser);
    return Results.Ok(new UploadedDumpListResponse
    {
        Uploads = uploads.Select(record => new UploadedDumpSummary
        {
            Id = record.Id,
            FileName = record.FileName,
            Operation = record.Operation,
            CreatedAtUtc = record.CreatedAtUtc,
            SizeBytes = record.SizeBytes,
            DetectedVin = record.VehicleIdentifier,
            VinStatus = "Present",
            UserProvidedVin = record.VehicleIdentifier,
            UserProvidedRegistrationNumber = record.RegistrationNumber
        }).ToList()
    });
});

app.MapGet("/api/files", async (string? search, int? page, int? pageSize, IUploadedDumpStore uploadedDumpStore, ICurrentUser currentUser) =>
{
    var pageNumber = Math.Max(1, page ?? 1);
    var pageSizeValue = Math.Clamp(pageSize ?? 50, 1, 200);
    var allRecords = await uploadedDumpStore.ListAsync(currentUser, search, 1, int.MaxValue);
    var records = await uploadedDumpStore.ListAsync(currentUser, search, pageNumber, pageSizeValue);
    var items = records.Select(record => BuildStoredFileListItem(record)).ToList();
    var totalCount = allRecords.Count;
    return Results.Ok(new StoredFileListResponse
    {
        Items = items,
        Page = pageNumber,
        PageSize = pageSizeValue,
        TotalCount = totalCount,
        TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSizeValue)
    });
});

app.MapGet("/api/files/{storedFileId:guid}", async Task<IResult> (Guid storedFileId, IUploadedDumpStore uploadedDumpStore, ICurrentUser currentUser) =>
{
    var record = await uploadedDumpStore.GetByIdAsync(storedFileId, currentUser);
    if (record is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "Stored file was not found.", ErrorCode = "not_found" });
    }

    return Results.Ok(BuildStoredFileDetails(record));
});

app.MapGet("/api/files/{storedFileId:guid}/download", async Task<IResult> (Guid storedFileId, IUploadedDumpStore uploadedDumpStore, ICurrentUser currentUser) =>
{
    try
    {
        var bytes = await uploadedDumpStore.ReadStoredFileAsync(storedFileId, currentUser);
        var record = await uploadedDumpStore.GetByIdAsync(storedFileId, currentUser);
        return Results.File(bytes, "application/octet-stream", record?.FileName ?? "download.bin");
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "Stored file was not found.", ErrorCode = "not_found" });
    }
});

app.MapPost("/api/files/{storedFileId:guid}/reanalyze", async Task<IResult> (Guid storedFileId, ILicenseService licenseService, IUploadedDumpStore uploadedDumpStore, IEisAnalysisService analysisService, ICurrentUser currentUser) =>
{
    var license = licenseService.CheckFeature(FeatureName.AnalyzeDump, currentUser);
    if (!license.IsGranted)
    {
        return Results.Forbid();
    }

    var record = await uploadedDumpStore.GetByIdAsync(storedFileId, currentUser);
    if (record is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "Stored file was not found.", ErrorCode = "not_found" });
    }

    await uploadedDumpStore.AnalyzeAndStoreAsync(storedFileId, analysisService);
    return Results.Ok(BuildStoredFileDetails(await uploadedDumpStore.GetByIdAsync(storedFileId, currentUser)));
});

app.MapPost("/api/files/upload", async Task<IResult> (IFormFile? file, [FromForm] string? userProvidedVin, [FromForm] string? userProvidedRegistrationNumber, [FromForm] bool vehicleIdentifierConfirmed, ILicenseService licenseService, IUploadedDumpStore uploadedDumpStore, IEisAnalysisService analysisService, ILoggerFactory loggerFactory, HttpContext httpContext, ICurrentUser currentUser, CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A dump file is required.", ErrorCode = "missing_file", RequestId = httpContext.TraceIdentifier });
    }

    if (!vehicleIdentifierConfirmed)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Confirmation is required before upload.", ErrorCode = "confirmation_required", RequestId = httpContext.TraceIdentifier });
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
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=upload requestId={RequestId} success=false fileSize={FileSize} sha256={Sha256} reason=invalid_size", httpContext.TraceIdentifier, bytes.Length, sha);
        return Results.BadRequest(new ApiErrorResponse { Message = "Mercedes EIS dumps must be exactly 256 bytes.", ErrorCode = "invalid_size", RequestId = httpContext.TraceIdentifier });
    }

    var workflow = new AnalysisWorkflowService();
    var analysis = workflow.Analyze(bytes, file.FileName);
    var validation = workflow.ValidateUpload(bytes, userProvidedVin, userProvidedRegistrationNumber, vehicleIdentifierConfirmed);
    if (!validation.IsValid)
    {
        if (validation.Message.Contains("does not match", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new ApiErrorResponse { Message = validation.Message, ErrorCode = "vin_mismatch", RequestId = httpContext.TraceIdentifier });
        }

        return Results.BadRequest(new ApiErrorResponse { Message = validation.Message, ErrorCode = "invalid_upload_metadata", RequestId = httpContext.TraceIdentifier });
    }

    UploadedDumpRecord savedUpload;
    try
    {
        savedUpload = await uploadedDumpStore.PersistAsync(bytes, file.FileName, userProvidedVin ?? string.Empty, userProvidedRegistrationNumber ?? string.Empty, "upload", analysisService);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = ex.Message, ErrorCode = "invalid_upload_metadata", RequestId = httpContext.TraceIdentifier });
    }

    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=upload requestId={RequestId} success=true fileSize={FileSize} sha256={Sha256}", httpContext.TraceIdentifier, bytes.Length, sha);
    var analysisDetails = analysisService.Analyze(bytes, file.FileName);
    return Results.Ok(new UploadDumpResponse
    {
        FileName = file.FileName,
        Status = "Uploaded",
        Sha256 = sha,
        FileSizeBytes = bytes.Length,
        UploadId = savedUpload.Id,
        DetectedVin = analysisDetails.DetectedVin,
        VinStatus = analysisDetails.VinStatus,
        Message = validation.Message,
        AnalysisDetails = analysisDetails
    });
}).DisableAntiforgery();

app.MapPost("/api/dumps/analyze", async Task<IResult> (IFormFile? file, ILicenseService licenseService, IEisAnalysisService analysisService, ILoggerFactory loggerFactory, HttpContext httpContext, CancellationToken cancellationToken) =>
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

    var workflow = new AnalysisWorkflowService();
    var analysis = workflow.Analyze(bytes, file.FileName);
    var analysisDetails = analysisService.Analyze(bytes, file.FileName);
    var response = new AnalyzeDumpResponse
    {
        FileName = file.FileName,
        DetectedFormat = analysis.DetectedFormat,
        DetectedVin = analysisDetails.DetectedVin,
        VinStatus = analysisDetails.VinStatus,
        VinSource = analysis.VinSource,
        EisType = analysisDetails.EisType ?? string.Empty,
        McuType = analysisDetails.McuType ?? string.Empty,
        KeyCount = analysisDetails.KeyCount?.ToString() ?? "NotMapped",
        Sha256 = sha,
        FileSizeBytes = bytes.Length,
        AnalysisSucceeded = analysis.AnalysisSucceeded,
        Message = analysis.Message,
        Status = analysis.AnalysisSucceeded ? "Analyzed" : "Failed",
        AnalysisDetails = analysisDetails
    };

    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=analyze requestId={RequestId} success=true fileSize={FileSize} sha256={Sha256}", httpContext.TraceIdentifier, bytes.Length, sha);
    return Results.Ok(response);
}).DisableAntiforgery();

app.MapPost("/api/dumps/compare", async Task<IResult> (IFormFile? leftFile, IFormFile? rightFile, [FromForm] CompareDumpsRequest request, ILicenseService licenseService, IUploadedDumpStore uploadedDumpStore, ILoggerFactory loggerFactory, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (leftFile is null || rightFile is null)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Two dump files are required.", ErrorCode = "missing_files", RequestId = httpContext.TraceIdentifier });
    }

    if (string.IsNullOrWhiteSpace(request.VehicleIdentifier))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A vehicle identifier is required for uploads.", ErrorCode = "missing_vehicle_identifier", RequestId = httpContext.TraceIdentifier });
    }

    if (string.IsNullOrWhiteSpace(request.RegistrationNumber))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A registration number is required for uploads.", ErrorCode = "missing_registration_number", RequestId = httpContext.TraceIdentifier });
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

    try
    {
        await uploadedDumpStore.PersistAsync(leftBytes, leftFile.FileName, request.VehicleIdentifier, request.RegistrationNumber, "compare-left");
        await uploadedDumpStore.PersistAsync(rightBytes, rightFile.FileName, request.VehicleIdentifier, request.RegistrationNumber, "compare-right");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = ex.Message, ErrorCode = "invalid_upload_metadata", RequestId = httpContext.TraceIdentifier });
    }

    var service = new EisDumpService();
    var comparison = service.CompareDumps(leftBytes, rightBytes);
    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=compare requestId={RequestId} success=true leftSize={LeftSize} rightSize={RightSize} leftSha={LeftSha} rightSha={RightSha}", httpContext.TraceIdentifier, leftBytes.Length, rightBytes.Length, shaLeft, shaRight);
    return Results.Ok(new CompareDumpsResponse { TotalDifferences = comparison.TotalDifferences, DifferingOffsets = comparison.DifferingOffsets, VehicleIdentifier = request.VehicleIdentifier, RegistrationNumber = request.RegistrationNumber });
}).DisableAntiforgery();

app.Run();

static string ComputeSha256(byte[] bytes)
{
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(bytes));
}

static StoredFileListItemDto BuildStoredFileListItem(UploadedDumpRecord record)
{
    var latest = record.LatestAnalysis;
    return new StoredFileListItemDto
    {
        Id = record.Id,
        OriginalFileName = record.FileName,
        UploadedAtUtc = record.CreatedAtUtc,
        UserProvidedVin = string.IsNullOrWhiteSpace(record.VehicleIdentifier) ? null : record.VehicleIdentifier,
        DetectedVin = latest?.DetectedVin,
        RegistrationNumber = string.IsNullOrWhiteSpace(record.RegistrationNumber) ? null : record.RegistrationNumber,
        DetectedFormat = latest?.DetectedFormat ?? "Unknown",
        EisType = latest?.EisType,
        McuType = latest?.McuType,
        KeyCount = latest?.KeyCount,
        KeyCountStatus = latest?.KeyCountStatus.ToString() ?? "NotMapped",
        EisPassword = latest?.EisPassword?.Value,
        EisPasswordStatus = latest?.EisPassword?.Status.ToString() ?? "NotMapped",
        Ssid = latest?.Ssid?.Value,
        SsidStatus = latest?.Ssid?.Status.ToString() ?? "NotMapped",
        KeyPasswordsFound = latest?.Keys.Count(item => item.Password is not null) ?? 0,
        AnalysisStatus = latest is null ? "Pending" : (latest.AnalysisSucceeded ? "Analyzed" : "Failed"),
        ParserVersion = latest?.ParserVersion ?? string.Empty,
        FileSizeBytes = record.SizeBytes,
        Sha256 = ComputeSha256(File.ReadAllBytes(record.StoredFilePath)),
        IsDeleted = false,
        CanViewSensitiveFields = true
    };
}

static StoredFileDetailsDto BuildStoredFileDetails(UploadedDumpRecord? record)
{
    if (record is null)
    {
        return new StoredFileDetailsDto();
    }

    var latest = record.LatestAnalysis;
    return new StoredFileDetailsDto
    {
        Id = record.Id,
        OriginalFileName = record.FileName,
        UploadedAtUtc = record.CreatedAtUtc,
        UserProvidedVin = string.IsNullOrWhiteSpace(record.VehicleIdentifier) ? null : record.VehicleIdentifier,
        DetectedVin = latest?.DetectedVin,
        VinStatus = latest?.VinStatus ?? "NotMapped",
        RegistrationNumber = string.IsNullOrWhiteSpace(record.RegistrationNumber) ? null : record.RegistrationNumber,
        DetectedFormat = latest?.DetectedFormat ?? "Unknown",
        EisType = latest?.EisType,
        EisTypeStatus = latest?.EisTypeStatus.ToString() ?? "NotMapped",
        McuType = latest?.McuType,
        McuTypeStatus = latest?.McuTypeStatus.ToString() ?? "NotMapped",
        KeyCount = latest?.KeyCount,
        KeyCountStatus = latest?.KeyCountStatus.ToString() ?? "NotMapped",
        EisPassword = latest?.EisPassword?.Value,
        EisPasswordStatus = latest?.EisPassword?.Status.ToString() ?? "NotMapped",
        Ssid = latest?.Ssid?.Value,
        SsidStatus = latest?.Ssid?.Status.ToString() ?? "NotMapped",
        Keys = latest?.Keys ?? new List<KeySlotDto>(),
        ParserVersion = latest?.ParserVersion ?? string.Empty,
        AnalyzedAtUtc = latest?.AnalyzedAtUtc,
        FileSizeBytes = record.SizeBytes,
        Sha256 = ComputeSha256(File.ReadAllBytes(record.StoredFilePath)),
        IsDeleted = false,
        CanViewSensitiveFields = true
    };
}

public partial class Program
{
}
