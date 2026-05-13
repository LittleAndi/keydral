using System.Threading.RateLimiting;

namespace Keydral.API.RateLimiting;

/// <summary>
/// Middleware that injects <c>X-RateLimit-*</c> headers into every HTTP response.
///
/// For endpoints annotated with <see cref="EndpointRateLimitPolicy"/>:
/// <list type="bullet">
///   <item><c>X-RateLimit-Limit</c>    – configured permit limit for the endpoint.</item>
///   <item><c>X-RateLimit-Remaining</c>– available permits left in the current window,
///         read from the live per-endpoint <see cref="PartitionedRateLimiter{HttpContext}"/>.</item>
///   <item><c>X-RateLimit-Reset</c>    – approximate Unix timestamp when the window resets.</item>
/// </list>
/// <c>Retry-After</c> is set by the rate-limiter's <c>OnRejected</c> callback on 429 responses.
/// </summary>
public sealed class RateLimitHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitPolicyStore? _policyStore;
    private readonly PartitionedRateLimiter<HttpContext>? _perEndpointLimiter;

    public RateLimitHeadersMiddleware(
        RequestDelegate next,
        RateLimitPolicyStore? policyStore = null,
        PartitionedRateLimiter<HttpContext>? perEndpointLimiter = null)
    {
        _next = next;
        _policyStore = policyStore;
        _perEndpointLimiter = perEndpointLimiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_policyStore != null)
        {
            context.Response.OnStarting(() =>
            {
                var policyName = context.GetEndpoint()
                    ?.Metadata.GetMetadata<EndpointRateLimitPolicy>()
                    ?.PolicyName;

                if (policyName == null || !_policyStore.TryGetPolicy(policyName, out var policyInfo))
                    return Task.CompletedTask;

                context.Response.Headers["X-RateLimit-Limit"] =
                    policyInfo.PermitLimit.ToString();

                context.Response.Headers["X-RateLimit-Reset"] =
                    DateTimeOffset.UtcNow
                        .AddSeconds(policyInfo.WindowSeconds)
                        .ToUnixTimeSeconds()
                        .ToString();

                // Read the current window's remaining permits from the live limiter.
                if (_perEndpointLimiter != null)
                {
                    var stats = _perEndpointLimiter.GetStatistics(context);
                    if (stats != null)
                    {
                        context.Response.Headers["X-RateLimit-Remaining"] =
                            stats.CurrentAvailablePermits.ToString();
                    }
                }

                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for the rate-limit headers middleware.
/// </summary>
public static class RateLimitHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds <see cref="RateLimitHeadersMiddleware"/> to the pipeline.
    /// Must be placed <em>before</em> <c>UseRateLimiter()</c> so the
    /// <c>OnStarting</c> callback is registered before the rate limiter runs.
    /// </summary>
    public static IApplicationBuilder UseRateLimitHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimitHeadersMiddleware>();
}
