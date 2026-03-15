using Spectre.Console;
using Keydral.CLI.Config;
using Keydral.CLI.Services;

namespace Keydral.CLI.Commands;

/// <summary>
/// Login command - authenticates user via device flow.
/// </summary>
public class LoginCommand
{
    private readonly ConfigManager _configManager;

    public LoginCommand(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();

            AnsiConsole.MarkupLine("[bold cyan]Keydral CLI Login[/]");
            AnsiConsole.MarkupLine($"[dim]Keycloak: {config.KeycloakUrl}[/]");
            AnsiConsole.MarkupLine("");

            var authService = new AuthenticationService(config.KeycloakUrl, config.Realm, config.ClientId);

            AnsiConsole.MarkupLine("[yellow]Requesting device code...[/]");
            var deviceAuth = await authService.RequestDeviceCodeAsync();

            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[bold green]Device Code:[/] " + deviceAuth.UserCode);
            AnsiConsole.MarkupLine($"[yellow]Visit:[/] {deviceAuth.VerificationUrl}");
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[dim]Waiting for authorization (timeout in " +
                deviceAuth.ExpiresIn + " seconds)...[/]");

            var tokenResponse = await authService.PollForTokenAsync(deviceAuth.DeviceCode, deviceAuth.ExpiresIn);

            // Extract user info from token
            var claims = authService.ExtractClaims(tokenResponse.AccessToken);
            var username = claims.ContainsKey("preferred_username") ?
                (string)claims["preferred_username"] : "user";
            var userId = claims.ContainsKey("sub") ?
                (string)claims["sub"] : string.Empty;

            // Save token
            await _configManager.UpdateTokenAsync(
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                username,
                userId);

            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[bold green]✓ Login successful![/]");
            AnsiConsole.MarkupLine($"[dim]Username: {username}[/]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[dim]You can now use:[/]");
            AnsiConsole.MarkupLine("  [cyan]keydral secret list[/]");
            AnsiConsole.MarkupLine("  [cyan]keydral secret get <name>[/]");
            AnsiConsole.MarkupLine("  [cyan]keydral secret set <name> <value>[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]✗ Login failed:[/] {ex.Message}");
            Environment.Exit(1);
        }
    }
}
