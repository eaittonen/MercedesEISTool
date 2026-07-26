using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Core.Services;
using MercedesEISTool.Server.Data;
using MercedesEISTool.Server.Middleware;
using MercedesEISTool.Server.Models;
using MercedesEISTool.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var configuredUrls = builder.Configuration["ASPNETCORE_URLS"]
    ?? builder.Configuration["Urls"]
    ?? builder.Configuration["Server:Urls"]
    ?? builder.Configuration["Server:BaseUrl"];

if (string.IsNullOrWhiteSpace(configuredUrls))
{
    configuredUrls = "http://0.0.0.0:5080";
}

builder.WebHost.UseUrls(configuredUrls);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=mercedes-eis-auth.db";
var environmentName = builder.Environment.EnvironmentName;
var isTestHost = Environment.GetEnvironmentVariable("VSTEST_HOST_DEBUG") is not null
    || AppContext.BaseDirectory.Contains("testhost", StringComparison.OrdinalIgnoreCase)
    || Environment.ProcessPath?.Contains("testhost", StringComparison.OrdinalIgnoreCase) == true;

if (isTestHost && string.Equals(connectionString, "Data Source=mercedes-eis-auth.db", StringComparison.OrdinalIgnoreCase))
{
    var testDatabasePath = Path.Combine(Path.GetTempPath(), $"mercedes-eis-tool-tests-{Guid.NewGuid():N}.sqlite");
    connectionString = new SqliteConnectionStringBuilder(connectionString)
    {
        DataSource = testDatabasePath
    }.ToString();
}

var sqliteResolver = new SqliteDatabasePathResolver();
var resolvedDatabasePath = sqliteResolver.Resolve(connectionString, builder.Environment.ContentRootPath, NullLogger.Instance);
var effectiveConnectionString = new SqliteConnectionStringBuilder(connectionString)
{
    DataSource = resolvedDatabasePath
}.ToString();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IUploadedDumpStore, JsonUploadedDumpStore>();
builder.Services.AddSingleton<IEisAnalysisService, EisAnalysisService>();
builder.Services.AddSingleton<IKeyFileAnalysisService, KeyFileAnalysisService>();
builder.Services.AddSingleton<BulkConsumeService>();
builder.Services.AddAntiforgery();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(effectiveConnectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 1;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ILicenseService, DevelopmentLicenseService>();
    builder.Services.AddSingleton<ICurrentUser, DevelopmentCurrentUser>();
}
else
{
    builder.Services.AddSingleton<ILicenseService, ProductionLicenseService>();
    builder.Services.AddSingleton<ICurrentUser, ProductionCurrentUser>();
}

builder.Services.Configure<DevelopmentBootstrapOptions>(builder.Configuration.GetSection("Authentication:DevelopmentBootstrap"));
builder.Services.AddScoped<DevelopmentBootstrapService>();

var app = builder.Build();

var databasePath = effectiveConnectionString.Contains("Data Source=")
    ? new SqliteConnectionStringBuilder(effectiveConnectionString).DataSource
    : resolvedDatabasePath;

app.Logger.LogInformation("startup self-test aspnetcore_environment={Environment}", environmentName);
app.Logger.LogInformation("startup self-test database_connection_string={ConnectionString}", effectiveConnectionString);
app.Logger.LogInformation("startup self-test database_file_path={DatabasePath}", databasePath);
app.Logger.LogInformation("startup self-test application_version={ApplicationVersion}", typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
app.Logger.LogInformation("startup self-test listening_urls={Urls}", string.Join(",", app.Urls));

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MercedesEISTool.Server");

    try
    {
        logger.LogInformation("startup self-test database_connection=testing");
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.CloseConnectionAsync();
        logger.LogInformation("startup self-test database_connection=ok");

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        logger.LogInformation("startup self-test pending_migrations={PendingMigrationCount}", pendingMigrations.Count());

        logger.LogInformation("startup self-test applying_migrations=true");
        try
        {
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("startup self-test migrations=applied");
        }
        catch (Exception ex) when (ex.Message.Contains("no such column") || ex.Message.Contains("does not exist"))
        {
            logger.LogWarning(ex, "startup self-test migration_failed_with_schema_mismatch; attempting compatibility repair");
            await RepairSchemaCompatibilityAsync(dbContext, logger);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("startup self-test migrations=applied_after_repair");
        }

        await RepairSchemaCompatibilityAsync(dbContext, logger);
        logger.LogInformation("startup self-test schema_repair=completed");

        var schemaVersion = await dbContext.Database.SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'").ToListAsync();
        logger.LogInformation("startup self-test schema_version={SchemaVersion}", schemaVersion.Count > 0 ? "present" : "missing");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "startup self-test failed: {Message}", ex.Message);
        throw;
    }

    var uploadStorageRoot = JsonUploadedDumpStore.ResolveStorageRoot();
    try
    {
        Directory.CreateDirectory(uploadStorageRoot);
        logger.LogInformation("startup self-test upload_storage=ok path={Path}", uploadStorageRoot);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "startup self-test upload_storage_failed: {Message}", ex.Message);
    }

    var bootstrap = scope.ServiceProvider.GetRequiredService<DevelopmentBootstrapService>();
    await bootstrap.SeedAsync();
}

