# Release & Operations Runbook — Current Runtime

Updated: 2026-11-05  
Owner: Thành viên 2 — Week 4 Task 2

## 1. Scope and architecture truth

This runbook verifies the **current canonical runtime** in this repository:

- Web (Nginx + React)
- ASP.NET Core API
- Recommendation gRPC service
- Background Worker
- SQL Server 2022
- Redis 7 with AOF

ADR-005 additionally describes an API Gateway, four separate REST domain services, service-owned logical databases, Worker Ops on :8086, and a public HTTPS :443 edge. Those items are **Target Architecture only** at this checkpoint and must not be described as deployed until real projects, databases and Compose/runtime evidence exist.

For local/demo safety, all published Compose ports are bound to `127.0.0.1`. This still is **not** the target production TLS topology.

## 2. Prerequisites and configuration

Required: Docker Desktop/Engine + Compose v2, .NET SDK 9, Node.js 24, Git and PowerShell.

Initialize local configuration:

```powershell
.\scripts\Initialize-LocalDevelopment.ps1
```

Or copy `.env.example` to `.env` and replace every `CHANGE_ME` value. Never commit `.env`.

## 3. Static readiness

```powershell
.\scripts\Test-ReleaseReadiness.ps1
docker compose --env-file .env.example config --quiet
.\scripts\Validate-ProjectStructure.ps1
git diff --check
```

The readiness script checks canonical structure, Compose parsing, loopback-only checked-in port publishing, Git-ignore protection for `.env`, and tracked backup/secret artifacts.

## 4. Start / stop / restart

```powershell
docker compose up -d --build
docker compose ps
.\scripts\Test-ReleaseReadiness.ps1 -RequireRunningStack
```

Keep named-volume data:

```powershell
docker compose stop
docker compose start
docker compose down
```

Destructive reset:

```powershell
docker compose down -v
```

Only use `-v` when data loss is intentional.

## 5. Health and logs

Local/demo endpoints:

- Web: `http://127.0.0.1:5173`
- API health: `http://127.0.0.1:8080/health`
- API OpenAPI in Development: `http://127.0.0.1:8080/openapi/v1.json`
- Recommendation gRPC: private Compose DNS `recommendation-grpc:8081`
- SQL Server: `127.0.0.1:14330`
- Redis: `127.0.0.1:6379`

Useful commands:

```powershell
docker compose ps
docker compose logs api --tail 200
docker compose logs worker --tail 200
docker compose logs recommendation-grpc --tail 200
docker compose exec redis redis-cli PING
docker compose exec redis redis-cli XINFO GROUPS longevity:weekly-report-events
docker compose exec redis redis-cli XPENDING longevity:weekly-report-events weekly-report-worker
```

Worker diagnostics are Admin-only through `GET /api/v1/admin/worker/diagnostics`.

## 6. Migrations and seed

Development API startup applies EF migrations and deterministic Catalog/Challenge seed.

Model drift check:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\src\LongevityDiet.Repositories\LongevityDiet.Repositories.csproj `
  --startup-project .\src\LongevityDiet.API\LongevityDiet.API.csproj `
  --context LongevityDietDbContext
