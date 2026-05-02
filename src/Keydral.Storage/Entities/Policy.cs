namespace Keydral.Storage.Entities;

/// <summary>
/// Represents an RBAC policy for secret access control.
/// Defines rules like "team-a can read secrets under /team-a/*".
/// </summary>
public class Policy
{
    /// <summary>
    /// Unique identifier for the policy.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable name for the policy.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this policy governs.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The policy rules in JSON format.
    /// Example:
    /// {
    ///   "effect": "Allow",
    ///   "actions": ["secrets:read"],
    ///   "resources": ["/team-a/*"],
    ///   "principals": ["group:team-a"]
    /// }
    /// </summary>
    public string Rules { get; set; } = string.Empty;

    /// <summary>
    /// The principal (user/group/role) this policy applies to.
    /// Format: "user:john", "group:team-a", "role:admin", etc.
    /// </summary>
    public string Principal { get; set; } = string.Empty;

    /// <summary>
    /// Secret path pattern this policy applies to (e.g., "/team-a/*", "/admin/**", "*").
    /// </summary>
    public string ResourcePattern { get; set; } = "*";

    /// <summary>
    /// Effect of the policy: "Allow" or "Deny".
    /// </summary>
    public string Effect { get; set; } = "Allow";

    /// <summary>
    /// Comma-separated list of actions (e.g., "secrets:read,secrets:write,secrets:delete").
    /// </summary>
    public string Actions { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating if this policy is currently enforced.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Timestamp when the policy was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the policy was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created this policy.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Soft delete flag for audit trail.
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