app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAntiforgery();

app.MapGet("/api/health", (ILicenseService licenseService, ILoggerFactory loggerFactory) =>
{
    var status = licenseService.CheckFeature(FeatureName.AnalyzeDump);
    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("health-check status={Status}", status.IsGranted ? "Healthy" : "Restricted");
    return Results.Ok(new HealthResponse
    {
        IsHealthy = true,
        Status = status.IsGranted ? "Healthy" : "Restricted",
        ServerVersion = "1.0.0",
        ServiceName = "MercedesEISTool.Server"
    });
});

app.MapPost("/api/auth/login", async Task<IResult> (LoginRequestDto request, UserManager<ApplicationUser> userManager, DevelopmentBootstrapService bootstrapService, IOptions<DevelopmentBootstrapOptions> bootstrapOptions, ILoggerFactory loggerFactory, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Json(new ApiErrorResponse { Message = "Email and password are required.", ErrorCode = "invalid_credentials", RequestId = httpContext.TraceIdentifier }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var user = await userManager.FindByEmailAsync(request.Email);
    var shouldBootstrapForConfiguredCredentials = string.Equals(request.Email, bootstrapOptions.Value.AdminEmail, StringComparison.OrdinalIgnoreCase)
        || string.Equals(request.Email, bootstrapOptions.Value.UserEmail, StringComparison.OrdinalIgnoreCase);

    if ((user is null || !await userManager.CheckPasswordAsync(user, request.Password)) && shouldBootstrapForConfiguredCredentials)
    {
        await bootstrapService.SeedAsync();
        user = await userManager.FindByEmailAsync(request.Email);
    }

    if (user is null)
    {
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=auth-login requestId={RequestId} success=false email={Email} reason=not_found", httpContext.TraceIdentifier, request.Email);
        return Results.Json(new ApiErrorResponse { Message = "Invalid email or password.", ErrorCode = "invalid_credentials", RequestId = httpContext.TraceIdentifier }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!user.IsEnabled)
    {
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=auth-login requestId={RequestId} success=false email={Email} reason=disabled", httpContext.TraceIdentifier, request.Email);
        return Results.Json(new ApiErrorResponse { Message = "This account is disabled.", ErrorCode = "account_disabled", RequestId = httpContext.TraceIdentifier }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (!await userManager.CheckPasswordAsync(user, request.Password))
    {
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=auth-login requestId={RequestId} success=false email={Email} reason=bad_password", httpContext.TraceIdentifier, request.Email);
        return Results.Json(new ApiErrorResponse { Message = "Invalid email or password.", ErrorCode = "invalid_credentials", RequestId = httpContext.TraceIdentifier }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var roles = (await userManager.GetRolesAsync(user)).ToList();
    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=auth-login requestId={RequestId} success=true email={Email} roles={Roles}", httpContext.TraceIdentifier, request.Email, string.Join(",", roles));
    return Results.Ok(new AuthResponseDto
    {
        AccessToken = user.Id,
        RefreshToken = user.Id,
        AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8),
        UserId = user.Id,
        Email = user.Email ?? request.Email,
        DisplayName = user.DisplayName,
        Roles = roles
    });
});

app.MapGet("/api/auth/me", async Task<IResult> (UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        return Results.Unauthorized();
    }

    var token = authHeader.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);
    var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == token);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var roles = (await userManager.GetRolesAsync(user)).ToList();
    return Results.Ok(new CurrentUserResponseDto
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        DisplayName = user.DisplayName,
        Roles = roles,
        IsAdministrator = roles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase))
    });
});

app.MapGet("/api/admin/users", async Task<IResult> (UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var usersQuery = dbContext.Users
        .Include(user => user.Organization)
        .AsQueryable();

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        usersQuery = usersQuery.Where(user => user.OrganizationId == currentUser.OrganizationId);
    }

    var users = await usersQuery
        .OrderBy(user => user.Email)
        .ToListAsync();

    var items = new List<AdminUserListItemDto>();
    foreach (var user in users)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        items.Add(new AdminUserListItemDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            OrganizationId = user.OrganizationId,
            OrganizationName = user.Organization?.Name ?? string.Empty,
            Roles = roles,
            IsEnabled = user.IsEnabled,
            MustChangePassword = user.MustChangePassword,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc
        });
    }

    return Results.Ok(new AdminUserListResponseDto { Items = items.OrderBy(item => item.Email, StringComparer.OrdinalIgnoreCase).ToList() });
});

