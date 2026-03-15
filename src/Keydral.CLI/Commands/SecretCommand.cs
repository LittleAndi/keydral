using Spectre.Console;
using Keydral.CLI.Config;
using Keydral.CLI.Services;

namespace Keydral.CLI.Commands;

/// <summary>
/// Command for secret operations (get, set, delete, list).
/// </summary>
public class SecretCommand
{
    private readonly ConfigManager _configManager;
    private readonly string _operation;
    private readonly string[] _args;

    public SecretCommand(ConfigManager configManager, string operation, string[] args)
    {
        _configManager = configManager;
        _operation = operation.ToLower();
        _args = args;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();

            // Check authentication
            if (string.IsNullOrEmpty(config.AccessToken))
            {
                AnsiConsole.MarkupLine("[bold red]✗ Not authenticated[/]");
                AnsiConsole.MarkupLine("Please run: [cyan]keydral login[/]");
                Environment.Exit(1);
            }

            // Handle token refresh if needed
            if (!config.IsTokenValid() && !string.IsNullOrEmpty(config.RefreshToken))
            {
                await RefreshTokenAsync(config);
            }

            if (!config.IsTokenValid())
            {
                AnsiConsole.MarkupLine("[bold red]✗ Token expired[/]");
                AnsiConsole.MarkupLine("Please run: [cyan]keydral login[/]");
                Environment.Exit(1);
            }

            var client = new SecretsApiClient(config.ApiUrl, config.AccessToken);

            switch (_operation)
            {
                case "get":
                    await GetSecretAsync(client);
                    break;
                case "set":
                    await SetSecretAsync(client, config);
                    break;
                case "delete":
                    await DeleteSecretAsync(client);
                    break;
                case "list":
                    await ListSecretsAsync(client);
                    break;
                default:
                    AnsiConsole.MarkupLine($"[bold red]✗ Unknown operation:[/] {_operation}");
                    Environment.Exit(1);
                    break;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]✗ Error:[/] {ex.Message}");
            Environment.Exit(1);
        }
    }

    private async Task GetSecretAsync(SecretsApiClient client)
    {
        if (_args.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] keydral secret get <name>");
            Environment.Exit(1);
        }

        var name = _args[0];
        var secret = await client.GetSecretAsync(name);

        if (secret == null)
        {
            AnsiConsole.MarkupLine($"[bold red]✗ Secret not found:[/] {name}");
            Environment.Exit(1);
        }

        // Check for --json flag
        if (_args.Length > 1 && _args[1] == "--json")
        {
            var json = System.Text.Json.JsonSerializer.Serialize(secret, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.Write(json);
        }
        else
        {
            AnsiConsole.MarkupLine($"[cyan]{secret.Name}[/] (v{secret.Version})");
            AnsiConsole.MarkupLine($"[bold]{secret.Value}[/]");
            if (!string.IsNullOrEmpty(secret.Description))
                AnsiConsole.MarkupLine($"[dim]{secret.Description}[/]");
        }
    }

    private async Task SetSecretAsync(SecretsApiClient client, CliConfig config)
    {
        if (_args.Length < 2)
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] keydral secret set <name> <value> [--description <desc>]");
            Environment.Exit(1);
        }

        var name = _args[0];
        var value = _args[1];
        string? description = null;

        // Parse optional description
        for (int i = 2; i < _args.Length; i++)
        {
            if (_args[i] == "--description" && i + 1 < _args.Length)
            {
                description = _args[i + 1];
                i++;
            }
        }

        AnsiConsole.MarkupLine("[yellow]Storing secret...[/]");

        var result = await client.SetSecretAsync(name, value, description);
        if (result == null)
        {
            AnsiConsole.MarkupLine($"[bold red]✗ Failed to store secret[/]");
            Environment.Exit(1);
        }

        AnsiConsole.MarkupLine($"[bold green]✓ Secret stored:[/] {result.Name} (v{result.Version})");
    }

    private async Task DeleteSecretAsync(SecretsApiClient client)
    {
        if (_args.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] keydral secret delete <name>");
            Environment.Exit(1);
        }

        var name = _args[0];

        // Confirm deletion
        if (!AnsiConsole.Confirm($"Delete secret '[red]{name}[/]'?", false))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled[/]");
            return;
        }

        var success = await client.DeleteSecretAsync(name);
        if (!success)
        {
            AnsiConsole.MarkupLine($"[bold red]✗ Failed to delete secret[/]");
            Environment.Exit(1);
        }

        AnsiConsole.MarkupLine($"[bold green]✓ Secret deleted:[/] {name}");
    }

    private async Task ListSecretsAsync(SecretsApiClient client)
    {
        AnsiConsole.MarkupLine("[yellow]Fetching secrets...[/]");

        var result = await client.ListSecretsAsync();
        if (result == null || result.Items.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No secrets found[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("[cyan]Name[/]");
        table.AddColumn("[cyan]Version[/]");
        table.AddColumn("[cyan]Created By[/]");
        table.AddColumn("[cyan]Created At[/]");

        foreach (var secret in result.Items)
        {
            table.AddRow(
                secret.Name,
                secret.Version.ToString(),
                secret.CreatedBy ?? "-",
                secret.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        AnsiConsole.Write(table);
    }

    private async Task RefreshTokenAsync(CliConfig config)
    {
        try
        {
            var authService = new AuthenticationService(config.KeycloakUrl, config.Realm, config.ClientId);
            var tokenResponse = await authService.RefreshTokenAsync(config.RefreshToken!);

            await _configManager.UpdateTokenAsync(
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                config.Username ?? "user",
                config.UserId ?? string.Empty);

            // Reload config
            var updated = await _configManager.LoadAsync();
            config.AccessToken = updated.AccessToken;
            config.RefreshToken = updated.RefreshToken;
            config.TokenExpiresAt = updated.TokenExpiresAt;
        }
        catch
        {
            // Silently fail token refresh, will fail on next operation
        }
    }
}
