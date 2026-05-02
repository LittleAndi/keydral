using Keydral.API.Authentication;
using Keydral.API.Middleware;
using Keydral.API.Endpoints;
using Keydral.API.Auditing;
using Keydral.Core.Extensions;
using Keydral.Storage;
using Keydral.Storage.Repositories;
using Keydral.Encryption.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text.Json;
using Scalar.AspNetCore;

// Ensure ASPNETCORE_ENVIRONMENT is visible early (important for test hosts)
var aspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

// Configure Serilog logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{aspNetCoreEnv}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Register health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

// Configure OpenTelemetry - exports automatically when OTEL_EXPORTER_OTLP_ENDPOINT is set (e.g. by Aspire)
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(c => c.AddService(builder.Environment.ApplicationName))
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation();
    });

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

// Use Serilog as the logging provider
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();

// Add database context and repositories
// ConnectionStrings__keydral is injected by Aspire; Database:ConnectionString is the fallback for standalone runs
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("keydral")
        ?? builder.Configuration["Database:ConnectionString"]));
builder.Services.AddScoped<ISecretRepository, SecretRepository>();
builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Add encryption layer - configuration is merged from environment-specific appsettings
builder.Services.AddEncryption(builder.Configuration);

// Add authentication and authorization
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthenticationAndAuthorization();

// Add audit logging
builder.Services.AddAuditLogging(builder.Configuration);

// Add OpenAPI/Swagger generation
builder.Services.AddOpenApi();

var app = builder.Build();

// Run EF Core migrations automatically in Development (covers Aspire-orchestrated runs)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

// Audit logging middleware (before authentication for complete tracing)
app.UseAuditLogging();

// Authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseUserContext();

// Health check endpoints: /alive (liveness) and /health (readiness)
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
}).AllowAnonymous();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString().ToLower()
        });
        await context.Response.WriteAsync(result);
    }
}).AllowAnonymous();

// Map API endpoints
app.MapSecretEndpoints();
app.MapPolicyEndpoints();
app.MapAuditLogEndpoints();

await (app.RunAsync() ?? Task.CompletedTask);

public partial class Program { }