app.MapGet("/api/admin/organizations", async Task<IResult> (UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var organizationsQuery = dbContext.Organizations
        .Include(organization => organization.Users)
        .AsQueryable();

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        organizationsQuery = organizationsQuery.Where(organization => organization.Id == currentUser.OrganizationId);
    }

    var organizations = await organizationsQuery
        .OrderBy(organization => organization.Name)
        .ToListAsync();

    var response = new OrganizationListResponseDto
    {
        Items = organizations.Select(organization => new OrganizationSummaryDto
        {
            Id = organization.Id,
            Name = organization.Name,
            ContactEmail = organization.ContactEmail,
            Country = organization.Country,
            IsActive = organization.IsActive,
            LicenseType = organization.LicenseType,
            LicenseExpirationUtc = organization.LicenseExpirationUtc,
            MaxUsers = organization.MaxUsers,
            UserCount = organization.Users.Count
        }).ToList()
    };

    return Results.Ok(response);
});

app.MapGet("/api/admin/organizations/options", async Task<IResult> (UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var organizationsQuery = dbContext.Organizations.AsQueryable();

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        organizationsQuery = organizationsQuery.Where(organization => organization.Id == currentUser.OrganizationId);
    }

    var organizations = await organizationsQuery
        .OrderBy(organization => organization.Name)
        .Select(organization => new OrganizationOptionDto(organization.Id, organization.Name))
        .ToListAsync();

    return Results.Ok(organizations);
});

app.MapGet("/api/admin/roles", async Task<IResult> (UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var roleNames = new[] { "SystemAdministrator", "CompanyAdministrator", "Administrator", "Technician", "Research", "ReadOnly", "User" };
    var roleOptions = roleNames.Select(name => new RoleOptionDto(name, currentRoles.Contains("SystemAdministrator", StringComparer.OrdinalIgnoreCase) || !name.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase))).ToList();
    return Results.Ok(roleOptions);
});

app.MapPost("/api/admin/organizations", async Task<IResult> (CreateOrganizationRequestDto request, UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Contains("SystemAdministrator", StringComparer.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Organization name is required.", ErrorCode = "invalid_request" });
    }

    var organization = new Organization
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = request.Name,
        ContactEmail = request.ContactEmail,
        Country = request.Country,
        IsActive = request.IsActive,
        LicenseType = request.LicenseType,
        LicenseExpirationUtc = request.LicenseExpirationUtc,
        MaxUsers = request.MaxUsers,
        CreatedUtc = DateTimeOffset.UtcNow,
        UpdatedUtc = DateTimeOffset.UtcNow
    };

    dbContext.Organizations.Add(organization);
    await dbContext.SaveChangesAsync();

    return Results.Ok(new OrganizationDetailDto
    {
        Id = organization.Id,
        Name = organization.Name,
        ContactEmail = organization.ContactEmail,
        Country = organization.Country,
        IsActive = organization.IsActive,
        LicenseType = organization.LicenseType,
        LicenseExpirationUtc = organization.LicenseExpirationUtc,
        MaxUsers = organization.MaxUsers,
        UserCount = 0
    });
});

app.MapDelete("/api/admin/organizations/{organizationId}", async Task<IResult> (string organizationId, UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Contains("SystemAdministrator", StringComparer.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var organization = await dbContext.Organizations.Include(organization => organization.Users).FirstOrDefaultAsync(candidate => candidate.Id == organizationId);
    if (organization is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "Organization was not found.", ErrorCode = "not_found" });
    }

    if (organization.Users.Any())
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Organizations with assigned users cannot be deleted.", ErrorCode = "invalid_operation" });
    }

    dbContext.Organizations.Remove(organization);
    await dbContext.SaveChangesAsync();

    return Results.Ok(new ApiErrorResponse { Message = "Organization deleted.", ErrorCode = "ok" });
});

app.MapPut("/api/admin/organizations/{organizationId}", async Task<IResult> (string organizationId, UpdateOrganizationRequestDto request, UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Contains("SystemAdministrator", StringComparer.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var organization = await dbContext.Organizations.Include(organization => organization.Users).FirstOrDefaultAsync(candidate => candidate.Id == organizationId);
    if (organization is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "Organization was not found.", ErrorCode = "not_found" });
    }

    organization.Name = request.Name;
    organization.ContactEmail = request.ContactEmail;
    organization.Country = request.Country;
    organization.IsActive = request.IsActive;
    organization.LicenseType = request.LicenseType;
    organization.LicenseExpirationUtc = request.LicenseExpirationUtc;
    organization.MaxUsers = request.MaxUsers;
    organization.UpdatedUtc = DateTimeOffset.UtcNow;

    await dbContext.SaveChangesAsync();

    return Results.Ok(new OrganizationDetailDto
    {
        Id = organization.Id,
        Name = organization.Name,
        ContactEmail = organization.ContactEmail,
        Country = organization.Country,
        IsActive = organization.IsActive,
        LicenseType = organization.LicenseType,
        LicenseExpirationUtc = organization.LicenseExpirationUtc,
        MaxUsers = organization.MaxUsers,
        UserCount = organization.Users.Count
    });
});

