# Keydral Deployment Guide

## Production Deployment

This guide covers deploying Keydral to production environments with security hardening, high availability, and operational best practices.

## Deployment Architecture

Keydral follows a cloud-native architecture:

```
Internet/Load Balancer
        ↓
   [API Gateway]
        ↓
   [Keydral API] (multiple replicas)
        ↓
   [PostgreSQL] (managed service or HA cluster)
        ↓
   [Keycloak] (external OIDC provider)
```

---

## Pre-Deployment Checklist

- [ ] PostgreSQL 14+ production database
- [ ] Keycloak 23+ configured with HTTPS
- [ ] TLS certificates for API domain
- [ ] Load balancer configured
- [ ] Backup and recovery plan
- [ ] Monitoring and alerting setup
- [ ] Security group rules configured
- [ ] API key rotation schedule
- [ ] Audit log retention policy
- [ ] Disaster recovery runbook

---

## Deployment Options

### Option 1: Kubernetes (Recommended)

Production-grade deployment with high availability and autoscaling.

#### Prerequisites

- Kubernetes cluster (v1.24+)
- kubectl configured
- Helm 3+ installed
- Container registry (Docker Hub, ECR, ACR, etc.)

#### Step 1: Build & Push Docker Image

```bash
# Build image
docker build -f src/Keydral.API/Dockerfile -t myregistry/keydral-api:1.0.0 .

# Push to registry
docker push myregistry/keydral-api:1.0.0
```

#### Step 2: Configure Helm Values

Create `helm/keydral/values-prod.yaml`:

```yaml
replicaCount: 3

image:
  repository: myregistry/keydral-api
  tag: "1.0.0"
  pullPolicy: IfNotPresent

imagePullSecrets: []
nameOverride: ""
fullnameOverride: ""

serviceAccount:
  create: true
  annotations: {}
  name: ""

podAnnotations:
  prometheus.io/scrape: "true"
  prometheus.io/port: "5001"

podSecurityContext:
  runAsNonRoot: true
  runAsUser: 1000
  fsGroup: 1000

securityContext:
  allowPrivilegeEscalation: false
  capabilities:
    drop:
      - ALL
  readOnlyRootFilesystem: true

service:
  type: ClusterIP
  port: 80
  targetPort: 5001

ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
  hosts:
    - host: keydral.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: keydral-tls
      hosts:
        - keydral.example.com

resources:
  limits:
    cpu: 500m
    memory: 512Mi
  requests:
    cpu: 250m
    memory: 256Mi

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80

livenessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 10
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 5
  periodSeconds: 5

nodeSelector: {}
tolerations: []
affinity:
  podAntiAffinity:
    preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          labelSelector:
            matchExpressions:
              - key: app
                operator: In
                values:
                  - keydral
          topologyKey: kubernetes.io/hostname

env:
  ASPNETCORE_ENVIRONMENT: Production
  KEYCLOAK_AUTHORITY: https://keycloak.example.com/realms/master

secrets:
  ASPNETCORE_URLS: https://+:443
  KEYCLOAK_CLIENT_SECRET: {} # Use sealed-secrets or external-secrets
  DATABASE_CONNECTION_STRING: {} # Use sealed-secrets

postgresql:
  enabled: false # Use managed database (e.g., AWS RDS)
```

#### Step 3: Deploy with Helm

```bash
# Create namespace
kubectl create namespace keydral

# Deploy with Helm
helm install keydral ./helm/keydral \
  -n keydral \
  -f helm/keydral/values-prod.yaml

# Verify deployment
kubectl get pods -n keydral
kubectl get svc -n keydral
```

#### Step 4: Configure PostgreSQL

Use managed database services:

**AWS RDS:**

```bash
# Create RDS instance
aws rds create-db-instance \
  --db-instance-identifier keydral-prod \
  --db-instance-class db.t3.medium \
  --engine postgres \
  --engine-version 15.1 \
  --allocated-storage 100 \
  --storage-type gp3
```

**Azure Database for PostgreSQL:**

```bash
# Create PostgreSQL Flexible Server
az postgres flexible-server create \
  --name keydral-prod \
  --resource-group keydral-rg \
  --sku-name Standard_B2s \
  --tier Burstable \
  --version 15
```

#### Step 5: Apply Database Migrations

```bash
# Port-forward to Kubernetes pod
kubectl port-forward -n keydral svc/keydral 5001:80 &

# Run migrations
dotnet ef database update

# Or via pod exec
kubectl exec -it deployment/keydral -n keydral -- \
  dotnet ef database update
```

---

### Option 2: Docker Compose (Small Scale)

For single-server deployments or staging environments.

#### Prerequisites

- Docker 20.0+
- Docker Compose 2.0+
- Linux server (Ubuntu 20.04+ recommended)

#### Step 1: Prepare Server

