namespace Keydral.Storage.Entities;

/// <summary>
/// Represents a user in Keydral.
/// Primarily used to cache user identity from Keycloak locally for role/permission lookups.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user row.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Keycloak user ID (sub claim from token).
    /// </summary>
    public string KeycloakId { get; set; } = string.Empty;

    /// <summary>
    /// Username (username claim from token).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address (email claim from token).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Display name for the user.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Comma-separated list of groups this user belongs to.
    /// Cached from Keycloak for faster policy evaluation.
    /// </summary>
    public string? Groups { get; set; }

    /// <summary>
    /// Comma-separated list of roles assigned to this user.
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Optional metadata (JSON) for storing extended user attributes.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Flag indicating if the user is currently active/enabled.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when this user was first synced/created locally.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this user's cached data was last updated from Keycloak.
    /// </summary>
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the user was last active in Keydral.
    /// </summary>
    public DateTime? LastActivityAt { get; set; }
}
