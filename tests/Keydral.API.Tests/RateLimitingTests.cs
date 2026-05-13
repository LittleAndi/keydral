using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Keydral.Storage;
using Keydral.Storage.Repositories;
using Keydral.Encryption;
using Keydral.Encryption.Extensions;
using Keydral.API.RateLimiting;
using Keydral.API.Tests.Utilities;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Keydral.API.Tests;

/// <summary>
/// Integration tests for rate limiting middleware.
/// Uses a custom factory that overrides the RateLimitPolicyStore to use very low
/// limits (3 req/min) so that tests can trigger 429 responses efficiently.
/// </summary>
public class RateLimitingTests : IAsyncLifetime
{
    private RateLimitingTestFactory _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _authenticatedClient = null!;

    public async Task InitializeAsync()
    {
        _factory = new RateLimitingTestFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _authenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateToken("test-user-1"));
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authenticatedClient?.Dispose();
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ health check

    [Fact]
    public async Task HealthCheck_NotRateLimited_ReturnsOk()
    {
        // Health check has no EndpointRateLimitPolicy; only subject to per-IP limit (1000).
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------ per-user limits

    [Fact]
    public async Task GetSecrets_ExceedsLimit_Returns429()
    {
        // Limit is set to 3 req/min in the test factory. Authenticated requests are
        // required because the rate limiter runs after UseAuthentication.
        for (int i = 0; i < 3; i++)
        {
            var ok = await _authenticatedClient.GetAsync("/api/secrets");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, ok.StatusCode);
        }

        var rejected = await _authenticatedClient.GetAsync("/api/secrets");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task GetSecrets_RejectedResponse_ContainsRetryAfterHeader()
    {
        for (int i = 0; i < 3; i++)
            await _authenticatedClient.GetAsync("/api/secrets");

        var response = await _authenticatedClient.GetAsync("/api/secrets");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"),
            "Retry-After header must be present on 429 responses");
    }

    [Fact]
    public async Task GetSecrets_RejectedResponse_ContainsJsonBody()
    {
        for (int i = 0; i < 3; i++)
            await _authenticatedClient.GetAsync("/api/secrets");

        var response = await _authenticatedClient.GetAsync("/api/secrets");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Contains("429", body);
        Assert.Contains("Too Many Requests", body);
    }

    // ------------------------------------------------------------------ rate-limit response headers

    [Fact]
    public async Task GetSecrets_SuccessfulResponse_ContainsRateLimitHeaders()
    {
        var response = await _authenticatedClient.GetAsync("/api/secrets");

        // Headers are populated for endpoints with an EndpointRateLimitPolicy.
        // Status 200/403 is fine – we're only checking the headers are present.
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"),
            "X-RateLimit-Limit must be present");
        Assert.True(response.Headers.Contains("X-RateLimit-Reset"),
            "X-RateLimit-Reset must be present");
    }

    [Fact]
    public async Task GetSecrets_SuccessfulResponse_RateLimitLimitMatchesConfig()
    {
        var response = await _authenticatedClient.GetAsync("/api/secrets");

        var limitHeader = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.NotNull(limitHeader);
        // Test factory sets SecretsGet:PermitLimit to 3 via RateLimitPolicyStore.
        Assert.Equal("3", limitHeader);
    }

    [Fact]
    public async Task HealthCheck_NoRateLimitHeaders()
    {
        // Endpoints without EndpointRateLimitPolicy must NOT emit X-RateLimit-* headers.
        var response = await _client.GetAsync("/health");

        Assert.False(response.Headers.Contains("X-RateLimit-Limit"),
            "Health check must not have X-RateLimit-Limit header");
    }

    // ------------------------------------------------------------------ whitelist (per-IP)

    [Fact]
    public async Task WhitelistedIp_NeverRateLimited()
    {
        // The whitelist test factory:
        //   - sets per-IP limit = 3 (low) AND whitelists "unknown"
        //     (TestServer sets RemoteIpAddress = null -> "unknown")
        //   - sets per-user limit = 1000 (high) so only per-IP is tested
        // Without whitelist the 4th request would be 429; with whitelist it must not be.
        // Authenticated requests are required because the rate limiter runs after auth.
        using var factory = new RateLimitingWhitelistTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateToken("whitelist-test-user"));

        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/api/secrets");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    // ------------------------------------------------------------------ per-user isolation

    [Fact]
    public async Task TwoAuthenticatedUsers_HaveIndependentPerUserQuotas()
    {
        // Exhaust user1's quota (limit = 3 in the test factory).
        for (int i = 0; i < 3; i++)
            await _authenticatedClient.GetAsync("/api/secrets");

        // user1 must now be rate limited.
        var user1Rejected = await _authenticatedClient.GetAsync("/api/secrets");
        Assert.Equal(HttpStatusCode.TooManyRequests, user1Rejected.StatusCode);

        // user2 has a completely separate per-user partition – must NOT be rate limited.
        using var user2Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        user2Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateToken("test-user-2"));

        var user2Response = await user2Client.GetAsync("/api/secrets");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, user2Response.StatusCode);
    }
}

