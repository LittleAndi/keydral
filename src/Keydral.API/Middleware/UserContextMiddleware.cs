using Keydral.Core.Authentication;

namespace Keydral.API.Middleware;

/// <summary>
/// Middleware that extracts user context from JWT claims.
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAuthenticationService authenticationService)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userContext = authenticationService.ExtractUserContext(context.User);
            if (userContext != null)
            {
                // Store in HttpContext.Items for later retrieval
                context.Items["UserContext"] = userContext;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering user context middleware.
/// </summary>
public static class UserContextMiddlewareExtensions
{
    /// <summary>
    /// Add user context middleware to the pipeline.
    /// Should be placed after UseAuthentication().
    /// </summary>
    public static IApplicationBuilder UseUserContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<UserContextMiddleware>();
    }

    /// <summary>
    /// Get the current user context from the HttpContext.
    /// </summary>
    public static UserContext? GetUserContext(this HttpContext context)
    {
        return context.Items.TryGetValue("UserContext", out var userContext)
            ? userContext as UserContext
            : null;
    }
}
