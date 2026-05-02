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

Currently not implemented. Coming in Phase 2.

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
