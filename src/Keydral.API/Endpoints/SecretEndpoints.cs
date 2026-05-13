using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Keydral.API.Models;
using Keydral.API.Authorization;
using Keydral.API.Middleware;
using Keydral.API.RateLimiting;
using Keydral.API.Search;
using Keydral.Core.Authorization;
using Keydral.Core.Authentication;
using Keydral.Encryption;
using Keydral.Storage.Repositories;
using System.Globalization;

namespace Keydral.API.Endpoints;

/// <summary>
/// Extension methods for mapping secret endpoints.
/// </summary>
public static class SecretEndpoints
{
    private const string TagName = "secrets";

    public static void MapSecretEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/secrets")
            .WithTags(TagName);

        group.MapGet("/search", SearchSecrets)
            .WithName("SearchSecrets")
            .WithDescription("Search secrets with full-text and advanced filters")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetSecretsPolicy));

        group.MapGet("/", ListSecrets)
            .WithName("ListSecrets")
            .WithDescription("List all secrets (without values)")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetSecretsPolicy));

        group.MapGet("/{name}", GetSecret)
            .WithName("GetSecret")
            .WithDescription("Get a specific secret by name")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetSecretsPolicy));

        group.MapPost("/", CreateSecret)
            .WithName("CreateSecret")
            .WithDescription("Create a new secret")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.PostSecretsPolicy));

        group.MapPut("/{name}", UpdateSecret)
            .WithName("UpdateSecret")
            .WithDescription("Update an existing secret")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.PostSecretsPolicy));

        group.MapDelete("/{name}", DeleteSecret)
            .WithName("DeleteSecret")
            .WithDescription("Delete a secret (soft delete)")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.PostSecretsPolicy));

        group.MapGet("/{name}/versions", GetSecretVersions)
            .WithName("GetSecretVersions")
            .WithDescription("Get version history for a secret")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.GetSecretsPolicy));

        group.MapPost("/{name}/restore/{version}", RestoreSecretVersion)
            .WithName("RestoreSecretVersion")
            .WithDescription("Restore a secret to a previous version")
            .WithMetadata(new EndpointRateLimitPolicy(RateLimitingExtensions.PostSecretsPolicy));
    }

    /// <summary>
    /// List all secrets (without values).
    /// </summary>
    private static async Task<Ok<PaginatedResponse<SecretListItemResponse>>> ListSecrets(
        HttpContext context,
        ISecretRepository secretRepository,
        IRbacPolicyEngine policyEngine,
        ILogger<Program> logger,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Ok(new PaginatedResponse<SecretListItemResponse> { Items = new() });

        try
        {
            // Get all secrets (we'll filter by permission)
            var secrets = await secretRepository.GetActiveSecretsAsync();
            var allowedSecrets = await SearchEndpoints.FilterReadableSecretsAsync(secrets, userContext.Id, policyEngine);
            var response = SearchFilterService.Paginate(
                allowedSecrets.Select(SearchFilterService.ToSecretListItem),
                pageNumber,
                pageSize);

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing secrets");
            throw;
        }
    }

    /// <summary>
    /// Search secrets with full-text and advanced filters.
    /// </summary>
    private static async Task<Ok<PaginatedResponse<SecretListItemResponse>>> SearchSecrets(
        HttpContext context,
        ISecretRepository secretRepository,
        IRbacPolicyEngine policyEngine,
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] string? tags = null,
        [FromQuery(Name = "created-after")] string? createdAfter = null,
        [FromQuery(Name = "created-before")] string? createdBefore = null,
        [FromQuery(Name = "updated-after")] string? updatedAfter = null,
        [FromQuery(Name = "updated-before")] string? updatedBefore = null,
        [FromQuery(Name = "created-by")] string? createdBy = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        ILogger<Program>? logger = null)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
        {
            return TypedResults.Ok(new PaginatedResponse<SecretListItemResponse> { Items = new() });
        }

        try
        {
            var createdAfterFilter = ParseUtcDateFilter(createdAfter, endOfDay: false);
            var createdBeforeFilter = ParseUtcDateFilter(createdBefore, endOfDay: true);
            var updatedAfterFilter = ParseUtcDateFilter(updatedAfter, endOfDay: false);
            var updatedBeforeFilter = ParseUtcDateFilter(updatedBefore, endOfDay: true);
            var searchRequest = new SecretSearchRequest
            {
                Query = query,
                Tags = ParseCsv(tags),
                CreatedAfter = createdAfterFilter,
                CreatedBefore = createdBeforeFilter,
                UpdatedAfter = updatedAfterFilter,
                UpdatedBefore = updatedBeforeFilter,
                CreatedBy = createdBy,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var filteredSecretCandidates = await secretRepository.GetSecretsFilteredAsync(
                searchRequest.Query,
                searchRequest.NamePattern,
                searchRequest.Tags,
                searchRequest.CreatedAfter,
                searchRequest.CreatedBefore,
                searchRequest.UpdatedAfter,
                searchRequest.UpdatedBefore,
                searchRequest.CreatedBy);
            var allowedSecrets = await SearchEndpoints.FilterReadableSecretsAsync(filteredSecretCandidates, userContext.Id, policyEngine);
            var filteredSecrets = allowedSecrets.Select(SearchFilterService.ToSecretListItem);

            return TypedResults.Ok(SearchFilterService.Paginate(filteredSecrets, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error searching secrets");
            throw;
        }
    }

    /// <summary>
    /// Get a specific secret by name.
    /// </summary>
    private static async Task<Results<Ok<SecretResponse>, NotFound, UnauthorizedHttpResult>> GetSecret(
        string name,
        HttpContext context,
        ISecretRepository secretRepository,
        IEncryptionService encryptionService,
        IRbacPolicyEngine policyEngine,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();

        try
        {
            // Check RBAC policy
            var canRead = await policyEngine.CanPerformAsync(
                userContext.Id,
                $"/secrets/{name}",
                "secrets:read");

            if (!canRead)
            {
                await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
                {
                    Action = "READ",
                    Actor = userContext.Username,
                    ResourceType = "Secret",
                    ResourceId = name,
                    Result = "FAILED",
                    StatusCode = 403,
                    ErrorMessage = "Access denied by RBAC policy",
                    SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"].ToString(),
                    Timestamp = DateTime.UtcNow
                });
                await auditLogRepository.SaveChangesAsync();

                return TypedResults.Unauthorized();
            }

            var secret = await secretRepository.GetByNameAsync(name);
            if (secret == null || secret.IsDeleted)
            {
                await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
                {
                    Action = "READ",
                    Actor = userContext.Username,
                    ResourceType = "Secret",
                    ResourceId = name,
                    Result = "FAILED",
                    StatusCode = 404,
                    ErrorMessage = "Secret not found",
                    SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"].ToString(),
                    Timestamp = DateTime.UtcNow
                });
                await auditLogRepository.SaveChangesAsync();

                return TypedResults.NotFound();
            }

            // Decrypt the secret value
            var decryptedValue = await encryptionService.DecryptAsync(
                new Encryption.Models.EncryptedSecret(
                    secret.EncryptedValue,
                    secret.EncryptedDataKey ?? string.Empty,
                    secret.InitializationVector ?? string.Empty,
                    secret.AuthenticationTag ?? string.Empty,
                    secret.KeyInitializationVector ?? string.Empty,
                    secret.KeyAuthenticationTag ?? string.Empty,
                    secret.EncryptionKeyId
                ));

            await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
            {
                Action = "READ",
                Actor = userContext.Username,
                ResourceType = "Secret",
                ResourceId = name,
                Result = "SUCCESS",
                StatusCode = 200,
                SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                SecretId = secret.Id
            });
            await auditLogRepository.SaveChangesAsync();

            var response = new SecretResponse
            {
                Id = secret.Id,
                Name = secret.Name,
                Description = secret.Description,
                Value = decryptedValue,
                Version = secret.CurrentVersion,
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt,
                CreatedBy = secret.CreatedBy,
                IsDeleted = secret.IsDeleted,
                Tags = secret.Tags
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting secret {Name}", name);
            throw;
        }
    }

    /// <summary>
    /// Create a new secret.
    /// </summary>
    private static async Task<Results<Created<SecretResponse>, BadRequest<string>, UnauthorizedHttpResult>> CreateSecret(
        CreateUpdateSecretRequest request,
        HttpContext context,
        ISecretRepository secretRepository,
        IEncryptionService encryptionService,
        IRbacPolicyEngine policyEngine,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();

        try
        {
            // For create, we need a 'name' from somewhere - this is simplified
            // In practice, you might extract it from the request body or header
            var tempName = request.Value.GetHashCode().ToString().Substring(0, 8);

            // Check RBAC policy
            var canCreate = await policyEngine.CanPerformAsync(
                userContext.Id,
                $"/secrets/{tempName}",
                "secrets:write");

            if (!canCreate)
            {
                return TypedResults.BadRequest("Access denied by RBAC policy");
            }

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Value))
                return TypedResults.BadRequest("Secret value cannot be empty");

            // Encrypt the secret
            var encryptedSecret = await encryptionService.EncryptAsync(request.Value);

            // Create database record
            var secret = new Storage.Entities.Secret
            {
                Name = tempName,
                Description = request.Description,
                EncryptedValue = encryptedSecret.EncryptedData,
                EncryptionKeyId = encryptedSecret.EncryptionKeyId,
                InitializationVector = encryptedSecret.InitializationVector,
                AuthenticationTag = encryptedSecret.AuthenticationTag,
                EncryptedDataKey = encryptedSecret.EncryptedDataKey,
                KeyInitializationVector = encryptedSecret.KeyInitializationVector,
                KeyAuthenticationTag = encryptedSecret.KeyAuthenticationTag,
                CurrentVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = userContext.Username,
                Tags = request.Tags,
                IsDeleted = false
            };

            await secretRepository.AddAsync(secret);
            await secretRepository.SaveChangesAsync();

            await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
            {
                Action = "CREATE",
                Actor = userContext.Username,
                ResourceType = "Secret",
                ResourceId = secret.Name,
                Result = "SUCCESS",
                StatusCode = 201,
                SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                SecretId = secret.Id
            });
            await auditLogRepository.SaveChangesAsync();

            var response = new SecretResponse
            {
                Id = secret.Id,
                Name = secret.Name,
                Description = secret.Description,
                Value = request.Value,
                Version = secret.CurrentVersion,
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt,
                CreatedBy = secret.CreatedBy,
                IsDeleted = secret.IsDeleted,
                Tags = secret.Tags
            };

            return TypedResults.Created($"/api/secrets/{secret.Name}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating secret");
            return TypedResults.BadRequest("Failed to create secret");
        }
    }

    /// <summary>
    /// Update an existing secret.
    /// </summary>
    private static async Task<Results<Ok<SecretResponse>, NotFound, BadRequest<string>, UnauthorizedHttpResult>> UpdateSecret(
        string name,
        CreateUpdateSecretRequest request,
        HttpContext context,
        ISecretRepository secretRepository,
        IEncryptionService encryptionService,
        IRbacPolicyEngine policyEngine,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();

        try
        {
            var canUpdate = await policyEngine.CanPerformAsync(
                userContext.Id,
                $"/secrets/{name}",
                "secrets:write");

            if (!canUpdate)
                return TypedResults.Unauthorized();

            var secret = await secretRepository.GetByNameAsync(name);
            if (secret == null || secret.IsDeleted)
                return TypedResults.NotFound();

            if (string.IsNullOrWhiteSpace(request.Value))
                return TypedResults.BadRequest("Secret value cannot be empty");

            // Encrypt the new value
            var encryptedSecret = await encryptionService.EncryptAsync(request.Value);

            // Update secret
            secret.EncryptedValue = encryptedSecret.EncryptedData;
            secret.EncryptionKeyId = encryptedSecret.EncryptionKeyId;
            secret.InitializationVector = encryptedSecret.InitializationVector;
            secret.AuthenticationTag = encryptedSecret.AuthenticationTag;
            secret.EncryptedDataKey = encryptedSecret.EncryptedDataKey;
            secret.KeyInitializationVector = encryptedSecret.KeyInitializationVector;
            secret.KeyAuthenticationTag = encryptedSecret.KeyAuthenticationTag;
            secret.CurrentVersion += 1;
            secret.UpdatedAt = DateTime.UtcNow;
            secret.Description = request.Description ?? secret.Description;
            secret.Tags = request.Tags ?? secret.Tags;

            await secretRepository.UpdateAsync(secret);
            await secretRepository.SaveChangesAsync();

            await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
            {
                Action = "UPDATE",
                Actor = userContext.Username,
                ResourceType = "Secret",
                ResourceId = name,
                Result = "SUCCESS",
                StatusCode = 200,
                SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                SecretId = secret.Id
            });
            await auditLogRepository.SaveChangesAsync();

            var response = new SecretResponse
            {
                Id = secret.Id,
                Name = secret.Name,
                Description = secret.Description,
                Value = request.Value,
                Version = secret.CurrentVersion,
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt,
                CreatedBy = secret.CreatedBy,
                IsDeleted = secret.IsDeleted,
                Tags = secret.Tags
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating secret {Name}", name);
            return TypedResults.BadRequest("Failed to update secret");
        }
    }

    /// <summary>
    /// Delete a secret (soft delete).
    /// </summary>
    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeleteSecret(
        string name,
        HttpContext context,
        ISecretRepository secretRepository,
        IRbacPolicyEngine policyEngine,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();

        try
        {
            var canDelete = await policyEngine.CanPerformAsync(
                userContext.Id,
                $"/secrets/{name}",
                "secrets:delete");

            if (!canDelete)
                return TypedResults.Unauthorized();

            var secret = await secretRepository.GetByNameAsync(name);
            if (secret == null || secret.IsDeleted)
                return TypedResults.NotFound();

            // Soft delete
            secret.IsDeleted = true;
            secret.UpdatedAt = DateTime.UtcNow;

            await secretRepository.UpdateAsync(secret);
            await secretRepository.SaveChangesAsync();

            await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
            {
                Action = "DELETE",
                Actor = userContext.Username,
                ResourceType = "Secret",
                ResourceId = name,
                Result = "SUCCESS",
                StatusCode = 204,
                SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                SecretId = secret.Id
            });
            await auditLogRepository.SaveChangesAsync();

            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting secret {Name}", name);
            throw;
        }
    }

    /// <summary>
    /// Get version history for a secret.
    /// </summary>
    private static async Task<Ok<List<SecretVersionResponse>>> GetSecretVersions(
        string name,
        HttpContext context,
        ISecretRepository secretRepository,
        IRbacPolicyEngine policyEngine,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Ok(new List<SecretVersionResponse>());

        try
        {
            var canRead = await policyEngine.CanPerformAsync(
                userContext.Id,
                $"/secrets/{name}",
                "secrets:read");

            if (!canRead)
                return TypedResults.Ok(new List<SecretVersionResponse>());

            var secret = await secretRepository.GetByNameAsync(name);
            if (secret == null)
                return TypedResults.Ok(new List<SecretVersionResponse>());

            var versions = await secretRepository.GetSecretVersionsAsync(secret.Id);
            var response = versions
                .Select(v => new SecretVersionResponse
                {
                    VersionNumber = v.VersionNumber,
                    ChangeDescription = v.ChangeDescription,
                    CreatedAt = v.CreatedAt,
                    CreatedBy = v.CreatedBy
                })
                .ToList();

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting secret versions for {Name}", name);
            throw;
        }
    }

    /// <summary>
    /// Restore a secret to a previous version.
    /// </summary>
    private static async Task<Results<Ok<SecretResponse>, NotFound, BadRequest<string>, UnauthorizedHttpResult>> RestoreSecretVersion(
        string name,
        int version,
        HttpContext context,
        ISecretRepository secretRepository,
        IEncryptionService encryptionService,
        IRbacPolicyEngine policyEngine,
        IAuditLogRepository auditLogRepository,
        ILogger<Program> logger)
    {
        var userContext = context.GetUserContext();
        if (userContext == null)
            return TypedResults.Unauthorized();

        try
        {
            var canUpdate = await policyEngine.CanPerformAsync(
                userContext.Id,
                $"/secrets/{name}",
                "secrets:write");

            if (!canUpdate)
                return TypedResults.Unauthorized();

            var secret = await secretRepository.GetByNameAsync(name);
            if (secret == null || secret.IsDeleted)
                return TypedResults.NotFound();

            var targetVersion = await secretRepository.GetSecretVersionAsync(secret.Id, version);
            if (targetVersion == null)
                return TypedResults.BadRequest($"Version {version} not found");

            // Note: In a real implementation, you'd restore the encrypted value from the version
            // For now, we just update the version reference
            secret.CurrentVersion = version;
            secret.UpdatedAt = DateTime.UtcNow;

            await secretRepository.UpdateAsync(secret);
            await secretRepository.SaveChangesAsync();

            await auditLogRepository.AddAsync(new Storage.Entities.AuditLog
            {
                Action = "UPDATE",
                Actor = userContext.Username,
                ResourceType = "Secret",
                ResourceId = name,
                Result = "SUCCESS",
                StatusCode = 200,
                SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                SecretId = secret.Id
            });
            await auditLogRepository.SaveChangesAsync();

            var response = new SecretResponse
            {
                Id = secret.Id,
                Name = secret.Name,
                Description = secret.Description,
                Version = secret.CurrentVersion,
                CreatedAt = secret.CreatedAt,
                UpdatedAt = secret.UpdatedAt,
                CreatedBy = secret.CreatedBy,
                IsDeleted = secret.IsDeleted,
                Tags = secret.Tags
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restoring secret {Name} to version {Version}", name, version);
            return TypedResults.BadRequest("Failed to restore version");
        }
    }

    private static List<string> ParseCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static DateTime? ParseUtcDateFilter(string? value, bool endOfDay)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            var dateTime = parsedDate.ToDateTime(endOfDay ? TimeOnly.MaxValue : TimeOnly.MinValue, DateTimeKind.Utc);
            return endOfDay ? dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerMicrosecond == 0 ? 0 : 0)) : dateTime;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDateTime))
        {
            return DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc);
        }

        throw new BadHttpRequestException($"Invalid date filter '{value}'. Use yyyy-MM-dd.");
    }
}
