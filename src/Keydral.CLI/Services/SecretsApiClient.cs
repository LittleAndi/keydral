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

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }
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
}