```bash
# Update system
sudo apt-get update && sudo apt-get upgrade -y

# Install Docker
curl https://get.docker.com | sh
sudo usermod -aG docker $USER

# Install Docker Compose
sudo curl -L \
  "https://github.com/docker/compose/releases/download/v2.20.0/docker-compose-$(uname -s)-$(uname -m)" \
  -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
```

#### Step 2: Configure Production Compose File

Create `docker-compose.prod.yml`:

```yaml
version: "3.9"

services:
  api:
    image: myregistry/keydral-api:1.0.0
    ports:
      - "5001:5001"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: https://+:443;http://+:80
      KEYCLOAK_AUTHORITY: https://keycloak.example.com/realms/master
      DATABASE_PASSWORD: ${DATABASE_PASSWORD}
      KEYCLOAK_CLIENT_SECRET: ${KEYCLOAK_CLIENT_SECRET}
    volumes:
      - ./certs:/app/certs:ro
      - ./logs:/app/logs
    depends_on:
      - postgres
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5001/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: keydral_prod
      POSTGRES_USER: keydral_user
      POSTGRES_PASSWORD: ${DATABASE_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U keydral_user"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
```

#### Step 3: Deploy

```bash
# Create .env file with secrets
cat > .env << EOF
DATABASE_PASSWORD=$(openssl rand -base32)
KEYCLOAK_CLIENT_SECRET=your-keycloak-client-secret
EOF

# Start services
docker-compose -f docker-compose.prod.yml up -d

# Verify
docker-compose -f docker-compose.prod.yml ps
```

---

## Security Configuration

### TLS/HTTPS

**Kubernetes with cert-manager:**

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: keydral-tls
  namespace: keydral
spec:
  secretName: keydral-tls
  commonName: keydral.example.com
  dnsNames:
    - keydral.example.com
  issuerRef:
    name: letsencrypt-prod
    kind: ClusterIssuer
```

**Docker Compose:**

```bash
# Generate self-signed cert (or use Let's Encrypt)
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout certs/keydral.key \
  -out certs/keydral.crt
```

### Master Key Protection

**Kubernetes Secrets:**

```bash
# Create secret
kubectl create secret generic keydral-keys \
  -n keydral \
  --from-file=masterkey=./masterkey.txt

# Mount in pod
volumeMounts:
  - name: keys
    mountPath: /etc/keydral-keys
    readOnly: true
```

**AWS Secrets Manager:**

```csharp
// In Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSecretsManager(
  region: "us-east-1",
  secretsManager: new SecretsManagerService()
);
```

**Azure Key Vault:**

```csharp
var client = new SecretClient(
  vaultUri: new Uri(keyVaultUrl),
  credential: new DefaultAzureCredential()
);

var secret = await client.GetSecretAsync("keydral-masterkey");
```

### Network Security

**Kubernetes Network Policy:**

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: keydral-netpol
  namespace: keydral
spec:
  podSelector:
    matchLabels:
      app: keydral
  policyTypes:
    - Ingress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx
      ports:
        - protocol: TCP
          port: 80
        - protocol: TCP
          port: 443
```

**Database Security:**

```bash
# Restrict database access to application only
# In AWS Security Group:
# - Allow port 5432 only from application security group

# Enable SSL for database connection
# In connection string:
# Host=postgres.example.com;SSLMode=Require;TrustServerCertificate=false
```

---

## Database Administration

### Backup Strategy

**Automated Daily Backups:**

```bash
#!/bin/bash
# backup.sh - Run via cron daily

PGHOST=postgres.example.com
PGUSER=keydral_user
PGDATABASE=keydral_prod
BACKUP_DIR=/backups/keydral
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR

pg_dump -h $PGHOST -U $PGUSER $PGDATABASE | \
  gzip > $BACKUP_DIR/keydral_$DATE.sql.gz

# Keep only last 30 days
find $BACKUP_DIR -name "keydral_*.sql.gz" -mtime +30 -delete

# Upload to S3
aws s3 cp $BACKUP_DIR/keydral_$DATE.sql.gz \
  s3://keydral-backups/
```

**AWS RDS Automated Backups:**

```bash
aws rds create-db-instance \
  --db-instance-identifier keydral-prod \
  --backup-retention-period 30 \
  --enable-cloudwatch-logs-exports postgresql
```

### Monitoring & Alerts

**Prometheus Metrics:**

```bash
# Enable metrics exposure
# In appsettings.json:
{
  "Logging": {
    "Metrics": {
      "Enabled": true,
      "Port": 9090
    }
  }
}
```

**Kubernetes Pod Monitor:**

```yaml
apiVersion: monitoring.coreos.com/v1
kind: PodMonitor
metadata:
  name: keydral
  namespace: keydral
spec:
  selector:
    matchLabels:
      app: keydral
  podMetricsEndpoints:
    - port: metrics
      interval: 30s
```

---

## Scaling Considerations

