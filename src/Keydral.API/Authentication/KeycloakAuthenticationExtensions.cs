using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Keydral.API.Authentication;

/// <summary>
/// Configuration for Keycloak OIDC authentication settings.
/// </summary>
public class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Keycloak base URL (e.g., http://localhost:8080).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak realm name.
    /// </summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// API client ID registered in Keycloak.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// API client secret (for confidential clients).
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Get the JWT discovery endpoint URL.
    /// </summary>
    public string GetDiscoveryUrl() => $"{Url}/realms/{Realm}/.well-known/openid-configuration";

    /// <summary>
    /// Get the JWKS (JSON Web Key Set) endpoint URL.
    /// </summary>
    public string GetJwksUrl() => $"{Url}/realms/{Realm}/protocol/openid-connect/certs";

    /// <summary>
    /// Validate required configuration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidOperationException("Keycloak:Url is required");
        if (string.IsNullOrWhiteSpace(Realm))
            throw new InvalidOperationException("Keycloak:Realm is required");
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("Keycloak:ClientId is required");
    }
}

/// <summary>
/// Extension methods for configuring OIDC authentication with Keycloak.
/// </summary>
public static class KeycloakAuthenticationExtensions
{
    /// <summary>
    /// Add Keycloak OIDC authentication using JWT bearer tokens.
    /// </summary>
    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloakOptions = new KeycloakOptions();
        configuration.GetSection(KeycloakOptions.SectionName).Bind(keycloakOptions);
        keycloakOptions.Validate();

        // Configure JWT bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Configure JWT validation
            options.Authority = $"{keycloakOptions.Url}/realms/{keycloakOptions.Realm}";
            options.Audience = keycloakOptions.ClientId;
            options.MetadataAddress = keycloakOptions.GetDiscoveryUrl();

            // Require HTTPS for metadata endpoint (Keycloak now runs on HTTPS in Aspire)
            options.RequireHttpsMetadata = true;

            // Security token parameters
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                // Issuer URL must match Keycloak realm
                ValidIssuer = $"{keycloakOptions.Url}/realms/{keycloakOptions.Realm}",

                // Audience should match client ID
                ValidAudience = keycloakOptions.ClientId,

                // Allow some clock skew for server time differences
                ClockSkew = TimeSpan.FromSeconds(60),

                // Disable name claim mapping to preserve original JWT claims
                NameClaimType = "preferred_username",
            };

            // Configure exception handling
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("JWT authentication failed: {Message}", context.Exception?.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    // Token validation successful - claims are now available in User context
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogDebug("JWT token validated for user: {User}", context.Principal?.FindFirst("preferred_username")?.Value ?? "unknown");
                    return Task.CompletedTask;
                }
            };

            // Allow reflection of issued claims
            options.SaveToken = true;
        });

        // Configure Authorization policy
        services.AddAuthorization(options =>
        {
            // Default policy requires authenticated user
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // Store Keycloak options for use in other services
        services.AddSingleton(keycloakOptions);

        return services;
    }
}
