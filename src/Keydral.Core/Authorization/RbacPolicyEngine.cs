using Keydral.Storage.Repositories;

namespace Keydral.Core.Authorization;

/// <summary>
/// RBAC policy decision result.
/// </summary>
public class PolicyDecision
{
    /// <summary>
    /// Whether the action is allowed.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Reason for the decision (for debugging/logging).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Create an allow decision.
    /// </summary>
    public static PolicyDecision Allow(string reason = "Allowed by policy") =>
        new() { IsAllowed = true, Reason = reason };

    /// <summary>
    /// Create a deny decision.
    /// </summary>
    public static PolicyDecision Deny(string reason = "No policy allows this action") =>
        new() { IsAllowed = false, Reason = reason };
}

/// <summary>
/// Service for evaluating RBAC policies.
/// </summary>
public interface IRbacPolicyEngine
{
    /// <summary>
    /// Evaluate if a principal can perform an action on a resource.
    /// </summary>
    /// <param name="principalId">User or group identifier</param>
    /// <param name="resourcePath">Resource path pattern (e.g., /team-a/secret)</param>
    /// <param name="action">Action to perform (e.g., secrets:read)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Policy decision with allow/deny and reason</returns>
    Task<PolicyDecision> EvaluateAsync(
        string principalId,
        string resourcePath,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a principal has explicit permission for an action.
    /// </summary>
    Task<bool> CanPerformAsync(
        string principalId,
        string resourcePath,
        string action,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of RBAC policy evaluation.
/// Uses glob pattern matching for resource paths.
/// </summary>
public class RbacPolicyEngine : IRbacPolicyEngine
{
    private readonly IPolicyRepository _policyRepository;

    public RbacPolicyEngine(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
    }

    /// <summary>
    /// Evaluate if a principal can perform an action on a resource.
    /// </summary>
    public async Task<PolicyDecision> EvaluateAsync(
        string principalId,
        string resourcePath,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(principalId))
            return PolicyDecision.Deny("Missing principal ID");

        if (string.IsNullOrWhiteSpace(resourcePath))
            return PolicyDecision.Deny("Missing resource path");

        if (string.IsNullOrWhiteSpace(action))
            return PolicyDecision.Deny("Missing action");

        // Get applicable policies for this principal and resource
        var applicablePolicies = await _policyRepository.GetApplicablePoliciesAsync(
            principalId, resourcePath, cancellationToken);

        // Check for explicit deny first (deny takes precedence)
        var denyPolicy = applicablePolicies.FirstOrDefault(p => p.Effect == "Deny");
        if (denyPolicy != null && IsActionInPolicy(denyPolicy, action))
        {
            return PolicyDecision.Deny($"Denied by policy: {denyPolicy.Id}");
        }

        // Check for allow
        var allowPolicy = applicablePolicies.FirstOrDefault(p => p.Effect == "Allow" && IsActionInPolicy(p, action));
        if (allowPolicy != null)
        {
            return PolicyDecision.Allow($"Allowed by policy: {allowPolicy.Id}");
        }

        return PolicyDecision.Deny("No policy allows this action");
    }

    /// <summary>
    /// Check if a principal has explicit permission for an action.
    /// </summary>
    public async Task<bool> CanPerformAsync(
        string principalId,
        string resourcePath,
        string action,
        CancellationToken cancellationToken = default)
    {
        var decision = await EvaluateAsync(principalId, resourcePath, action, cancellationToken);
        return decision.IsAllowed;
    }

    /// <summary>
    /// Check if an action is included in a policy's actions.
    /// Supports wildcards (e.g., "secrets:*" matches "secrets:read").
    /// </summary>
    private static bool IsActionInPolicy(Storage.Entities.Policy policy, string action)
    {
        if (policy?.Actions == null)
            return false;

        var allowedActions = policy.Actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var allowedAction in allowedActions)
        {
            // Exact match
            if (allowedAction.Equals(action, StringComparison.OrdinalIgnoreCase))
                return true;

            // Wildcard match (e.g., "secrets:*" matches "secrets:read")
            if (allowedAction.EndsWith("*"))
            {
                var prefix = allowedAction[..^1]; // Remove the "*"
                if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
