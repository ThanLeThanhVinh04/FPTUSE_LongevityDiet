# Task 3 - Release Regression, PRN232 Evidence & Demo Rehearsal

**Owner:** Thành viên 3
**Reviewer:** Thành viên 1
**Branch:** `feature/w4-t3-regression-evidence-demo`
**Status:** Done

## Architecture alignment (ADR-005)

- **Evidence chain:** C0 -> C1 Target -> Current/Cloud Deployment -> Recommendation/Outbox Dynamic views.
- **Protocol evidence:** HTTPS :443, REST :8081-:8084, gRPC/HTTP2 :8085, Ops :8086, TDS :1433, Redis Streams :6379.
- **Truth rule:** never claim a target container/database is implemented without runtime evidence.


## Mục tiêu

Chứng minh release candidate đúng bằng automated regression, rubric evidence có trọng số và một final demo repeatable đúng 7 mục PDF.

## Hoàn thành Deliverables Week 4 Task 3

1. **Automated Release Regression Suite**:
   - File: `tests/LongevityDiet.IntegrationTests/ReleaseRegressionSuiteTests.cs` (18/18 Integration Tests PASS).
   - Bao quát toàn bộ hành trình end-to-end: Register -> JWT Profile -> 7-Day Plan -> gRPC Recommendation v2 & Replacement -> Meal Log & Eating Window 12h -> FMD Safety Gate & Cycle Tracking -> Cross-tenant Security.
   - Script tự động hóa kiểm thử: `scripts/Run-ReleaseRegression.ps1`.

2. **100% PRN232 Assessment Matrix & Evidence Traceability**:
   - Ánh xạ chi tiết toàn bộ 7 tiêu chí Rubric trong `docs/10-PRN232-TRACEABILITY.md`.

3. **Final Demo Checklist (7/7 mục PDF) & Controlled Failure Guide**:
   - File kịch bản diễn tập thuyết trình chi tiết: `docs/FINAL-DEMO-SCRIPT-7-STEPS.md`.
   - Bao gồm kịch bản controlled failure (giả lập tắt gRPC hoặc Redis và chứng minh tính năng phục hồi an toàn).

## Release regression

Test matrix tối thiểu:
1. Register/Login/Profile.
2. Catalog/rule CRUD + REST status semantics.
3. Search/filter/sort/pagination.
4. Generate plan + meal log/eating window.
5. Activity + LDAS + challenge + dashboard.
6. Web -> API Gateway -> Planning -> Recommendation `gRPC/HTTP2 :8085` + replacement.
7. Owning-service Outbox -> `XADD` Redis Streams -> Worker `XREADGROUP/XACK` -> result event/owning-service persistence.
8. Reminder/notification/weekly report.
9. FMD safety block.
10. Admin rule governance/audit.
11. Docker health/restart/communication.

Rules:
- deterministic fixtures;
- không phụ thuộc manual DB state;
- flaky/order-dependent test = defect;
- async test có timeout/diagnostic rõ;
- targeted real SQL/Redis/gRPC integration khi provider behavior cần được chứng minh.

## PRN232 assessment matrix — 100%

| Criterion | Weight | Evidence phải chuẩn bị |
|---|---:|---|
| System architecture and design | 20% | C0/C1/deployment + ADR + dependency boundaries |
| REST API implementation | 20% | CRUD + layered architecture + JWT + REST semantics + query features + Swagger |
| Background Job | 10% | Worker scheduled/async business job + persisted result |
| Message Broker integration | 15% | Redis producer/consumer + ACK/idempotency/recovery |
| gRPC service | 15% | independent Recommendation gRPC + Planning -> gRPC/HTTP2 :8085 trace/result |
| Docker/Cloud deployment | 10% | Current Local + Cloud Docker Target views, target Compose runtime, health, service DNS, private ports and volume persistence |
| Documentation and presentation | 10% | README/docs/evidence + rehearsed presentation |
| **Total** | **100%** | không được thiếu mandatory evidence |

Mỗi rubric row phải map tới:
- code location;
- endpoint/service;
- diagram/doc;
- automated test;
- runtime command;
- demo step.

Không claim feature chưa implement; file path/port/service name phải tồn tại và đúng.

## Final Demo Checklist — 7/7 theo PDF

1. **System architecture**.
2. **REST API functionality**.
3. **Background job execution**.
4. **Message publishing and consuming**.
5. **gRPC communication**.
6. **Docker or cloud deployment**.
7. **End-to-end business workflow**.

Một demo flow nên chứng minh nhiều rubric item liên tục thay vì nhảy qua các màn hình rời rạc.

## Demo rehearsal

- Architecture segment đi theo thứ tự: **C0 -> C1 Target -> Current Local / Cloud Docker Deployment -> Outbox Redis Dynamic**, không nhảy lẫn abstraction level.
- Khi trình bày Cloud Docker, chỉ rõ `ldc-network`, Docker service DNS, `sql-data -> /var/opt/mssql`, `redis-data -> /data (AOF)` và Persistent Disk.
- Synthetic stable demo data.
- Main flow time-boxed.
- Người điều khiển/nói từng segment rõ.
- Chuẩn bị một controlled failure: gRPC hoặc Redis unavailable -> graceful state -> recovery.
- Command/browser/log view verify trước.
- Chạy full demo ít nhất hai lần liên tiếp không sửa data thủ công.
- Backup screenshots/logs chỉ là fallback, không thay runtime evidence.

## Definition of Done

Regression suite repeatable trên clean test environment; 100% rubric matrix có real evidence; demo 7/7 được chạy hai lần liên tiếp và controlled failure-recovery hoạt động như mô tả.
