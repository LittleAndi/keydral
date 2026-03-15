using Microsoft.Extensions.DependencyInjection;
using Keydral.Core.Authentication;
using Keydral.Core.Authorization;

namespace Keydral.Core.Extensions;

/// <summary>
/// Dependency injection extensions for authentication and authorization services.
/// </summary>
public static class AuthenticationAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Add authentication and RBAC services to the container.
    /// </summary>
    public static IServiceCollection AddAuthenticationAndAuthorization(
        this IServiceCollection services)
    {
        // Authentication service for JWT claim extraction
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // RBAC policy engine for authorization decisions
        services.AddScoped<IRbacPolicyEngine, RbacPolicyEngine>();

        return services;
    }
}
