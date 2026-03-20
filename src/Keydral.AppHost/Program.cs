var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL — connection string injected into the API as ConnectionStrings__keydral
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("keydral-postgres-data")
    .AddDatabase("keydral");

// Keycloak — stable port 8080 to avoid OIDC cookie issues across restarts
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume("keydral-keycloak-data")
    .WithRealmImport("./realm")
    .WaitFor(postgres);

// API — waits for infrastructure, receives injected connection strings
builder.AddProject<Projects.Keydral_API>("keydral-api", launchProfileName: "https")
    .WithReference(postgres)
    .WithEnvironment("Keycloak__Url", keycloak.GetEndpoint("https"))
    .WithEnvironment("Keycloak__Realm", "keydral")
    .WithEnvironment("Keycloak__ClientId", "keydral-api")
    .WaitFor(postgres)
    .WaitFor(keycloak);

builder.Build().Run();
