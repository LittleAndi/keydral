# ADR-001: Authentication Provider Selection (Keycloak)

**Date**: 2026-03-15  
**Status**: PROPOSED  
**Authors**: Keydral MVP Team

---

## Context

Keydral requires an identity provider for:

- OIDC token generation and validation
- User/service account management
- Integration with Kubernetes ServiceAccounts
- Future cloud (Entra ID, etc.) compatibility

Options evaluated:

1. **Keycloak** (BUSL-1.1 license)
2. **Ory Hydra** (Apache 2.0)
3. **Custom minimal OAuth2 provider** (significant implementation effort)

## Decision

**Use Keycloak as the recommended OIDC provider for MVP**

### Rationale

**Pros:**

- Well-established, production-ready
- Containerized (docker image available)
- Supports OIDC device flow (ideal for CLI login)
- Easily integrates with Docker Compose for local dev
- Strong community and documentation
- Keycloak now owned by Red Hat/open source (community edition)

**Cons:**

- License is BUSL-1.1 (Business Source License)
  - Code is available to read, but has commercial use restrictions
  - **ACTION REQUIRED**: Verify org licensing aligns with Keycloak BUSL-1.1 terms
  - For internal/on-prem use (Keydral target), typically acceptable
  - If blocked: alternative is **Ory Hydra** (Apache 2.0)

## Implementation

### MVP Scope

- Keycloak runs in Docker Compose alongside PostgreSQL
- Secrets API validates JWTs from Keycloak
- Keycloak realm + client configured via init scripts (future)
- CLI uses device flow for login

### Future Options

- HSM/TPM signing keys for master certificates
- Custom claims for RBAC policies
- Multi-realm support for multi-tenancy (Phase 2)

## Licensing Verification Required

**ACTION**: Before proceeding with Keycloak integration:

1. Review Keycloak BUSL-1.1 license terms: https://www.keycloak.org/
2. Confirm compliance with org legal/licensing policy
3. If blocked, switch to **Ory Hydra** (Apache 2.0, same OIDC capabilities)

**Timeline**: Must complete before Sprint 2 (Phase 1D: Auth implementation).

## Alternatives Reconsidered

### Ory Hydra

- Apache 2.0 licensed (no restrictions)
- More lightweight than Keycloak
- Requires more manual setup for user management
- **Fallback if Keycloak licensing is blocked**

### Custom OAuth2 Provider

- Full control, MIT-compatible
- 3-4 weeks implementation effort (not suitable for MVP timeline)
- **Consider Phase 2 if Keycloak licensing remains unresolved**

## Related Decisions

- [ADR-002]: Database storage backend (PostgreSQL)
- [ADR-003]: API technology stack (.NET minimal APIs)

---

### Sign-off

- [ ] Legal/Licensing review: ******\_\_\_******
- [ ] Architecture: ******\_\_\_******
- [ ] Team: ******\_\_\_******
