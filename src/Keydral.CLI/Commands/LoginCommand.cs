using System.Globalization;
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
    private const int PollingStatusRefreshMilliseconds = 200;
    private static readonly TimeSpan ShortLivedTokenWarningThreshold = TimeSpan.FromMinutes(5);

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

            var tokenResponse = await WaitForAuthorizationAsync(authService, deviceAuth);

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

            var tokenLifetimeWarning = GetTokenLifetimeWarning(claims, DateTimeOffset.UtcNow);
            if (tokenLifetimeWarning is not null)
            {
                AnsiConsole.MarkupLine($"[bold yellow]⚠ {Markup.Escape(tokenLifetimeWarning)}[/]");
            }

            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[dim]You can now use:[/]");
            AnsiConsole.MarkupLine("  [cyan]keydral secret list[/]");
            AnsiConsole.MarkupLine("  [cyan]keydral secret get <name>[/]");
            AnsiConsole.MarkupLine("  [cyan]keydral secret set <name> <value>[/]");
        }
        catch (OAuth2Exception ex)
        {
            if (ex.IsRecoverable)
            {
                AnsiConsole.MarkupLine($"[bold yellow]⚠ Login was interrupted:[/] {ex.Message}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold red]✗ Login failed:[/] {ex.Message}");
                if (!string.IsNullOrEmpty(ex.ErrorCode))
                {
                    AnsiConsole.MarkupLine($"[dim]Error code: {ex.ErrorCode}[/]");
                }
            }
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]✗ Unexpected error:[/] {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task<TokenResponse> WaitForAuthorizationAsync(
        AuthenticationService authService,
        DeviceAuthorizationResponse deviceAuth)
    {
        var statusLock = new object();
        var currentStatus = "Waiting for authorization...";
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(deviceAuth.ExpiresIn);

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(BuildPollingStatus(GetCurrentStatus(), GetSecondsRemaining(expiresAt)), async ctx =>
            {
                var pollingTask = authService.PollForTokenAsync(
                    deviceAuth.DeviceCode,
                    deviceAuth.ExpiresIn,
                    deviceAuth.Interval,
                    update =>
                    {
                        lock (statusLock)
                        {
                            currentStatus = update.StatusMessage;
                        }
                    });

                while (!pollingTask.IsCompleted)
                {
                    ctx.Status(BuildPollingStatus(GetCurrentStatus(), GetSecondsRemaining(expiresAt)));
                    await Task.WhenAny(
                        pollingTask,
                        Task.Delay(PollingStatusRefreshMilliseconds));
                }

                return await pollingTask;
            });

        string GetCurrentStatus()
        {
            lock (statusLock)
            {
                return currentStatus;
            }
        }
    }

    private static string BuildPollingStatus(string status, int secondsRemaining) =>
        $"{Markup.Escape(status)} [dim](expires in {secondsRemaining}s)[/]";

    private static int GetSecondsRemaining(DateTimeOffset expiresAt) =>
        Math.Max(0, (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));

    internal static string? GetTokenLifetimeWarning(
        IReadOnlyDictionary<string, object> claims,
        DateTimeOffset nowUtc)
    {
        if (!TryGetExpiration(claims, out var expiresAt))
        {
            return null;
        }

        var remainingLifetime = expiresAt - nowUtc;
        if (remainingLifetime >= ShortLivedTokenWarningThreshold || remainingLifetime <= TimeSpan.Zero)
        {
            return null;
        }

        return $"Token expires in {FormatRemainingLifetime(remainingLifetime)} — consider refreshing before running long commands";
    }

    private static bool TryGetExpiration(
        IReadOnlyDictionary<string, object> claims,
        out DateTimeOffset expiresAt)
    {
        expiresAt = default;

        if (!claims.TryGetValue("exp", out var expValue))
        {
            return false;
        }

        var expText = Convert.ToString(expValue, CultureInfo.InvariantCulture);
        if (!long.TryParse(expText, CultureInfo.InvariantCulture, out var unixTimeSeconds))
        {
            return false;
        }

        expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        return true;
    }

    private static string FormatRemainingLifetime(TimeSpan remainingLifetime)
    {
        if (remainingLifetime.TotalMinutes >= 1)
        {
            var wholeMinutes = (int)Math.Floor(remainingLifetime.TotalMinutes);
            return wholeMinutes == 1 ? "1 minute" : $"{wholeMinutes} minutes";
        }

        var wholeSeconds = Math.Max(1, (int)Math.Floor(remainingLifetime.TotalSeconds));
        return wholeSeconds == 1 ? "1 second" : $"{wholeSeconds} seconds";
    }
}
