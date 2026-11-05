# Task 2 - 14-Day Challenge + Progress Dashboard

**Owner:** Thành viên 2
**Reviewer:** Thành viên 4
**Branch:** `feature/w2-t2-challenge-dashboard`
**Status:** Ready for Review

## Architecture alignment (ADR-005)

- **Target owners:** Planning Service owns Challenge state in `LongevityPlanningDb`; Tracking & Progress Service owns Dashboard/progress projections in `LongevityTrackingDb`.
- **Integration:** projection updates are event-driven/idempotent; replay must not double count.
- **Guardrail:** dashboard aggregation must not become a cross-database SQL join.


## Mục tiêu

Biến meal/activity/LDAS thành vòng lặp hành vi 14 ngày và dashboard tổng hợp dùng dữ liệu thật.

## Phạm vi chính

- ChallengeTemplate, ChallengeDayTemplate, UserChallenge, UserChallengeDay.
- Start/complete/uncomplete challenge, progress và streak theo local date.
- Completion idempotent; không tự backfill ngày đã bỏ lỡ.
- DailyWellnessSummary/WeeklyProgressSummary hoặc projection tương đương.
- Dashboard aggregate: latest LDAS, activity, eating-window, challenge, logging consistency.
- Projection update idempotent, có rebuild strategy.
- Event replay không được double count.

## API/UI

- List/start/current/history challenge.
- Complete/uncomplete day.
- Dashboard summary + trend series.
- 14-day timeline, current-day card, progress/streak.
- Dashboard cards/trend có loading, empty, stale/update state.

## Testing bắt buộc

- Duplicate challenge start.
- 14/14 completion.
- Timezone/streak boundary.
- Ownership.
- Projection idempotency.
- Event replay.
- Empty/new user.
- Date-range aggregation.

## Deliverables

- Schema/migration.
- Challenge service/API/UI.
- Projection service + dashboard API/UI.
- Unit/integration tests.

## Definition of Done

User có thể chạy challenge 14 ngày và xem dashboard từ dữ liệu persist thật; reload/event replay không làm sai streak hoặc aggregate.


## Implementation Evidence — 2026-11-05

- Implemented as logical Planning/Tracking boundaries inside the current canonical shared runtime; no separate Planning/Tracking databases or deployed microservices are claimed.
- Challenge persistence: `ChallengeTemplate`, 14 deterministic `ChallengeDayTemplate` rows, `UserChallenge`, `UserChallengeDay`; migration `20261105064242_20261105_ChallengeProgress` and idempotent `ChallengeSeed`.
- Challenge behavior: list/start/current/history, complete/uncomplete day, duplicate-active challenge conflict, per-user ownership, local-date handling, no future-day completion, idempotent repeated completion and automatic Completed state at 14/14.
- Progress projection: persistent `DailyWellnessSummary` with unique User + LocalDate, rebuild-on-demand strategy, and `ProgressEventReceipt` unique EventId so replay cannot double count.
- Dashboard API/UI: 14-day trend, logging consistency, eating-window state, challenge progress/streak and latest calories from persisted data. Activity/LDAS are explicitly reported as `unavailable` because those source projections are not integrated into this runtime; no fabricated values are shown.
- Frontend route `/progress` provides start flow, 14-day timeline, complete/uncomplete interaction, progress/streak cards, dashboard trend, loading/error states, and navigation from planner.
- EF validation: `dotnet ef migrations has-pending-model-changes` reports no pending model changes.
- Week 2 targeted integration tests: 5/5 PASS covering duplicate start, ownership, 14/14 completion, repeated-completion idempotency, uncomplete/streak behavior, empty/new user date-range dashboard, persisted daily summaries and event replay idempotency.
- Quality gates: project structure PASS; `git diff --check` PASS; .NET build 0 warnings / 0 errors; unit tests 51/51 PASS; frontend tests 6/6 PASS; frontend lint 0 warnings / 0 errors; production build PASS.
- Full integration suite currently reports 3 failures in pre-existing MealLog create flows (`Expected Created, Actual InternalServerError`). Each failing test was reproduced unchanged on clean `origin/develop` at commit `474b475`; this is a baseline blocker outside Week 2 Task 2, not a regression introduced by this branch.
- Process gate: reviewer (Thành viên 4) review/approval required before Done. Branch push is currently blocked by GitHub repository permission: authenticated account `ThanLeThanhVinh04` receives HTTP 403 for `YAHsaveme/FPTUSE_LongevityDiet`.
