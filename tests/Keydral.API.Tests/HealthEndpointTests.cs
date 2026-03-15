using System.Net;
using Xunit;

namespace Keydral.API.Tests;

/// <summary>
/// Integration tests for health check endpoint.
/// </summary>
public class HealthEndpointTests : IAsyncLifetime
{
    private KeydralApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new KeydralApiFactory();
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Health_WithoutAuthentication_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }

    [Fact]
    public async Task Health_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(json);
        Assert.Contains("status", json);
    }
}
