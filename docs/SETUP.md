# Keydral Setup Guide

## Local Development Setup

This guide walks you through setting up Keydral for local development on Windows, macOS, or Linux.

There are three supported approaches:

- **[Aspire Orchestration](#option-a-aspire-orchestration-recommended)** — fully automated service orchestration via .NET Aspire (recommended)
- **[Dev Container](#option-b-dev-container)** — zero-config, fully isolated environment inside VS Code
- **[Manual setup](#option-c-manual-setup)** — run infrastructure locally via Docker Compose and the API with the .NET SDK

---

## Option A: Aspire Orchestration (Recommended)

.NET Aspire automatically orchestrates PostgreSQL, Keycloak, and the Keydral API as managed services, with zero manual configuration. This is the fastest and most integrated way to develop locally.

### Prerequisites

- **Podman** (v5.0+) with [Podman Desktop](https://podman.io/podman-desktop/) (recommended) — or **Docker** (v20.0+) - [Download](https://docs.docker.com/get-docker/)
- **.NET SDK 10.0** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Git**

### Steps

1. Clone the repository:

   ```bash
   git clone https://github.com/LittleAndi/keydral.git
   cd keydral
   ```

2. Start Aspire orchestration:

   ```bash
   cd src/Keydral.AppHost
   dotnet run
   ```

3. Wait for services to be ready (30-60 seconds):
   - **Aspire Dashboard** opens automatically at `http://localhost:15000` (service monitoring)
   - **Keydral API** available at `http://localhost:5001`
   - **Keycloak admin console** available at `http://localhost:8080/admin`

### What's Included

- PostgreSQL 16 (automatic connection string injection)
- Keycloak 24 with pre-imported `keydral` realm
- Keydral API with automatic database migrations
- .NET 10 SDK
- Service discovery and health checks (via ServiceDefaults)

### Forwarded Ports

| Port  | Service          |
| ----- | ---------------- |
| 5001  | Keydral API      |
| 5432  | PostgreSQL       |
| 8080  | Keycloak         |
| 15000 | Aspire Dashboard |

### Verify Setup

```bash
# Check API health
curl http://localhost:5001/health
# Response: {"status":"healthy",...}

# Keycloak admin console (default: admin / admin)
open http://localhost:8080/admin

# Aspire telemetry dashboard
open http://localhost:15000
```

### Access Swagger UI

1. Navigate to: `http://localhost:5001/swagger`
2. Click **Authorize** button
3. Use Keycloak credentials (from Step 4 below) to get access token
4. Try API endpoints from the UI

### Configure Keycloak (if needed)

The `keydral` realm is automatically imported with pre-configured:

- **Clients**: `keydral-api` (bearer-only), `keydral-cli` (device flow)
- **Roles**: `secret-reader`, `secret-writer`, `secret-admin`
- **Users**: Use the admin console to create test users

If you need to customize the realm, edit [src/Keydral.AppHost/realm/keydral-realm.json](../src/Keydral.AppHost/realm/keydral-realm.json) and restart Aspire.

### Stop Services

```bash
# Press Ctrl+C in the terminal running 'dotnet run'
# All containers automatically clean up
```

### Troubleshooting Aspire

**Services fail to start:**

```bash
# Check Docker is running
docker ps

# Restart Aspire
# Press Ctrl+C, then run 'dotnet run' again
```

**Port conflicts:**

```bash
# If port 5001 (API) is already in use, change it:
# Edit src/Keydral.API/Properties/launchSettings.json
# Change applicationUrl port from 5001 to another (e.g., 5002)
```

**Keycloak not responding:**

```bash
# Keycloak takes 30-60 seconds to start. Check Aspire Dashboard at http://localhost:15000
# Wait for Keycloak health to show "Healthy"
```

---

## Option B: Dev Container

An alternative lightweight option: the dev container spins up the full environment (app, PostgreSQL, Keycloak) automatically using VS Code Dev Containers. No local .NET SDK or PostgreSQL installation needed on your host machine.

### Prerequisites

- **Podman** (v5.0+) with [Podman Desktop](https://podman.io/podman-desktop/) (recommended) — or **Docker** (v20.0+) - [Download](https://docs.docker.com/get-docker/)
- **VS Code** with the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
- **Git**

### Steps

1. Clone the repository:

   ```bash
   git clone https://github.com/LittleAndi/keydral.git
   cd keydral
   ```

2. Open the folder in VS Code:

   ```bash
   code .
   ```

3. When prompted _"Reopen in Container"_, click it — or open the command palette (`Ctrl+Shift+P` / `Cmd+Shift+P`) and run **Dev Containers: Reopen in Container**.

4. VS Code will build the container and install dependencies automatically (`dotnet restore` runs on creation).

### Forwarded Ports

| Port | Service     |
| ---- | ----------- |
| 5001 | Keydral API |
| 5432 | PostgreSQL  |
| 8080 | Keycloak    |

### What's Included

- .NET 10 SDK
- C# and C# Dev Kit VS Code extensions pre-installed
- PostgreSQL 16 and Keycloak 24 as service containers
- Environment variables pre-configured to connect API → PostgreSQL and Keycloak

### Run the API (inside the container)

```bash
cd src/Keydral.API
dotnet run
# API available at http://localhost:5001
```

To apply database migrations:

```bash
cd src/Keydral.API
dotnet ef database update
```

Keycloak admin console is available at `http://localhost:8080/admin` (admin / admin).

Continue from [Step 4: Configure Keycloak](#step-4-configure-keycloak) to finish setup.

---

## Option C: Manual Setup

For maximum control: run container infrastructure locally via Docker Compose (or Podman Compose) and the API using the .NET SDK directly. This approach gives you explicit control over each component.

### Prerequisites

- **Podman** (v5.0+) with `podman-compose` (recommended) — or **Docker** (v20.0+) - [Download](https://docs.docker.com/get-docker/)
  - For Podman: `podman run -it --rm --volume /run/podman/podman.sock:/run/podman/podman.sock ghcr.io/containers/podman-compose`
- **.NET SDK 10.0** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Node.js 18+** (optional) - For frontend development
- **Git** - For cloning the repository
- **Command-line tools**: `curl`, `jq` (for API testing)

### Verify Installation

```bash
# Check Podman (or Docker)
podman --version
# Podman version 5.0.x or later
# (or 'docker --version' for Docker 20.10.x or later)

# Check .NET
dotnet --version
# 10.0.x or later

# Check Git
git --version
# git version 2.x or later
```

---

## Step 1: Clone the Repository

```bash
git clone https://github.com/LittleAndi/keydral.git
cd keydral
```

---

## Step 2: Start Infrastructure

Keydral requires PostgreSQL and Keycloak running via Docker Compose.

### Start Services

```bash
# From the keydral root directory
# Using Podman (recommended):
podman-compose up -d

# Or using Docker:
# docker-compose up -d

# Verify services are running
podman-compose ps
# (or 'docker-compose ps' if using Docker)
```

You should see:

- `keydral-postgres` (port 5432)
- `keydral-keycloak` (port 8080)

### Wait for Startup

Services take 30-60 seconds to start:

```bash
# Watch logs (Ctrl+C to exit)
podman-compose logs -f
# (or 'docker-compose logs -f' if using Docker)

# Check Keycloak readiness
curl -s http://localhost:8080/health | jq '.status'
# Should return: "UP"

# Check PostgreSQL readiness
podman-compose exec postgres pg_isready -U keydral_user -d keydral_dev
# (or 'docker-compose exec postgres ...' if using Docker)
# Should return: "accepting connections"
```

---

## Step 3: Database Setup

### Run EF Core Migrations

```bash
cd src/Keydral.API

# Apply pending migrations
dotnet ef database update

# Verify tables created
dotnet ef dbcontext info
```

Expected output:

```
Provider: Npgsql.EntityFrameworkCore.PostgreSQL
Database: keydral_dev
Connections strings: Host=localhost;Port=5432;Database=keydral_dev;...
```

### Seed Initial Data (Optional)

```bash
# Run seed script if exists
# Currently no automated seeding; manual creation via API/CLI recommended
```

---

## Step 4: Configure Keycloak

Keycloak runs at `http://localhost:8080`. Initial setup:

### Access Keycloak Admin Console

1. Navigate to: `http://localhost:8080/admin`
2. Login with:
   - **Username**: `admin`
   - **Password**: `admin` (default, change in production!)

### Create Keydral Client

1. Navigate to **Clients** → **Create**
2. **Client ID**: `keydral-api`
3. **Client Protocol**: `openid-connect`
4. **Root URL**: `http://localhost:5001`
5. Click **Save**

### Configure Client Settings

1. **Settings** tab:
   - **Access Type**: `confidential`
   - **Valid Redirect URIs**: `http://localhost:5001/swagger/oauth2-redirect.html`, `http://localhost:5001/callback`
   - **Web Origins**: `http://localhost:5001`
2. Click **Save**

### Get Client Credentials

1. Navigate to **Credentials** tab
2. Copy **Client Secret**
3. Update `appsettings.Development.json`:

```json
{
  "Keycloak": {
    "Url": "http://localhost:8080",
    "Realm": "master",
    "ClientId": "keydral-api",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE"
  }
}
```

### Create Test Users

1. Navigate to **Users** → **Add User**
2. Create user: `john` (email: `john@example.com`)
3. Set password (non-temporary)
4. Repeat for additional test users

### Create Test Roles

1. Navigate to **Roles** → **Create**
2. Create roles:
   - `keydral-admin` - Full access
   - `keydral-user` - Read/write secrets
   - `keydral-viewer` - Read-only secrets

3. Assign roles to users:
   - Users tab → Select user → Role Mappings → Assign roles

---

## Step 5: Build & Run API

### Restore Dependencies

```bash
cd src/Keydral.API
dotnet restore
```

### Run API Server

```bash
# From Keydral.API directory
dotnet run

# Or with hot-reload during development
dotnet watch run
```

Expected output:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to quit.
```

### Test API Health

```bash
curl http://localhost:5001/health
# Response: {"status":"healthy"}
```

---

## Step 6: Access Swagger UI

Interactive API documentation:

1. Navigate to: `http://localhost:5001/swagger`
2. Click **Authorize** button
3. Enter Keycloak credentials to get OAuth token
4. Try API endpoints from the UI

---

## Step 7: Build & Run CLI

### Build CLI Tool

```bash
cd src/Keydral.CLI
dotnet build -c Release

# On Windows:
# CLI executable: bin/Release/net10.0/Keydral.CLI.exe

# On MacOS/Linux:
# CLI executable: bin/Release/net10.0/Keydral.CLI
```

### Test CLI

```bash
# Show help
dotnet run -- --help

# Login
keydral login

# List secrets
keydral secret list

# Get secret
keydral secret get db-password

# Create secret
keydral secret set api-key "my-secret-value"
```

---

## Step 8: Run Tests

### Unit Tests

```bash
# From project root
dotnet test

# Run specific test file
dotnet test tests/Keydral.Encryption.Tests/Keydral.Encryption.Tests.csproj

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Integration Tests

```bash
# Requires API running (from Step 5)
dotnet test tests/Keydral.API.Tests/Keydral.API.Tests.csproj
```

---

## Configuration Files

### appsettings.Development.json

Located at `src/Keydral.API/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Keycloak": {
    "Url": "http://localhost:8080",
    "Realm": "master",
    "ClientId": "keydral-api",
    "ClientSecret": "YOUR_SECRET"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=keydral_dev;Username=keydral_user;Password=keydral_password"
  },
  "Serilog": {
    "MinimumLevel": "Debug",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/keydral-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

---

## Environment Variables

Optional environment variable overrides:

```bash
# PostgreSQL
export POSTGRES_PASSWORD=keydral_password
export KEYDRAL_DB_PASSWORD=keydral_password

# Keycloak
export KEYCLOAK_ADMIN_PASSWORD=admin

# Keydral API
export ASPNETCORE_ENVIRONMENT=Development
export KEYDRAL_MASTERKEY_PATH=/path/to/masterkey.txt
```

---

## Troubleshooting

### Issue: Cannot connect to PostgreSQL

```bash
# Check if container is running
podman-compose ps
# (or 'docker-compose ps' if using Docker)

# Restart PostgreSQL
podman-compose restart postgres
# (or 'docker-compose restart postgres' if using Docker)

# Check logs
podman-compose logs postgres
# (or 'docker-compose logs postgres' if using Docker)
```

> **Dev Container**: The `postgres` service is a sidecar container. Verify it is healthy with
> `podman inspect keydral-devcontainer-postgres-1 | grep Status` (or `docker inspect` if using Docker).

### Issue: Keycloak not responding

```bash
# Check if container is running
podman-compose ps keycloak
# (or 'docker-compose ps keycloak' if using Docker)

# Restart Keycloak
podman-compose restart keycloak
# (or 'docker-compose restart keycloak' if using Docker)

# Wait 30+ seconds for startup
```

> **Dev Container**: Keycloak may take up to 60 seconds to become ready after the container starts.
> Check **Ports** view in VS Code to confirm port 8080 is forwarded before accessing the admin console.

### Issue: Dev Container fails to build

```bash
# Rebuild from scratch (clears cached layers)
# Command palette → Dev Containers: Rebuild Container

# Or from the terminal
podman-compose -f .devcontainer/podman-compose.devcontainer.yml down -v
# (or 'docker-compose -f .devcontainer/docker-compose.devcontainer.yml down -v' if using Docker)
# Then reopen in container
```

### Issue: JWT Token validation fails

```bash
# Verify Client ID and Secret in appsettings.Development.json
# Verify Keycloak Authority URL is correct
# Check that token is from same realm (master)

# Clear local token cache
rm ~/.keydral/config.json  # Unix/Mac
del %APPDATA%\.keydral\config.json  # Windows
```

### Issue: Database migration fails

```bash
# Check database exists
docker-compose exec postgres psql -U keydral_user -l

# View migration history
dotnet ef migrations list

# Rollback to previous migration
dotnet ef database update <previous-migration-name>
```

### Issue: Port already in use

```bash
# Find process using port 5001
lsof -i :5001  # Mac/Linux
netstat -ano | findstr :5001  # Windows

# Kill process
kill -9 <PID>  # Mac/Linux
taskkill /PID <PID> /F  # Windows

# Or change port in appsettings.Development.json
```

---

## Next Steps

After setup is complete:

1. **Read API Documentation**: [docs/api/README.md](README.md)
2. **Review Architecture**: [docs/architecture/](../architecture/)
3. **Create Your First Secret**:
   ```bash
   keydral secret set myapp-password "super-secret-value"
   ```
4. **Run Tests**: `dotnet test`
5. **Explore Swagger**: `http://localhost:5001/swagger`

---

## Development Tips

### Hot Reload

For automatic recompilation during development:

```bash
dotnet watch run

# Watches for file changes and restarts application
```

### Database Reset

Start fresh with clean database:

```bash
# Stop and remove containers
docker-compose down -v

# Start fresh
docker-compose up -d

# Run migrations again
dotnet ef database update
```

### View Logs

```bash
# API logs
tail -f logs/keydral-*.txt  # Unix/Mac
Get-Content logs/keydral-*.txt -Tail 100 -Wait  # Windows

# Docker logs
docker-compose logs -f api
docker-compose logs -f postgres
docker-compose logs -f keycloak
```

### Generate New Master Key

```bash
# Create secure random master key
openssl rand -base64 32 > masterkey.txt

# Or use CLI to generate
keydral master-key generate
```

---

## Cleanup

To completely clean up:

```bash
# Stop and remove all containers, volumes, and networks
docker-compose down -v

# Remove local database files
rm -rf ./data/postgres

# Clear application data
rm -rf ~/.keydral/

# Clean build artifacts
dotnet clean
```

---

## Support

For issues or questions:

1. Check [docs/decisions/](../decisions/) for architectural decisions
2. Review [GitHub Issues](https://github.com/LittleAndi/keydral/issues)
3. Create new issue with setup details
