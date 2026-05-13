using Spectre.Console;
using Keydral.CLI.Config;
using Keydral.CLI.Services;
using System.Globalization;

namespace Keydral.CLI.Commands;

/// <summary>
/// Command for audit-log search operations.
/// </summary>
public class AuditCommand
{
    private readonly ConfigManager _configManager;
    private readonly string _operation;
    private readonly string[] _args;

    public AuditCommand(ConfigManager configManager, string operation, string[] args)
    {
        _configManager = configManager;
        _operation = operation.ToLowerInvariant();
        _args = args;
    }

    public async Task ExecuteAsync()
    {
        var config = await CommandAuthentication.GetAuthenticatedConfigAsync(_configManager);
        var client = new SecretsApiClient(config.ApiUrl, config.AccessToken!);

        switch (_operation)
        {
            case "search":
                await SearchAuditLogsAsync(client);
                break;
            default:
                throw new InvalidOperationException($"Unknown audit operation: {_operation}");
        }
    }

    internal static AuditSearchCommandOptions ParseSearchOptions(string[] args)
    {
        var request = new AuditSearchOptions();
        var queryParts = new List<string>();
        var asJson = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--actor":
                    request.Actor = ReadRequiredValue(args, ref index, "--actor");
                    break;
                case "--action":
                    request.Action = ReadRequiredValue(args, ref index, "--action");
                    break;
                case "--result":
                    request.Result = ReadRequiredValue(args, ref index, "--result");
                    break;
                case "--resource-type":
                    request.ResourceType = ReadRequiredValue(args, ref index, "--resource-type");
                    break;
                case "--resource-id":
                    request.ResourceId = ReadRequiredValue(args, ref index, "--resource-id");
                    break;
                case "--from-date":
                    request.FromDate = ParseDate(ReadRequiredValue(args, ref index, "--from-date"), "--from-date");
                    break;
                case "--to-date":
                    request.ToDate = ParseDate(ReadRequiredValue(args, ref index, "--to-date"), "--to-date");
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
        return new AuditSearchCommandOptions(request, asJson);
    }

    private async Task SearchAuditLogsAsync(SecretsApiClient client)
    {
        var options = ParseSearchOptions(_args);
        var result = await client.SearchAuditLogsAsync(options.Request);

        if (result == null || result.Items.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No audit log entries found[/]");
            return;
        }

        if (options.AsJson)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.Write(json);
            return;
        }

        var table = new Table();
        table.AddColumn("[cyan]Timestamp[/]");
        table.AddColumn("[cyan]Action[/]");
        table.AddColumn("[cyan]Actor[/]");
        table.AddColumn("[cyan]Resource[/]");
        table.AddColumn("[cyan]Result[/]");

        foreach (var auditLog in result.Items)
        {
            table.AddRow(
                auditLog.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                auditLog.Action,
                auditLog.Actor,
                $"{auditLog.ResourceType}:{auditLog.ResourceId}",
                auditLog.Result);
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
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new InvalidOperationException($"{optionName} must be a valid date in yyyy-MM-dd format");
        }

        return parsed;
    }
}

internal sealed record AuditSearchCommandOptions(AuditSearchOptions Request, bool AsJson);
