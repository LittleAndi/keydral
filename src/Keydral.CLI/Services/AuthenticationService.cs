using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;

namespace Keydral.CLI.Services;

/// <summary>
/// Typed exception for OAuth 2.0 Device Flow errors (RFC 8628).
/// </summary>
public class OAuth2Exception : Exception
{
    public string? ErrorCode { get; set; }
    public string? ErrorDescription { get; set; }
    public bool IsRecoverable { get; set; } = false;

    public OAuth2Exception(string message) : base(message) { }

    public OAuth2Exception(string errorCode, string message, bool isRecoverable = false)
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorDescription = message;
        IsRecoverable = isRecoverable;
    }
}

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
/// OAuth 2.0 error response (RFC 8628).
/// </summary>
internal class OAuth2ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }
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

        if (!response.IsSuccessStatusCode)
        {
            throw new OAuth2Exception("device_request_failed",
                $"Failed to initiate device flow: {response.StatusCode}");
        }

        var json = await System.Text.Json.JsonSerializer.DeserializeAsync<DeviceCodeResponse>(
            await response.Content.ReadAsStreamAsync()) ?? throw new OAuth2Exception("invalid_response", "Invalid device code response from server");
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
    /// Implements RFC 8628 Device Authorization Grant error handling.
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

                // Parse error response per RFC 8628
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorJson = await System.Text.Json.JsonSerializer.DeserializeAsync<OAuth2ErrorResponse>(
                        await response.Content.ReadAsStreamAsync());

                    if (errorJson?.Error != null)
                    {
                        switch (errorJson.Error)
                        {
                            // Fast-fail errors: user action required or unrecoverable
                            case "access_denied":
                                throw new OAuth2Exception(
                                    "access_denied",
                                    "Login was denied. Please try again and approve the request in the browser.",
                                    isRecoverable: true);

                            case "expired_token":
                                throw new OAuth2Exception(
                                    "expired_token",
                                    "Device code has expired. Please start the login process again.",
                                    isRecoverable: true);

                            case "invalid_grant":
                                throw new OAuth2Exception(
                                    "invalid_grant",
                                    "Invalid device code. Please try logging in again.",
                                    isRecoverable: true);

                            // Recoverable errors: keep polling
                            case "authorization_pending":
                            case "slow_down":
                                // Continue polling (slow_down handling: exponential backoff in future)
                                break;

                            // Unknown error
                            default:
                                throw new OAuth2Exception(
                                    errorJson.Error,
                                    errorJson.ErrorDescription ?? $"OAuth error: {errorJson.Error}",
                                    isRecoverable: false);
                        }
                    }
                }

                // If not ready yet, wait and retry
                await Task.Delay(interval * 1000);
            }
            catch (OAuth2Exception)
            {
                throw; // Re-throw OAuth2 errors immediately
            }
            catch (HttpRequestException)
            {
                // Network error, retry after interval
                await Task.Delay(interval * 1000);
            }
        }

        throw new OAuth2Exception("timeout", "Device code authorization timed out. Please check your internet connection and try again.", isRecoverable: true);
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

        if (!response.IsSuccessStatusCode)
        {
            // Parse error response
            var errorJson = await System.Text.Json.JsonSerializer.DeserializeAsync<OAuth2ErrorResponse>(
                await response.Content.ReadAsStreamAsync());

            if (errorJson?.Error != null)
            {
                throw new OAuth2Exception(
                    errorJson.Error,
                    errorJson.ErrorDescription ?? $"Token refresh failed: {errorJson.Error}",
                    isRecoverable: true);
            }

            response.EnsureSuccessStatusCode();
        }

        var json = await System.Text.Json.JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync()) ?? throw new OAuth2Exception("invalid_response", "Invalid token response from server");
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
