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
    /// Get non-deleted secrets matching advanced search filters.
    /// </summary>
    public async Task<IEnumerable<Secret>> GetSecretsFilteredAsync(
        string? query,
        string? namePattern,
        IReadOnlyCollection<string>? tags,
        DateTime? createdAfter,
        DateTime? createdBefore,
        DateTime? updatedAfter,
        DateTime? updatedBefore,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var secretQuery = Context.Secrets
            .AsNoTracking()
            .Where(secret => !secret.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var term in terms)
            {
                var searchPattern = BuildContainsPattern(term);
                secretQuery = secretQuery.Where(secret =>
                    EF.Functions.ILike(secret.Name, searchPattern)
                    || (secret.Description != null && EF.Functions.ILike(secret.Description, searchPattern))
                    || (secret.Tags != null && EF.Functions.ILike(secret.Tags, searchPattern)));
            }
        }

        if (!string.IsNullOrWhiteSpace(namePattern))
        {
            secretQuery = secretQuery.Where(secret => EF.Functions.ILike(secret.Name, BuildWildcardLikePattern(namePattern)));
        }

        if (tags is { Count: > 0 })
        {
            foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                var exactTagPattern = $"%,{EscapeLikeFragment(tag.Trim())},%";
                secretQuery = secretQuery.Where(secret =>
                    secret.Tags != null
                    && EF.Functions.ILike("," + secret.Tags.Replace(" ", string.Empty) + ",", exactTagPattern));
            }
        }

        if (createdAfter.HasValue)
        {
            secretQuery = secretQuery.Where(secret => secret.CreatedAt >= createdAfter.Value);
        }

        if (createdBefore.HasValue)
        {
            secretQuery = secretQuery.Where(secret => secret.CreatedAt <= createdBefore.Value);
        }

        if (updatedAfter.HasValue)
        {
            secretQuery = secretQuery.Where(secret => secret.UpdatedAt >= updatedAfter.Value);
        }

        if (updatedBefore.HasValue)
        {
            secretQuery = secretQuery.Where(secret => secret.UpdatedAt <= updatedBefore.Value);
        }

        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            secretQuery = secretQuery.Where(secret =>
                secret.CreatedBy != null && EF.Functions.ILike(secret.CreatedBy, BuildContainsPattern(createdBy)));
        }

        return await secretQuery
            .OrderByDescending(secret => secret.UpdatedAt)
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
        var regexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*");

        var regex = new System.Text.RegularExpressions.Regex($"^{regexPattern}$");

        var all = await Context.Secrets
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken);

        return all.Where(s => regex.IsMatch(s.Name));
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

    private static string BuildContainsPattern(string value) => $"%{EscapeLikeFragment(value.Trim())}%";

    private static string BuildWildcardLikePattern(string value)
    {
        return EscapeLikeFragment(value.Trim()).Replace("*", "%");
    }

    private static string EscapeLikeFragment(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }
}
