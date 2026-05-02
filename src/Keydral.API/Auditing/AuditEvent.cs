namespace Keydral.API.Auditing;

/// <summary>
/// Event data captured during request processing for audit trail.
/// </summary>
public class AuditEvent
{
    /// <summary>
    /// Unique request identifier for tracing.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc).
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// Request path/route.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Request query string (without sensitive values).
    /// </summary>
    public string? QueryString { get; set; }

    /// <summary>
    /// IP address of the requester.
    /// </summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// User agent from request headers.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Authenticated user ID (from JWT).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Authenticated username (from JWT).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// HTTP status code response.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Request processing duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Request body size in bytes (if captured).
    /// </summary>
    public long? RequestBodySize { get; set; }

    /// <summary>
    /// Response body size in bytes (if captured).
    /// </summary>
    public long? ResponseBodySize { get; set; }

    /// <summary>
    /// Exception/error message if request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Full exception details in development mode.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// When the request was received.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Request was successful (2xx, 3xx status codes).
    /// </summary>
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 400;

    /// <summary>
    /// Request had client error (4xx status codes).
    /// </summary>
    public bool IsClientError => StatusCode >= 400 && StatusCode < 500;

    /// <summary>
    /// Request had server error (5xx status codes).
    /// </summary>
    public bool IsServerError => StatusCode >= 500;
}