### Horizontal Scaling

Keydral API is stateless and can scale horizontally:

```bash
# Kubernetes HPA
kubectl autoscale deployment keydral \
  --min=3 --max=10 \
  --cpu-percent=70 \
  -n keydral

# Monitor scaling
kubectl get hpa keydral -n keydral -w
```

### Database Scaling

PostgreSQL performance optimization:

```sql
-- Monitor slow queries
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

SELECT query, calls, mean_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;

-- Create indexes for common queries
CREATE INDEX idx_secrets_name ON Secrets(Name);
CREATE INDEX idx_audit_logs_timestamp ON AuditLogs(Timestamp DESC);
```

---

## Maintenance

### Zero-Downtime Deployments

```bash
# Kubernetes rolling update
kubectl set image deployment/keydral \
  keydral=myregistry/keydral-api:1.1.0 \
  -n keydral

# Monitor rollout
kubectl rollout status deployment/keydral -n keydral

# Rollback if needed
kubectl rollout undo deployment/keydral -n keydral
```

### Database Migrations

```bash
# Run migrations before deploying new API version
kubectl exec -it deployment/keydral -n keydral -- \
  dotnet ef database update

# Verify schema changes
kubectl exec -it deployment/keydral -n keydral -- \
  dotnet ef dbcontext info
```

### Log Rotation

```bash
# Configure Serilog for production
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/keydral/keydral-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600
        }
      }
    ]
  }
}
```

---

## Disaster Recovery

### Recovery Time Objectives

| Scenario                | Recovery Time | Data Loss        |
| ----------------------- | ------------- | ---------------- |
| API crash               | 2-5 minutes   | None (stateless) |
| Single database failure | 15-30 minutes | Up to 1 hour     |
| Regional outage         | 1-4 hours     | 1-24 hours       |

### Recovery Procedures

**API Recovery:**

```bash
# Kubernetes will automatically restart failed pods
kubectl get pods -n keydral

# Watch recovery
kubectl logs -f deployment/keydral -n keydral
```

**Database Recovery:**

```bash
# Restore from backup
pg_restore -h postgres.example.com \
  -U keydral_user \
  -d keydral_prod \
  backup_2024-01-15.sql.gz

# Verify restoration
SELECT COUNT(*) FROM Secrets;
```

---

## Performance Optimization

### Caching Strategy

Use Redis for performance:

```csharp
// Add Redis caching
services.AddStackExchangeRedisCache(options => {
    options.Configuration = Configuration.GetConnectionString("Redis");
});

// Cache secret metadata
var cacheKey = $"secrets:list:page:{pageNumber}";
if (!_cache.TryGetValue(cacheKey, out var secrets)) {
    secrets = await _repository.ListSecretsAsync(pageNumber);
    _cache.Set(cacheKey, secrets, TimeSpan.FromHours(1));
}
```

### Query Optimization

```sql
-- Add indexes for common queries
CREATE INDEX idx_secrets_created_at ON Secrets(CreatedAt DESC);
CREATE INDEX idx_audit_logs_actor ON AuditLogs(Actor, Timestamp DESC);

-- Monitor query plans
EXPLAIN ANALYZE SELECT * FROM Secrets WHERE CreatedAt > '2024-01-01';
```

---

## Compliance & Audit

### Audit Log Retention

```sql
-- Retain audit logs for compliance (typically 1-7 years)
-- Configure automated archival to cold storage

-- Archive old logs
CREATE TABLE AuditLogs_Archive AS
SELECT * FROM AuditLogs
WHERE Timestamp < CURRENT_DATE - INTERVAL '1 year';

-- Compress and store
pg_dump keydral_prod | gzip > archive_2023.sql.gz
aws s3 cp archive_2023.sql.gz s3://audit-archives/
```

### Compliance Requirements

- [ ] SOC 2 Type II certification for production
- [ ] Encryption in transit (TLS 1.2+)
- [ ] Encryption at rest (AES-256)
- [ ] Access controls (RBAC, MFA)
- [ ] Audit logging (immutable, 7+ years retention)
- [ ] Data residency compliance
- [ ] Regular security audits

---

## Support & Troubleshooting

### Check Deployment Status

```bash
# Kubernetes
kubectl get all -n keydral

# Docker Compose
docker-compose ps
docker-compose logs api

# Health endpoints
curl https://keydral.example.com/health
```

### Common Issues

| Issue                       | Solution                                    |
| --------------------------- | ------------------------------------------- |
| Pod crashes on startup      | Check logs: `kubectl logs <pod> -n keydral` |
| Database connection timeout | Verify security groups/network policies     |
| High API latency            | Check database indexes, enable caching      |
| Audit logs filling up       | Configure log archival to cold storage      |

---

## Further Reading

- [Setup Guide](./SETUP.md)
- [API Documentation](./api/)
- [Architecture Decisions](./decisions/)

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
