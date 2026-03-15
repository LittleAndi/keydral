using Keydral.API.Models;
using Keydral.API.Authorization;
using Keydral.API.Middleware;
using Keydral.Core.Authentication;
using Keydral.Storage.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Keydral.API.Endpoints;

/// <summary>
/// Extension methods for mapping policy endpoints.
/// </summary>
public static class PolicyEndpoints
{
    private const string TagName = "policies";

    public static void MapPolicyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/policies")
            .WithTags(TagName);

        group.MapGet("/", ListPolicies)
            .WithName("ListPolicies")
            .WithOpenApi()
            .WithDescription("List all RBAC policies");

        group.MapGet("/{id}", GetPolicy)
            .WithName("GetPolicy")
            .WithOpenApi()
            .WithDescription("Get a specific policy by ID");

        group.MapPost("/", CreatePolicy)
            .WithName("CreatePolicy")
            .WithOpenApi()
            .WithDescription("Create a new RBAC policy");

        group.MapPut("/{id}", UpdatePolicy)
            .WithName("UpdatePolicy")
            .WithOpenApi()
            .WithDescription("Update an existing policy");

        group.MapDelete("/{id}", DeletePolicy)
            .WithName("DeletePolicy")
            .WithOpenApi()
            .WithDescription("Delete a policy (soft delete)");
    }

    /// <summary>
    /// List all policies (requires admin role).
    /// </summary>
    private static async Task<Results<Ok<List<PolicyResponse>>, ForbidHttpResult, UnauthorizedHttpResult>> ListPolicies(
        HttpContext context,
        IPolicyRepository policyRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var policies = await policyRepository.GetActivePoliciesAsync();
            var response = policies.Select(p => MapToDto(p)).ToList();
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing policies");
            throw;
        }
    }

    /// <summary>
    /// Get a specific policy by ID.
    /// </summary>
    private static async Task<Results<Ok<PolicyResponse>, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> GetPolicy(
        Guid id,
        HttpContext context,
        IPolicyRepository policyRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var policy = await policyRepository.GetByIdAsync(id);
            if (policy == null || policy.IsDeleted)
                return TypedResults.NotFound();

            return TypedResults.Ok(MapToDto(policy));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting policy {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Create a new policy (requires admin role).
    /// </summary>
    private static async Task<Results<Created<PolicyResponse>, BadRequest<string>, ForbidHttpResult, UnauthorizedHttpResult>> CreatePolicy(
        CreateUpdatePolicyRequest request,
        HttpContext context,
        IPolicyRepository policyRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Name))
                return TypedResults.BadRequest("Policy name cannot be empty");
            if (string.IsNullOrWhiteSpace(request.Principal))
                return TypedResults.BadRequest("Principal cannot be empty");
            if (string.IsNullOrWhiteSpace(request.Actions))
                return TypedResults.BadRequest("Actions cannot be empty");
            if (request.Effect != "Allow" && request.Effect != "Deny")
                return TypedResults.BadRequest("Effect must be 'Allow' or 'Deny'");

            var policy = new Storage.Entities.Policy
            {
                Name = request.Name,
                Description = request.Description,
                Principal = request.Principal,
                ResourcePattern = request.ResourcePattern,
                Effect = request.Effect,
                Actions = request.Actions,
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = userContext.Username,
                IsDeleted = false
            };

            await policyRepository.AddAsync(policy);

            return TypedResults.Created($"/api/policies/{policy.Id}", MapToDto(policy));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating policy");
            return TypedResults.BadRequest("Failed to create policy");
        }
    }

    /// <summary>
    /// Update an existing policy (requires admin role).
    /// </summary>
    private static async Task<Results<Ok<PolicyResponse>, NotFound, BadRequest<string>, ForbidHttpResult, UnauthorizedHttpResult>> UpdatePolicy(
        Guid id,
        CreateUpdatePolicyRequest request,
        HttpContext context,
        IPolicyRepository policyRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var policy = await policyRepository.GetByIdAsync(id);
            if (policy == null || policy.IsDeleted)
                return TypedResults.NotFound();

            // Update fields
            policy.Name = request.Name ?? policy.Name;
            policy.Description = request.Description ?? policy.Description;
            policy.Principal = request.Principal ?? policy.Principal;
            policy.ResourcePattern = request.ResourcePattern ?? policy.ResourcePattern;
            policy.Effect = request.Effect ?? policy.Effect;
            policy.Actions = request.Actions ?? policy.Actions;
            policy.IsEnabled = request.IsEnabled;
            policy.UpdatedAt = DateTime.UtcNow;

            await policyRepository.UpdateAsync(policy);

            return TypedResults.Ok(MapToDto(policy));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating policy {Id}", id);
            return TypedResults.BadRequest("Failed to update policy");
        }
    }

    /// <summary>
    /// Delete a policy (soft delete, requires admin role).
    /// </summary>
    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> DeletePolicy(
        Guid id,
        HttpContext context,
        IPolicyRepository policyRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();
        if (!userContext.HasRole("secret-admin"))
            return TypedResults.Forbid();

        try
        {
            var policy = await policyRepository.GetByIdAsync(id);
            if (policy == null || policy.IsDeleted)
                return TypedResults.NotFound();

            // Soft delete
            policy.IsDeleted = true;
            policy.UpdatedAt = DateTime.UtcNow;

            await policyRepository.UpdateAsync(policy);

            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting policy {Id}", id);
            throw;
        }
    }

    private static PolicyResponse MapToDto(Storage.Entities.Policy policy)
    {
        return new PolicyResponse
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            Principal = policy.Principal,
            ResourcePattern = policy.ResourcePattern,
            Effect = policy.Effect,
            Actions = policy.Actions,
            IsEnabled = policy.IsEnabled,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt,
            CreatedBy = policy.CreatedBy,
            IsDeleted = policy.IsDeleted
        };
    }
}
