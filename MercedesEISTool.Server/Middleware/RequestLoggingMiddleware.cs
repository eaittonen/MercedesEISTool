using System.Security.Cryptography;
using System.Text;

namespace MercedesEISTool.Server.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var operation = context.Request.Path.Value ?? "unknown";
        var start = DateTimeOffset.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            var statusCode = context.Response.StatusCode;
            var success = statusCode >= 200 && statusCode < 400;
            _logger.LogInformation("operation={Operation} requestId={RequestId} success={Success} statusCode={StatusCode} elapsedMs={ElapsedMs}", operation, requestId, success, statusCode, (DateTimeOffset.UtcNow - start).TotalMilliseconds);
        }
    }
}
