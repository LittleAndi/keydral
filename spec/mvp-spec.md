# 🧩 Keydral MVP Spec — Local Secrets Vault

## 🎯 Goals

- Store **secrets / certificates / keys**
- Secure access via **identity + RBAC**
- Provide **API + CLI + K8s integration**
- Support **encryption at rest + in transit**
- Easy to run **locally or in a cluster**
- Good developer experience (DX)

---

# 🏗️ High-Level Architecture

```
Clients (apps, CLI, CI/CD)
        |
   Auth Gateway (OIDC / mTLS)
        |
   Secrets API Service
        |
   Encryption Layer (KMS / master key)
        |
   Storage Backend (DB)
```

---

# 🔐 Core Components (Minimal Set)

## 1️⃣ Secrets API Service

**Responsibility**

- CRUD secrets
- versioning
- access control checks
- audit logging

**Tech choices**

- Lightweight REST API (Go / .NET minimal API)
- OpenAPI spec
- Stateless pods

**Features**

- `/secrets/{name}`
- `/secrets/{name}/versions`
- `/certificates`
- `/keys`

---

## 2️⃣ Authentication Layer

Must support **real identity** — not just API keys.

### Recommended options

- **OIDC provider**
  Example: Keycloak
- OR **mTLS for service-to-service**

This enables:

- Kubernetes ServiceAccount → OIDC token
- CLI login via browser/device flow
- Future cloud compatibility

---

## 3️⃣ Authorization (RBAC)

Simple but effective:

Example model:

```
Role: secret-reader
Role: secret-writer
Role: secret-admin
```

Policy example:

```
team-a can read secrets under /team-a/*
```

Implementation:

- Store policies in DB
- Cache in memory

---

## 4️⃣ Encryption Layer (VERY Important)

### Envelope Encryption Model

- **Master key**
- **Per-secret data keys**

Flow:

```
secret -> encrypted with data key
data key -> encrypted with master key
```

### Master key options

- File-based key (simple)
- TPM integration (better)
- K8s secret (acceptable for local)
- Hardware token later

---

## 5️⃣ Storage Backend

Keep simple.

### Good options

- PostgreSQL
- SQLite (single node dev)
- etcd (native K8s style)

For cluster use → PostgreSQL recommended.

Store:

- encrypted secret blob
- metadata
- version
- policy refs
- audit events

---

## 6️⃣ Kubernetes Integration

This is where UX becomes excellent ⭐

Add:

### 🔹 CSI Secret Driver

App pod mounts secret like:

```
/mnt/secrets/db-password
```

Architecture:

```
App Pod
  -> CSI driver
     -> Secrets API
```

Similar pattern used by:

- Azure Key Vault
- HashiCorp Vault

---

## 7️⃣ CLI Tool (Developer Experience)

This is hugely important.

Example:

```
keydral login
keydral secret set db-password
keydral secret get db-password
```

Should support:

- interactive login
- JSON output
- kube-login mode
- token caching

---

## 8️⃣ Audit Logging

Minimal but powerful:

Log:

- who accessed what
- from where
- result

Store:

- append-only table
- optionally ship to OpenTelemetry later (fits your current logging migration 👍)

---

# 🚀 Optional Phase-2 Features

Do NOT build initially.

- dynamic secrets (DB credentials)
- secret rotation
- HSM integration
- UI dashboard
- multi-tenant isolation
- replication

---

# 🧠 Suggested Deployment Layout (Local K8s)

```
namespace: keydral-system

- secrets-api deployment
- postgres statefulset
- keycloak deployment
- csi-driver daemonset
```

---

# ✅ Security Checklist (Minimum)

- TLS everywhere
- mTLS inside cluster (if possible)
- secrets never logged
- memory zeroing after decrypt
- short-lived access tokens
- RBAC enforced server-side
- rate limiting

---

# ❓ Clarifying Questions (Important)

These will affect architecture a LOT:

1️⃣ Do you want this mainly for

- **local dev convenience**
- or **production-grade on-prem replacement**?

2️⃣ Should apps access secrets:

- via **API at runtime**
- or **mounted files only**
- or **both**

3️⃣ Do you want:

- **Azure Entra-like identity integration later**
- or fully standalone identity forever?

4️⃣ Single cluster or **multi-cluster secret sharing**?

5️⃣ Should secret values be:

- small (passwords/tokens)
- or large (cert bundles / connection configs)

6️⃣ Is **high availability required** initially?
