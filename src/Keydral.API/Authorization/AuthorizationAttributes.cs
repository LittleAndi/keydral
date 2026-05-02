using Microsoft.AspNetCore.Authorization;

namespace Keydral.API.Authorization;

/// <summary>
/// Authorization attribute for requiring a specific role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Create authorization requirement for a specific role.
    /// </summary>
    /// <param name="role">Required role (e.g., "secret-admin")</param>
    public RequireRoleAttribute(string role)
    {
        Roles = role;
    }
}

/// <summary>
/// Authorization attribute for requiring membership in a group.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireGroupAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "group:";

    /// <summary>
    /// Create authorization requirement for group membership.
    /// </summary>
    /// <param name="group">Required group (e.g., "team-a")</param>
    public RequireGroupAttribute(string group)
    {
        Policy = $"{PolicyPrefix}{group}";
    }
}

/// <summary>
/// Authorization attribute for RBAC policy evaluation.
/// Checks if the user can perform an action on a specific resource path.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequireResourcePolicyAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "resource:";

    /// <summary>
    /// The resource path to check policy for (e.g., "/secrets/database-password").
    /// Can use route parameters like "/secrets/{name}".
    /// </summary>
    public string ResourcePath { get; set; }

    /// <summary>
    /// The action to check access for (e.g., "secrets:read").
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Create authorization requirement for resource policy evaluation.
    /// </summary>
    /// <param name="resourcePath">Resource path (can have route parameters)</param>
    /// <param name="action">Action to check (e.g., "secrets:read")</param>
    public RequireResourcePolicyAttribute(string resourcePath, string action)
    {
        ResourcePath = resourcePath ?? throw new ArgumentNullException(nameof(resourcePath));
        Action = action ?? throw new ArgumentNullException(nameof(action));

        // Combine resource path and action into policy name
        Policy = $"{PolicyPrefix}{resourcePath}:{action}";
    }
}
