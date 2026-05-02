using System.Diagnostics;
using System.Text;
using Keydral.API.Middleware;
using Keydral.Storage.Entities;
using Keydral.Storage.Repositories;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace Keydral.API.Auditing;

/// <summary>
/// Configuration options for audit logging middleware.
/// </summary>
public class AuditLoggingOptions
{
    public bool Enabled { get; set; } = true;
    public bool LogRequestBody { get; set; } = false;
    public bool LogResponseBody { get; set; } = false;
    public List<string> IgnorePaths { get; set; } = new();
    public List<string> SensitiveDataPatterns { get; set; } = new();
}

/// <summary>
/// Middleware for comprehensive HTTP request/response auditing.
/// Logs all API calls to Serilog and AuditLog database table.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuditLoggingOptions _options;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, IOptions<AuditLoggingOptions> options, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogRepository auditRepository)
    {
        if (!_options.Enabled || ShouldIgnorePath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var auditEvent = new AuditEvent
        {
            TraceId = context.TraceIdentifier,
            HttpMethod = context.Request.Method,
            Path = context.Request.Path.Value ?? string.Empty,
            QueryString = SanitizeQueryString(context.Request.QueryString.Value),
            SourceIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers["User-Agent"].ToString(),
            Timestamp = DateTime.UtcNow
        };

        // Extract user context if available
        var userContext = context.GetUserContext();
        if (userContext != null)
        {
            auditEvent.UserId = userContext.Id;
            auditEvent.Username = userContext.Username;
        }

        // Capture request body if enabled (for debugging)
        if (_options.LogRequestBody && context.Request.ContentLength.GetValueOrDefault(0) > 0)
        {
            auditEvent.RequestBodySize = context.Request.ContentLength ?? 0;
        }

        // Wrap response stream to capture output
        var originalBodyStream = context.Response.Body;
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Process request through pipeline
            await _next(context);

            stopwatch.Stop();
            auditEvent.DurationMs = stopwatch.ElapsedMilliseconds;
            auditEvent.StatusCode = context.Response.StatusCode;

            // Capture response size
            if (context.Response.ContentLength.HasValue)
            {
                auditEvent.ResponseBodySize = context.Response.ContentLength.Value;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            auditEvent.DurationMs = stopwatch.ElapsedMilliseconds;
            auditEvent.StatusCode = context.Response.StatusCode;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Exception = ex.ToString();

            _logger.LogError(ex, "Request processing failed: {Path}", auditEvent.Path);
            throw;
        }
        finally
        {
            // Copy response back to original stream
            responseStream.Position = 0;
            await responseStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            // Log to Serilog with structured data
            using (LogContext.PushProperty("AuditEvent", auditEvent, destructureObjects: true))
            {
                if (auditEvent.IsSuccess)
                {
                    _logger.LogInformation(
                        "{HttpMethod} {Path} {StatusCode} {Duration}ms",
                        auditEvent.HttpMethod, auditEvent.Path, auditEvent.StatusCode, auditEvent.DurationMs);
                }
                else if (auditEvent.IsClientError)
                {
                    _logger.LogWarning(
                        "{HttpMethod} {Path} {StatusCode} {Duration}ms - Client Error",
                        auditEvent.HttpMethod, auditEvent.Path, auditEvent.StatusCode, auditEvent.DurationMs);
                }
                else if (auditEvent.IsServerError)
                {
                    _logger.LogError(
                        "{HttpMethod} {Path} {StatusCode} {Duration}ms - Server Error: {Error}",
                        auditEvent.HttpMethod, auditEvent.Path, auditEvent.StatusCode, auditEvent.DurationMs, auditEvent.ErrorMessage);
                }
            }

            // Save to audit database
            await SaveAuditLogAsync(auditEvent, auditRepository);
        }
    }

    private bool ShouldIgnorePath(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;
        return _options.IgnorePaths.Any(ignorePath =>
            pathValue.StartsWith(ignorePath, StringComparison.OrdinalIgnoreCase));
    }

    private string? SanitizeQueryString(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
            return null;

        // Remove sensitive query parameters
        var sensitiveParams = new[] { "password", "token", "key", "secret", "credential", "apikey" };
        var sanitized = queryString;

        foreach (var param in sensitiveParams)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                $@"({param}=)[^&]*",
                "$1***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return sanitized;
    }

    private async Task SaveAuditLogAsync(AuditEvent auditEvent, IAuditLogRepository auditRepository)
    {
        try
        {
            // Determine resource information from path
            var (resourceType, resourceId, resourceName) = ExtractResourceInfo(auditEvent.Path);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = DetermineAction(auditEvent.HttpMethod, auditEvent.StatusCode),
                ResourceType = resourceType,
                ResourceId = resourceId,
                ResourceName = resourceName,
                Actor = auditEvent.Username ?? "anonymous",
                SourceIp = auditEvent.SourceIp,
                HttpMethod = auditEvent.HttpMethod,
                StatusCode = auditEvent.StatusCode,
                UserAgent = auditEvent.UserAgent,
                Timestamp = auditEvent.Timestamp,
                Result = auditEvent.IsSuccess ? "SUCCESS" : "FAILED",
                ErrorMessage = auditEvent.ErrorMessage,
                DurationMs = auditEvent.DurationMs
            };

            if (auditEvent.UserId != null)
            {
                auditLog.UserId = Guid.TryParse(auditEvent.UserId, out var uid) ? uid : null;
            }

            await auditRepository.AddAsync(auditLog);
            await auditRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Don't fail the request if audit logging fails
            _logger.LogError(ex, "Failed to save audit log for {Path}", auditEvent.Path);
        }
    }

    private (string ResourceType, string ResourceId, string ResourceName) ExtractResourceInfo(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2)
        {
            var resourceType = segments[1].ToUpperInvariant();
            var resourceId = segments.Length > 2 ? System.Net.WebUtility.UrlDecode(segments[2]) : string.Empty;
            var resourceName = segments.Length > 2 ? System.Net.WebUtility.UrlDecode(segments[2]) : string.Empty;

            return (resourceType, resourceId, resourceName);
        }

        return ("API", path, path);
    }

    private string DetermineAction(string httpMethod, int statusCode)
    {
        if (statusCode >= 400)
            return "FAILED_ACCESS";

        return httpMethod.ToUpper() switch
        {
            "GET" => "READ",
            "POST" => "CREATE",
            "PUT" => "UPDATE",
            "DELETE" => "DELETE",
            "PATCH" => "UPDATE",
            _ => "UNKNOWN"
        };
    }
}

/// <summary>
/// Extension methods for configuring audit logging.
/// </summary>
public static class AuditLoggingExtensions
{
    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new AuditLoggingOptions();
        configuration.GetSection("AuditLogging").Bind(options);

        services.Configure<AuditLoggingOptions>(configuration.GetSection("AuditLogging"));

        return services;
    }

    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLoggingMiddleware>();
    }
}
