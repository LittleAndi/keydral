using Keydral.CLI.Commands;

namespace Keydral.CLI.Tests;

public class LoginCommandTests
{
    [Fact]
    public void GetTokenLifetimeWarning_WithShortLivedToken_ReturnsWarning()
    {
        var now = new DateTimeOffset(2026, 5, 13, 22, 0, 0, TimeSpan.Zero);
        var claims = new Dictionary<string, object>
        {
            ["exp"] = now.AddMinutes(3).ToUnixTimeSeconds().ToString()
        };

        var warning = LoginCommand.GetTokenLifetimeWarning(claims, now);

        Assert.Equal(
            "Token expires in 3 minutes — consider refreshing before running long commands",
            warning);
    }

    [Fact]
    public void GetTokenLifetimeWarning_WithTokenAtThreshold_DoesNotWarn()
    {
        var now = new DateTimeOffset(2026, 5, 13, 22, 0, 0, TimeSpan.Zero);
        var claims = new Dictionary<string, object>
        {
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds()
        };

        var warning = LoginCommand.GetTokenLifetimeWarning(claims, now);

        Assert.Null(warning);
    }

    [Fact]
    public void GetTokenLifetimeWarning_WithoutExpClaim_DoesNotWarn()
    {
        var warning = LoginCommand.GetTokenLifetimeWarning(
            new Dictionary<string, object>(),
            new DateTimeOffset(2026, 5, 13, 22, 0, 0, TimeSpan.Zero));

        Assert.Null(warning);
    }
}
