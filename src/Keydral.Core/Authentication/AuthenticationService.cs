using System.Security.Claims;
using System.Text.Json;

namespace Keydral.Core.Authentication;

/// <summary>
/// Service for handling JWT and OIDC operations.
/// Extracts and validates user context from JWT claims.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Extract user context from JWT claims.
    /// </summary>
    /// <param name="claimsPrincipal">The parsed JWT claims</param>
    /// <returns>UserContext if successful; null if validation fails</returns>
    UserContext? ExtractUserContext(ClaimsPrincipal claimsPrincipal);

    /// <summary>
    /// Validate that a user has a specific role.
    /// </summary>
    bool HasRole(UserContext userContext, string requiredRole);

    /// <summary>
    /// Validate that a user is in a specific group.
    /// </summary>
    bool IsInGroup(UserContext userContext, string requiredGroup);
}

/// <summary>
/// Default implementation of IAuthenticationService.
/// Extracts user claims from Keycloak JWT tokens.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private const string SubClaimType = "sub";
    private const string PreferredUsernameClaimType = "preferred_username";
    private const string EmailClaimType = "email";
    private const string NameClaimType = "name";
    private const string RealmAccessClaimType = "realm_access";
    private const string GroupsClaimType = "groups";

    /// <summary>
    /// Extract user context from JWT claims.
    /// </summary>
    public UserContext? ExtractUserContext(ClaimsPrincipal claimsPrincipal)
    {
        if (claimsPrincipal?.Identity?.IsAuthenticated != true)
            return null;

        var idClaim = claimsPrincipal.FindFirst(SubClaimType);
        var usernameClaim = claimsPrincipal.FindFirst(PreferredUsernameClaimType);

        if (idClaim?.Value == null || usernameClaim?.Value == null)
            return null;

        var userContext = new UserContext
        {
            Id = idClaim.Value,
            Username = usernameClaim.Value,
            Email = claimsPrincipal.FindFirst(EmailClaimType)?.Value,
            DisplayName = claimsPrincipal.FindFirst(NameClaimType)?.Value,
            RealmRoles = ExtractRealmRoles(claimsPrincipal),
            Groups = ExtractGroups(claimsPrincipal)
        };

        return userContext;
    }

    /// <summary>
    /// Validate that a user has a specific role.
    /// </summary>
    public bool HasRole(UserContext userContext, string requiredRole)
    {
        if (userContext == null || string.IsNullOrWhiteSpace(requiredRole))
            return false;

        return userContext.HasRole(requiredRole);
    }

    /// <summary>
    /// Validate that a user is in a specific group.
    /// </summary>
    public bool IsInGroup(UserContext userContext, string requiredGroup)
    {
        if (userContext == null || string.IsNullOrWhiteSpace(requiredGroup))
            return false;

        return userContext.IsInGroup(requiredGroup);
    }

    /// <summary>
    /// Extract realm roles from Keycloak's nested realm_access.roles claim.
    /// </summary>
    private static List<string> ExtractRealmRoles(ClaimsPrincipal claimsPrincipal)
    {
        var roles = new List<string>();

        // Keycloak includes roles in realm_access claim as JSON
        var realmAccessClaim = claimsPrincipal.FindFirst(RealmAccessClaimType);
        if (realmAccessClaim?.Value != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(realmAccessClaim.Value);
                var root = doc.RootElement;
                if (root.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var roleElement in rolesElement.EnumerateArray())
                    {
                        if (roleElement.ValueKind == JsonValueKind.String && roleElement.GetString() is { } role)
                        {
                            roles.Add(role);
                        }
                    }
                }
            }
            catch
            {
                // Ignore JSON parsing errors and continue
            }
        }

        return roles;
    }

    /// <summary>
    /// Extract groups from Keycloak's groups claim (array of group names).
    /// </summary>
    private static List<string> ExtractGroups(ClaimsPrincipal claimsPrincipal)
    {
        var groups = new List<string>();

        // Groups may be a single claim with value, or multiple claims
        var groupClaims = claimsPrincipal.FindAll(GroupsClaimType);
        foreach (var claim in groupClaims)
        {
            if (claim.Value != null)
            {
                // Group names might be paths like /team-a, extract the actual name
                var groupName = claim.Value.TrimStart('/');
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    groups.Add(groupName);
                }
            }
        }

        return groups;
    }
}
