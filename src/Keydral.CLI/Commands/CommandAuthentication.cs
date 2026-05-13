using Keydral.CLI.Config;
using Keydral.CLI.Services;

namespace Keydral.CLI.Commands;

internal static class CommandAuthentication
{
    public static async Task<CliConfig> GetAuthenticatedConfigAsync(ConfigManager configManager)
    {
        var config = await configManager.LoadAsync();

        if (string.IsNullOrWhiteSpace(config.AccessToken))
        {
            throw new InvalidOperationException("Not authenticated. Please run: keydral login");
        }

        if (!config.IsTokenValid() && !string.IsNullOrWhiteSpace(config.RefreshToken))
        {
            var authService = new AuthenticationService(config.KeycloakUrl, config.Realm, config.ClientId);
            var tokenResponse = await authService.RefreshTokenAsync(config.RefreshToken);

            await configManager.UpdateTokenAsync(
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                config.Username ?? "user",
                config.UserId ?? string.Empty);

            config = await configManager.LoadAsync();
        }

        if (!config.IsTokenValid())
        {
            throw new InvalidOperationException("Token expired. Please run: keydral login");
        }

        return config;
    }
}
