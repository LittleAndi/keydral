# Keydral API Documentation

## Overview

Keydral is a **local-first secrets management vault** providing:

- **Secure storage** with AES-256-GCM envelope encryption
- **Identity-based access control** via Keycloak OIDC
- **Fine-grained RBAC** with policy-based authorization
- **Complete audit trail** of all access and modifications
- **REST API** and **CLI tool** for easy integration

## Quick Start

### Authentication

All API endpoints (except `/health`) require OAuth 2.0 Bearer token authentication via Keycloak:

```bash
curl -H "Authorization: Bearer <token>" \
  http://localhost:5001/api/secrets
```

Obtain a token using the CLI:

```bash
keydral login      # Initiates device flow
keydral secret list # Uses cached token
```

### API Base URL

- **Development**: `http://localhost:5001`
- **Production**: `https://keydral.your-domain.com`

---

## Endpoints

### Health Check

**GET** `/health`

- No authentication required
- Returns API health status

**Example:**

```bash
curl http://localhost:5001/health
```

**Response:**

```json
{
  "status": "healthy"
}
```

---

## 🔐 Secrets Endpoints

### List Secrets

**GET** `/api/secrets?pageNumber=1&pageSize=50`

Lists all secrets the authenticated user has read permission for.

**Parameters:**

- `pageNumber` (query, int): Page number (default: 1)
- `pageSize` (query, int): Items per page (default: 50)

**Response:**

```json
{
  "items": [
    {
      "name": "db-password",
      "version": 3,
      "description": "PostgreSQL password",
      "createdBy": "john@example.com",
      "createdAt": "2026-03-15T10:30:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 1,
  "hasNextPage": false
}
```

---

### Search Secrets

**GET** `/api/secrets/search?q=postgres&tags=production,backend&created-by=alice@example.com`

Search secret metadata with full-text matching across **name**, **description**, and **tags**.

**Parameters:**

- `q` (query, string): Full-text search term
- `tags` (query, string): Comma-separated tags that must all match
- `created-after` (query, date): Filter by creation timestamp lower bound
- `created-before` (query, date): Filter by creation timestamp upper bound
- `updated-after` (query, date): Filter by update timestamp lower bound
- `updated-before` (query, date): Filter by update timestamp upper bound
- `created-by` (query, string): Filter by creator
- `pageNumber` (query, int): Page number (default: 1)
- `pageSize` (query, int): Items per page (default: 50)

**Examples:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5001/api/secrets/search?q=postgres&tags=production"

