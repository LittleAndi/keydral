namespace Keydral.Storage.Entities;

/// <summary>
/// Represents a role in Keydral's RBAC system.
/// Roles define sets of permissions (e.g., secret-reader, secret-writer, secret-admin).
/// </summary>
public class Role
{
    /// <summary>
    /// Unique identifier for the role.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Role name (e.g., "secret-reader", "secret-writer", "secret-admin").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the role.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Comma-separated list of permissions granted by this role.
    /// Examples: "secrets:read", "secrets:write", "secrets:delete", "policies:*", "audit:read".
    /// </summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating if this is a system-defined role (cannot be deleted/modified).
    /// </summary>
    public bool IsSystemRole { get; set; } = false;

    /// <summary>
    /// Flag indicating if this role is currently active/available.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the role was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the role was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created this role.
    /// </summary>
    public string? CreatedBy { get; set; }
}