app.MapPost("/api/admin/users/{userId}/disable", async Task<IResult> (string userId, UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        return Results.Unauthorized();
    }

    var token = authHeader.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);
    var currentUser = await userManager.Users.FirstOrDefaultAsync(u => u.Id == token);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var targetUser = await userManager.FindByIdAsync(userId);
    if (targetUser is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "User was not found.", ErrorCode = "not_found" });
    }

    if (targetUser.Id == currentUser.Id)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Administrators cannot disable their own account.", ErrorCode = "invalid_operation" });
    }

    targetUser.IsEnabled = false;
    var result = await userManager.UpdateAsync(targetUser);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", result.Errors.Select(error => error.Description)), ErrorCode = "update_failed" });
    }

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = targetUser.Id,
        IsEnabled = targetUser.IsEnabled,
        Message = "User disabled."
    });
});

app.MapPost("/api/admin/users/{userId}/enable", async Task<IResult> (string userId, UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var authHeader = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        return Results.Unauthorized();
    }

    var token = authHeader.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);
    var currentUser = await userManager.Users.FirstOrDefaultAsync(u => u.Id == token);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var targetUser = await userManager.FindByIdAsync(userId);
    if (targetUser is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "User was not found.", ErrorCode = "not_found" });
    }

    targetUser.IsEnabled = true;
    var result = await userManager.UpdateAsync(targetUser);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", result.Errors.Select(error => error.Description)), ErrorCode = "update_failed" });
    }

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = targetUser.Id,
        IsEnabled = targetUser.IsEnabled,
        Message = "User enabled."
    });
});

app.MapPost("/api/admin/users", async Task<IResult> (CreateOrUpdateUserRequestDto request, UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, ILoggerFactory loggerFactory, HttpContext httpContext) =>
{
    var logger = loggerFactory.CreateLogger("MercedesEISTool.Server");
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)) && !string.Equals(request.OrganizationId, currentUser.OrganizationId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiErrorResponse { Message = "You can only create users in your own organization.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)) && request.Roles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "Company administrators cannot assign system administrator roles.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Email is required.", ErrorCode = "invalid_request" });
    }

    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Display name is required.", ErrorCode = "invalid_request" });
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Password is required when creating a user.", ErrorCode = "invalid_request" });
    }

    if (string.IsNullOrWhiteSpace(request.OrganizationId))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Select an organization.", ErrorCode = "invalid_request" });
    }

    var normalizedRoles = request.Roles?
        .Where(role => !string.IsNullOrWhiteSpace(role))
        .Select(role => role.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList() ?? new List<string>();

    if (normalizedRoles.Count == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Select at least one role.", ErrorCode = "invalid_request" });
    }

    var allowedRoles = new[] { "SystemAdministrator", "CompanyAdministrator", "Administrator", "Technician", "Research", "ReadOnly", "User" };
    var invalidRoles = normalizedRoles.Where(role => !allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
    if (invalidRoles.Count > 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "One or more roles are not allowed.", ErrorCode = "invalid_request" });
    }

    var organization = await dbContext.Organizations.SingleOrDefaultAsync(candidate => candidate.Id == request.OrganizationId);
    if (organization is null)
    {
        logger.LogWarning("user_save_failed reason=organization_not_found organization_id={OrganizationId}", request.OrganizationId);
        return Results.NotFound(new ApiErrorResponse { Message = "Organization was not found.", ErrorCode = "not_found" });
    }

    var existingUser = await userManager.FindByEmailAsync(request.Email);
    if (existingUser is not null)
    {
        return Results.Conflict(new ApiErrorResponse { Message = "Email is already in use.", ErrorCode = "already_exists" });
    }

    var user = new ApplicationUser
    {
        UserName = request.Email,
        Email = request.Email,
        DisplayName = request.DisplayName,
        IsEnabled = request.IsEnabled,
        EmailConfirmed = true,
        OrganizationId = organization.Id,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        MustChangePassword = request.MustChangePassword
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", result.Errors.Select(error => error.Description)), ErrorCode = "create_failed" });
    }

    foreach (var role in normalizedRoles)
    {
        await userManager.AddToRoleAsync(user, role);
    }

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = user.Id,
        IsEnabled = user.IsEnabled,
        Message = "User created."
    });
});

