using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using IdentityModel.Client;

namespace Keydral.CLI.Services;

/// <summary>
/// Response from Keycloak token endpoint.
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";
}

/// <summary>
/// Handles authentication with device flow for CLI login.
/// </summary>
public class AuthenticationService
{
    private readonly string _keycloakUrl;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly HttpClient _httpClient;

    public AuthenticationService(string keycloakUrl, string realm, string clientId)
    {
        _keycloakUrl = keycloakUrl.TrimEnd('/');
        _realm = realm;
        _clientId = clientId;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Initiate device flow login and return device code information.
    /// </summary>
    public async Task<DeviceAuthorizationResponse> RequestDeviceCodeAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/auth/device");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId)
        });

        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await System.Text.Json.JsonSerializer.DeserializeAsync<DeviceCodeResponse>(
            await response.Content.ReadAsStreamAsync()) ?? throw new InvalidOperationException("Invalid device code response");
        return new DeviceAuthorizationResponse
        {
            DeviceCode = json.DeviceCode,
            UserCode = json.UserCode,
            VerificationUrl = json.VerificationUrl,
            ExpiresIn = json.ExpiresIn,
            Interval = json.Interval
        };
    }

    /// <summary>
    /// Poll for token completion after user approves device code.
    /// </summary>
    public async Task<TokenResponse> PollForTokenAsync(string deviceCode, int expiresIn)
    {
        var tokenUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/token";
        var startTime = DateTime.UtcNow;
        var interval = 5; // Default poll interval in seconds

        while ((DateTime.UtcNow - startTime).TotalSeconds < expiresIn)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                    new KeyValuePair<string, string>("device_code", deviceCode),
                    new KeyValuePair<string, string>("client_id", _clientId)
                });

                request.Content = content;
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await System.Text.Json.JsonSerializer.DeserializeAsync<TokenResponse>(
                        await response.Content.ReadAsStreamAsync());
                    if (json != null) return json;
                }

                // If not ready yet, wait and retry
                await Task.Delay(interval * 1000);
            }
            catch (HttpRequestException)
            {
                // Network error, retry after interval
                await Task.Delay(interval * 1000);
            }
        }

        throw new InvalidOperationException("Device code authorization timed out");
    }

    /// <summary>
    /// Exchange refresh token for new access token.
    /// </summary>
    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var tokenUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/token";

        var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", _clientId)
        });

        request.Content = content;
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await System.Text.Json.JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync()) ?? throw new InvalidOperationException("Invalid token response");
        return json;
    }

    /// <summary>
    /// Extract claims from JWT token without validation (CLI use case).
    /// </summary>
    public Dictionary<string, object> ExtractClaims(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(accessToken);

            var claims = new Dictionary<string, object>();
            foreach (var claim in jwtToken.Claims)
            {
                claims[claim.Type] = claim.Value;
            }

            return claims;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }
}

/// <summary>
/// Keycloak device authorization response.
/// </summary>
internal class DeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("verification_url")]
    public string VerificationUrl { get; set; } = string.Empty;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

/// <summary>
/// Device authorization response returned to CLI.
/// </summary>
public class DeviceAuthorizationResponse
{
    public string DeviceCode { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUrl { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public int Interval { get; set; }
}
