using Microsoft.EntityFrameworkCore;
using Keydral.Storage.Entities;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Repository implementation for Secret entity with specialized query logic.
/// </summary>
public class SecretRepository : Repository<Secret>, ISecretRepository
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public SecretRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get a secret by name.
    /// </summary>
    public async Task<Secret?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Context.Secrets
            .Where(s => s.Name == name && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get all non-deleted secrets.
    /// </summary>
    public async Task<IEnumerable<Secret>> GetActiveSecretsAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Secrets
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get secrets matching a path pattern (with wildcard support).
    /// Supports patterns like "/team-a/*", "/team-a/db-*", "/admin/**".
    /// </summary>
    public async Task<IEnumerable<Secret>> GetSecretsByPathPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (pattern == "*")
        {
            return await GetActiveSecretsAsync(cancellationToken);
        }

        // Simple glob pattern matching
        var regexPattern = pattern
            .Replace(".", @"\.")
            .Replace("**", ".*")
            .Replace("*", "[^/]*");

        var regex = new System.Text.RegularExpressions.Regex($"^{regexPattern}$");

        return await Context.Secrets
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken)
            .ContinueWith(task =>
            {
                return task.Result.Where(s => regex.IsMatch(s.Name)).ToList() as IEnumerable<Secret>;
            }, cancellationToken);
    }

    /// <summary>
    /// Check if a secret name exists (ignoring soft-deletes).
    /// </summary>
    public async Task<bool> SecretNameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Context.Secrets
            .AnyAsync(s => s.Name == name && !s.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Get all versions of a secret.
    /// </summary>
    public async Task<IEnumerable<SecretVersion>> GetSecretVersionsAsync(Guid secretId, CancellationToken cancellationToken = default)
    {
        return await Context.SecretVersions
            .Where(v => v.SecretId == secretId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get a specific version of a secret.
    /// </summary>
    public async Task<SecretVersion?> GetSecretVersionAsync(Guid secretId, int versionNumber, CancellationToken cancellationToken = default)
    {
        return await Context.SecretVersions
            .FirstOrDefaultAsync(v => v.SecretId == secretId && v.VersionNumber == versionNumber, cancellationToken);
    }

    /// <summary>
    /// Create a new version of a secret.
    /// </summary>
    public async Task<SecretVersion> AddSecretVersionAsync(SecretVersion version, CancellationToken cancellationToken = default)
    {
        var entry = await Context.SecretVersions.AddAsync(version, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// Soft-delete a secret (mark as deleted without removing from DB).
    /// </summary>
    public async Task SoftDeleteSecretAsync(Guid secretId, CancellationToken cancellationToken = default)
    {
        var secret = await GetByIdAsync(secretId, cancellationToken);
        if (secret != null)
        {
            secret.IsDeleted = true;
            secret.DeletedAt = DateTime.UtcNow;
            await UpdateAsync(secret, cancellationToken);
        }
    }

    /// <summary>
    /// Get secrets created or updated within a time range.
    /// </summary>
    public async Task<IEnumerable<Secret>> GetSecretsByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await Context.Secrets
            .Where(s => !s.IsDeleted &&
                    ((s.CreatedAt >= from && s.CreatedAt <= to) ||
                     (s.UpdatedAt >= from && s.UpdatedAt <= to)))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
