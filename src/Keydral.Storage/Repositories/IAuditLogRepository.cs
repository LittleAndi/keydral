using Keydral.Storage.Entities;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Repository interface for AuditLog entity with specialized queries.
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Get audit logs for a specific resource within a date range.
    /// </summary>
    Task<IEnumerable<AuditLog>> GetAuditLogsByResourceAsync(string resourceId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs by actor (user who performed the action).
    /// </summary>
    Task<IEnumerable<AuditLog>> GetAuditLogsByActorAsync(string actor, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs by action type (READ, WRITE, DELETE, etc.).
    /// </summary>
    Task<IEnumerable<AuditLog>> GetAuditLogsByActionAsync(string action, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get failed access attempts (security audit).
    /// </summary>
    Task<IEnumerable<AuditLog>> GetFailedAccessAttemptsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs for a specific secret.
    /// </summary>
    Task<IEnumerable<AuditLog>> GetAuditLogsBySecretAsync(Guid secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paginated audit logs.
    /// </summary>
    Task<(IEnumerable<AuditLog> logs, int totalCount)> GetAuditLogsPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent audit logs (last N entries).
    /// </summary>
    Task<IEnumerable<AuditLog>> GetRecentAuditLogsAsync(int count = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add an audit log entry.
    /// </summary>
    Task<AuditLog> AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken = default);
}