app.MapPut("/api/admin/users/{userId}", async Task<IResult> (string userId, CreateOrUpdateUserRequestDto request, UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, ILoggerFactory loggerFactory, HttpContext httpContext) =>
{
    var logger = loggerFactory.CreateLogger("MercedesEISTool.Server");
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    var canManage = currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase));
    if (!canManage)
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await userManager.FindByIdAsync(userId);
    if (user is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "User was not found.", ErrorCode = "not_found" });
    }

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)) && !string.Equals(user.OrganizationId, currentUser.OrganizationId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiErrorResponse { Message = "You can only manage users in your own organization.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)) && request.Roles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "Company administrators cannot assign system administrator roles.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Email is required.", ErrorCode = "invalid_request" });
    }

    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Display name is required.", ErrorCode = "invalid_request" });
    }

    if (string.IsNullOrWhiteSpace(request.OrganizationId))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Select an organization.", ErrorCode = "invalid_request" });
    }

    var normalizedRoles = request.Roles?
        .Where(role => !string.IsNullOrWhiteSpace(role))
        .Select(role => role.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList() ?? new List<string>();

    if (normalizedRoles.Count == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Select at least one role.", ErrorCode = "invalid_request" });
    }

    var allowedRoles = new[] { "SystemAdministrator", "CompanyAdministrator", "Administrator", "Technician", "Research", "ReadOnly", "User" };
    var invalidRoles = normalizedRoles.Where(role => !allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
    if (invalidRoles.Count > 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "One or more roles are not allowed.", ErrorCode = "invalid_request" });
    }

    var organization = await dbContext.Organizations.SingleOrDefaultAsync(candidate => candidate.Id == request.OrganizationId);
    if (organization is null)
    {
        logger.LogWarning("user_save_failed reason=organization_not_found organization_id={OrganizationId}", request.OrganizationId);
        return Results.NotFound(new ApiErrorResponse { Message = "Organization was not found.", ErrorCode = "not_found" });
    }

    user.DisplayName = request.DisplayName;
    user.Email = request.Email;
    user.UserName = request.Email;
    user.IsEnabled = request.IsEnabled;
    user.OrganizationId = organization.Id;
    user.MustChangePassword = request.MustChangePassword;

    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        var tokenResult = await userManager.RemovePasswordAsync(user);
        if (tokenResult.Succeeded)
        {
            await userManager.AddPasswordAsync(user, request.Password);
        }
    }

    var updateResult = await userManager.UpdateAsync(user);
    if (!updateResult.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", updateResult.Errors.Select(error => error.Description)), ErrorCode = "update_failed" });
    }

    var currentUserRoles = (await userManager.GetRolesAsync(user)).ToList();
    foreach (var currentRole in currentUserRoles)
    {
        await userManager.RemoveFromRoleAsync(user, currentRole);
    }

    foreach (var role in normalizedRoles)
    {
        await userManager.AddToRoleAsync(user, role);
    }

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = user.Id,
        IsEnabled = user.IsEnabled,
        Message = "User updated."
    });
});

app.MapDelete("/api/admin/users/{userId}", async Task<IResult> (string userId, UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var targetUser = await userManager.FindByIdAsync(userId);
    if (targetUser is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "User was not found.", ErrorCode = "not_found" });
    }

    if (targetUser.Id == currentUser.Id)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Administrators cannot delete their own account.", ErrorCode = "invalid_operation" });
    }

    var targetRoles = (await userManager.GetRolesAsync(targetUser)).ToList();
    if (targetRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)) && currentRoles.All(role => !role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "Company administrators cannot delete system administrators.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var deleteResult = await userManager.DeleteAsync(targetUser);
    if (!deleteResult.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", deleteResult.Errors.Select(error => error.Description)), ErrorCode = "delete_failed" });
    }

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = targetUser.Id,
        IsEnabled = false,
        Message = "User deleted."
    });
});

app.MapPost("/api/admin/users/{userId}/reset-password", async Task<IResult> (string userId, ResetPasswordRequestDto request, UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A new password is required.", ErrorCode = "invalid_request" });
    }

    var targetUser = await userManager.FindByIdAsync(userId);
    if (targetUser is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "User was not found.", ErrorCode = "not_found" });
    }

    var passwordToken = await userManager.RemovePasswordAsync(targetUser);
    if (!passwordToken.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", passwordToken.Errors.Select(error => error.Description)), ErrorCode = "password_update_failed" });
    }

    var addPasswordResult = await userManager.AddPasswordAsync(targetUser, request.NewPassword);
    if (!addPasswordResult.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", addPasswordResult.Errors.Select(error => error.Description)), ErrorCode = "password_update_failed" });
    }

    targetUser.MustChangePassword = request.ForcePasswordChange;
    await userManager.UpdateAsync(targetUser);

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = targetUser.Id,
        IsEnabled = targetUser.IsEnabled,
        Message = request.ForcePasswordChange ? "Password reset and password change required." : "Password reset."
    });
});

app.MapPost("/api/admin/users/{userId}/force-password-change", async Task<IResult> (string userId, ForcePasswordChangeRequestDto request, UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) || role.Equals("CompanyAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var targetUser = await userManager.FindByIdAsync(userId);
    if (targetUser is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "User was not found.", ErrorCode = "not_found" });
    }

    targetUser.MustChangePassword = request.RequirePasswordChange;
    var result = await userManager.UpdateAsync(targetUser);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = string.Join("; ", result.Errors.Select(error => error.Description)), ErrorCode = "update_failed" });
    }

    return Results.Ok(new AdminUserActionResponseDto
    {
        UserId = targetUser.Id,
        IsEnabled = targetUser.IsEnabled,
        Message = request.RequirePasswordChange ? "Password change required on next sign-in." : "Password change requirement cleared."
    });
});

