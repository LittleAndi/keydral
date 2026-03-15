using System.Security.Claims;
using System.Text.Json;

namespace Keydral.Core.Authentication;

/// <summary>
/// Represents an authenticated user context extracted from JWT claims.
/// </summary>
public class UserContext
{
    /// <summary>
    /// Unique user identifier from Keycloak 'sub' claim.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Username from Keycloak 'preferred_username' claim.
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// Email from Keycloak 'email' claim.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Display name from Keycloak 'name' claim.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// List of realm roles from Keycloak 'realm_access.roles' claim.
    /// </summary>
    public List<string> RealmRoles { get; set; } = new();

    /// <summary>
    /// List of groups from Keycloak 'groups' claim.
    /// </summary>
    public List<string> Groups { get; set; } = new();

    /// <summary>
    /// Determines if the user has a specific role.
    /// </summary>
    public bool HasRole(string role) => RealmRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines if the user is in a specific group.
    /// </summary>
    public bool IsInGroup(string group) => Groups.Contains(group, StringComparer.OrdinalIgnoreCase);
}