/// <summary>
/// WebApplicationFactory that replaces <see cref="RateLimitPolicyStore"/> with a test version
/// that uses a permit limit of 3 per minute, allowing tests to trigger 429 quickly.
/// Uses ConfigureServices (runs AFTER the app's Program.cs) so the DI singleton replacement
/// takes effect before any singletons are resolved.
/// </summary>
internal sealed class RateLimitingTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            SetupTestInfrastructure(services);

            // Replace production RateLimitPolicyStore with a low-limit test version.
            // Because PartitionedRateLimiter<HttpContext> is a lazy DI factory that
            // reads from RateLimitPolicyStore at first-resolve time, it will pick up
            // these test values automatically.
            services.RemoveAll<RateLimitPolicyStore>();
            services.AddSingleton(new RateLimitPolicyStore(new Dictionary<string, RateLimitPolicyInfo>
            {
                [RateLimitingExtensions.GetSecretsPolicy] = new(3, 60),
                [RateLimitingExtensions.PostSecretsPolicy] = new(3, 60),
                [RateLimitingExtensions.GetAuditLogsPolicy] = new(3, 60),
            }));
        });
    }

    internal static void SetupTestInfrastructure(IServiceCollection services)
    {
        // Remove real DB context
        var dbCtx = services.FirstOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
        if (dbCtx != null) services.Remove(dbCtx);

        // Configure JWT Bearer for testing: disable OIDC discovery and validate tokens
        // with a local symmetric signing key (no Keycloak server required).
        // Must use Configure (not PostConfigure) so that Authority and MetadataAddress are
        // cleared BEFORE the built-in JwtBearerPostConfigureOptions validates them.
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opts =>
        {
            opts.RequireHttpsMetadata = false;
            opts.Authority = null;        // disable OIDC discovery
            opts.MetadataAddress = null;  // no metadata URL to fetch
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = TestJwtTokenFactory.SigningKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
            };
        });

        // Replace encryption with test provider
        var mkpDesc = services.FirstOrDefault(
            d => d.ServiceType == typeof(Keydral.Encryption.Providers.IMasterKeyProvider));
        if (mkpDesc != null) services.Remove(mkpDesc);

        services.AddSingleton<Keydral.Encryption.Providers.IMasterKeyProvider>(
            new TestMasterKeyProvider());

        // Replace repositories with no-op mocks
        ReplaceWithMock<ISecretRepository>(services);
        ReplaceWithMock<IPolicyRepository>(services);
        ReplaceWithMock<IAuditLogRepository>(services);
    }

    private static void ReplaceWithMock<T>(IServiceCollection services) where T : class
    {
        var desc = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (desc != null) services.Remove(desc);
        services.AddScoped(_ => new Mock<T>().Object);
    }
}

/// <summary>
/// WebApplicationFactory that configures a very low per-IP limit (3 req/min) AND whitelists
/// "unknown" (the effective IP when TestServer has no real network connection, i.e.
/// RemoteIpAddress is null). Used to verify that the whitelist bypasses the per-IP limit.
/// </summary>
internal sealed class RateLimitingWhitelistTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RateLimitingTestFactory.SetupTestInfrastructure(services);

            // High per-user limit so only the per-IP limiter fires in this test.
            services.RemoveAll<RateLimitPolicyStore>();
            services.AddSingleton(new RateLimitPolicyStore(new Dictionary<string, RateLimitPolicyInfo>
            {
                [RateLimitingExtensions.GetSecretsPolicy] = new(1000, 60),
                [RateLimitingExtensions.PostSecretsPolicy] = new(1000, 60),
                [RateLimitingExtensions.GetAuditLogsPolicy] = new(1000, 60),
            }));

            // Replace GlobalLimiterConfigurator with a test version that uses
            // per-IP limit = 3 AND whitelists "unknown".
            services.RemoveAll<IPostConfigureOptions<RateLimiterOptions>>();
            services.AddSingleton<IPostConfigureOptions<RateLimiterOptions>>(sp =>
                new WhitelistTestGlobalLimiter(sp));
        });
    }
}

/// <summary>
/// IPostConfigureOptions implementation used by <see cref="RateLimitingWhitelistTestFactory"/>.
/// Sets a per-IP limit of 3 req/min and whitelists the "unknown" IP (used by TestServer).
/// </summary>
file sealed class WhitelistTestGlobalLimiter : IPostConfigureOptions<RateLimiterOptions>
{
    private readonly IServiceProvider _sp;
    public WhitelistTestGlobalLimiter(IServiceProvider sp) => _sp = sp;

    public void PostConfigure(string? name, RateLimiterOptions options)
    {
        // Per-IP limiter with limit=3 and "unknown" whitelisted.
        var perIpLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // "unknown" is the effective IP in TestServer (RemoteIpAddress == null).
            if (ip == "unknown")
                return RateLimitPartition.GetNoLimiter("whitelisted:unknown");

            return RateLimitPartition.GetSlidingWindowLimiter(
                $"ip:{ip}",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromSeconds(60),
                    SegmentsPerWindow = 6,
                    AutoReplenishment = true
                });
        });

        var perEndpointLimiter = _sp.GetRequiredService<PartitionedRateLimiter<HttpContext>>();

        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            perIpLimiter,
            perEndpointLimiter);
    }
}
