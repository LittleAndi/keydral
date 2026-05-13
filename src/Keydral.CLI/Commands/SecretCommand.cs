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
            var config = await CommandAuthentication.GetAuthenticatedConfigAsync(_configManager);
            var client = new SecretsApiClient(config.ApiUrl, config.AccessToken!);

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
                case "search":
                    await SearchSecretsAsync(client);
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

    internal static SecretSearchCommandOptions ParseSearchOptions(string[] args)
    {
        var request = new SecretSearchOptions();
        var queryParts = new List<string>();
        var asJson = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--tag":
                    request.Tags.Add(ReadRequiredValue(args, ref index, "--tag"));
                    break;
                case "--created-after":
                    request.CreatedAfter = ParseDate(ReadRequiredValue(args, ref index, "--created-after"), "--created-after");
                    break;
                case "--created-before":
                    request.CreatedBefore = ParseDate(ReadRequiredValue(args, ref index, "--created-before"), "--created-before");
                    break;
                case "--updated-after":
                    request.UpdatedAfter = ParseDate(ReadRequiredValue(args, ref index, "--updated-after"), "--updated-after");
                    break;
                case "--updated-before":
                    request.UpdatedBefore = ParseDate(ReadRequiredValue(args, ref index, "--updated-before"), "--updated-before");
                    break;
                case "--created-by":
                    request.CreatedBy = ReadRequiredValue(args, ref index, "--created-by");
                    break;
                case "--page-number":
                    request.PageNumber = ParsePositiveInt(ReadRequiredValue(args, ref index, "--page-number"), "--page-number");
                    break;
                case "--page-size":
                    request.PageSize = ParsePositiveInt(ReadRequiredValue(args, ref index, "--page-size"), "--page-size");
                    break;
                case "--json":
                    asJson = true;
                    break;
                default:
                    queryParts.Add(args[index]);
                    break;
            }
        }

        request.Query = queryParts.Count == 0 ? null : string.Join(' ', queryParts);
        return new SecretSearchCommandOptions(request, asJson);
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
                secret.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        AnsiConsole.Write(table);
    }

    private async Task SearchSecretsAsync(SecretsApiClient client)
    {
        var options = ParseSearchOptions(_args);
        var result = await client.SearchSecretsAsync(options.Request);

        if (result == null || result.Items.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No secrets found[/]");
            return;
        }

        if (options.AsJson)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.Write(json);
            return;
        }

        var table = new Table();
        table.AddColumn("[cyan]Name[/]");
        table.AddColumn("[cyan]Version[/]");
        table.AddColumn("[cyan]Tags[/]");
        table.AddColumn("[cyan]Updated At[/]");

        foreach (var secret in result.Items)
        {
            table.AddRow(
                secret.Name,
                secret.Version.ToString(),
                secret.Tags ?? "-",
                secret.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        AnsiConsole.Write(table);
    }

    private static string ReadRequiredValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for {optionName}");
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string optionName)
    {
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException($"{optionName} must be a positive integer");
        }

        return parsed;
    }

    private static DateTime ParseDate(string value, string optionName)
    {
        if (!DateTime.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"{optionName} must be a valid date");
        }

        return parsed;
    }
}

internal sealed record SecretSearchCommandOptions(SecretSearchOptions Request, bool AsJson);
