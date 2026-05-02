# ADR-002: Storage Backend Selection (PostgreSQL)

**Date**: 2026-03-15  
**Status**: ACCEPTED  
**Authors**: Keydral MVP Team

---

## Context

Keydral MVP requires persistent storage for:

- Encrypted secret values
- Secret metadata (name, version, created_at, tags)
- Encryption metadata (data key IDs, algorithms)
- RBAC policies and rules
- Audit logs (who accessed what, when, result)
- Optionally: user/role mappings (if not delegated to Keycloak)

Options evaluated:

1. **PostgreSQL** (production-grade, proven)
2. **SQLite** (simplicity, single-file)
3. **etcd** (K8s-native, built-in consistency)

## Decision

**Use PostgreSQL for MVP (and beyond)**

### Rationale

**Pros:**

- Mature, production-ready, well-understood
- ACID guarantees (crucial for secrets)
- Easy Docker Compose setup → local dev experience
- Strong typed schema (prevents data corruption)
- Rich query capabilities for filtering/auditing
- Scales horizontally with read replicas (Phase 2)
- No operational mystery (unlike etcd)
- Docker image available: `postgres:16-alpine`

**Cons:**

- More heavyweight than SQLite (not an issue for MVP)
- Requires separate service/deployment

## Not Chosen

### SQLite

- Would work for single-node local dev
- **Problem**: No clear upgrade path to multi-node
- Not recommended for "production-like" testing
- **Decision**: Start with PostgreSQL to avoid refactoring later

### etcd

- K8s-native approach
- **Problem**: Adds operational complexity (etcd best practices, backup, recovery)
- **Better choice**: PostgreSQL now, optional etcd integration in Phase 3

## Implementation

### MVP

- PostgreSQL 16 (Alpine image for small footprint)
- Entity Framework Core for ORM
- Migrations for schema versioning
- Docker Compose for local dev

### Schema Entities

1. `secrets` — encrypted secret data + metadata
2. `secret_versions` — version tracking
3. `encryption_keys` — data key IDs + encrypted master key
4. `policies` — RBAC rules (path-based)
5. `audit_logs` — append-only access logs
6. `users` (optional) — local cache of Keycloak identity
7. `roles` (optional) — local cache of role definitions

### Backup & Recovery

- **MVP**: Manual backups via `pg_dump`
- **Phase 2**: Automated backup strategy (WAL archiving, point-in-time recovery)

### Scaling

- **MVP**: Single PostgreSQL instance
- **Phase 2HA**: Primary + streaming replicas + load balancing
- **Phase 3**: Automated failover, multi-region

## Related Decisions

- [ADR-001]: Authentication provider (Keycloak)
- [ADR-003]: API technology stack (.NET minimal APIs)

---

### Sign-off

- [ ] Operations: ******\_\_\_******
- [ ] Architecture: ******\_\_\_******