app.MapGet("/api/admin/storage-diagnostics", async Task<IResult> (UserManager<ApplicationUser> userManager, HttpContext httpContext) =>
{
    var currentUser = await GetCurrentUserAsync(userManager, httpContext);
    if (currentUser is null)
    {
        return Results.Unauthorized();
    }

    var currentRoles = (await userManager.GetRolesAsync(currentUser)).ToList();
    if (!currentRoles.Any(role => role.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Json(new ApiErrorResponse { Message = "You do not have permission to access this resource.", ErrorCode = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var storageRoot = JsonUploadedDumpStore.ResolveStorageRoot();
    var databasePath = new SqliteDatabasePathResolver().Resolve("Data Source=mercedes-eis-auth.db", AppContext.BaseDirectory, NullLogger.Instance);
    var diagnostics = new StorageDiagnosticsResponseDto
    {
        StorageRoot = storageRoot,
        DatabasePath = databasePath,
        Environment = builder.Environment.EnvironmentName,
        TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
        IsWritable = true,
        ErrorMessage = null
    };

    try
    {
        Directory.CreateDirectory(storageRoot);
        var filePath = Path.Combine(storageRoot, ".write-test");
        await File.WriteAllTextAsync(filePath, "ok");
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup when another process is holding the file briefly.
            }
        }

        diagnostics.IsWritable = true;
    }
    catch (Exception ex)
    {
        diagnostics.IsWritable = false;
        diagnostics.ErrorMessage = ex.Message;
    }

    return Results.Ok(diagnostics);
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

app.MapPost("/api/bulk-consume/preview", async Task<IResult> (BulkConsumePreviewRequest request, BulkConsumeService bulkConsumeService) =>
{
    try
    {
        var response = await bulkConsumeService.PreviewAsync(request.SourceFolderPath, request.IncludeSubdirectories);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = ex.Message, ErrorCode = "bulk_consume_preview_failed" });
    }
});

app.MapPost("/api/bulk-consume/import", async Task<IResult> (BulkConsumeImportRequest request, BulkConsumeService bulkConsumeService, ICurrentUser currentUser) =>
{
    try
    {
        var response = await bulkConsumeService.ImportAsync(request, currentUser);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = ex.Message, ErrorCode = "bulk_consume_import_failed" });
    }
});

app.MapPost("/api/bulk-consume/files", async Task<IResult> (IFormFile? file, [FromForm] string? registrationNumber, [FromForm] string? vehicleIdentifier, [FromForm] string? customerName, [FromForm] string? originalSourceFolderName, [FromForm] string? originalSourceRelativePath, [FromForm] string? sha256, [FromForm] string? classification, ILicenseService licenseService, IUploadedDumpStore uploadedDumpStore, IEisAnalysisService analysisService, IKeyFileAnalysisService keyFileAnalysisService, ILoggerFactory loggerFactory, HttpContext httpContext, ICurrentUser currentUser, CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A dump file is required.", ErrorCode = "missing_file", RequestId = httpContext.TraceIdentifier });
    }

    var license = licenseService.CheckFeature(FeatureName.AnalyzeDump);
    if (!license.IsGranted)
    {
        return Results.Forbid();
    }

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream, cancellationToken);
    var bytes = stream.ToArray();
    var computedSha = ComputeSha256(bytes);
    if (!string.IsNullOrWhiteSpace(sha256) && !string.Equals(computedSha, sha256, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new ApiErrorResponse { Message = "Uploaded file hash does not match the client-provided hash.", ErrorCode = "sha256_mismatch", RequestId = httpContext.TraceIdentifier });
    }

    var keyFileAnalysis = keyFileAnalysisService.Analyze(bytes, file.FileName);
    var isKeyFile = string.Equals(keyFileAnalysis.DetectedFormat, "CGMB key file", StringComparison.OrdinalIgnoreCase) && string.Equals(keyFileAnalysis.DetectionConfidence, "Verified", StringComparison.OrdinalIgnoreCase);
    if (!isKeyFile && bytes.Length != 256)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Mercedes EIS dumps must be exactly 256 bytes, or a verified CGMB key file.", ErrorCode = "invalid_size", RequestId = httpContext.TraceIdentifier });
    }

    if (!isKeyFile && string.IsNullOrWhiteSpace(vehicleIdentifier))
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A vehicle identifier is required for bulk-consume uploads.", ErrorCode = "missing_vehicle_identifier", RequestId = httpContext.TraceIdentifier });
    }

    var savedUpload = await uploadedDumpStore.PersistAsync(bytes, file.FileName, vehicleIdentifier ?? string.Empty, registrationNumber ?? string.Empty, "bulk-consume", analysisService, currentUser, isKeyFile ? FileCategory.KeyFile : FileCategory.EisDump, customerName);
    if (isKeyFile)
    {
        await uploadedDumpStore.AnalyzeAndStoreKeyFileAsync(savedUpload.Id, keyFileAnalysisService, currentUser);
    }

    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=bulk-consume-upload requestId={RequestId} success=true fileName={FileName} sha256={Sha256}", httpContext.TraceIdentifier, file.FileName, computedSha);
    return Results.Ok(new UploadDumpResponse
    {
        FileName = file.FileName,
        Status = "Uploaded",
        Sha256 = computedSha,
        FileSizeBytes = bytes.Length,
        UploadId = savedUpload.Id,
        CustomerName = savedUpload.CustomerName,
        Message = "Bulk consume upload complete."
    });
});

