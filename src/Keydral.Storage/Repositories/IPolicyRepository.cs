using Keydral.Storage.Entities;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Repository interface for Policy entity with specialized queries for RBAC.
/// </summary>
public interface IPolicyRepository : IRepository<Policy>
{
    /// <summary>
    /// Get a policy by name.
    /// </summary>
    Task<Policy?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active (enabled and not deleted) policies.
    /// </summary>
    Task<IEnumerable<Policy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get policies that apply to a specific principal (user/group/role).
    /// </summary>
    Task<IEnumerable<Policy>> GetPoliciesByPrincipalAsync(string principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get policies that apply to a specific resource pattern.
    /// </summary>
    Task<IEnumerable<Policy>> GetPoliciesByResourcePatternAsync(string resourcePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get policies matching both principal and resource.
    /// </summary>
    Task<IEnumerable<Policy>> GetApplicablePoliciesAsync(string principal, string resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get policies by effect (Allow or Deny).
    /// </summary>
    Task<IEnumerable<Policy>> GetPoliciesByEffectAsync(string effect, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a principal can perform an action on a resource.
    /// </summary>
    Task<bool> CanPerformActionAsync(string principal, string action, string resource, CancellationToken cancellationToken = default);
}
