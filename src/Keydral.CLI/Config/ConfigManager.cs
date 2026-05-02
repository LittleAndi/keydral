using System.Text.Json;

namespace Keydral.CLI.Config;

/// <summary>
/// Manages CLI configuration persistence and retrieval.
/// </summary>
public class ConfigManager
{
    private readonly string _configPath;
    private CliConfig? _config;

    public ConfigManager()
    {
        _configPath = CliConfig.GetConfigPath();
    }

    /// <summary>
    /// Load configuration from disk or return default.
    /// </summary>
    public async Task<CliConfig> LoadAsync()
    {
        if (_config != null)
            return _config;

        try
        {
            if (!File.Exists(_configPath))
            {
                _config = new CliConfig();
                return _config;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            _config = JsonSerializer.Deserialize<CliConfig>(json) ?? new CliConfig();
            return _config;
        }
        catch
        {
            _config = new CliConfig();
            return _config;
        }
    }

    /// <summary>
    /// Save configuration to disk.
    /// </summary>
    public async Task SaveAsync(CliConfig config)
    {
        _config = config;

        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save config: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Clear stored credentials (logout).
    /// </summary>
    public async Task ClearCredentialsAsync()
    {
        if (_config == null)
            return;

        _config.AccessToken = null;
        _config.RefreshToken = null;
        _config.TokenExpiresAt = null;
        _config.Username = null;
        _config.UserId = null;

        await SaveAsync(_config);
    }

    /// <summary>
    /// Update and persist token info.
    /// </summary>
    public async Task UpdateTokenAsync(string accessToken, string? refreshToken, int expiresInSeconds, string username, string userId)
    {
        if (_config == null)
            _config = new CliConfig();

        _config.AccessToken = accessToken;
        _config.RefreshToken = refreshToken;
        _config.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        _config.Username = username;
        _config.UserId = userId;

        await SaveAsync(_config);
    }
}