app.MapPost("/api/files/upload", async Task<IResult> (IFormFile? file, [FromForm] string? userProvidedVin, [FromForm] string? userProvidedRegistrationNumber, [FromForm] bool vehicleIdentifierConfirmed, [FromForm] string? customerName, ILicenseService licenseService, IUploadedDumpStore uploadedDumpStore, IEisAnalysisService analysisService, IKeyFileAnalysisService keyFileAnalysisService, ILoggerFactory loggerFactory, HttpContext httpContext, ICurrentUser currentUser, CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A dump file is required.", ErrorCode = "missing_file", RequestId = httpContext.TraceIdentifier });
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

    var keyFileAnalysis = keyFileAnalysisService.Analyze(bytes, file.FileName);
    var isKeyFile = string.Equals(keyFileAnalysis.DetectedFormat, "CGMB key file", StringComparison.OrdinalIgnoreCase) && string.Equals(keyFileAnalysis.DetectionConfidence, "Verified", StringComparison.OrdinalIgnoreCase);

    if (!isKeyFile && bytes.Length != 256)
    {
        loggerFactory.CreateLogger("MercedesEISTool.Server").LogWarning("operation=upload requestId={RequestId} success=false fileSize={FileSize} sha256={Sha256} reason=invalid_size", httpContext.TraceIdentifier, bytes.Length, sha);
        return Results.BadRequest(new ApiErrorResponse { Message = "Mercedes EIS dumps must be exactly 256 bytes, or a verified CGMB key file.", ErrorCode = "invalid_size", RequestId = httpContext.TraceIdentifier });
    }

    if (!vehicleIdentifierConfirmed && !isKeyFile)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "Confirmation is required before upload.", ErrorCode = "confirmation_required", RequestId = httpContext.TraceIdentifier });
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
        savedUpload = await uploadedDumpStore.PersistAsync(bytes, file.FileName, userProvidedVin ?? string.Empty, userProvidedRegistrationNumber ?? string.Empty, "upload", analysisService, currentUser, isKeyFile ? FileCategory.KeyFile : FileCategory.EisDump, customerName);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = ex.Message, ErrorCode = "invalid_upload_metadata", RequestId = httpContext.TraceIdentifier });
    }

    if (isKeyFile)
    {
        await uploadedDumpStore.AnalyzeAndStoreKeyFileAsync(savedUpload.Id, keyFileAnalysisService, currentUser);
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
        CustomerName = savedUpload.CustomerName,
        Message = validation.Message,
        AnalysisDetails = analysisDetails
    });
}).DisableAntiforgery();

app.MapPost("/api/key-files/analyze", async Task<IResult> (IFormFile? file, ILicenseService licenseService, IKeyFileAnalysisService keyFileAnalysisService, ILoggerFactory loggerFactory, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "A key file is required.", ErrorCode = "missing_file", RequestId = httpContext.TraceIdentifier });
    }

    if (file.Length > 1_048_576)
    {
        return Results.BadRequest(new ApiErrorResponse { Message = "The uploaded key file must be no larger than 1 MB.", ErrorCode = "file_too_large", RequestId = httpContext.TraceIdentifier });
    }

    var license = licenseService.CheckFeature(FeatureName.AnalyzeDump);
    if (!license.IsGranted)
    {
        return Results.Forbid();
    }

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream, cancellationToken);
    var bytes = stream.ToArray();
    var result = keyFileAnalysisService.Analyze(bytes, file.FileName);
    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=key-file-analyze requestId={RequestId} success=true fileSize={FileSize}", httpContext.TraceIdentifier, bytes.Length);
    return Results.Ok(result);
}).DisableAntiforgery();