```

Fresh migration was verified from zero into a disposable database with all **7 migrations**.

Seed idempotency evidence on 2026-11-05:

```text
Before restart: 4 Foods | 2 Recipes | 2 DietRules | 1 ChallengeTemplate | 14 ChallengeDayTemplates
After restart 1: same
After restart 2: same
SEED_IDEMPOTENCY=PASS
```

Production should run migrations as an explicit controlled deployment step rather than depend on Development startup behavior.

## 7. Backup and restore

Create a verified backup:

```powershell
.\scripts\Backup-LocalData.ps1
```

It creates a SQL `.bak` with CHECKSUM, runs `RESTORE VERIFYONLY`, then stores the verified SQL backup and a Redis `.rdb` snapshot under ignored `.local-backups\`.

Verify a SQL backup by restoring into a disposable database:

```powershell
$bak = Get-ChildItem .\.local-backups\LongevityDietDb-*.bak |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
.\scripts\Test-SqlRestore.ps1 -BackupPath $bak.FullName
```

By default the verification database is removed after success; live `LongevityDietDb` is not overwritten.

Verified on 2026-11-05: SQL backup CHECKSUM + `RESTORE VERIFYONLY` PASS; disposable restore returned migration count 7 and was removed.

Do not commit `.bak`, `.rdb`, dumps or copied volumes.

## 8. Named-volume persistence

```powershell
.\scripts\Test-LocalPersistence.ps1
```

Verified on 2026-11-05:

- SQL migrations before restart: 7
- SQL migrations after restart: 7
- Redis marker persisted: PASS
- named-volume restart persistence: PASS

Named volumes:

- `longevity-diet-sqlserver-data` -> SQL `/var/opt/mssql`
- `longevity-diet-redis-data` -> Redis `/data` with AOF

On a cloud VM, Docker volume data must reside on persistent VM disk. This repository cannot prove a future VM storage class; deployment evidence must record it.

## 9. Redis Streams / Worker recovery

Weekly report async channel:

```text
stream: longevity:weekly-report-events
consumer group: weekly-report-worker
```

Behavior:

- producer: XADD;
- consumer: XREADGROUP;
- stale pending recovery: `StreamAutoClaimAsync`;
- Processed EventId is unique;
- XACK occurs only after business side effects succeed;
- bounded SQL job retry/backoff;
- poison job -> dead letter;
- stale Processing job -> Pending recovery;
- Admin recovery: `POST /api/v1/admin/worker/dead-letters/{id}/recovery`;
- Worker heartbeat is persisted.

Runtime evidence on 2026-11-05:

- EventId `9a466a20-72ce-4640-a4b4-25a00d2c1b63` entered Redis.
- Worker persisted one processed-event row and report `1EC5DEDB-9BAF-4224-AFCB-C78621CEE58A`.
- Report status `Ready`, week `2026-10-26`, version 1.
- `XPENDING` returned 0 after processing.

## 10. Controlled dependency failure

Redis:

```powershell
docker compose stop redis
docker compose logs worker --tail 100
docker compose start redis
docker compose exec redis redis-cli PING
```

Unread/pending stream messages are not acknowledged while Redis is unavailable.

Recommendation gRPC:

```powershell
docker compose stop recommendation-grpc
Invoke-WebRequest http://127.0.0.1:8080/health
docker compose start recommendation-grpc
```

API process health is independent of the gRPC container. Recommendation calls should exercise the application's deterministic fallback path; validate the recommendation flow before release.

## 11. CI gates

`.github/workflows/project-quality.yml` runs:

1. project structure validation;
2. Compose config validation;
3. .NET restore;
4. .NET build;
5. .NET test;
6. frontend `npm ci`;
7. frontend tests;
8. frontend lint;
9. frontend build.

Pushes to `main` additionally run **current-runtime-release-smoke** with ephemeral CI-only SQL/JWT secrets, Docker build/start, API/Web health wait, Playwright Docker E2E, Redis evidence and teardown.

The job is deliberately not called a Target Architecture smoke because the target Gateway/domain-service/TLS topology is not implemented.

## 12. Known blockers

### Pre-existing integration failure

The full integration suite has three failures in the existing MealLog create flow: expected HTTP 201, actual HTTP 500. The same failures were reproduced on clean `origin/develop` commit `474b475`; they are not introduced by Member 2 Week 2–4.

Current verified result:

- Unit: 51/51 PASS
- Integration: 35/38 PASS
- Week 2 targeted: 5/5 PASS
- Week 3 targeted: 5/5 PASS
- Frontend: 6/6 PASS
- Frontend lint/build: PASS

CI correctly runs the full .NET suite, so it remains red until the owning baseline MealLog defect is fixed. Do not skip or weaken tests merely to obtain a green build.

### Target architecture gap

The following ADR-005 target components are not real deployables yet:

- public HTTPS :443 edge;
- API Gateway;
- Identity REST :8081;
- Catalog REST :8082;
- Planning REST :8083;
- Tracking REST :8084;
- separate Worker Ops :8086;
- six service-owned databases enforced at runtime.

Therefore those diagrams/topologies must remain labeled **Target**.

## 13. Rollback policy

For current demo runtime:

1. create and verify SQL/Redis backup;
2. record release Git commit/image tag;
3. stop API/Worker before destructive recovery;
4. restore only from verified backup;
5. redeploy previous known-good commit/image;
6. run readiness, health and targeted regression checks;
7. do not silently rewrite historical WeeklyReports or published RuleVersions.

Schema downgrade is not assumed safe. Prefer forward-fix migrations unless a rollback migration and backup were tested.

## 14. Release-candidate checklist

- [ ] Git working tree is clean.
- [ ] No secret or backup artifact is tracked.
- [ ] Structure validation passes.
- [ ] Compose config passes.
- [ ] EF has no pending model changes.
- [ ] Build has 0 warnings / 0 errors.
- [ ] Full test result, including blockers, is recorded.
- [ ] Frontend tests/lint/build pass.
- [ ] Clean Docker image build passes.
- [ ] API/Web/Redis runtime smoke passes.
- [ ] Backup + verify passes.
- [ ] Named-volume persistence passes.
- [ ] Redis Worker pending state is inspected.
- [ ] Current-vs-Target architecture wording is accurate.
