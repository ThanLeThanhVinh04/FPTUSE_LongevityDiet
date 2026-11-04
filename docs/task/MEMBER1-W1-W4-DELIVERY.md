# Member 1 — Week 1 to Week 4 Delivery and Evidence

**Reviewed:** 2026-11-05 (Asia/Ho_Chi_Minh)  
**Branch:** `feature/member1-weeks2-4-implementation`  
**Baseline:** `1962735c02b61b69a0697741fab4173362cade33` (`main`), including prior diagrams commit `d1bc2f13f06be2f8ce18120d21ddb6124333b55e`.  
**Assignment sources:** `docs/task/Week {1,2,3,4}/Task 1.md`, `docs/task/ARCHITECTURE-ALIGNMENT-MATRIX.md`.

## Delivery matrix

| Week | Member 1 assignment | Verified local implementation | Environment/dependency gate |
| --- | --- | --- | --- |
| 1 | Identity, Auth, Profile & Onboarding | Reused existing User/Profile, JWT, Secure/HttpOnly/SameSite cookie, login/refresh rotation/revoke; original tests kept | SQL Server E2E requires Docker environment |
| 2 | Activity Tracking and LDAS | Separate `LongevityTrackingDb` migration; owner-scoped activity CRUD, soft delete, date/pagination, timezone-aware daily/weekly summary, configurable activity goals; versioned deterministic LDAS partial score; transactional ActivityLogged/Deleted Outbox; `/activity` UX | Actual MealLog & EatingWindow signals from Member 3 not yet in repo: LDAS total is **NULL**, not invented |
| 3 | Reminder Scheduling and Notification Center | Identity DB prefs/notifications/inbox/outbox; Worker DB schedules/jobs/retry durable inbox/dispatch/outbox; Redis Streams publisher/consumer skeleton, duplicate event and window handling; timezone/quiet hours/DST, opt-out; `/notifications` UI; profile TZ update publishes rescheduling event | Actual Redis→Worker→Identity live integration not yet exercised against configured SQL Server, Redis and seeded data |
| 4 | Security, DI, resilience, observability and performance | JWT owner-scoping, rate limits on Auth and Member API, HTTP 429 with Retry-After, safe correlation ID and logs, liveness and readiness paths, secure cookies, DB/Redis health, EF Core migrations, source-generated Worker/bridge logs, feature-flag Cloudflare default OFF | Live performance p50/p95, Docker/Structurizr target verification and production tracing backend have **not** been measured |

**Interpretation:** Major Week 2–4 feature code is implemented with passing in-memory tests, but **some acceptance criteria are incomplete**: upstream LDAS sources, live SQL/Redis round trip, service deployment, real benchmarks, and independent Task 3 reviewer gates. Passing tests do **not** imply 100% production acceptance.

## Data boundaries

- `LongevityDietDb`: existing Identity/Profile plus `ReminderPreferences`, `UserNotifications`, `IdentityInbox`, `IdentityOutbox`. Only Identity API accesses it.
- `LongevityTrackingDb`: `ActivityLogs`, `ActivityGoals`, `AdherenceScores`, `ScoreDimensions`, `ScoreConfigurationVersions`, `TrackingScoreSignals`, `TrackingOutbox`. Separate DbContext; no cross-database FKs.
- `LongevityWorkerDb`: `ReminderSchedules`, `ReminderDispatchRecords`, `WorkerProcessedEvents`, `WorkerOutbox`. Worker never queries Identity or Tracking DB.
- APIs run in current single ASP.NET Core host as **transitional adapters**, not a claim that planned six standalone services or YARP API Gateway are deployed.
- In-memory SQLite tests replace each DbContext with independent in-memory connection; real SQL-specific execution plan and EF migrations require dedicated SQL Server acceptance check.

## API contract

| Domain | Routes |
| --- | --- |
| Activity | `POST/GET /api/v1/activities`, `PUT/DELETE /api/v1/activities/{id}`, `GET /api/v1/activities/summary`, `GET/PUT /api/v1/activities/goal` |
| LDAS | `GET /api/v1/ldas/{localDate}`, `GET /api/v1/ldas/history` |
| Reminders | `GET/PUT /api/v1/reminders/preferences`, `GET /api/v1/reminders/preview` |
| Notifications | `GET /api/v1/notifications`, `GET /api/v1/notifications/unread-count`, `POST /api/v1/notifications/{id}/read`, `POST /api/v1/notifications/read-all` |
| Reliability | `GET /health` liveness, `GET /ready` SQL and Redis readiness, `X-Correlation-ID` response header |

