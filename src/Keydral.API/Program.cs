using Keydral.API.Authentication;
using Keydral.API.Middleware;
using Keydral.API.Endpoints;
using Keydral.Core.Extensions;
using Keydral.Storage;
using Keydral.Storage.Repositories;
using Keydral.Encryption.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context and repositories
builder.Services.AddScoped<ApplicationDbContext>();
builder.Services.AddScoped<ISecretRepository, SecretRepository>();
builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Add encryption layer
builder.Services.AddEncryption(builder.Configuration);

// Add authentication and authorization
builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthenticationAndAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseUserContext();

// Health check endpoint (no auth required)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithName("Health")
    .WithOpenApi();

// Map API endpoints
app.MapSecretEndpoints();
app.MapPolicyEndpoints();
app.MapAuditLogEndpoints();

app.Run();

