# C2, Complete Database Diagrams, and Cloudflare Refresh Sessions — Delivery Report

**Local delivery date:** 2026-11-05 (Vietnam time)
**Repository:** `YAHsaveme/FPTUSE_LongevityDiet`
**Source baseline:** `main` at `1962735c02b61b69a0697741fab4173362cade33`.
**Working branch:** `docs/c2-all-databases-cloudflare-prototype`.
**Workspace:** `D:\PRN232\FPTUSE_LongevityDiet`.

## Editable diagrams, presentations, and rendered evidence

| Artifact | Path | Status |
| --- | --- | --- |
| C2 component diagrams (8 pages) | `docs/assignment/extended-diagrams/05-c2-component-architecture.drawio` | Native editable draw.io XML, generated and render-inspected |
| Database diagrams (9 pages) | `docs/assignment/extended-diagrams/06-all-database-erd.drawio` | Current DB + 6 Target SQL + proposed Cloudflare D1 |
| C2 handout | `docs/assignment/extended-diagrams/05-c2-component-architecture.pdf` | All 8 pages, exported with verified draw.io desktop |
| Database handout | `docs/assignment/extended-diagrams/06-all-database-erd.pdf` | All 9 pages, exported with verified draw.io desktop |
| PNG evidence | `docs/assignment/extended-diagrams/previews/` | 17 page images |
| Data ownership rationale | `docs/assignment/06-C2-AND-DATABASE-MAPPING.md` | Current vs Target; 33 logical entity definitions |

Architecture **C2 = C4 Level 3 Component**. Current implemented REST API is shown separately from seven Target views; the Gateway/6-service target split is not falsely described as deployed.

## Database truth and scope

- **Current:** exactly **one application SQL Server database** `LongevityDietDb`, with three EF Core entity tables `Users`, `UserProfiles`, `RefreshTokens`, in the examined code/migration snapshot.
- **Target:** six **logical** SQL databases: Identity, Catalog, Planning, Tracking, Recommendation, Worker; 33 distinct logical entity definitions covered **33/33**. One physical SQL Server may contain all six logical databases in the intended local/target deployment.
- The separate optional **Cloudflare D1** design is a future seventh data store for refresh session hashes and audit metadata, not part of the current six target SQL databases.
- **20 intra-database FK relationships** are explicitly drawn, and no cross-service SQL foreign keys are invented. Cross-boundary IDs are documented as stable IDs resolved via APIs/events.
- Proposed Recommendation snapshots and Worker jobs/dead-letter tables are labeled **PROPOSED** and not included in the canonical 33 names.
- Outbox and ProcessedEvent are *patterns replicated per relevant service DB*, not a single global Outbox table controlled by Worker. Actual physical table instances may exceed 33.

## Cloudflare implementation: code ready, activation NOT complete

- `cloudflare/refresh-sessions/src/index.mjs` — authenticated Cloudflare Worker API for hashed session issue/lookup/atomic rotate/revoke.
- `cloudflare/refresh-sessions/migrations/0001_create_refresh_sessions.sql` — D1 relational schema, indexes and constraints.
- `src/LongevityDiet.API/Cloudflare/CloudflareRefreshTokenRepository.cs` — .NET opt-in provider behind `CloudflareSessions:Enabled`.
- `src/LongevityDiet.API/Program.cs` — fail-fast config validation for HTTPS base URL and service credential; SQL provider remains the default when disabled.
- `docs/security/CLOUDFLARE-REFRESH-TOKENS.md` — architecture, secret management, session cutover and rollback gates.
- Raw refresh tokens are **not sent to D1**; the browser receives an HttpOnly/SameSite session cookie. Only SHA-256 hashes and server-side session metadata are transmitted to the private Worker.
- Worker transactions use D1 `batch()`, validate expiry, and deny old-token replay without trusting a stale local read.
- **No live Cloudflare account, D1 database, Worker route or secrets have been provisioned or accessed here.** Feature flag remains disabled. A real migration and production E2E gate are still required before claiming refresh tokens are stored on Cloudflare.

**Opt-in environment examples (do NOT commit real secret values):**
```text
CloudflareSessions__Enabled=true
CloudflareSessions__BaseUrl=https://YOUR_PRIVATE_WORKER_HOST/
CloudflareSessions__ServiceBearer=<SECRET_FROM_SECURE_SECRET_MANAGER>
```

Only set these after provisioning and running production-like integration and concurrency tests. Existing SQL sessions need an explicit reauthentication/migration decision.

## Verification evidence (completed)

| Gate | Evidence | Result |
| --- | --- | --- |
| draw.io XML and C2/ERD semantics | `scripts/Validate-C2DbDiagrams.ps1` | **PASS**, 8 C2 pages + 9 DB pages; 33/33 target types, 20 FK connectors, 0 malformed edge endpoints or out-of-bounds shapes |
| Rendering | draw.io Desktop v32.6.1, SHA-256 verified | **PASS**, 17 PNG + 2 PDF produced and sample images inspected |
| Existing project structure | `scripts/Validate-ProjectStructure.ps1` | **PASS** |
| API build | `dotnet build src/LongevityDiet.API -c Release` | **PASS**, 0 warnings / 0 errors |
| Existing .NET unit tests | `dotnet test tests/LongevityDiet.UnitTests -c Release` | **PASS**, 8/8 |
| API integration tests + Cloudflare adapter fake-handler tests | `dotnet test tests/LongevityDiet.IntegrationTests -c Release` | **PASS**, 12/12 |
| Worker in-memory D1 mock tests | `node --test cloudflare/refresh-sessions/tests/worker.test.mjs` | **PASS**, 6/6 |
| SQLite migration/rotation constraints | `scripts/Test-CloudflareSqlSchema.ps1` | **PASS**, 3/3 |
| Existing assignment C0/C1 export + semantic/33-table check | `scripts/Validate-AssignmentDiagrams.ps1` | **Partial PASS**: C0/C1 exports, geometry and 33-table check passed; final Docker/Structurizr live validation blocked because **Docker daemon is not running/available** |

**Not yet verified:** actual Cloudflare D1 connectivity and authorization, live Worker concurrent refresh, live legacy-session cutover and rollback, Docker/Structurizr live validation in this machine environment, and full production deployment. Do not mark these gates complete without evidence.

## Diagram tools

21 unique source repositories from the request were shallow-cloned under `D:\PRN232\_diagram_toolkit_sources`, with commit records in `sync-report.tsv`. Four relevant skill directories were copied into the project's `.agents/skills/`. The portable draw.io Desktop v32.6.1 and uv v0.13.7 were downloaded from official releases and SHA-256 verified.

Open any `.drawio` file using diagrams.net or draw.io Desktop (File > Open From > Device). Original canonical C0/C1 and assignment documents remain in `docs/assignment/diagrams/`; extended C2/ERD files intentionally live separately.
