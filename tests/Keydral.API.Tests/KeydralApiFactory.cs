using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Moq;
using Keydral.Storage;
using Keydral.Storage.Repositories;
using Keydral.Encryption;
using Keydral.Encryption.Extensions;
using Keydral.Encryption.Configuration;
using Keydral.API.Tests.Utilities;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Keydral.Core.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Keydral.Core.Authentication;

namespace Keydral.API.Tests;

/// <summary>
/// WebApplicationFactory for integration testing the Keydral API.
/// Provides test environment with mock repositories and real encryption using a test key.
/// </summary>
public class KeydralApiFactory : WebApplicationFactory<Program>
{
    public Mock<ISecretRepository> MockSecretRepository { get; } = new();
    public Mock<IPolicyRepository> MockPolicyRepository { get; } = new();
    public Mock<IAuditLogRepository> MockAuditLogRepository { get; } = new();
    public Mock<IRbacPolicyEngine> MockPolicyEngine { get; } = new();

    public KeydralApiFactory()
    {
        // Set environment before host creation so correct appsettings file is loaded
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        MockPolicyEngine
            .Setup(engine => engine.CanPerformAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            });

            // Remove real database context (not needed for health endpoint test)
            var dbContextDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Configure JWT Bearer for testing: no HTTPS required, and Authority must include
            // the realm path (/realms/keydral) because Keycloak scopes its OIDC discovery
            // endpoint under the realm — <Authority>/.well-known/openid-configuration
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = null;
                options.MetadataAddress = null!;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestJwtTokenFactory.SigningKey,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                };
            });

            // Replace real encryption service with test encryption using a test master key
            // This ensures encryption is properly tested, not mocked away
            var encryptionDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IEncryptionService));
            if (encryptionDescriptor != null)
                services.Remove(encryptionDescriptor);

            // Remove encryption-related services that depend on file/Kubernetes config
            var masterKeyProviderDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(Keydral.Encryption.Providers.IMasterKeyProvider));
            if (masterKeyProviderDescriptor != null)
                services.Remove(masterKeyProviderDescriptor);

            var encryptionOptionsDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(Keydral.Encryption.Configuration.EncryptionOptions));
            if (encryptionOptionsDescriptor != null)
                services.Remove(encryptionOptionsDescriptor);

            // Add encryption with test provider - no file I/O, deterministic key
            var testEncryptionOptions = new Keydral.Encryption.Configuration.EncryptionOptions
            {
                Provider = "none", // Provider field is ignored when masterKeyProvider is explicit
                Algorithm = "AES-256-GCM"
            };
            services.AddEncryption(new TestMasterKeyProvider(), testEncryptionOptions);

            // Replace real repositories with mocks for testing
            var secretRepoDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ISecretRepository));
            if (secretRepoDescriptor != null)
                services.Remove(secretRepoDescriptor);

            var policyRepoDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IPolicyRepository));
            if (policyRepoDescriptor != null)
                services.Remove(policyRepoDescriptor);

            var auditRepoDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IAuditLogRepository));
            if (auditRepoDescriptor != null)
                services.Remove(auditRepoDescriptor);

            var policyEngineDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IRbacPolicyEngine));
            if (policyEngineDescriptor != null)
                services.Remove(policyEngineDescriptor);

            services.AddScoped(_ => MockSecretRepository.Object);
            services.AddScoped(_ => MockPolicyRepository.Object);
            services.AddScoped(_ => MockAuditLogRepository.Object);
            services.AddScoped(_ => MockPolicyEngine.Object);
            services.AddSingleton<IStartupFilter, TestUserContextStartupFilter>();
        });
    }

    /// <summary>
    /// Create an HTTP client with Bearer token for authenticated requests.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string userId = "test-user", params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(',', roles));
        }

        return client;
    }

    /// <summary>
    /// Reset all mocks to clean state.
    /// </summary>
    public void ResetMocks()
    {
        MockSecretRepository.Reset();
        MockPolicyRepository.Reset();
        MockAuditLogRepository.Reset();
        MockPolicyEngine.Reset();
        MockPolicyEngine
            .Setup(engine => engine.CanPerformAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }
}

internal sealed class TestUserContextStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, middlewareNext) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-User", out var userIdValues))
                {
                    var roles = context.Request.Headers.TryGetValue("X-Test-Roles", out var roleValues)
                        ? roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                        : [];

                    context.Items["UserContext"] = new UserContext
                    {
                        Id = userIdValues.ToString(),
                        Username = userIdValues.ToString(),
                        RealmRoles = roles
                    };
                }

                await middlewareNext();
            });

            next(app);
        };
    }
}

internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userIdValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.ToString();
        var roles = Request.Headers.TryGetValue("X-Test-Roles", out var roleValues)
            ? roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("preferred_username", userId),
            new("realm_access", JsonSerializer.Serialize(new { roles }))
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
