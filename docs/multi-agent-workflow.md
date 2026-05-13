# Multi-Agent Workflow for Keydral

This document describes the recommended multi-agent workflow for developing Keydral features using AI coding agents (e.g. GitHub Copilot coding agent, Copilot in VS Code agent mode). It maps agent roles to the actual project structure, defines branch conventions, provides copy-paste prompt templates, and outlines the security guardrails that every agent must respect.

---

## Table of Contents

1. [Agent Roles and Ownership](#1-agent-roles-and-ownership)
2. [Branch Naming and Integration Strategy](#2-branch-naming-and-integration-strategy)
3. [Prompt Templates](#3-prompt-templates)
4. [Execution Playbook](#4-execution-playbook)
5. [Security Guardrails](#5-security-guardrails)

---

## 1. Agent Roles and Ownership

Seven specialised agents cover the full Keydral codebase. Each agent owns a distinct slice of the source tree. A brief description of responsibilities and a "do not touch" list are given so agents stay in their lane.

| Agent | Owned paths | Responsibilities |
|---|---|---|
| **Planner / Orchestrator** | `README.md`, `spec/`, `docs/decisions/`, `docs/multi-agent-workflow.md` | Decomposes the feature into tasks, writes the integration plan, assigns work to specialist agents, reviews the final integration branch |
| **API agent** | `src/Keydral.API/` | Minimal-API endpoints, middleware, rate-limiting, request/response DTOs, OpenAPI annotations, `appsettings.json` alignment |
| **Core-domain agent** | `src/Keydral.Core/` | Domain models, business rules, repository interfaces, service interfaces, RBAC policy evaluation |
| **Encryption agent** | `src/Keydral.Encryption/` | Envelope encryption, key derivation, `EncryptionOptions`, cryptographic buffer wiping, algorithm upgrades |
| **Storage agent** | `src/Keydral.Storage/` | EF Core context, migrations, repository implementations, database query optimisation, seed data |
| **CLI / Ops agent** | `src/Keydral.CLI/`, `src/Keydral.AppHost/`, `src/Keydral.ServiceDefaults/`, `Dockerfile`, `docker-compose.yml`, `scripts/` | CLI commands and UX, Aspire orchestration, dev-container, Docker/Compose configuration, health checks |
| **Test + Review agent** | `tests/` | Unit tests, integration tests, test utilities; reviews PRs from all other agents for correctness and coverage |

> **Planner authority**: The Planner owns the high-level plan and the integration branch. Only the Planner merges specialist branches into `integration/*`. Specialist agents must not merge into `main` directly.

---

## 2. Branch Naming and Integration Strategy

### Specialist branches

Each agent works on a short-lived specialist branch named after its role and the feature:

```
<agent-role>/<feature-slug>

Examples:
  api/secret-restore
  core/secret-restore
  encryption/key-rotation
  storage/audit-log-pagination
  cli/login-device-flow
  tests/secret-restore-coverage
```

### Integration branch

The Planner creates a single integration branch before any specialist work begins:

```
integration/<feature-slug>

Example:
  integration/secret-restore
```

Specialist branches are merged into the integration branch as they complete. The final PR to `main` is opened from `integration/<feature-slug>` after all specialist work is merged and the Test + Review agent has signed off.

### Merge order recommendation

For features that touch multiple layers, merge in dependency order to reduce conflicts:

1. `core/*` — domain types and interfaces first (no external deps)
2. `encryption/*` — consumes Core interfaces
3. `storage/*` — consumes Core interfaces, implements them
4. `api/*` — composes Core + Storage + Encryption
5. `cli/*` — consumes API or Core
6. `tests/*` — covers all of the above

---

## 3. Prompt Templates

Copy the relevant template, fill in the `[PLACEHOLDER]` sections, and paste it as the agent's initial instruction. Each template includes the mandatory security checks for that layer.

---

### 3.1 Planner / Orchestrator

```
You are the Planner for Keydral, a .NET/C# local-first secrets management service.

Feature: [FEATURE NAME]
Issue / spec ref: [GITHUB ISSUE URL or spec/mvp-spec.md section]

Your job:
1. Read README.md, spec/mvp-spec.md, and any linked ADRs in docs/decisions/ to understand constraints.
2. Decompose the feature into tasks for each specialist agent (API, Core, Encryption, Storage, CLI/Ops, Tests/Review).
3. Create the integration branch: integration/[feature-slug]
4. Write a plain-text task card for each agent that specifies:
   - What to implement
   - Which files to create or modify (within that agent's owned paths only)
   - Acceptance criteria
   - Cross-agent contracts (types, interfaces, endpoint signatures) that must not change once agreed
5. After all specialist branches are merged into integration/[feature-slug], review the integrated diff for
   architectural consistency and open the final PR to main.

Do NOT write production code yourself. Coordinate agents and review; do not implement.
```

---

### 3.2 API Agent

```
You are the API agent for Keydral. Your scope is src/Keydral.API/ only.
Do not modify files outside src/Keydral.API/.

Feature: [FEATURE NAME]
Branch: api/[feature-slug]
Integration branch: integration/[feature-slug]

Task:
[PASTE PLANNER TASK CARD FOR API AGENT]

Keydral-specific conventions you MUST follow:
- Use ASP.NET Core Minimal APIs only (no MVC controllers).
- Register endpoints in an extension method under Endpoints/ (e.g. SecretsEndpoints.cs).
- Return typed results (TypedResults.Ok, TypedResults.Created, TypedResults.Forbid, etc.).
- Authorization failures must return 403 Forbidden — never 200, 404, or 400 as a substitute.
- Every computed authorization boolean (isAdmin, canRead, etc.) must be checked before any data access.
- When adding a new appsettings.json key, verify the property name matches the corresponding Options class exactly.
  Do not use real or plausible credentials — use empty strings or "<replace-me>" placeholders.
- Rate-limiting policies are registered in RateLimiting/RateLimitingExtensions.cs; add new policies there.
- Annotate new endpoints with OpenAPI metadata (Tags, Summary, Produces).
- Run: dotnet build Keydral.sln && dotnet test Keydral.sln --no-build
  All builds and tests must pass before you consider the branch ready.
```

---

### 3.3 Core-Domain Agent

```
You are the Core-domain agent for Keydral. Your scope is src/Keydral.Core/ only.
Do not modify files outside src/Keydral.Core/.

Feature: [FEATURE NAME]
Branch: core/[feature-slug]
Integration branch: integration/[feature-slug]

Task:
[PASTE PLANNER TASK CARD FOR CORE AGENT]

Keydral-specific conventions you MUST follow:
- Domain models and interfaces live here. No EF Core attributes, no HTTP types, no CLI dependencies.
- Repository interfaces (ISecretRepository, etc.) are defined in Core; implementations live in Storage.
- Service interfaces (IEncryptionService, etc.) are defined in Core; implementations live in Encryption or Storage.
- Apply RBAC policy evaluation logic in Core, not in API endpoints.
- Run: dotnet build Keydral.sln && dotnet test Keydral.sln --no-build
  All builds and tests must pass before you consider the branch ready.
```

---

### 3.4 Encryption Agent

```
You are the Encryption agent for Keydral. Your scope is src/Keydral.Encryption/ only.
Do not modify files outside src/Keydral.Encryption/.

Feature: [FEATURE NAME]
Branch: encryption/[feature-slug]
Integration branch: integration/[feature-slug]

Task:
[PASTE PLANNER TASK CARD FOR ENCRYPTION AGENT]

Keydral-specific conventions you MUST follow:
- Keydral uses envelope encryption: per-secret data keys encrypted by a master key (see spec/mvp-spec.md §4).
- EncryptionOptions (Provider, MasterKeyFilePath, Algorithm) must stay in sync with appsettings.json — update both
  in the same PR if you change option property names.
- Wipe all sensitive byte buffers with CryptographicOperations.ZeroMemory, NOT Array.Clear.
  Array.Clear may be elided by the JIT in release builds; ZeroMemory is guaranteed non-elide-able.
- Never log plaintext key material, raw secret bytes, or decrypted values anywhere.
- New cryptographic primitives must be reviewed against the OWASP Cryptographic Storage cheat sheet.
- Run: dotnet build Keydral.sln && dotnet test Keydral.sln --no-build
  All builds and tests must pass before you consider the branch ready.
```

---

### 3.5 Storage Agent

```
You are the Storage agent for Keydral. Your scope is src/Keydral.Storage/ only.
Do not modify files outside src/Keydral.Storage/.

Feature: [FEATURE NAME]
Branch: storage/[feature-slug]
Integration branch: integration/[feature-slug]

Task:
[PASTE PLANNER TASK CARD FOR STORAGE AGENT]

Keydral-specific conventions you MUST follow:
- Repository<T> base class calls SaveChangesAsync inside AddAsync and UpdateAsync.
  Callers must NOT call SaveChangesAsync again — it causes a redundant round-trip.
- Filtering, sorting, and pagination on unbounded tables (AuditLogs, Secrets, Versions) must be pushed
  to the DB query (Where/OrderBy/Skip/Take before ToListAsync), not done in memory after GetAllAsync.
- PolicyRepository.MatchesResourcePattern is the canonical reference for safe glob-to-regex conversion.
  Always call Regex.Escape() on the pattern before applying wildcard substitutions.
- HasData seed values must use fixed timestamp constants (new DateTime(2026, 1, 1, ...)) — never
  DateTime.UtcNow or Guid.NewGuid(), which generate spurious migrations on every build.
- After adding a migration, verify no empty/phantom migration is generated on the next build.
- Run: dotnet build Keydral.sln && dotnet test Keydral.sln --no-build
  All builds and tests must pass before you consider the branch ready.
```

---

### 3.6 CLI / Ops Agent

```
You are the CLI/Ops agent for Keydral.
Your scope is: src/Keydral.CLI/, src/Keydral.AppHost/, src/Keydral.ServiceDefaults/, Dockerfile,
docker-compose.yml, scripts/.
Do not modify files outside these paths.

Feature: [FEATURE NAME]
Branch: cli/[feature-slug]
Integration branch: integration/[feature-slug]

Task:
[PASTE PLANNER TASK CARD FOR CLI/OPS AGENT]

Keydral-specific conventions you MUST follow:
- CLI commands use the keydral <noun> <verb> pattern (e.g. keydral secret set, keydral policy apply).
- Interactive prompts must mask sensitive input (passwords, secret values).
- JSON output mode must be supported for all data-returning commands (--output json flag).
- Aspire resource definitions live in AppHost/Program.cs — keep the dev-time resource graph consistent
  with docker-compose.yml so both run the same dependency set.
- Do not hard-code ports or hostnames that differ between Aspire and Docker Compose without a clear comment.
- Dockerfile multi-stage builds must not embed secrets or tokens in image layers.
- Run: dotnet build Keydral.sln && dotnet test Keydral.sln --no-build
  All builds and tests must pass before you consider the branch ready.
```

---

### 3.7 Test + Review Agent

```
You are the Test + Review agent for Keydral. Your scope for new test code is tests/ only.
You also review diffs from all other agents before the integration branch is merged to main.

Feature: [FEATURE NAME]
Branch: tests/[feature-slug]
Integration branch: integration/[feature-slug]

Task:
[PASTE PLANNER TASK CARD FOR TEST AGENT]

Keydral-specific testing conventions you MUST follow:
- Test projects mirror the src/ layout under tests/ (e.g. tests/Keydral.API.Tests/).
- Every test method must contain at least one assertion. Remove placeholder test stubs before merging.
- Use xUnit. Arrange-Act-Assert structure. Meaningful test method names.
- Encryption tests must verify that ZeroMemory is called on key material buffers (use a Memory<byte> spy or
  verify buffer contents are zeroed after the call under test).
- Authorization tests must verify that a 403 Forbidden is returned when the caller lacks the required role —
  not an empty list, not 404, not 400.
- Storage tests that cover filtering or pagination must assert the query runs at the DB layer (verify
  SQL or use EF InMemory with explicit record counts that would fail if filtering happened in memory).
- After writing tests, run: dotnet build Keydral.sln && dotnet test Keydral.sln --no-build
  All tests must pass, including pre-existing ones.

Review checklist (apply to every agent's diff before approving integration merge):
[Use the checklist in .github/PULL_REQUEST_TEMPLATE.md as the authoritative list]
```

---

## 4. Execution Playbook

This is the step-by-step script for running a complete feature through the multi-agent workflow. The example feature is **secret restore** (restoring a soft-deleted secret to active state).

### Step 0 — Planner sets up the integration branch

```bash
# Start from main
git fetch origin main
git checkout main
git pull origin main

# Create the integration branch
git checkout -b integration/secret-restore
git push -u origin integration/secret-restore
```

### Step 1 — Planner decomposes the feature and assigns tasks

The Planner opens the Planner prompt template, fills in the feature details, and produces task cards for each relevant specialist agent. Task cards are posted as comments on the GitHub issue or in a shared planning document.

For secret restore, the relevant agents are:
- **Core** — add `RestoreAsync` to `ISecretRepository`, add a `RestoredAt` timestamp to the domain model
- **Storage** — implement `RestoreAsync` in `SecretRepository`, add EF migration
- **API** — add `POST /secrets/{name}/restore` endpoint
- **Tests** — unit tests for the service logic, integration tests for the endpoint

### Step 2 — Specialist agents work in parallel on their branches

```bash
# Core agent
git checkout -b core/secret-restore origin/integration/secret-restore
# ... implements ISecretRepository.RestoreAsync, domain changes ...
git push -u origin core/secret-restore

# Storage agent (starts once Core interface is agreed)
git checkout -b storage/secret-restore origin/integration/secret-restore
# ... implements SecretRepository.RestoreAsync, migration ...
git push -u origin storage/secret-restore

# API agent (starts once Core interface is agreed)
git checkout -b api/secret-restore origin/integration/secret-restore
# ... adds POST /secrets/{name}/restore endpoint ...
git push -u origin api/secret-restore
```

### Step 3 — Merge specialist branches into the integration branch in dependency order

```bash
git checkout integration/secret-restore

# 1. Core first
git merge --no-ff core/secret-restore -m "merge: core/secret-restore into integration"

# 2. Storage (depends on Core interface)
git merge --no-ff storage/secret-restore -m "merge: storage/secret-restore into integration"

# 3. API (depends on Core + Storage)
git merge --no-ff api/secret-restore -m "merge: api/secret-restore into integration"

git push origin integration/secret-restore
```

### Step 4 — Test + Review agent writes and runs tests

```bash
git checkout -b tests/secret-restore origin/integration/secret-restore
# ... writes tests in tests/Keydral.API.Tests and tests/Keydral.Core.Tests ...
dotnet build Keydral.sln
dotnet test Keydral.sln --no-build
git push -u origin tests/secret-restore

# Merge tests into integration
git checkout integration/secret-restore
git merge --no-ff tests/secret-restore -m "merge: tests/secret-restore into integration"
git push origin integration/secret-restore
```

### Step 5 — Test + Review agent reviews the full integration diff

The Test + Review agent opens a review against `main` and works through the checklist in `.github/PULL_REQUEST_TEMPLATE.md`. Issues are raised as PR review comments; specialist agents fix them in their branches and the corrections are re-merged into the integration branch.

### Step 6 — Planner opens the final PR to main

```bash
gh pr create \
  --base main \
  --head integration/secret-restore \
  --title "feat: secret restore" \
  --body "Closes #[ISSUE_NUMBER]

  Implements soft-deleted secret restore via POST /secrets/{name}/restore.

  Specialist branches merged:
  - core/secret-restore
  - storage/secret-restore
  - api/secret-restore
  - tests/secret-restore"
```

### Step 7 — CI validation and merge

All CI checks must pass on the integration branch before the PR is merged. The Planner is responsible for resolving any CI failures by routing them back to the appropriate specialist agent.

---

## 5. Security Guardrails

The following rules apply to **all agents** working on any part of Keydral. These are not optional: a PR that violates them must not be merged.

### 5.1 No real credentials in source

`appsettings.json` and `appsettings.Development.json` are committed to git and become permanent history. They must never contain real or plausible credentials.

```jsonc
// ✅ Correct
"Keycloak": {
  "ClientSecret": ""
}

// ❌ Wrong
"Keycloak": {
  "ClientSecret": "abc123realpassword"
}
```

Supply real values via `dotnet user-secrets` (local dev) or environment variables (CI/prod).

### 5.2 Authorization must be enforced, not just computed

A boolean that is evaluated but not used as a guard is a silent security hole.

```csharp
// ✅ Correct
bool isAdmin = userContext.HasRole("secret-admin");
if (!isAdmin) return TypedResults.Forbid();

// ❌ Wrong — isAdmin is computed but never checked
bool isAdmin = userContext.HasRole("secret-admin");
var secret = await secretRepo.GetAsync(name); // proceeds regardless
```

### 5.3 Authorization failures return 403

Using `200` (empty list), `404`, or `400` for access-denied cases leaks information about what exists.

### 5.4 Cryptographic buffer wiping

```csharp
// ✅ Correct — guaranteed non-elide-able
CryptographicOperations.ZeroMemory(keyBytes);

// ❌ Wrong — JIT may optimise this away in release builds
Array.Clear(keyBytes, 0, keyBytes.Length);
```

### 5.5 Never log plaintext secrets or key material

Structured logging (Serilog) is configured for Keydral. Log identifiers and metadata, not values.

```csharp
// ✅ Correct
_logger.LogInformation("Secret {Name} accessed by {Actor}", name, actor);

// ❌ Wrong
_logger.LogInformation("Secret value: {Value}", plaintextValue);
```

### 5.6 Encryption agent has exclusive write access to src/Keydral.Encryption/

No other agent should modify cryptographic implementation files. If a cross-cutting change requires updating `EncryptionOptions`, the Encryption agent owns that change; the API agent may only update `appsettings.json` to match, after coordination.

### 5.7 Configuration alignment

Every new `appsettings.json` key must exactly match the corresponding `Options` class property name. Mismatches bind silently as defaults, which can disable security features without any error.

| Config section | Options class | Validated in |
|---|---|---|
| `Keycloak` | `KeycloakOptions` | `Validate()` on startup |
| `Encryption` | `EncryptionOptions` | `Validate()` on startup |
| `AuditLogging` | `AuditLoggingOptions` | Bound via `Configure<T>` |
| `ConnectionStrings` | Consumed by `AddDbContext` | EF startup |

### 5.8 Test expectations for security paths

The Test + Review agent must include at minimum:

- A test asserting `403 Forbidden` is returned when a caller lacks the required role (for every new protected endpoint).
- A test asserting that plaintext secret values are not present in audit log entries.
- For any new encryption operation: a test that the input buffer is zeroed after the operation completes.

---

*This document is maintained alongside the rest of the Keydral architecture documentation. If the project structure changes, update the ownership table in §1 and the prompt templates in §3 to match.*