keydral secret search postgres --tag production --tag backend
```

---

### Get Secret

**GET** `/api/secrets/{name}`

Retrieve a specific secret by name with decrypted value.

**Path Parameters:**

- `name` (string): Secret name (URL-encoded)

**Response:**

```json
{
  "name": "db-password",
  "value": "super-secret-password",
  "version": 3,
  "description": "PostgreSQL password",
  "tags": {
    "environment": "production",
    "app": "backend"
  },
  "createdBy": "john@example.com",
  "createdAt": "2026-03-15T10:30:00Z"
}
```

**Errors:**

- `404 Not Found`: Secret not found
- `403 Forbidden`: User lacks read permission

---

### Create/Update Secret

**PUT** `/api/secrets/{name}`

Create a new secret or update an existing one. Always creates a new version.

**Path Parameters:**

- `name` (string): Secret name (URL-encoded)

**Request Body:**

```json
{
  "value": "new-secret-value",
  "description": "Optional description",
  "tags": {
    "key": "value"
  }
}
```

**Response:**

```json
{
  "name": "db-password",
  "value": "new-secret-value",
  "version": 4,
  "description": "Optional description",
  "tags": {
    "key": "value"
  },
  "createdBy": "john@example.com",
  "createdAt": "2026-03-15T11:00:00Z"
}
```

**Errors:**

- `400 Bad Request`: Invalid input
- `403 Forbidden`: User lacks write permission

---

### Delete Secret

**DELETE** `/api/secrets/{name}`

Soft-delete a secret (marks as inactive, preserves history).

**Path Parameters:**

- `name` (string): Secret name (URL-encoded)

**Response:**

```json
{
  "success": true,
  "message": "Secret deleted"
}
```

**Errors:**

- `404 Not Found`: Secret not found
- `403 Forbidden`: User lacks delete permission

---

### Get Secret Versions

**GET** `/api/secrets/{name}/versions`

Retrieve version history for a secret.

**Path Parameters:**

- `name` (string): Secret name (URL-encoded)

**Response:**

```json
{
  "items": [
    {
      "versionNumber": 4,
      "changeDescription": "Updated for production",
      "createdAt": "2026-03-15T11:00:00Z",
      "createdBy": "john@example.com"
    },
    {
      "versionNumber": 3,
      "changeDescription": "Initial creation",
      "createdAt": "2026-03-15T10:30:00Z",
      "createdBy": "john@example.com"
    }
  ]
}
```

---

### Restore Secret Version

**POST** `/api/secrets/{name}/restore/{version}`

Restore a secret to a previous version.

**Path Parameters:**

- `name` (string): Secret name (URL-encoded)
- `version` (int): Version number to restore to

**Request Body:**

```json
{
  "changeDescription": "Reverting to stable version"
}
```

**Response:**

```json
{
  "success": true,
  "version": 5,
  "message": "Secret restored to version 3"
}
```

---

## 🛡️ Policy Endpoints (Admin Only)

### List Policies

**GET** `/api/policies?pageNumber=1&pageSize=50`

Lists all RBAC policies. **Admin-only operation**.

**Response:**

```json
{
  "items": [
    {
      "id": "policy-123",
      "name": "team-a-read",
      "description": "Allow team A to read secrets",
      "principal": "group:team-a",
      "resourcePattern": "/secrets/team-a/*",
      "effect": "Allow",
      "actions": ["secrets:read"],
      "isEnabled": true,
      "createdAt": "2026-03-15T10:00:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 1,
  "hasNextPage": false
}
```

---

### Create Policy

**POST** `/api/policies`

Create a new RBAC policy. **Admin-only operation**.

**Request Body:**

```json
{
  "name": "team-a-read",
  "description": "Allow team A to read secrets",
  "principal": "group:team-a",
  "resourcePattern": "/secrets/team-a/*",
  "effect": "Allow",
  "actions": ["secrets:read"],
  "isEnabled": true
}
```

**Response:**

```json
{
  "id": "policy-123",
  "name": "team-a-read",
  ...
}
```

---

## 📋 Audit Log Endpoints

### List Audit Logs

**GET** `/api/audit-logs?actor=john&action=READ&result=SUCCESS&pageNumber=1`

Query audit logs with filtering.

**Parameters:**

- `actor` (query, string): Filter by user
- `action` (query, string): Filter by action type (READ, CREATE, UPDATE, DELETE)
- `resourceId` (query, string): Filter by resource
- `result` (query, string): Filter by result (SUCCESS, FAILED)
- `pageNumber` (query, int): Page number (default: 1)
- `pageSize` (query, int): Items per page (default: 50)

**Response:**

```json
{
  "items": [
    {
      "id": "log-123",
      "action": "READ",
      "actor": "john@example.com",
      "resourceType": "Secret",
      "resourceId": "db-password",
      "result": "SUCCESS",
      "statusCode": 200,
      "sourceIp": "192.168.1.100",
      "userAgent": "curl/7.68.0",
      "timestamp": "2026-03-15T10:30:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 100,
  "hasNextPage": true
}
```

---

### Search Audit Logs

**GET** `/api/audit-logs/search?actor=alice@example.com&action=CREATE&result=SUCCESS&from-date=2026-01-01&to-date=2026-01-31`

Search audit logs with full-text matching plus advanced filters.

**Parameters:**

- `q` (query, string): Full-text search term across actor, action, resource type, resource ID, resource name, and errors
- `actor` (query, string): Filter by actor
- `action` (query, string): Filter by action type
- `result` (query, string): Filter by result (SUCCESS, FAILED)
- `resource-type` (query, string): Filter by resource type
- `resource-id` (query, string): Filter by resource identifier
- `from-date` (query, date): Filter by timestamp lower bound
- `to-date` (query, date): Filter by timestamp upper bound
- `pageNumber` (query, int): Page number (default: 1)
- `pageSize` (query, int): Items per page (default: 50)

**Examples:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5001/api/audit-logs/search?action=CREATE&result=SUCCESS&resource-type=Secret"

keydral audit search --action CREATE --result SUCCESS --resource-type Secret
```

---

## 🔎 Query DSL

### Cross-Resource Search

**GET** `/api/search?q=name:"*password" AND tags:production AND action:CREATE`

Search secrets and audit logs together with a lightweight AND-based query DSL.

**Supported fields:**

| Field | Meaning |
|---|---|
| `name` | Secret name, supports `*` wildcards |
| `description` | Secret description full-text filter |
| `tags` | Secret tag filter |
| `created` | Secret created date range, e.g. `[2026-01-01 TO 2026-01-31]` |
| `updated` | Secret updated date range |
| `created-by` | Secret creator |
| `actor` | Audit actor |
| `action` | Audit action |
| `result` | Audit result |
| `resource-type` | Audit resource type |
| `resource-id` | Audit resource ID |
| `date` / `timestamp` | Audit date range |

**Example:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5001/api/search?q=name:%22*password%22%20AND%20tags:production%20AND%20action:CREATE"
```

The response contains paginated `secrets` and `auditLogs` sections.

---

### Get Audit Log

**GET** `/api/audit-logs/{id}`

Retrieve a specific audit log entry.

**Path Parameters:**

- `id` (string): Audit log ID

**Response:**

```json
{
  "id": "log-123",
  "action": "READ",
  "actor": "john@example.com",
  "resourceType": "Secret",
  "resourceId": "db-password",
  "result": "SUCCESS",
  "statusCode": 200,
  "errorMessage": null,
  "sourceIp": "192.168.1.100",
  "userAgent": "curl/7.68.0",
  "timestamp": "2026-03-15T10:30:00Z"
}
```

---

## Error Responses

### 400 Bad Request

Invalid input or malformed request

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request body is invalid"
}
```

### 401 Unauthorized

Missing or invalid authentication token

```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Token not provided or expired"
}
```

### 403 Forbidden

User lacks permission for the resource

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "User does not have permission to access this resource"
}
```

### 404 Not Found

Resource not found

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Secret 'db-password' not found"
}
```

### 500 Internal Server Error

Server error

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred"
}
```

---

## Rate Limiting

The API enforces rate limits at two levels to ensure stability and prevent abuse.

### Per-IP Global Limit

Every IP address is limited to **1000 requests per minute** across all endpoints. This is the primary DDoS/noisy-neighbour mitigation layer.

Trusted source IPs (internal services, monitoring agents) can be whitelisted in `appsettings.json`:

```json
"RateLimiting": {
  "WhitelistedIPs": ["10.0.0.1", "192.168.1.50"]
}
```

### Per-User Per-Endpoint Limits

Authenticated requests are additionally limited per user per endpoint group:

| Endpoint Group | Limit |
|---|---|
| `GET /api/secrets` | 100 requests / minute |
| `POST /api/secrets`, `PUT`, `DELETE`, `restore` | 10 requests / minute |
| `GET /api/audit-logs` | 50 requests / minute |
| `GET /health` | No per-user limit (only global per-IP) |

Unauthenticated requests are limited by source IP within the same partition.

### Rate Limit Response Headers

Every response from a rate-limited endpoint includes:

| Header | Description |
|---|---|
| `X-RateLimit-Limit` | Maximum requests allowed per window |
| `X-RateLimit-Remaining` | Permits remaining in the current window |
| `X-RateLimit-Reset` | Unix timestamp when the window resets |

When a request is rejected (HTTP **429 Too Many Requests**):

| Header | Description |
|---|---|
| `Retry-After` | Seconds to wait before retrying |

### 429 Too Many Requests

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Please try again later."
}
```

### Configuration (`appsettings.json`)

```json
"RateLimiting": {
  "Enabled": true,
  "WhitelistedIPs": [],
  "PerIp": {
    "PermitLimit": 1000,
    "WindowSeconds": 60
  },
  "SecretsGet": {
    "PermitLimit": 100,
    "WindowSeconds": 60
  },
  "SecretsPost": {
    "PermitLimit": 10,
    "WindowSeconds": 60
  },
  "AuditLogsGet": {
    "PermitLimit": 50,
    "WindowSeconds": 60
  }
}
```

All limits use a **6-segment sliding window** (replenished every 10 seconds) for smooth rate distribution.

---

## Pagination

List endpoints support cursor-based pagination:

```json
{
  "items": [...],
  "pageNumber": 2,
  "pageSize": 50,
  "totalCount": 127,
  "hasNextPage": true
}
```

Use `pageNumber` and `pageSize` to navigate results.

---

## Swagger/OpenAPI

Interactive API documentation available at:

- **Development**: `http://localhost:5001/swagger`
- **Production**: `https://keydral.your-domain.com/swagger`

---

## Examples

### cURL

**List secrets:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5001/api/secrets
```

**Get secret:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5001/api/secrets/db-password
```

**Create secret:**

```bash
curl -X PUT \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"value":"secret123","description":"My secret"}' \
  http://localhost:5001/api/secrets/my-secret
```

**Search secrets:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5001/api/secrets/search?q=postgres&tags=production"
```

**Search audit logs:**

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5001/api/audit-logs/search?action=CREATE&result=SUCCESS"
```

### JavaScript/Node.js

```javascript
const token = process.env.KEYDRAL_TOKEN;

const response = await fetch("http://localhost:5001/api/secrets", {
  headers: {
    Authorization: `Bearer ${token}`,
  },
});

const secrets = await response.json();
console.log(secrets);
```

### Python

```python
import requests

token = os.environ['KEYDRAL_TOKEN']
headers = {'Authorization': f'Bearer {token}'}

response = requests.get(
  'http://localhost:5001/api/secrets',
  headers=headers
)

secrets = response.json()
print(secrets)
```

---

## Support

For issues, questions, or suggestions, please:

1. Check the [Architecture Documentation](../architecture/)
2. Review [Deployment Guide](../deployment.md)
3. Check existing [GitHub Issues](https://github.com/LittleAndi/keydral/issues)
4. Create a new issue with detailed description
