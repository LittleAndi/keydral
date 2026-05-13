using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Keydral.API.Models;
using Keydral.API.Middleware;
using Keydral.API.RateLimiting;
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
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var (logs, totalCount) = await auditLogRepository.GetAuditLogsFilteredAsync(
                actor, action, resourceId, result, pageNumber, pageSize);

            var response = new PaginatedResponse<AuditLogResponse>
            {
                Items = logs.Select(MapToDto).ToList(),
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
