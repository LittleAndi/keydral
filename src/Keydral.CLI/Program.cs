using Spectre.Console;
using Keydral.CLI.Config;
using Keydral.CLI.Commands;

// Parse command-line arguments
if (args.Length == 0)
{
    ShowHelp();
    Environment.Exit(0);
}

var command = args[0].ToLower();
var commandArgs = args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>();

var configManager = new ConfigManager();

try
{
    switch (command)
    {
        case "login":
            var loginCmd = new LoginCommand(configManager);
            await loginCmd.ExecuteAsync();
            break;

        case "logout":
            await configManager.ClearCredentialsAsync();
            AnsiConsole.MarkupLine("[bold green]✓ Logged out[/]");
            break;

        case "secret":
            if (commandArgs.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Usage:[/] keydral secret <operation> [options]");
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("[cyan]Operations:[/]");
                AnsiConsole.MarkupLine("  [green]list[/]                    List all accessible secrets");
                AnsiConsole.MarkupLine("  [green]get <name>[/]              Get secret value");
                AnsiConsole.MarkupLine("  [green]set <name> <value>[/]     Set/update secret");
                AnsiConsole.MarkupLine("  [green]delete <name>[/]           Delete secret");
                Environment.Exit(0);
            }
            var secretCmd = new SecretCommand(configManager, commandArgs[0], commandArgs.Skip(1).ToArray());
            await secretCmd.ExecuteAsync();
            break;

        case "--version":
        case "-v":
            AnsiConsole.MarkupLine("Keydral CLI [cyan]1.0.0[/]");
            break;

        case "--help":
        case "-h":
        case "help":
            ShowHelp();
            break;

        default:
            AnsiConsole.MarkupLine($"[bold red]✗ Unknown command:[/] {command}");
            ShowHelp();
            Environment.Exit(1);
            break;
    }
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[bold red]✗ Error:[/] {ex.Message}");
    if (Environment.GetEnvironmentVariable("KEYDRAL_DEBUG") == "1")
    {
        AnsiConsole.WriteException(ex);
    }
    Environment.Exit(1);
}

void ShowHelp()
{
    Console.WriteLine("Keydral CLI - Local Secrets Vault");
    Console.WriteLine("");
    Console.WriteLine("Usage:");
    Console.WriteLine("  keydral [command] [options]");
    Console.WriteLine("");
    Console.WriteLine("Commands:");
    Console.WriteLine("  login                   Authenticate with Keydral");
    Console.WriteLine("  logout                  Clear stored credentials");
    Console.WriteLine("  secret                  Secret management");
    Console.WriteLine("  help                    Show this help message");
    Console.WriteLine("");
    Console.WriteLine("Secret Operations:");
    Console.WriteLine("  keydral secret list                  List secrets");
    Console.WriteLine("  keydral secret get <name>            Get secret");
    Console.WriteLine("  keydral secret set <name> <value>   Set secret");
    Console.WriteLine("  keydral secret delete <name>         Delete secret");
    Console.WriteLine("");
    Console.WriteLine("Options:");
    Console.WriteLine("  --help, -h              Show help");
    Console.WriteLine("  --version, -v           Show version");
    Console.WriteLine("");
    Console.WriteLine("Examples:");
    Console.WriteLine("  keydral login");
    Console.WriteLine("  keydral secret set db-password 'super-secret'");
    Console.WriteLine("  keydral secret get db-password");
    Console.WriteLine("  keydral secret list");
}

