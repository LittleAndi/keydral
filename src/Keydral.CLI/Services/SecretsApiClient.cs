using System.Text.Json.Serialization;

namespace Keydral.CLI.Services;

/// <summary>
/// DTO for secret response from API.
/// </summary>
public class SecretDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for audit log list/search responses.
/// </summary>
public class AuditLogListItemDto
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;

    [JsonPropertyName("resource_type")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonPropertyName("resource_id")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DTO for secret list item.
/// </summary>
public class SecretListItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }
}

/// <summary>
/// DTO for paginated response.
/// </summary>
public class PaginatedResponse<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("has_next_page")]
    public bool HasNextPage { get; set; }
}

public sealed class SecretSearchOptions
{
    public string? Query { get; set; }

    public List<string> Tags { get; set; } = [];

    public DateTime? CreatedAfter { get; set; }

    public DateTime? CreatedBefore { get; set; }

    public DateTime? UpdatedAfter { get; set; }

    public DateTime? UpdatedBefore { get; set; }

    public string? CreatedBy { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class AuditSearchOptions
{
    public string? Query { get; set; }

    public string? Actor { get; set; }

    public string? Action { get; set; }

    public string? Result { get; set; }

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>
/// API client for secrets operations.
/// </summary>
public class SecretsApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;

    public SecretsApiClient(string apiUrl, string accessToken)
    {
        _apiUrl = apiUrl.TrimEnd('/');
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Get a secret by name.
    /// </summary>
    public async Task<SecretDto?> GetSecretAsync(string name)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/api/secrets/{Uri.EscapeDataString(name)}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await System.Text.Json.JsonSerializer.DeserializeAsync<SecretDto>(
                await response.Content.ReadAsStreamAsync());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// List all accessible secrets.
    /// </summary>
    public async Task<PaginatedResponse<SecretListItemDto>?> ListSecretsAsync(int pageNumber = 1, int pageSize = 50)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_apiUrl}/api/secrets?pageNumber={pageNumber}&pageSize={pageSize}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await System.Text.Json.JsonSerializer.DeserializeAsync<PaginatedResponse<SecretListItemDto>>(
                await response.Content.ReadAsStreamAsync());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Search secrets using the dedicated search endpoint.
    /// </summary>
    public async Task<PaginatedResponse<SecretListItemDto>?> SearchSecretsAsync(SecretSearchOptions options)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}{BuildSecretSearchPath(options)}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await System.Text.Json.JsonSerializer.DeserializeAsync<PaginatedResponse<SecretListItemDto>>(
                await response.Content.ReadAsStreamAsync());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Search audit logs using the dedicated search endpoint.
    /// </summary>
    public async Task<PaginatedResponse<AuditLogListItemDto>?> SearchAuditLogsAsync(AuditSearchOptions options)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}{BuildAuditSearchPath(options)}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await System.Text.Json.JsonSerializer.DeserializeAsync<PaginatedResponse<AuditLogListItemDto>>(
                await response.Content.ReadAsStreamAsync());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create or update a secret.
    /// </summary>
    public async Task<SecretDto?> SetSecretAsync(string name, string value, string? description = null, Dictionary<string, string>? tags = null)
    {
        try
        {
            var request = new { value, description, tags };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PutAsync(
                $"{_apiUrl}/api/secrets/{Uri.EscapeDataString(name)}", content);

            if (!response.IsSuccessStatusCode)
                return null;

            return await System.Text.Json.JsonSerializer.DeserializeAsync<SecretDto>(
                await response.Content.ReadAsStreamAsync());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Delete a secret.
    /// </summary>
    public async Task<bool> DeleteSecretAsync(string name)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"{_apiUrl}/api/secrets/{Uri.EscapeDataString(name)}");

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get secret version history.
    /// </summary>
    public async Task<List<dynamic>?> GetSecretVersionsAsync(string name)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_apiUrl}/api/secrets/{Uri.EscapeDataString(name)}/versions");

            if (!response.IsSuccessStatusCode)
                return null;

            return await System.Text.Json.JsonSerializer.DeserializeAsync<List<dynamic>>(
                await response.Content.ReadAsStreamAsync());
        }
        catch
        {
            return null;
        }
    }

    internal static string BuildSecretSearchPath(SecretSearchOptions options)
    {
        var queryString = BuildQueryString(new Dictionary<string, string?>
        {
            ["q"] = options.Query,
            ["tags"] = options.Tags.Count == 0 ? null : string.Join(',', options.Tags),
            ["created-after"] = options.CreatedAfter?.ToString("yyyy-MM-dd"),
            ["created-before"] = options.CreatedBefore?.ToString("yyyy-MM-dd"),
            ["updated-after"] = options.UpdatedAfter?.ToString("yyyy-MM-dd"),
            ["updated-before"] = options.UpdatedBefore?.ToString("yyyy-MM-dd"),
            ["created-by"] = options.CreatedBy,
            ["pageNumber"] = options.PageNumber.ToString(),
            ["pageSize"] = options.PageSize.ToString()
        });

        return string.IsNullOrWhiteSpace(queryString) ? "/api/secrets/search" : $"/api/secrets/search?{queryString}";
    }

    internal static string BuildAuditSearchPath(AuditSearchOptions options)
    {
        var queryString = BuildQueryString(new Dictionary<string, string?>
        {
            ["q"] = options.Query,
            ["actor"] = options.Actor,
            ["action"] = options.Action,
            ["result"] = options.Result,
            ["resource-type"] = options.ResourceType,
            ["resource-id"] = options.ResourceId,
            ["from-date"] = options.FromDate?.ToString("yyyy-MM-dd"),
            ["to-date"] = options.ToDate?.ToString("yyyy-MM-dd"),
            ["pageNumber"] = options.PageNumber.ToString(),
            ["pageSize"] = options.PageSize.ToString()
        });

        return string.IsNullOrWhiteSpace(queryString) ? "/api/audit-logs/search" : $"/api/audit-logs/search?{queryString}";
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string?> parameters)
    {
        return string.Join("&",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));
    }
}
