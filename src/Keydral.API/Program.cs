using Keydral.API.Authentication;
using Keydral.API.Middleware;
using Keydral.API.Endpoints;
using Keydral.API.Auditing;
using Keydral.Core.Extensions;
using Keydral.Storage;
using Keydral.Storage.Repositories;
using Keydral.Encryption.Extensions;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

// Add Aspire service defaults: OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Use Serilog as the logging provider
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthenticationAndAuthorization();

// Add audit logging
builder.Services.AddAuditLogging(builder.Configuration);

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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Audit logging middleware (before authentication for complete tracing)
app.UseAuditLogging();

// Authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseUserContext();

// Aspire health check endpoints: /health (readiness) and /alive (liveness)
app.MapDefaultEndpoints();

// Map API endpoints
app.MapSecretEndpoints();
app.MapPolicyEndpoints();
app.MapAuditLogEndpoints();

await (app.RunAsync() ?? Task.CompletedTask);

public partial class Program { }

