using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Keydral.API.Middleware;
using Keydral.API.Models;
using Keydral.API.RateLimiting;
using Keydral.API.Search;
using Keydral.Core.Authorization;
using Keydral.Storage.Entities;
using Keydral.Storage.Repositories;

namespace Keydral.API.Endpoints;

/// <summary>
/// Extension methods for mapping cross-resource search endpoints.
/// </summary>
public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapGet("/api/search", Search)
            .WithName("Search")
            .WithDescription("Search secrets and audit logs using the query DSL")
            .WithTags("search")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetSecretsPolicy));
    }

    private static async Task<Results<Ok<SearchResultsResponse>, UnauthorizedHttpResult>> Search(
        HttpContext context,
        ISecretRepository secretRepository,
        IAuditLogRepository auditLogRepository,
        IRbacPolicyEngine policyEngine,
        [FromQuery(Name = "q")] string query = "",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        ILogger<Program>? logger = null)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var (secretRequest, auditRequest) = SearchQueryParser.Parse(query, pageNumber, pageSize);
            var response = new SearchResultsResponse();

            var secrets = await secretRepository.GetActiveSecretsAsync();
            var allowedSecrets = await FilterReadableSecretsAsync(secrets, userContext.Id, policyEngine);
            var filteredSecrets = SearchFilterService.FilterSecrets(allowedSecrets, secretRequest)
                .Select(SearchFilterService.ToSecretListItem);
            response.Secrets = SearchFilterService.Paginate(filteredSecrets, pageNumber, pageSize);

            if (userContext.HasRole("secret-admin"))
            {
                var auditLogs = await auditLogRepository.GetAllAsync();
                var filteredAuditLogs = SearchFilterService.FilterAuditLogs(auditLogs, auditRequest)
                    .Select(SearchFilterService.ToAuditLogResponse);
                response.AuditLogs = SearchFilterService.Paginate(filteredAuditLogs, pageNumber, pageSize);
            }

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error executing DSL search");
            throw;
        }
    }

    internal static async Task<List<Secret>> FilterReadableSecretsAsync(
        IEnumerable<Secret> secrets,
        string userId,
        IRbacPolicyEngine policyEngine)
    {
        var allowedSecrets = new List<Secret>();
        foreach (var secret in secrets)
        {
            var canRead = await policyEngine.CanPerformAsync(userId, $"/secrets/{secret.Name}", "secrets:read");
            if (canRead)
            {
                allowedSecrets.Add(secret);
            }
        }

        return allowedSecrets;
    }
}
