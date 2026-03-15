using Keydral.API.Authentication;
using Keydral.API.Middleware;
using Keydral.Core.Extensions;
using Keydral.Storage;
using Keydral.Encryption.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context
builder.Services.AddScoped<ApplicationDbContext>();

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

// TODO: Add secret endpoints (list, get, create, update, delete)
// TODO: Add policy endpoints (list, get, create, update, delete)
// TODO: Add audit log endpoints (list)

app.Run();

