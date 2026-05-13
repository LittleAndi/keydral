using Keydral.API.Models;
using Keydral.Storage.Entities;

namespace Keydral.API.Search;

/// <summary>
/// Shared search/filter helpers used by dedicated and DSL search endpoints.
/// </summary>
public static class SearchFilterService
{
    public static PaginatedResponse<T> Paginate<T>(IEnumerable<T> items, int pageNumber, int pageSize)
    {
        var safePageNumber = Math.Max(1, pageNumber);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var materialized = items.ToList();

        return new PaginatedResponse<T>
        {
            Items = materialized
                .Skip((safePageNumber - 1) * safePageSize)
                .Take(safePageSize)
                .ToList(),
            PageNumber = safePageNumber,
            PageSize = safePageSize,
            TotalCount = materialized.Count
        };
    }

    public static SecretListItemResponse ToSecretListItem(Secret secret)
    {
        return new SecretListItemResponse
        {
            Id = secret.Id,
            Name = secret.Name,
            Description = secret.Description,
            Version = secret.CurrentVersion,
            UpdatedAt = secret.UpdatedAt,
            Tags = secret.Tags
        };
    }

    public static AuditLogResponse ToAuditLogResponse(AuditLog auditLog)
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
