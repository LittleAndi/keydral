using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Moq;
using Keydral.Storage;
using Keydral.Storage.Repositories;
using Keydral.Encryption;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;

namespace Keydral.API.Tests;

/// <summary>
/// WebApplicationFactory for integration testing the Keydral API.
/// Provides test environment with mock repositories and in-memory database.
/// </summary>
public class KeydralApiFactory : WebApplicationFactory<Program>
{
    public Mock<ISecretRepository> MockSecretRepository { get; } = new();
    public Mock<IPolicyRepository> MockPolicyRepository { get; } = new();
    public Mock<IAuditLogRepository> MockAuditLogRepository { get; } = new();
    public Mock<IEncryptionService> MockEncryptionService { get; } = new();

    public KeydralApiFactory()
    {
        // Set environment before host creation so correct appsettings file is loaded
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove real database context (not needed for health endpoint test)
            var dbContextDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Configure JWT Bearer to not require HTTPS for testing
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = "http://localhost:8080";
            });

            // Remove real encryption service and use mock
            var encryptionDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IEncryptionService));
            if (encryptionDescriptor != null)
                services.Remove(encryptionDescriptor);

            // Replace real repositories with mocks for testing  
            var secretRepoDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ISecretRepository));
            if (secretRepoDescriptor != null)
                services.Remove(secretRepoDescriptor);

            var policyRepoDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IPolicyRepository));
            if (policyRepoDescriptor != null)
                services.Remove(policyRepoDescriptor);

            var auditRepoDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IAuditLogRepository));
            if (auditRepoDescriptor != null)
                services.Remove(auditRepoDescriptor);

            services.AddSingleton(_ => MockEncryptionService.Object);
            services.AddScoped(_ => MockSecretRepository.Object);
            services.AddScoped(_ => MockPolicyRepository.Object);
            services.AddScoped(_ => MockAuditLogRepository.Object);
        });
    }

    /// <summary>
    /// Create an HTTP client with Bearer token for authenticated requests.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string token = "test-token")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Reset all mocks to clean state.
    /// </summary>
    public void ResetMocks()
    {
        MockEncryptionService.Reset();
        MockSecretRepository.Reset();
        MockPolicyRepository.Reset();
        MockAuditLogRepository.Reset();
    }
}
