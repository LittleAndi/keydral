using System.Text.RegularExpressions;
using Keydral.API.Models;
using Keydral.Storage.Entities;

namespace Keydral.API.Search;

/// <summary>
/// Shared search/filter helpers used by dedicated and DSL search endpoints.
/// </summary>
public static class SearchFilterService
{
    public static IEnumerable<Secret> FilterSecrets(IEnumerable<Secret> secrets, SecretSearchRequest request)
    {
        var query = secrets.Where(secret => !secret.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var terms = SplitTerms(request.Query);
            query = query.Where(secret => terms.All(term =>
                ContainsIgnoreCase(secret.Name, term)
                || ContainsIgnoreCase(secret.Description, term)
                || ContainsIgnoreCase(secret.Tags, term)));
        }

        if (!string.IsNullOrWhiteSpace(request.NamePattern))
        {
            var regex = WildcardToRegex(request.NamePattern);
            query = query.Where(secret => regex.IsMatch(secret.Name));
        }

        if (request.Tags.Count > 0)
        {
            query = query.Where(secret => request.Tags.All(tag => HasTag(secret.Tags, tag)));
        }

        if (request.CreatedAfter.HasValue)
        {
            query = query.Where(secret => secret.CreatedAt >= request.CreatedAfter.Value);
        }

        if (request.CreatedBefore.HasValue)
        {
            query = query.Where(secret => secret.CreatedAt <= request.CreatedBefore.Value);
        }

        if (request.UpdatedAfter.HasValue)
        {
            query = query.Where(secret => secret.UpdatedAt >= request.UpdatedAfter.Value);
        }

        if (request.UpdatedBefore.HasValue)
        {
            query = query.Where(secret => secret.UpdatedAt <= request.UpdatedBefore.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            query = query.Where(secret => ContainsIgnoreCase(secret.CreatedBy, request.CreatedBy));
        }

        return query.OrderByDescending(secret => secret.UpdatedAt);
    }

    public static IEnumerable<AuditLog> FilterAuditLogs(IEnumerable<AuditLog> auditLogs, AuditLogSearchRequest request)
    {
        var query = auditLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var terms = SplitTerms(request.Query);
            query = query.Where(log => terms.All(term =>
                ContainsIgnoreCase(log.Actor, term)
                || ContainsIgnoreCase(log.Action, term)
                || ContainsIgnoreCase(log.ResourceType, term)
                || ContainsIgnoreCase(log.ResourceId, term)
                || ContainsIgnoreCase(log.ResourceName, term)
                || ContainsIgnoreCase(log.ErrorMessage, term)
                || ContainsIgnoreCase(log.Metadata, term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Actor))
        {
            query = query.Where(log => ContainsIgnoreCase(log.Actor, request.Actor));
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(log => string.Equals(log.Action, request.Action, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Result))
        {
            query = query.Where(log => string.Equals(log.Result, request.Result, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            query = query.Where(log => string.Equals(log.ResourceType, request.ResourceType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
        {
            query = query.Where(log => ContainsIgnoreCase(log.ResourceId, request.ResourceId));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(log => log.Timestamp >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(log => log.Timestamp <= request.ToDate.Value);
        }

        return query.OrderByDescending(log => log.Timestamp);
    }

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

    private static bool ContainsIgnoreCase(string? source, string candidate)
    {
        return !string.IsNullOrWhiteSpace(source)
            && source.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> SplitTerms(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool HasTag(string? tags, string candidate)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(tag => tag.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern.Trim()).Replace(@"\*", ".*");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
