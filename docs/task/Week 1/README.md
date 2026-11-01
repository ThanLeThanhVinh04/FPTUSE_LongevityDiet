# Week 1 - Core Product Foundation + Distributed Thin Slice

## Mục tiêu tuần

Hoàn tất các dependency cốt lõi để hệ thống có business implementation thật và chứng minh sớm các technology bắt buộc của PRN232.

## Phân công

| Task | Owner | Reviewer | Trạng thái |
|---|---|---|---|
| Task 1 - Identity, Authentication, Profile & Onboarding | Thành viên 1 (bạn) | Thành viên 3 | **Done** |
| Task 2 - Food, Recipe, Diet Rule Catalog & Query Foundation | Thành viên 2 | Thành viên 4 | Not Started |
| Task 3 - Deterministic Meal Planning, Meal Logging & Eating Window | Thành viên 3 | Thành viên 1 | **Done** |
| Task 4 - gRPC Recommendation + Outbox + Redis Streams + Worker | Thành viên 4 | Thành viên 2 | Not Started |

## Dependency chính

- Task 1 đã cung cấp User/Profile/current-user/auth foundation.
- Task 2 cung cấp Food/Recipe/Rule data.
- Task 3 phụ thuộc Task 1 + Task 2.
- Task 4 dùng candidate/business event contract từ Task 2 + Task 3.

## Integration order

1. Freeze shared contracts.
2. Giữ nguyên Task 1 baseline đã nghiệm thu.
3. Merge Task 2 catalog/rules.
4. Merge Task 3 plan/log/eating-window.
5. Merge Task 4 distributed thin slice.
6. Chạy fresh DB migration + full build/test + Docker smoke.

## Week exit criteria

- Auth/Profile regression vẫn pass.
- Catalog CRUD + search/filter/sort/pagination pass.
- Plan -> Log -> Eating Window pass.
- REST -> gRPC pass.
- Outbox -> Redis -> Worker pass.
- dotnet build/test, npm build và Docker Compose smoke pass.
