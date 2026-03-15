using Microsoft.EntityFrameworkCore;
using Keydral.Storage.Entities;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Repository implementation for AuditLog entity with specialized queries for audit trails.
/// </summary>
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public AuditLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get audit logs for a specific resource within a date range.
    /// </summary>
    public async Task<IEnumerable<AuditLog>> GetAuditLogsByResourceAsync(string resourceId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = Context.AuditLogs
            .Where(a => a.ResourceId == resourceId);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get audit logs by actor (user who performed the action).
    /// </summary>
    public async Task<IEnumerable<AuditLog>> GetAuditLogsByActorAsync(string actor, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = Context.AuditLogs
            .Where(a => a.Actor == actor);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get audit logs by action type (READ, WRITE, DELETE, etc.).
    /// </summary>
    public async Task<IEnumerable<AuditLog>> GetAuditLogsByActionAsync(string action, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = Context.AuditLogs
            .Where(a => a.Action == action);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get failed access attempts (security audit).
    /// </summary>
    public async Task<IEnumerable<AuditLog>> GetFailedAccessAttemptsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = Context.AuditLogs
            .Where(a => a.Result == "FAILED");

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get audit logs for a specific secret.
    /// </summary>
    public async Task<IEnumerable<AuditLog>> GetAuditLogsBySecretAsync(Guid secretId, CancellationToken cancellationToken = default)
    {
        return await Context.AuditLogs
            .Where(a => a.SecretId == secretId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get paginated audit logs.
    /// </summary>
    public async Task<(IEnumerable<AuditLog> logs, int totalCount)> GetAuditLogsPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Context.AuditLogs.AsQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (logs, totalCount);
    }

    /// <summary>
    /// Get recent audit logs (last N entries).
    /// </summary>
    public async Task<IEnumerable<AuditLog>> GetRecentAuditLogsAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        return await Context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Add an audit log entry.
    /// </summary>
    public async Task<AuditLog> AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        return await AddAsync(log, cancellationToken);
    }
}
