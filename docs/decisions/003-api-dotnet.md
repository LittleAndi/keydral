# ADR-003: API Technology Stack (.NET Minimal APIs)

**Date**: 2026-03-15  
**Status**: ACCEPTED  
**Authors**: Keydral MVP Team

---

## Context

Keydral API service must:

- Provide stateless REST endpoints (CRUD secrets, policies, audit)
- Validate JWT tokens from Keycloak
- Perform envelope encryption/decryption
- Integrate with PostgreSQL
- Support OpenAPI/Swagger documentation
- Run in Kubernetes (container-ready)
- Have excellent async/concurrency support

Language/framework options evaluated:

1. **.NET 8 (minimal APIs)** — C#
2. **Go** — `net/http` or `gin`
3. **Rust** — `actix-web` or `axum`

## Decision

**Use .NET 8 with Minimal APIs (C#)**

### Rationale

**Pros:**

- ✅ Minimal APIs: lightweight REST framework (no MVC overhead)
- ✅ Async-first: excellent concurrency with `async/await`
- ✅ Built-in DI: native dependency injection
- ✅ OpenAPI support: automatic Swagger generation
- ✅ Entity Framework Core: top-tier ORM for PostgreSQL
- ✅ Security: built-in auth utilities, token validation
- ✅ .NET 8 LTS: production-ready with 3+ years support
- ✅ Performance: comparable to Go, excellent at scale
- ✅ Docker: single-binary deployment via Native AOT (future)
- ✅ Team capability match (your selection)

**Cons:**

- Requires .NET SDK installation (not an issue with Docker)
- Slightly larger container image than Go (mitigated by Alpine images)

## Not Chosen

### Go

- Good choice; lighter container footprint
- Less type safety than .NET
- Manual error handling (vs. exceptions)
- **Reason not chosen**: Team preference for .NET ecosystem

### Rust

- Maximum safety + performance
- Steep learning curve
- Overkill for MVP (worth revisiting Phase 2 if perf critical)
- **Reason not chosen**: Development velocity concern

## Implementation

### API Framework

- **ASP.NET Core 8.0** with **minimal APIs**
- No MVC controllers; endpoint declarations only
- Clean separation of concerns via extension methods

### Project Structure

```
Keydral.API/
  ├── Program.cs                 # Entry point, DI setup
  ├── appsettings.json          # Config
  └── Endpoints/
      ├── SecretsEndpoints.cs    # Secret CRUD
      ├── CertsEndpoints.cs      # Certificate CRUD
      ├── PoliciesEndpoints.cs   # RBAC policy
      └── AuditEndpoints.cs      # Audit log access
```

### Key Dependencies

- **Entity Framework Core** (Npgsql provider)
- **System.IdentityModel.Tokens.Jwt** (OIDC token validation)
- **Serilog** (structured logging)
- **Swashbuckle.AspNetCore** (OpenAPI/Swagger)

### Database Layer

- Entity Framework Core (LINQ-to-SQL)
- Migrations for schema versioning
- Repository pattern for data access (optional, DI handles most)

### Authentication

- JWT bearer token middleware
- Custom claims extraction from Keycloak token
- Per-endpoint authorization attributes

### Deployment

- **Local dev**: `dotnet run`
- **Docker**: Multi-stage build, .NET 8 Alpine base image
- **Kubernetes**: Helm chart (Phase 2)

### Observability

- Serilog for structured logging
- OpenAPI endpoint at `/swagger`
- Health check endpoint at `/health`
- Future: OpenTelemetry integration (aligns with org migration)

## Performance Targets

- **Throughput**: 1000+ requests/second (single instance)
- **Latency**: <100ms for secret read (p99)
- **Memory**: ~200MB baseline (container)

## Alternatives Reconsidered

### Go (gin or net/http)

- Lighter footprint, simpler concurrency model
- Strong choice if container size is critical
- **Fallback**: Consider for Phase 2 critical path if needed

### Rust (actix-web)

- Maximum type safety and performance
- Complexity not justified for MVP
- **Consider Phase 2**: If running into performance ceiling

## Related Decisions

- [ADR-002]: Storage backend (PostgreSQL)
- [ADR-001]: Auth provider (Keycloak)

---

### Sign-off

- [ ] Architecture: ******\_\_\_******
- [ ] Operations: ******\_\_\_******
- [ ] Team Lead: ******\_\_\_******
