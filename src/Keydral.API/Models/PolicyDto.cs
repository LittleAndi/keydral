namespace Keydral.API.Models;

/// <summary>
/// Request DTO for creating or updating an RBAC policy.
/// </summary>
public class CreateUpdatePolicyRequest
{
    /// <summary>
    /// Human-readable policy name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Policy description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Principal (user/group/role) this applies to.
    /// Format: "user:john", "group:team-a", "role:admin", "*" for all.
    /// </summary>
    public string Principal { get; set; } = string.Empty;

    /// <summary>
    /// Resource path pattern (e.g., "/team-a/*", "/admin/**", "*").
    /// </summary>
    public string ResourcePattern { get; set; } = "*";

    /// <summary>
    /// Policy effect: "Allow" or "Deny".
    /// </summary>
    public string Effect { get; set; } = "Allow";

    /// <summary>
    /// Comma-separated list of allowed actions.
    /// Examples: "secrets:read", "secrets:write", "secrets:delete", "secrets:*".
    /// </summary>
    public string Actions { get; set; } = string.Empty;

    /// <summary>
    /// Whether this policy is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Response DTO for an RBAC policy.
/// </summary>
public class PolicyResponse
{
    /// <summary>
    /// Policy identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Policy name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Policy description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Principal this policy applies to.
    /// </summary>
    public string Principal { get; set; } = string.Empty;

    /// <summary>
    /// Resource path pattern.
    /// </summary>
    public string ResourcePattern { get; set; } = string.Empty;

    /// <summary>
    /// Policy effect (Allow/Deny).
    /// </summary>
    public string Effect { get; set; } = "Allow";

    /// <summary>
    /// Comma-separated actions.
    /// </summary>
    public string Actions { get; set; } = string.Empty;

    /// <summary>
    /// Whether policy is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// When the policy was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the policy was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// User who created this policy.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Whether the policy is soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }
}
