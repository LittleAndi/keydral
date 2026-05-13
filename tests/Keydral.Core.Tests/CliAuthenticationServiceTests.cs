using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Keydral.CLI.Services;

namespace Keydral.Core.Tests;

public class CliAuthenticationServiceTests
{
    [Fact]
    public async Task PollForTokenAsync_WithAuthorizationPending_ReportsWaitingStatus()
    {
        var updates = new List<DeviceFlowPollUpdate>();
        using var httpClient = new HttpClient(new SequenceHttpMessageHandler(
            CreateJsonResponse(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}"""),
            CreateJsonResponse(HttpStatusCode.OK, """{"access_token":"token","refresh_token":"refresh","expires_in":300,"token_type":"Bearer"}""")));
        var authenticationService = new AuthenticationService(
            "https://keycloak.example",
            "master",
            "keydral-cli",
            httpClient,
            _ => Task.CompletedTask);

        var tokenResponse = await authenticationService.PollForTokenAsync(
            "device-code",
            expiresIn: 30,
            initialInterval: 1,
            onProgress: updates.Add);

        Assert.Equal("token", tokenResponse.AccessToken);
        Assert.Contains(updates, update => update.Stage == DeviceFlowPollStage.WaitingForAuthorization);
    }

    [Fact]
    public async Task PollForTokenAsync_WithSlowDown_ReportsRetryingStatus()
    {
        var updates = new List<DeviceFlowPollUpdate>();
        using var httpClient = new HttpClient(new SequenceHttpMessageHandler(
            CreateJsonResponse(HttpStatusCode.BadRequest, """{"error":"slow_down"}"""),
            CreateJsonResponse(HttpStatusCode.OK, """{"access_token":"token","refresh_token":"refresh","expires_in":300,"token_type":"Bearer"}""")));
        var authenticationService = new AuthenticationService(
            "https://keycloak.example",
            "master",
            "keydral-cli",
            httpClient,
            _ => Task.CompletedTask);

        var tokenResponse = await authenticationService.PollForTokenAsync(
            "device-code",
            expiresIn: 30,
            initialInterval: 1,
            onProgress: updates.Add);

        Assert.Equal("token", tokenResponse.AccessToken);
        Assert.Contains(updates, update => update.Stage == DeviceFlowPollStage.Retrying);
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"))
        };
    }

    private sealed class SequenceHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP responses remain for the test request.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
