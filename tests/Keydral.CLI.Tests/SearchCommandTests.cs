using Keydral.CLI.Commands;
using Keydral.CLI.Services;

namespace Keydral.CLI.Tests;

public class SearchCommandTests
{
    [Fact]
    public void ParseSecretSearchOptions_ParsesFiltersAndJsonFlag()
    {
        var options = SecretCommand.ParseSearchOptions(
        [
            "postgres",
            "--tag", "production",
            "--tag", "backend",
            "--created-by", "alice@example.com",
            "--page-number", "2",
            "--page-size", "25",
            "--json"
        ]);

        Assert.Equal("postgres", options.Request.Query);
        Assert.Equal(["production", "backend"], options.Request.Tags);
        Assert.Equal("alice@example.com", options.Request.CreatedBy);
        Assert.Equal(2, options.Request.PageNumber);
        Assert.Equal(25, options.Request.PageSize);
        Assert.True(options.AsJson);
    }

    [Fact]
    public void ParseAuditSearchOptions_ParsesAdvancedFilters()
    {
        var options = AuditCommand.ParseSearchOptions(
        [
            "--action", "CREATE",
            "--result", "SUCCESS",
            "--resource-type", "Secret",
            "--resource-id", "db-password",
            "--page-size", "10"
        ]);

        Assert.Equal("CREATE", options.Request.Action);
        Assert.Equal("SUCCESS", options.Request.Result);
        Assert.Equal("Secret", options.Request.ResourceType);
        Assert.Equal("db-password", options.Request.ResourceId);
        Assert.Equal(10, options.Request.PageSize);
    }

    [Fact]
    public void BuildSecretSearchPath_EncodesFilters()
    {
        var path = SecretsApiClient.BuildSecretSearchPath(new SecretSearchOptions
        {
            Query = "db password",
            Tags = ["production", "backend"],
            CreatedBy = "alice@example.com",
            PageNumber = 2,
            PageSize = 25
        });

        Assert.Equal("/api/secrets/search?q=db%20password&tags=production%2Cbackend&created-by=alice%40example.com&pageNumber=2&pageSize=25", path);
    }

    [Fact]
    public void BuildAuditSearchPath_EncodesFilters()
    {
        var path = SecretsApiClient.BuildAuditSearchPath(new AuditSearchOptions
        {
            Action = "CREATE",
            Result = "SUCCESS",
            ResourceType = "Secret",
            ResourceId = "db-password",
            PageNumber = 1,
            PageSize = 10
        });

        Assert.Equal("/api/audit-logs/search?action=CREATE&result=SUCCESS&resource-type=Secret&resource-id=db-password&pageNumber=1&pageSize=10", path);
    }
}
