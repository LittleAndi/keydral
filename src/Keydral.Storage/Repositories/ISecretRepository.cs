using Keydral.Storage.Entities;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Repository interface for Secret entity with specialized queries.
/// </summary>
public interface ISecretRepository : IRepository<Secret>
{
    /// <summary>
    /// Get a secret by name.
    /// </summary>
    Task<Secret?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all non-deleted secrets.
    /// </summary>
    Task<IEnumerable<Secret>> GetActiveSecretsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get non-deleted secrets matching advanced search filters.
    /// Filtering is applied in the database before results are materialized.
    /// </summary>
    Task<IEnumerable<Secret>> GetSecretsFilteredAsync(
        string? query,
        string? namePattern,
        IReadOnlyCollection<string>? tags,
        DateTime? createdAfter,
        DateTime? createdBefore,
        DateTime? updatedAfter,
        DateTime? updatedBefore,
        string? createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get secrets matching a path pattern (with wildcard support).
    /// </summary>
    Task<IEnumerable<Secret>> GetSecretsByPathPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a secret name exists (ignoring soft-deletes).
    /// </summary>
    Task<bool> SecretNameExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all versions of a secret.
    /// </summary>
    Task<IEnumerable<SecretVersion>> GetSecretVersionsAsync(Guid secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific version of a secret.
    /// </summary>
    Task<SecretVersion?> GetSecretVersionAsync(Guid secretId, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new version of a secret.
    /// </summary>
    Task<SecretVersion> AddSecretVersionAsync(SecretVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete a secret (mark as deleted without removing from DB).
    /// </summary>
    Task SoftDeleteSecretAsync(Guid secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get secrets created or updated within a time range.
    /// </summary>
    Task<IEnumerable<Secret>> GetSecretsByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
