using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Keydral.API.Models;
using Keydral.API.Middleware;
using Keydral.API.RateLimiting;
using Keydral.API.Search;
using Keydral.Storage.Repositories;

namespace Keydral.API.Endpoints;

/// <summary>
/// Extension methods for mapping audit log endpoints.
/// </summary>
public static class AuditLogEndpoints
{
    private const string TagName = "audit";

    public static void MapAuditLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit-logs")
            .WithTags(TagName);

        group.MapGet("/search", SearchAuditLogs)
            .WithName("SearchAuditLogs")
            .WithDescription("Search audit logs with full-text and advanced filters")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetAuditLogsPolicy));

        group.MapGet("/", ListAuditLogs)
            .WithName("ListAuditLogs")
            .WithDescription("List audit log entries with optional filtering")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetAuditLogsPolicy));

        group.MapGet("/{id}", GetAuditLog)
            .WithName("GetAuditLog")
            .WithDescription("Get a specific audit log entry by ID")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetAuditLogsPolicy));
    }

    /// <summary>
    /// List audit log entries with optional filtering and pagination.
    /// </summary>
    private static async Task<Results<Ok<PaginatedResponse<AuditLogResponse>>, ForbidHttpResult, UnauthorizedHttpResult>> ListAuditLogs(
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery(Name = "resource-type")] string? resourceType = null,
        [FromQuery(Name = "resource-id")] string? resourceId = null,
        [FromQuery] string? result = null,
        [FromQuery(Name = "from-date")] DateTime? fromDate = null,
        [FromQuery(Name = "to-date")] DateTime? toDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        ILogger<Program>? logger = null)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var (logs, totalCount) = await auditLogRepository.GetAuditLogsFilteredAsync(
                query,
                actor,
                action,
                resourceType,
                resourceId,
                result,
                fromDate,
                toDate,
                pageNumber,
                pageSize);
            var response = new PaginatedResponse<AuditLogResponse>
            {
                Items = logs.Select(SearchFilterService.ToAuditLogResponse).ToList(),
                PageNumber = Math.Max(1, pageNumber),
                PageSize = Math.Clamp(pageSize, 1, 200),
                TotalCount = totalCount
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error listing audit logs");
            throw;
        }
    }

    /// <summary>
    /// Search audit log entries with full-text search and advanced filters.
    /// </summary>
    private static async Task<Results<Ok<PaginatedResponse<AuditLogResponse>>, ForbidHttpResult, UnauthorizedHttpResult>> SearchAuditLogs(
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery(Name = "resource-type")] string? resourceType = null,
        [FromQuery(Name = "resource-id")] string? resourceId = null,
        [FromQuery] string? result = null,
        [FromQuery(Name = "from-date")] DateTime? fromDate = null,
        [FromQuery(Name = "to-date")] DateTime? toDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        ILogger<Program>? logger = null)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
        {
            return TypedResults.Unauthorized();
        }

        if (!userContext.HasRole("secret-admin"))
        {
            return TypedResults.Forbid();
        }

        try
        {
            var (logs, totalCount) = await auditLogRepository.GetAuditLogsFilteredAsync(
                query,
                actor,
                action,
                resourceType,
                resourceId,
                result,
                fromDate,
                toDate,
                pageNumber,
                pageSize);
            var response = new PaginatedResponse<AuditLogResponse>
            {
                Items = logs.Select(SearchFilterService.ToAuditLogResponse).ToList(),
                PageNumber = Math.Max(1, pageNumber),
                PageSize = Math.Clamp(pageSize, 1, 200),
                TotalCount = totalCount
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error searching audit logs");
            throw;
        }
    }

    /// <summary>
    /// Get a specific audit log entry by ID.
    /// </summary>
    private static async Task<Results<Ok<AuditLogResponse>, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> GetAuditLog(
        Guid id,
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var auditLog = await auditLogRepository.GetByIdAsync(id);
            if (auditLog == null)
                return TypedResults.NotFound();

            return TypedResults.Ok(MapToDto(auditLog));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting audit log {Id}", id);
            throw;
        }
    }

    private static AuditLogResponse MapToDto(Storage.Entities.AuditLog auditLog)
    {
        return SearchFilterService.ToAuditLogResponse(auditLog);
    }
}