All member data routes derive the User ID from the JWT, not the client payload.

## Scoring design and transparency

The current scoring formula is an **educational project policy**, NOT a clinically validated rule. Published version `ldas-v1`: activity 35%, meal 45%, eating-window 20%; stored version, breakdown, source reason and percentage of *observed* dimensions. Missing meal/window data yields `score: null`, `isPartial: true`; no fake `0`, health prediction, fabricated adherence or false clinical claim. The missing Member 3 event contract must specify trusted 0–100 source scores, monotonic source revisions and timezone-local date before ingesting those signals.

## Redis contract

- Identity Outbox → Redis `ldc:identity-events` with `ReminderPreferenceUpdated.v1` (eventId/userId/timeZone/opt-in/preferences/version).
- Worker Inbox processes with `WorkerProcessedEvents`; persistent `ReminderSchedules` and `ReminderDispatchRecords` prevent normal duplicate schedule dispatch.
- Worker Outbox → Redis `ldc:worker-events` with `NotificationReady.v1` (eventId/userId/category/title/body/allowedDeepLink/deduplicationKey).
- Identity Inbox checks current account status and opt-in, stores only local `UserNotifications` and deduplicates EventId + business key.
- Tracking Outbox → Redis `ldc:tracking-events` with `ActivityLogged.v1` / `ActivityDeleted.v1`, without free-text notes.
- Redis consumer groups allow replay after disconnect; malformed JSON moves to `ldc:dead-letter`. Higher-grade poison-event retry budgets and centralized monitoring should be completed with the Member 4 operator integration before production.

## Verified tests and build commands

```powershell
dotnet test tests/LongevityDiet.UnitTests/LongevityDiet.UnitTests.csproj -c Release
dotnet test tests/LongevityDiet.IntegrationTests/LongevityDiet.IntegrationTests.csproj -c Release
dotnet build src/LongevityDiet.API/LongevityDiet.API.csproj -c Release
dotnet build src/LongevityDiet.Worker/LongevityDiet.Worker.csproj -c Release
npm --prefix src/LongevityDiet.Web run build
```

Additional evidence: `Member1SecurityApiTests`, `ActivityLdasApiTests`, `ReminderNotificationsApiTests`, `ReminderWorkerEngineTests` and existing `IdentityApiTests`. Unit and integration tests use mock/in-memory databases, not actual SQL + Redis. `scripts/Benchmark-Member1Api.ps1` collects real p50/p95/p99/HTTP failure stats **only when live system is running**, never pre-fills fabricated numbers.

## Blockers / next acceptance gates (do not claim pass)

1. **Real Docker E2E:** Docker Desktop was initially stopped; after launching it, drive C: has only about **0.89 GB free**. No `.env` exists in this repo. Do not start SQL Server volume/compose or invent signing/password secrets with this space constraint. Obtain a separate environment or free safe space after user approval.
2. **Member 3 W2:** `MealLog` + `EatingWindow` pipeline absent. Need actual source events before complete LDAS 0–100 can be computed.
3. **Member 4 W3/W4:** Production Redis broker, stream permissions, DLQ operator, Worker deployment and observability collector pending. Worker code has local tests but real broker E2E pending.
4. **Member 3 W3/W4:** No running FMD safety/weekly report service to integrate. No fabricated end-to-end approval.
5. **Live performance:** Run `scripts/Benchmark-Member1Api.ps1` against deployed environment; document data set and hardware. Report observed p95 rather than assuming thresholds.

## Member 1 reviewer obligations for Task 3

| Week | Task 3 review target | What can be reviewed today | Status |
| --- | --- | --- | --- |
| W1 | Deployment & FE/BE split | Existing repo structure/ports and current auth routing | Static review only; integration regression scripts remain prerequisite |
| W2 | Recommendation V2 and source data | gRPC skeleton exists but no real ranking/brokers to run | **BLOCKED** by corresponding feature branch / API contract |
| W3 | FMD safety and compliance | Current source has no FMD operational flow | **BLOCKED** by owning member implementation |
| W4 | E2E regression and release gate | Unit/integration and static validation available | **PARTIAL**; Docker E2E and deployment gate still outstanding |

This report intentionally distinguishes **source implementation** from **actual production verification**.
