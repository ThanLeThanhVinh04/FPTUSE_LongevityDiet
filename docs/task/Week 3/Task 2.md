# Task 2 - Weekly Report + Worker Reliability & Recovery

**Owner:** Thành viên 2
**Reviewer:** Thành viên 4
**Branch:** `feature/w3-t2-report-worker-reliability`
**Status:** Ready for Review

## Architecture alignment (ADR-005)

- **Target owners:** Tracking & Progress Service owns `WeeklyReport` in `LongevityTrackingDb`; Background Worker owns only job/retry/idempotency state in `LongevityWorkerDb`.
- **Async flow:** Worker computes asynchronously and emits result; Tracking persists the domain result.
- **Guardrail:** Worker never reads/writes TrackingDb directly in the target architecture.


## Mục tiêu

Tạo weekly summary có business value đồng thời đưa async pipeline lên mức có retry, recovery và idempotency đáng tin cậy.

## Weekly report

- `WeeklyReport`, `WeeklyReportMetric`, generation version/status thuộc Tracking & Progress Service / `LongevityTrackingDb`.
- Input: activity summary, LDAS trend, eating window, challenge, logging consistency qua approved event/read-model contract.
- Worker schedule/compute job theo local week boundary bằng state trong `LongevityWorkerDb`, sau đó publish `WeeklyReportGenerated`/result event.
- Tracking & Progress Service consume result event và persist `WeeklyReport` vào `LongevityTrackingDb`.
- Idempotent key: User + Week + ReportVersion.
- Historical report không bị rewrite âm thầm.
- API list/detail/current status + UI report page.
- Worker không query/write `LongevityTrackingDb` trực tiếp.

## Worker reliability

- Bounded retry/backoff.
- Dead-letter/recovery path.
- XACK chỉ sau business side effect thành công.
- Pending-entry recovery.
- ProcessedEvent unique EventId.
- Outbox retry metadata + cleanup/retention.
- Graceful shutdown/cancellation.
- Read-only diagnostics: pending/failed/heartbeat.

## Testing bắt buộc

- Duplicate weekly job.
- Empty/new user.
- Week/timezone boundary.
- Worker crash before/after side effect.
- Duplicate delivery.
- Redis/SQL unavailable.
- Poison message + pending recovery.
- Concurrent consumers.

## Definition of Done

Weekly report được tạo idempotently và Worker có thể restart/retry mà không mất message hoặc double side effect; failure quan sát và phục hồi được.


## Implementation Evidence — 2026-11-05

- Implemented inside the current canonical shared runtime. The target split into `LongevityTrackingDb` and `LongevityWorkerDb` is **not** claimed as deployed; the current Worker reaches report/progress data through application/repository boundaries backed by the shared canonical database.
- Weekly report persistence: `WeeklyReport` + `WeeklyReportMetric`, immutable identity `User + WeekStart + ReportVersion`, migration `20261105065719_20261105_WeeklyReportWorkerReliability`, list/detail/current API and `/reports` UI.
- Weekly scheduler: Worker scans users periodically, resolves each user's local week through the configured timezone, and creates one idempotent `WeeklyReport` job per User + Week + Version.
- Worker reliability state: `WorkerJob`, `WorkerProcessedEvent`, `WorkerDeadLetter`, `WorkerHeartbeat`; bounded exponential backoff, max-attempt dead-lettering, explicit recovery, stale Processing-job recovery after crashes, heartbeat diagnostics and graceful cancellation.
- Redis Streams path is real, not mocked: API publishes `WeeklyReportRequested.v1` with `XADD`; Worker creates/uses consumer group, reads with `XREADGROUP`, reclaims stale pending messages with `StreamAutoClaimAsync`, persists the idempotent scheduling/processed-event side effect, then sends `XACK`. Redis connection/timeout errors are not acknowledged and are retried.
- Runtime Redis smoke on Docker Compose: event `9a466a20-72ce-4640-a4b4-25a00d2c1b63` was added to `longevity:weekly-report-events`; Worker created persisted report `1EC5DEDB-9BAF-4224-AFCB-C78621CEE58A` with status `Ready` for week `2026-10-26`; `WorkerProcessedEvents` contained exactly one row for that EventId; Redis `XPENDING longevity:weekly-report-events weekly-report-worker` returned `0` after processing.
- Targeted integration tests: 5/5 PASS for report idempotency/history immutability, duplicate weekly job key, duplicate event delivery, poison job -> dead-letter -> recovery, and stale-processing recovery.
- Quality gates: project structure PASS; `git diff --check` PASS; EF reports no pending model changes; .NET build 0 warnings / 0 errors; unit tests 51/51 PASS; frontend tests 6/6 PASS; frontend lint 0 warnings / 0 errors; production build PASS; Docker Compose rebuild/start PASS.
- Full integration suite: 35/38 PASS. The three failures are the same pre-existing MealLog create `Expected Created / Actual InternalServerError` failures already reproduced on clean `origin/develop` commit `474b475`; they are outside Week 3 Task 2.
- Architecture gap kept explicit: this transitional runtime does not yet have physically separate Tracking/Worker databases or a separate Tracking consumer that persists a Worker-emitted result event. Therefore this branch does **not** claim the ADR-005 target deployment is complete; Week 4 release-readiness documentation must continue to label that topology as Target until real services/databases exist.
- Branch push remains blocked by repository permission: authenticated GitHub account `ThanLeThanhVinh04` receives HTTP 403 for `YAHsaveme/FPTUSE_LongevityDiet`. Reviewer approval is still required before Done.
