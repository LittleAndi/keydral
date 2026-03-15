## Summary

<!-- Describe what this PR changes and why -->

## Type of Change

- [ ] Bug fix
- [ ] New feature
- [ ] Refactoring (no functional change)
- [ ] Documentation
- [ ] Dependency update

---

## Review Checklist

### Security & Authorization

- [ ] No real or plausible credentials in `appsettings.json` or `appsettings.Development.json` (use `dotnet user-secrets` or environment variables)
- [ ] Every computed authorization boolean (e.g. `isAdmin`, `canRead`) is actually enforced before accessing data — no computed-then-ignored checks
- [ ] Authorization failures return `403 Forbidden` (not `200`/`404`/`400`) so clients can distinguish "unauthorized" from "not found"

### Configuration

- [ ] Every new `appsettings.json` key maps to an existing `Options` class property (check property names match exactly — e.g. `MasterKeyFilePath` not `MasterKeyFile`)
- [ ] Any documentation examples showing `appsettings` config blocks have been updated to reflect the actual bound property names

### Entity Framework

- [ ] `HasData` seed values use **fixed** timestamp constants, not `DateTime.UtcNow` or `Guid.NewGuid()`
- [ ] Migrations have been reviewed — no unexpected model changes caused by non-deterministic seed data

### Repository / Data Access

- [ ] Repository callers do **not** call `SaveChangesAsync` after `AddAsync`/`UpdateAsync` — the base `Repository<T>` saves automatically
- [ ] Filtering, sorting, and pagination on any table that can grow unboundedly (audit logs, secrets) is pushed to the database query, not done in-memory via `GetAllAsync()` + LINQ

### Encryption & Cryptography

- [ ] Sensitive byte buffers are wiped with `CryptographicOperations.ZeroMemory`, not `Array.Clear`

### Middleware / Response Handling

- [ ] Middleware that replaces `Response.Body` with a `MemoryStream` resets `stream.Position = 0` before copying back to the original stream

### Regex / Pattern Matching

- [ ] Patterns built from user or config input use `Regex.Escape()` before applying any wildcard substitutions (follow the pattern in `PolicyRepository.MatchesResourcePattern`)

### Async Code

- [ ] No `ContinueWith` on hot tasks — use `await` followed by synchronous LINQ on the result
- [ ] `CancellationToken` is threaded through repository and service calls where available

### Testing

- [ ] All new test methods contain at least one assertion
- [ ] No empty placeholder tests remain (`UnitTest1.cs`-style)
- [ ] Test project paths in documentation match the actual `tests/` directory structure

### Documentation

- [ ] Any referenced CLI commands, file paths, or config examples have been manually verified against the running code
