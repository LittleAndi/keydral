using Microsoft.EntityFrameworkCore;
using Keydral.Storage.Entities;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Repository implementation for Policy entity with specialized queries for RBAC evaluation.
/// </summary>
public class PolicyRepository : Repository<Policy>, IPolicyRepository
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public PolicyRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get a policy by name.
    /// </summary>
    public async Task<Policy?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Context.Policies
            .Where(p => p.Name == name && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get all active (enabled and not deleted) policies.
    /// </summary>
    public async Task<IEnumerable<Policy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Policies
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get policies that apply to a specific principal (user/group/role).
    /// </summary>
    public async Task<IEnumerable<Policy>> GetPoliciesByPrincipalAsync(string principal, CancellationToken cancellationToken = default)
    {
        return await Context.Policies
            .Where(p => p.Principal == principal && p.IsEnabled && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get policies that apply to a specific resource pattern.
    /// </summary>
    public async Task<IEnumerable<Policy>> GetPoliciesByResourcePatternAsync(string resourcePattern, CancellationToken cancellationToken = default)
    {
        return await Context.Policies
            .Where(p => p.ResourcePattern == resourcePattern && p.IsEnabled && !p.IsDeleted)
            .OrderBy(p => p.Principal)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get policies matching both principal and resource.
    /// </summary>
    public async Task<IEnumerable<Policy>> GetApplicablePoliciesAsync(string principal, string resource, CancellationToken cancellationToken = default)
    {
        var allPolicies = await Context.Policies
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        // Filter policies by principal and resource pattern match
        return allPolicies
            .Where(p =>
            {
                // Check if principal matches (exact match or wildcard)
                if (p.Principal != principal && p.Principal != "*")
                    return false;

                // Check if resource matches the pattern
                return MatchesResourcePattern(resource, p.ResourcePattern);
            })
            .OrderBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Get policies by effect (Allow or Deny).
    /// </summary>
    public async Task<IEnumerable<Policy>> GetPoliciesByEffectAsync(string effect, CancellationToken cancellationToken = default)
    {
        return await Context.Policies
            .Where(p => p.Effect == effect && p.IsEnabled && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Check if a principal can perform an action on a resource.
    /// </summary>
    public async Task<bool> CanPerformActionAsync(string principal, string action, string resource, CancellationToken cancellationToken = default)
    {
        var applicablePolicies = await GetApplicablePoliciesAsync(principal, resource, cancellationToken);

        // Check for explicit Deny first (deny takes precedence)
        var denyPolicy = applicablePolicies.FirstOrDefault(p =>
            p.Effect == "Deny" && p.Actions!.Contains(action));

        if (denyPolicy != null)
            return false;

        // Then check for Allow
        var allowPolicy = applicablePolicies.FirstOrDefault(p =>
            p.Effect == "Allow" && p.Actions!.Contains(action));

        return allowPolicy != null;
    }

    /// <summary>
    /// Helper method to match resource against a pattern.
    /// Supports wildcards: * (matches single level), ** (matches multiple levels).
    /// </summary>
    private static bool MatchesResourcePattern(string resource, string pattern)
    {
        if (pattern == "*")
            return true;

        // Convert glob pattern to regex
        var regexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*");

        var regex = new System.Text.RegularExpressions.Regex($"^{regexPattern}$");
        return regex.IsMatch(resource);
    }
}
