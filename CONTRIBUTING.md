# Contributing to Keydral

## Getting Started

See [docs/SETUP.md](docs/SETUP.md) for local development setup.

---

## Coding Conventions

### Repository Pattern — Save Semantics

The base `Repository<T>` class calls `SaveChangesAsync` inside both `AddAsync` and `UpdateAsync`. **Do not call `SaveChangesAsync` again at the endpoint or service layer** after these methods — doing so causes a redundant round-trip.

```csharp
// ✅ Correct
await policyRepository.AddAsync(policy);
return TypedResults.Created(...);

// ❌ Wrong — double-save
await policyRepository.AddAsync(policy);
await policyRepository.SaveChangesAsync(); // redundant
```

If you need to batch multiple changes before saving, track entities with EF directly and call `SaveChangesAsync` once at the end — or refactor the repository to expose a `TrackAsync` + explicit save pattern.

---

### Database Filtering — Never Load Unbounded Tables In-Memory

For any table that can grow without bound (audit logs, secrets, versions), **filtering, sorting, and pagination must happen in the database**, not in application memory.

```csharp
// ✅ Correct — filter in DB
var logs = await context.AuditLogs
    .Where(a => a.Actor == actor)
    .OrderByDescending(a => a.Timestamp)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// ❌ Wrong — loads entire table
var logs = await auditLogRepository.GetAllAsync();
var filtered = logs.Where(a => a.Actor == actor).ToList();
```

---

### Regex Safety — Always Escape Before Glob Substitution

When building a regex pattern from a user-supplied or config-supplied string, **always call `Regex.Escape()` first**, then apply wildcard substitutions.

`PolicyRepository.MatchesResourcePattern` is the canonical reference implementation:

```csharp
// ✅ Correct
var regexPattern = Regex.Escape(pattern)
    .Replace(@"\*\*", ".*")
    .Replace(@"\*", "[^/]*");

// ❌ Wrong — raw special chars like +, ?, (, [ will be interpreted as regex metacharacters
var regexPattern = pattern
    .Replace(".", @"\.")
    .Replace("**", ".*")
    .Replace("*", "[^/]*");
```

---

### Async Patterns — Avoid `ContinueWith`

Prefer `await` + synchronous LINQ over `ContinueWith`. The latter complicates exception propagation and cancellation behaviour.

```csharp
// ✅ Correct
var all = await context.Secrets.Where(s => !s.IsDeleted).ToListAsync(ct);
return all.Where(s => regex.IsMatch(s.Name));

// ❌ Wrong
return await context.Secrets.ToListAsync(ct)
    .ContinueWith(t => t.Result.Where(s => regex.IsMatch(s.Name)), ct);
```

---

### Cryptographic Buffer Wiping

Use `CryptographicOperations.ZeroMemory` to wipe sensitive byte buffers. The JIT is permitted to elide `Array.Clear` calls in release builds; `ZeroMemory` is guaranteed non-elide-able.

```csharp
// ✅ Correct
CryptographicOperations.ZeroMemory(sensitiveBytes);

// ❌ Wrong — may be optimised away
Array.Clear(sensitiveBytes, 0, sensitiveBytes.Length);
```

---

### Response-Body Middleware — Stream Position

Middleware that replaces `HttpContext.Response.Body` with a `MemoryStream` to capture the response **must reset `Position = 0` before copying the stream back**. At the point of copying, the stream position is at the end and `CopyToAsync` will write 0 bytes.

```csharp
// ✅ Correct
await _next(context);
responseStream.Position = 0;                          // <-- reset
await responseStream.CopyToAsync(originalBodyStream);

// ❌ Wrong — clients receive empty responses
await _next(context);
await responseStream.CopyToAsync(originalBodyStream); // position is at end
```

---

### Authorization — Always Enforce the Result

Computed authorization booleans must be enforced with a guard before proceeding. A boolean that is evaluated but never checked is a silent security hole.

```csharp
// ✅ Correct
bool isAdmin = userContext.HasRole("secret-admin");
if (!isAdmin) return TypedResults.Forbid();
// proceed

// ❌ Wrong — isAdmin computed but never used
bool isAdmin = userContext.HasRole("secret-admin");
var allLogs = await auditRepository.GetAllAsync(); // proceeds regardless
```

Authorization failures must return `403 Forbidden`. Using `200` (empty list), `404`, or `400` for access-denied cases makes it impossible for clients to distinguish "you don't have permission" from "it doesn't exist" or "bad request".

---

### Configuration Alignment

Every `appsettings.json` section must use the exact property names defined in its corresponding `Options` class. Mismatches silently bind as defaults and can cause startup validation failures or subtly broken behaviour.

When modifying or adding an `Options` class, update `appsettings.json` (and any documentation examples) in the same PR.

| Config section      | Options class              | Validated in             |
| ------------------- | -------------------------- | ------------------------ |
| `Keycloak`          | `KeycloakOptions`          | `Validate()` on startup  |
| `Encryption`        | `EncryptionOptions`        | `Validate()` on startup  |
| `AuditLogging`      | `AuditLoggingOptions`      | Bound via `Configure<T>` |
| `ConnectionStrings` | Consumed by `AddDbContext` | EF startup               |

**`Keycloak` section shape** (matches `KeycloakOptions`):

```json
"Keycloak": {
  "Url": "http://localhost:8080",
  "Realm": "master",
  "ClientId": "keydral-api",
  "ClientSecret": ""
}
```

**`Encryption` section shape** (matches `EncryptionOptions`):

```json
"Encryption": {
  "Provider": "File",
  "MasterKeyFilePath": "/path/to/masterkey.txt",
  "Algorithm": "AES-256-GCM"
}
```

---

### Credentials in Source

`appsettings.json` and `appsettings.Development.json` **must not contain real or plausible credentials**. Use empty strings or `"<replace-me>"` placeholders. Supply real values via:

- `dotnet user-secrets` for local development
- Environment variables for CI and production

These are committed to git. Anything put here will exist in history permanently.

---

### Entity Framework Seed Data

`HasData` values are captured in the migration model snapshot. **Never use non-deterministic values** (`DateTime.UtcNow`, `Guid.NewGuid()`, etc.) in seed data — they cause EF to detect phantom model changes on every build and generate spurious empty migrations.

```csharp
// ✅ Correct
CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)

// ❌ Wrong — changes every run, causes noisy migrations
CreatedAt = DateTime.UtcNow
```

---

### Test Quality

- Every test method must contain at least one assertion.
- Remove or replace placeholder tests (`UnitTest1`, `[Fact] public void Test1() {}`) before merging.
- Test project paths follow the `tests/` directory structure (not `src/`).
