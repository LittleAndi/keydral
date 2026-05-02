namespace Keydral.API.Models;

/// <summary>
/// Response DTO for an audit log entry.
/// </summary>
public class AuditLogResponse
{
    /// <summary>
    /// Audit log entry identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The action performed (READ, CREATE, UPDATE, DELETE).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// User who performed the action.
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Type of resource (Secret, Policy, etc).
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the resource.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Result of the action (SUCCESS, FAILED).
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code if applicable.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Error message if action failed (never includes secret values).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Source IP address of the request.
    /// </summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// User agent of the client.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// When this action was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Query parameters for audit log filtering.
/// </summary>
public class AuditLogFilterRequest
{
    /// <summary>
    /// Filter by actor (user who performed the action).
    /// </summary>
    public string? Actor { get; set; }

    /// <summary>
    /// Filter by action type.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Filter by resource ID.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Filter by result (SUCCESS or FAILED).
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Start date for time range filter.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for time range filter.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Page number for pagination (1-based).
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size for pagination.
    /// </summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Paginated response for audit logs.
/// </summary>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Items on this page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Current page number.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of items matching the filter.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// Whether there are more pages.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
