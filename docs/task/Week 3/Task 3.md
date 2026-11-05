# Task 3 - FMD Education, Safety Gate & Cycle Tracking

**Owner:** Thành viên 3
**Reviewer:** Thành viên 1
**Branch:** `feature/w3-t3-fmd-safety-tracking`
**Status:** Done

## Architecture alignment (ADR-005)

- **Target owner:** Planning Service (`REST :8083`).
- **Owned data:** FMD safety assessment/cycle state in `LongevityPlanningDb`.
- **Guardrail:** backend safety state machine remains deterministic; Local AI cannot decide eligibility or therapeutic behavior.

## Mục tiêu

Implement FMD trong phạm vi **education + safety assessment + tracking**, không tạo therapeutic protocol và không biến ứng dụng thành medical device.

## Safety workflow

State tối thiểu:
1. EducationNotViewed
2. EducationViewed
3. AssessmentRequired
4. AssessmentSubmitted
5. EligibleForTracking
6. ProfessionalReviewRequired

Backend phải enforce transition; UI không phải security boundary.

## Phạm vi chính

- FmdEducationAcknowledgement.
- FmdSafetyAssessment/FmdSafetyAnswer/FmdEligibilityResult.
- FmdCycle/FmdCycleDay/FmdCycleStatus.
- Assessment/version/source metadata.
- High-risk flag route sang professional review.
- Chỉ EligibleForTracking mới start cycle.
- User có thể stop cycle bất kỳ lúc nào.
- Không generate fasting prescription hoặc therapeutic menu.

## API/UI

- Education metadata + acknowledgement (`GET /api/v1/fmd/education`, `POST /api/v1/fmd/education/acknowledge`).
- Assessment submit/current state (`POST /api/v1/fmd/safety/assessment`, `GET /api/v1/fmd/safety/status`).
- Eligibility/tracking state.
- Start/get/update/stop/complete cycle + history (`POST /api/v1/fmd/cycles`, `GET /api/v1/fmd/cycles/active`, `POST /api/v1/fmd/cycles/{id}/days/{day}`, `POST /api/v1/fmd/cycles/{id}/stop`, `POST /api/v1/fmd/cycles/{id}/complete`, `GET /api/v1/fmd/cycles`).
- UI hoàn chỉnh: `FmdWorkspaceSection.tsx` tích hợp trên `PlannerWorkspacePage.tsx`.

## Testing bắt buộc đã hoàn thành

- [x] Blocking/high-risk flags (`FmdSafetyAndCycleTests`, `FmdApiTests`).
- [x] Invalid state transition/direct API bypass (`FmdApiTests.Fmd_SafetyWorkflow_EnforcesStateTransitionsAndBlocksHighRiskUsers`).
- [x] Start without eligibility: blocked with 400/422.
- [x] Eligibility revoked / ProfessionalReviewRequired blocks start.
- [x] Duplicate active cycle: blocked with 409 Conflict.
- [x] Stop/complete/history immutability.
- [x] Ownership isolation: User B cannot access/log/stop User A's cycle.

## Definition of Done

High-risk user bị backend chặn khỏi self-directed cycle; eligible user chỉ được track cycle state/history và toàn bộ wording giữ đúng wellness/education scope.