app.MapPost("/api/files/{storedFileId:guid}/analyze-key", async Task<IResult> (Guid storedFileId, ILicenseService licenseService, IUploadedDumpStore uploadedDumpStore, IKeyFileAnalysisService keyFileAnalysisService, ILoggerFactory loggerFactory, HttpContext httpContext, ICurrentUser currentUser, CancellationToken cancellationToken) =>
{
    var license = licenseService.CheckFeature(FeatureName.AnalyzeDump, currentUser);
    if (!license.IsGranted)
    {
        return Results.Forbid();
    }

    var record = await uploadedDumpStore.GetByIdAsync(storedFileId, currentUser);
    if (record is null)
    {
        return Results.NotFound(new ApiErrorResponse { Message = "Stored file was not found.", ErrorCode = "not_found", RequestId = httpContext.TraceIdentifier });
    }

    var bytes = await uploadedDumpStore.ReadStoredFileAsync(storedFileId, currentUser);
    var result = keyFileAnalysisService.Analyze(bytes, record.FileName);
    await uploadedDumpStore.AnalyzeAndStoreKeyFileAsync(storedFileId, keyFileAnalysisService, currentUser);
    loggerFactory.CreateLogger("MercedesEISTool.Server").LogInformation("operation=key-file-reanalyze requestId={RequestId} success=true storedFileId={StoredFileId}", httpContext.TraceIdentifier, storedFileId);
    return Results.Ok(result);
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

static async Task RepairSchemaCompatibilityAsync(ApplicationDbContext dbContext, ILogger logger)
{
    await dbContext.Database.OpenConnectionAsync();
    try
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Organizations (
                Id TEXT NOT NULL CONSTRAINT PK_Organizations PRIMARY KEY,
                Name TEXT NOT NULL,
                ContactEmail TEXT NULL,
                Country TEXT NULL,
                IsActive INTEGER NOT NULL,
                LicenseType TEXT NULL,
                LicenseExpirationUtc TEXT NULL,
                MaxUsers INTEGER NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
        ";
        await command.ExecuteNonQueryAsync();

        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS AspNetUsers (
                Id TEXT NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
                UserName TEXT NULL,
                NormalizedUserName TEXT NULL,
                Email TEXT NULL,
                NormalizedEmail TEXT NULL,
                EmailConfirmed INTEGER NOT NULL,
                PasswordHash TEXT NULL,
                SecurityStamp TEXT NULL,
                ConcurrencyStamp TEXT NULL,
                PhoneNumber TEXT NULL,
                PhoneNumberConfirmed INTEGER NOT NULL,
                TwoFactorEnabled INTEGER NOT NULL,
                LockoutEnd TEXT NULL,
                LockoutEnabled INTEGER NOT NULL,
                AccessFailedCount INTEGER NOT NULL,
                DisplayName TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL,
                LastLoginAtUtc TEXT NULL,
                OrganizationId TEXT NULL,
                MustChangePassword INTEGER NOT NULL
            );
        ";
        await command.ExecuteNonQueryAsync();

        command.CommandText = "ALTER TABLE AspNetUsers ADD COLUMN OrganizationId TEXT";
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("startup self-test schema_repair duplicate_column=OrganizationId");
        }

        command.CommandText = "ALTER TABLE AspNetUsers ADD COLUMN MustChangePassword INTEGER";
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("startup self-test schema_repair duplicate_column=MustChangePassword");
        }

        command.CommandText = @"
            UPDATE AspNetUsers
            SET EmailConfirmed = COALESCE(EmailConfirmed, 0),
                PhoneNumberConfirmed = COALESCE(PhoneNumberConfirmed, 0),
                TwoFactorEnabled = COALESCE(TwoFactorEnabled, 0),
                LockoutEnabled = COALESCE(LockoutEnabled, 0),
                AccessFailedCount = COALESCE(AccessFailedCount, 0),
                IsEnabled = COALESCE(IsEnabled, 1),
                MustChangePassword = COALESCE(MustChangePassword, 0)
            WHERE EmailConfirmed IS NULL
               OR PhoneNumberConfirmed IS NULL
               OR TwoFactorEnabled IS NULL
               OR LockoutEnabled IS NULL
               OR AccessFailedCount IS NULL
               OR IsEnabled IS NULL
               OR MustChangePassword IS NULL;
        ";
        await command.ExecuteNonQueryAsync();

        command.CommandText = @"
            CREATE INDEX IF NOT EXISTS IX_AspNetUsers_OrganizationId ON AspNetUsers (OrganizationId);
            CREATE INDEX IF NOT EXISTS IX_AspNetUsers_NormalizedUserName ON AspNetUsers (NormalizedUserName);
            CREATE INDEX IF NOT EXISTS IX_AspNetUsers_NormalizedEmail ON AspNetUsers (NormalizedEmail);
        ";
        await command.ExecuteNonQueryAsync();
    }
    finally
    {
        await dbContext.Database.CloseConnectionAsync();
    }
}

static string ComputeSha256(byte[] bytes)
{
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(bytes));
}

static async Task<ApplicationUser?> GetCurrentUserAsync(UserManager<ApplicationUser> userManager, HttpContext httpContext)
{
    var authHeader = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        return null;
    }

    var token = authHeader.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);
    return await userManager.Users.FirstOrDefaultAsync(u => u.Id == token);
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
        CustomerName = string.IsNullOrWhiteSpace(record.CustomerName) ? null : record.CustomerName,
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
        CustomerName = string.IsNullOrWhiteSpace(record.CustomerName) ? null : record.CustomerName,
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
