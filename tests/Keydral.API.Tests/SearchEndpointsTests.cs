using System.Net;
using System.Text.Json;
using Moq;
using Keydral.API.Models;
using Keydral.API.Search;
using Keydral.Storage.Entities;

namespace Keydral.API.Tests;

public class SearchEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task SearchSecrets_FiltersByTextTagsAndRbac()
    {
        using var factory = new KeydralApiFactory();
        factory.MockSecretRepository
            .Setup(repository => repository.GetSecretsFilteredAsync(
                "postgres",
                null,
                It.Is<IReadOnlyCollection<string>>(tags => tags.Count == 1 && tags.Contains("production")),
                null,
                null,
                null,
                null,
                "alice@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Secret
                {
                    Id = Guid.NewGuid(),
                    Name = "db-password",
                    Description = "Postgres credential",
                    Tags = "production,backend",
                    CreatedBy = "alice@example.com",
                    CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc)
                },
                new Secret
                {
                    Id = Guid.NewGuid(),
                    Name = "staging-api-key",
                    Description = "API key",
                    Tags = "staging,backend",
                    CreatedBy = "alice@example.com",
                    CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        factory.MockPolicyEngine
            .Setup(engine => engine.CanPerformAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "secrets:read",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string resourcePath, string _, CancellationToken _) => resourcePath.EndsWith("db-password", StringComparison.Ordinal));

        using var client = factory.CreateAuthenticatedClient("alice");

        var response = await client.GetAsync("/api/secrets/search?q=postgres&tags=production&created-by=alice@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await JsonSerializer.DeserializeAsync<PaginatedResponse<SecretListItemResponse>>(
            await response.Content.ReadAsStreamAsync(),
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("db-password", payload.Items[0].Name);
    }

    [Fact]
    public async Task SearchAuditLogs_FiltersByAdvancedParameters()
    {
        using var factory = new KeydralApiFactory();
        factory.MockAuditLogRepository
            .Setup(repository => repository.GetAuditLogsFilteredAsync(
                null,
                null,
                "CREATE",
                null,
                null,
                "SUCCESS",
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                [
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        Action = "CREATE",
                        Actor = "alice@example.com",
                        ResourceType = "Secret",
                        ResourceId = "db-password",
                        Result = "SUCCESS",
                        Timestamp = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                1));

        using var client = factory.CreateAuthenticatedClient("admin-user", "secret-admin");

        var response = await client.GetAsync("/api/audit-logs/search?action=CREATE&result=SUCCESS&from-date=2026-01-01&to-date=2026-01-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await JsonSerializer.DeserializeAsync<PaginatedResponse<AuditLogResponse>>(
            await response.Content.ReadAsStreamAsync(),
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("CREATE", payload.Items[0].Action);
    }

    [Fact]
    public async Task SearchDsl_ReturnsSecretsAndAuditLogs()
    {
        using var factory = new KeydralApiFactory();
        factory.MockSecretRepository
            .Setup(repository => repository.GetSecretsFilteredAsync(
                null,
                "*password",
                It.Is<IReadOnlyCollection<string>>(tags => tags.Count == 1 && tags.Contains("production")),
                null,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Secret
                {
                    Id = Guid.NewGuid(),
                    Name = "db-password",
                    Description = "Primary password",
                    Tags = "production,backend",
                    UpdatedAt = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        factory.MockAuditLogRepository
            .Setup(repository => repository.GetAuditLogsFilteredAsync(
                null,
                null,
                "CREATE",
                null,
                null,
                null,
                null,
                null,
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                [
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        Action = "CREATE",
                        Actor = "alice@example.com",
                        ResourceType = "Secret",
                        ResourceId = "db-password",
                        Result = "SUCCESS",
                        Timestamp = new DateTime(2026, 1, 21, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                1));

        using var client = factory.CreateAuthenticatedClient("admin-user", "secret-admin");
        var query = Uri.EscapeDataString("name:\"*password\" AND tags:production AND action:CREATE");

        var response = await client.GetAsync($"/api/search?q={query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await JsonSerializer.DeserializeAsync<SearchResultsResponse>(
            await response.Content.ReadAsStreamAsync(),
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Single(payload.Secrets.Items);
        Assert.Single(payload.AuditLogs.Items);
        Assert.Equal("db-password", payload.Secrets.Items[0].Name);
        Assert.Equal("CREATE", payload.AuditLogs.Items[0].Action);
    }
}
