using Keydral.API.Models;

namespace Keydral.API.Search;

/// <summary>
/// Request contract for secret search and filtering.
/// </summary>
public sealed class SecretSearchRequest
{
    public string? Query { get; set; }

    public string? NamePattern { get; set; }

    public List<string> Tags { get; set; } = [];

    public DateTime? CreatedAfter { get; set; }

    public DateTime? CreatedBefore { get; set; }

    public DateTime? UpdatedAfter { get; set; }

    public DateTime? UpdatedBefore { get; set; }

    public string? CreatedBy { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Request contract for audit log search and filtering.
/// </summary>
public sealed class AuditLogSearchRequest
{
    public string? Query { get; set; }

    public string? Actor { get; set; }

    public string? Action { get; set; }

    public string? Result { get; set; }

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Request contract for the query DSL endpoint.
/// </summary>
public sealed class DslSearchRequest
{
    public string Query { get; set; } = string.Empty;

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Combined search response for the query DSL endpoint.
/// </summary>
public sealed class SearchResultsResponse
{
    public PaginatedResponse<SecretListItemResponse> Secrets { get; set; } = new();

    public PaginatedResponse<AuditLogResponse> AuditLogs { get; set; } = new();
}
