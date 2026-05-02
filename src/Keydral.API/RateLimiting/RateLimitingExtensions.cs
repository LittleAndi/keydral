using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Keydral.API.RateLimiting;

/// <summary>
/// Configuration for a named rate-limit policy, populated from appsettings.json.
/// </summary>
/// <param name="PermitLimit">Maximum requests allowed per window.</param>
/// <param name="WindowSeconds">Sliding window length in seconds.</param>
public sealed record RateLimitPolicyInfo(int PermitLimit, int WindowSeconds);

/// <summary>
/// Singleton lookup table from policy name to <see cref="RateLimitPolicyInfo"/>.
/// Registered as a DI factory so that test overrides (via ConfigureServices) are picked up
/// before any rate limiters are constructed.
/// </summary>
public sealed class RateLimitPolicyStore
{
    private readonly IReadOnlyDictionary<string, RateLimitPolicyInfo> _policies;

    public RateLimitPolicyStore(IReadOnlyDictionary<string, RateLimitPolicyInfo> policies)
        => _policies = policies;

    public bool TryGetPolicy(
        string name,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RateLimitPolicyInfo? policy)
        => _policies.TryGetValue(name, out policy);
}

/// <summary>
/// Metadata placed on endpoint groups to associate them with a named rate-limit policy.
/// The actual limits are resolved at runtime from <see cref="RateLimitPolicyStore"/>.
/// </summary>
/// <param name="PolicyName">Key into <see cref="RateLimitPolicyStore"/>.</param>
public sealed record EndpointRateLimitPolicy(string PolicyName);

/// <summary>
/// Extension methods for configuring API rate limiting.
/// </summary>
public static class RateLimitingExtensions
{
    // Policy name constants referenced by endpoint metadata and the header middleware.
    public const string GetSecretsPolicy   = "get-secrets";
    public const string PostSecretsPolicy  = "post-secrets";
    public const string GetAuditLogsPolicy = "get-audit-logs";

    /// <summary>
    /// Registers rate limiting services.
    ///
    /// Two limiters are applied as a chained global limiter:
    ///   1. Per-IP sliding-window limiter  – protects against DDoS / noisy neighbours.
    ///   2. Per-endpoint per-user limiter  – enforces fair-usage quotas.
    ///
    /// All limiters are registered as lazy DI factories so that test suites can replace
    /// <see cref="RateLimitPolicyStore"/> in ConfigureServices before the first request.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        if (!section.GetValue("Enabled", true))
            return services;

        // ---- per-endpoint policy store (lazy factory — replaceable in tests) ----------
        // Reads from IConfiguration at first resolve time, NOT at registration time.
        services.AddSingleton<RateLimitPolicyStore>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>().GetSection("RateLimiting");
            return new RateLimitPolicyStore(new Dictionary<string, RateLimitPolicyInfo>
            {
                [GetSecretsPolicy]   = new(cfg.GetValue("SecretsGet:PermitLimit",   100),
                                           cfg.GetValue("SecretsGet:WindowSeconds",  60)),
                [PostSecretsPolicy]  = new(cfg.GetValue("SecretsPost:PermitLimit",   10),
                                           cfg.GetValue("SecretsPost:WindowSeconds",  60)),
                [GetAuditLogsPolicy] = new(cfg.GetValue("AuditLogsGet:PermitLimit",  50),
                                           cfg.GetValue("AuditLogsGet:WindowSeconds", 60)),
            });
        });

        // ---- per-endpoint per-user limiter (lazy factory — replaceable in tests) ------
        // Reads RateLimitPolicyStore from DI on first resolve so test overrides apply.
        services.AddSingleton<PartitionedRateLimiter<HttpContext>>(sp =>
        {
            var policyStore = sp.GetRequiredService<RateLimitPolicyStore>();

            return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var policyName = context.GetEndpoint()
                    ?.Metadata.GetMetadata<EndpointRateLimitPolicy>()
                    ?.PolicyName;

                if (policyName == null || !policyStore.TryGetPolicy(policyName, out var policyInfo))
                    return RateLimitPartition.GetNoLimiter("no-policy");

                var userId  = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.User?.FindFirst("sub")?.Value;
                var ip      = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userKey = userId != null ? $"user:{userId}" : $"ip:{ip}";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    $"{policyName}:{userKey}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = policyInfo.PermitLimit,
                        Window = TimeSpan.FromSeconds(policyInfo.WindowSeconds),
                        SegmentsPerWindow = 6,
                        AutoReplenishment = true
                    });
            });
        });

        // ---- rate limiter middleware services ----------------------------------------
        services.AddRateLimiter(opts =>
        {
            opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opts.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.ContentType = "application/json";

                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    ctx.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                else
                    ctx.HttpContext.Response.Headers.RetryAfter = "60"; // default: retry in 60 s

                await ctx.HttpContext.Response.WriteAsync(
                    "{\"type\":\"https://tools.ietf.org/html/rfc6585#section-4\"," +
                    "\"title\":\"Too Many Requests\",\"status\":429," +
                    "\"detail\":\"Rate limit exceeded. Please try again later.\"}",
                    ct);
            };

            // GlobalLimiter is set lazily by GlobalLimiterConfigurator (IPostConfigureOptions)
            // to allow DI-resolved dependencies (perEndpointLimiter) to be overridden in tests.
        });

        // Register IPostConfigureOptions so GlobalLimiter can resolve DI services lazily.
        services.AddSingleton<IPostConfigureOptions<RateLimiterOptions>, GlobalLimiterConfigurator>();

        return services;
    }
}

/// <summary>
/// Configures <see cref="RateLimiterOptions.GlobalLimiter"/> lazily via
/// <see cref="IPostConfigureOptions{TOptions}"/> so that the per-endpoint limiter
/// is resolved from DI (enabling test overrides to take effect).
/// </summary>
internal sealed class GlobalLimiterConfigurator : IPostConfigureOptions<RateLimiterOptions>
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _configuration;

    public GlobalLimiterConfigurator(IServiceProvider sp, IConfiguration configuration)
    {
        _sp = sp;
        _configuration = configuration;
    }

    public void PostConfigure(string? name, RateLimiterOptions options)
    {
        var section = _configuration.GetSection("RateLimiting");

        var whitelistedIps = new HashSet<string>(
            section.GetSection("WhitelistedIPs").Get<string[]>() ?? [],
            StringComparer.OrdinalIgnoreCase);

        int perIpLimit  = section.GetValue("PerIp:PermitLimit",  1000);
        int perIpWindow = section.GetValue("PerIp:WindowSeconds",   60);

        var perIpLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (whitelistedIps.Contains(ip))
                return RateLimitPartition.GetNoLimiter($"whitelisted:{ip}");

            return RateLimitPartition.GetSlidingWindowLimiter(
                $"ip:{ip}",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = perIpLimit,
                    Window = TimeSpan.FromSeconds(perIpWindow),
                    SegmentsPerWindow = 6,
                    AutoReplenishment = true
                });
        });

        var perEndpointLimiter = _sp.GetRequiredService<PartitionedRateLimiter<HttpContext>>();

        // Chain: per-IP first, then per-endpoint per-user.
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            perIpLimiter,
            perEndpointLimiter);
    }
}
