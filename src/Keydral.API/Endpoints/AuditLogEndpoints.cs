using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Keydral.API.Models;
using Keydral.API.Middleware;
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

        group.MapGet("/", ListAuditLogs)
            .WithName("ListAuditLogs")
            .WithOpenApi()
            .WithDescription("List audit log entries with optional filtering");

        group.MapGet("/{id}", GetAuditLog)
            .WithName("GetAuditLog")
            .WithOpenApi()
            .WithDescription("Get a specific audit log entry by ID");
    }

    /// <summary>
    /// List audit log entries with optional filtering and pagination.
    /// </summary>
    private static async Task<Ok<PaginatedResponse<AuditLogResponse>>> ListAuditLogs(
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resourceId = null,
        [FromQuery] string? result = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        ILogger<Program>? logger = null)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Ok(new PaginatedResponse<AuditLogResponse>());

        try
        {
            // Only admins can see full audit logs
            // Regular users can only see logs for resources they have access to
            bool isAdmin = userContext.HasRole("secret-admin");

            // Fetch audit logs based on filters
            var auditLogs = await auditLogRepository.GetAllAsync();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(actor))
                auditLogs = auditLogs.Where(a => a.Actor.Contains(actor));

            if (!string.IsNullOrWhiteSpace(action))
                auditLogs = auditLogs.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(resourceId))
                auditLogs = auditLogs.Where(a => a.ResourceId == resourceId);

            if (!string.IsNullOrWhiteSpace(result))
                auditLogs = auditLogs.Where(a => a.Result == result);

            // Sort by timestamp descending
            var sortedLogs = auditLogs
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            // Pagination
            var totalCount = sortedLogs.Count;
            var paginatedLogs = sortedLogs
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PaginatedResponse<AuditLogResponse>
            {
                Items = paginatedLogs.Select(MapToDto).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
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
    /// Get a specific audit log entry by ID.
    /// </summary>
    private static async Task<Results<Ok<AuditLogResponse>, NotFound>> GetAuditLog(
        Guid id,
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.NotFound();

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
        return new AuditLogResponse
        {
            Id = auditLog.Id,
            Action = auditLog.Action,
            Actor = auditLog.Actor,
            ResourceType = auditLog.ResourceType,
            ResourceId = auditLog.ResourceId,
            Result = auditLog.Result,
            StatusCode = auditLog.StatusCode,
            ErrorMessage = auditLog.ErrorMessage,
            SourceIp = auditLog.SourceIp,
            UserAgent = auditLog.UserAgent,
            Timestamp = auditLog.Timestamp
        };
    }
}
