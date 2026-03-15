using System.Text.Json.Serialization;

namespace Keydral.CLI.Config;

/// <summary>
/// CLI configuration and state management.
/// Stores API endpoint, token, and user context.
/// </summary>
public class CliConfig
{
    [JsonPropertyName("api_url")]
    public string ApiUrl { get; set; } = "http://localhost:5001";

    [JsonPropertyName("keycloak_url")]
    public string KeycloakUrl { get; set; } = "http://localhost:8080";

    [JsonPropertyName("realm")]
    public string Realm { get; set; } = "master";

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "keydral-cli";

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    /// <summary>
    /// Get the config file path (platform-specific).
    /// </summary>
    public static string GetConfigPath()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var keydralDir = Path.Combine(appDataDir, "keydral");
        var configPath = Path.Combine(keydralDir, "config.json");
        return configPath;
    }

    /// <summary>
    /// Check if access token is still valid.
    /// </summary>
    public bool IsTokenValid()
    {
        if (string.IsNullOrEmpty(AccessToken) || !TokenExpiresAt.HasValue)
            return false;

        // Consider token valid if it expires in more than 1 minute
        return DateTime.UtcNow.AddMinutes(1) < TokenExpiresAt.Value;
    }
}
