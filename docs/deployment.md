# Deployment & Local Development Guide

## MVP Deployment Architecture

```
┌─────────────────────────────────────────────────────┐
│          Local Development (Docker Compose)         │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌────────────────┐  ┌──────────────────────────┐  │
│  │   Keydral API  │  │    Keycloak (OIDC)       │  │
│  │   (dotnet run) │◄──┤  Port: 8080              │  │
│  │   Port: 5000   │  │  Admin: admin/admin      │  │
│  └────────────────┘  └──────────────────────────┘  │
│           │                      │                 │
│           └──────────────────────┼─────────────┐   │
│                                  │             │   │
│                          ┌────────▼─────────────┴──┐│
│                          │   PostgreSQL 16         ││
│                          │  Port: 5432             ││
│                          │  User: keydral          ││
│                          │  Password: **** (dev)   ││
│                          └─────────────────────────┘│
└─────────────────────────────────────────────────────┘
```

---

## Quick Start (5 minutes)

### Prerequisites

- **Docker & Docker Compose** (v2.0+)
- **.NET SDK 8.0** (for local development)
- **Git**

### Step 1: Start Infrastructure

```bash
# From repository root
docker-compose up -d

# Verify services are healthy
docker-compose ps

# Expected output:
# CONTAINER ID  IMAGE                     STATUS
# xxxxx         postgres:16-alpine        Up 2s (healthy)
# xxxxx         keycloak/keycloak:24.0    Up 5s (healthy)
```

**Wait for health checks** (or 30 seconds):

- PostgreSQL: Port 5432 listening
- Keycloak: http://localhost:8080 (admin panel ready)

### Step 2: Initialize Keycloak Realm

1. Navigate to **http://localhost:8080/admin**
2. Login: `admin` / `admin`
3. Create realm: `keydral`
4. Create client: `keydral-api`
   - Access Type: confidential
   - Valid Redirect URIs: `http://localhost:5000/*`, `http://localhost/*`
   - Save and note the **Client Secret**

### Step 3: Run API (Local)

```bash
cd src/Keydral.API
export KEYCLOAK_URL=http://localhost:8080
export KEYCLOAK_REALM=keydral
export KEYCLOAK_CLIENT_ID=keydral-api
export KEYCLOAK_CLIENT_SECRET=<from-step-2>
export DATABASE_CONNECTION_STRING="Server=localhost;Port=5432;Database=keydral;User Id=keydral;Password=keydral_dev_password;"

dotnet run
```

Expected output:

```
info: Keydral.API.Program[0]
      Listening on https://localhost:7170 and http://localhost:5000
```

### Step 4: Test API

Open **http://localhost:5000/swagger** → Swagger UI appears

Test health endpoint:

```bash
curl http://localhost:5000/health
# Expected: {"status":"healthy"}
```

---

## Configuration

### Environment Variables

#### Database

```
DATABASE_CONNECTION_STRING=Server=<host>;Port=5432;Database=keydral;User Id=keydral;Password=<pwd>;
DATABASE_PROVIDER=postgres      # or sqlite for dev
```

#### Keycloak (OIDC)

```
KEYCLOAK_URL=http(s)://keycloak-host:8080
KEYCLOAK_REALM=keydral
KEYCLOAK_CLIENT_ID=keydral-api
KEYCLOAK_CLIENT_SECRET=<secret>
```

#### Encryption

```
ENCRYPTION_MASTER_KEY_FILE=/var/run/secrets/master-key.txt  # File path
ENCRYPTION_ALGORITHM=AES-256                                # or AES-128
```

#### API

```
ASPNETCORE_ENVIRONMENT=Development     # or Production
ASPNETCORE_URLS=http://0.0.0.0:5000
LOG_LEVEL=Information                  # Debug, Information, Warning, Error
```

### Docker Compose Overrides

Create `.env` file in repo root:

```bash
POSTGRES_PASSWORD=your_secure_password
KEYCLOAK_ADMIN_PASSWORD=your_admin_password
```

---

## Development Workflow

### Terminal 1: Infrastructure

```bash
docker-compose up
# Keep running; Ctrl+C to stop all services
```

### Terminal 2: API Development

```bash
cd src/Keydral.API
dotnet run --launch-profile https
# Auto-reloads on code changes
```

### Terminal 3: CLI Testing

```bash
cd src/Keydral.CLI
dotnet run -- secret set my-secret
# Interactive prompts for secret value
```

---

## Database Migrations (Phase 1B)

When Entity Framework models are ready:

```bash
cd src/Keydral.API

# Create new migration
dotnet ef migrations add InitialSchema -p ../Keydral.Storage

# Apply migrations (auto on first run; manual if needed)
dotnet ef database update
```

Verify schema created:

```bash
psql -h localhost -U keydral -d keydral

# Inside psql:
\dt              # List all tables
\d secrets       # Describe secrets table schema
\q              # Quit
```

---

## Testing

### Unit Tests

```bash
dotnet test
```

### Integration Tests (with containers)

```bash
# Ensure docker-compose is running
dotnet test --configuration Release
```

### Manual API Testing

**Login & get token** (once CLI is ready):

```bash
keydral login
# Token cached in ~/.keydral/token.json
```

**Create secret**:

```bash
curl -X POST http://localhost:5000/api/secrets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"db-password","value":"super-secret"}'
```

**Retrieve secret**:

```bash
curl http://localhost:5000/api/secrets/db-password \
  -H "Authorization: Bearer $TOKEN"
```

---

## Troubleshooting

### "Connection refused" on port 5432

```bash
# Check if PostgreSQL container is running
docker-compose ps

# If not, restart
docker-compose up postgres
```

### "Invalid token" from Keycloak

1. Verify Keycloak client secret matches in code
2. Check realm name is correct: `keydral`
3. Keycloak health: http://localhost:8080/health/ready

### Migrations fail

```bash
# Reset database (⚠️ loses data)
docker-compose down -v
docker-compose up postgres

# Re-run migrations
dotnet ef database update
```

### Port already in use

```bash
# Find process using port 5000
lsof -ni :5000   # macOS/Linux
netstat -ano | findstr :5000  # Windows

# Change docker-compose port
# Edit docker-compose.yml: ports: ["5001:5000"]
```

---

## Production Deployment (Phase 2)

For Kubernetes deployment:

1. Build Docker image:

   ```bash
   docker build -t keydral-api:latest .
   ```

2. Deploy to K8s:

   ```bash
   kubectl apply -f k8s/
   ```

3. Secrets management (instead of env files):
   ```bash
   kubectl create secret generic keydral-secrets \
     --from-literal=DATABASE_CONNECTION_STRING=... \
     --from-literal=KEYCLOAK_CLIENT_SECRET=... \
     -n keydral-system
   ```

---

## Cleanup

```bash
# Stop all containers
docker-compose down

# Remove volumes (careful! deletes databse)
docker-compose down -v

# Remove images
docker rmi postgres:16-alpine keycloak/keycloak:24.0
```

---

## Next Steps

- [ ] Phase 1B: Database schema & Entity Framework
- [ ] Phase 1C: Encryption layer implementation
- [ ] Phase 1D: OIDC middleware & RBAC
- [ ] Phase 1E: API endpoints
- [ ] Phase 1F: CLI commands
