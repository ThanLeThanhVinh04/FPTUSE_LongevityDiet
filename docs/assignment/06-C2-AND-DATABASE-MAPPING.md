# C2 Component Architecture & Complete Database Map

**Snapshot:** YAHsaveme/FPTUSE_LongevityDiet `main`, commit `1962735c02b61b69a0697741fab4173362cade33` (2026-10-26).

## Files for diagrams.net / draw.io

- [05-c2-component-architecture.drawio](extended-diagrams/05-c2-component-architecture.drawio) — eight editable C4 **Level 3 / Component** pages: implemented Current REST API and seven explicitly **Target** component boundaries (Gateway, Identity, Catalog, Planning, Tracking, Recommendation, Worker).
- [06-all-database-erd.drawio](extended-diagrams/06-all-database-erd.drawio) — nine editable pages: index, actual SQL database, six target SQL databases, optional Cloudflare D1 design.
- Prior teacher-facing C0, C1, conceptual ERD and 33-table physical data view remain unchanged in diagrams 01-04.

## Distinguish implementation and design

| Area | In current source and Compose | In Target ADR-005 |
| --- | --- | --- |
| Runtime REST | One ASP.NET Core 9 API, `AuthController` and `ProfileController` | YARP Gateway + Identity, Catalog, Planning and Tracking services |
| SQL storage | `LongevityDietDb` with `Users`, `UserProfiles`, `RefreshTokens` — one DbContext | Six **logical** service-owned SQL databases; may share one SQL Server instance |
| Recommendation | Separate gRPC host skeleton | Safety filter, ranking, local read models, service-owned database |
| Worker | Separate Worker process skeleton | Redis Streams consumer groups, idempotency, retries, job execution |
| Messaging | Redis configured in Compose | Outbox per publishing service, Redis Streams, consumer inbox |
| Cloudflare | Optional .NET adapter + Worker prototype built, feature flag OFF; **no live D1 configured** | Cloudflare D1 session-hash store; requires real deployment, migration and E2E |

The C2 Target diagrams are **logical component proposals**, not claims of implemented controllers, repositories, migrations, network ports or APIs.

## Canonical 33 target entity definitions and ownership

| Target SQL database | Documented entity definitions | Count |
| --- | --- | ---: |
| `LongevityIdentityDb` | Users, UserProfiles, RefreshTokens (legacy / until D1 cutover), Allergies, UserAllergies, UserExcludedFoods, Reminders | 7 |
| `LongevityCatalogDb` | Foods, Recipes, RecipeIngredients, RecipeAllergens, DietRules, RuleVersions, RuleSetVersions, RuleSetVersionItems | 8 |
| `LongevityPlanningDb` | MealPlans, MealPlanDays, PlannedMeals, Challenges, ChallengeDays, FmdSafetyAssessments, FmdCycles | 7 |
| `LongevityTrackingDb` | MealLogs, MealLogItems, ActivityLogs, EatingWindowSnapshots, AdherenceScores, ScoreDimensions, WeeklyReports | 7 |
| `LongevityRecommendationDb` | RecommendationFeedback | 1 |
| `LongevityWorkerDb` | ProcessedEvents, OutboxMessages, AuditLogs (operational representation only) | 3 |
| **Distinct logical entity types** | **All 33 documented names covered** | **33** |

**Important nuance:** This is an **ownership proposal** reconciled with the target service responsibility matrix, not an existing database migration. `OutboxMessages` is a *template replicated into every producing service's own DB*, `ProcessedEvents` into each consuming boundary, and domain `AuditLogs` into the relevant domain database. It is incorrect to centralize all publishers' outboxes inside `LongevityWorkerDb`. Therefore physical table *instances* across the six databases can exceed 33 even though the canonical distinct entity-type list is 33.

The target schema for Recommendation snapshots and Worker job state is not yet specified as full EF migrations. The dedicated ERD pages mark `CatalogSnapshots`, `RuleSnapshots`, `RankingRequests`, `WorkerJobs` and `DeadLetterEvents` **[PROPOSED]**, outside the canonical 33 definitions, rather than portraying them as implemented.

## Database integrity rules

1. Only local FKs inside one service-owned SQL database. Cross-service `UserId`, `RecipeId`, `RuleSetVersionId` and `AllergyId` are immutable/stable reference identifiers and must be resolved through APIs or versioned event read models.
2. Preserve unique indexes on `Users.NormalizedEmail`, `RefreshTokens.TokenHash`, `RuleVersions(DietRuleId,VersionNumber)`, `MealPlanDays(MealPlanId,Date)`, plus `ProcessedEvents(EventId,ConsumerName)`.
3. `MealLogItems` requires `FoodId XOR RecipeId`; `UserExcludedFoods` requires `FoodId OR FreeText`.
4. Prefer soft delete for historical Food/Recipe references. Keep audit and published rule versions immutable.
5. Domain messages use transactional Outbox within producer DB. Workers read Redis Streams instead of querying another service DB directly.
6. Cloudflare D1 is an additional optional database **outside** the six target SQL ownership boundaries. Only migrate refresh-token hashes after API changes, credential setup, consistency and replay tests, and a safe rollback plan.

## Visual / structural verification

Run: `powershell -File scripts/Validate-C2DbDiagrams.ps1` from anywhere.

This verifies XML parsing, page counts, valid edge references and canvas bounds. It does **not** assert pixel-perfect typography or that proposed target features already run; open both diagrams in diagrams.net or draw.io Desktop for visual proof. The current four original diagrams remain canonical for assignment C0/C1/conceptual and aggregate physical design.

## Grounding

- `docker-compose.yml` and current `LongevityDietDbContext`, `AuthenticationService`, `AuthController`.
- `docs/05-SYSTEM-ARCHITECTURE.md` / `docs/06-DATABASE-DESIGN.md`.
- `docs/assignment/02-SYSTEM-ARCHITECTURE-C4.md` / `03-DATA-MODEL.md`.
- `docs/task/ARCHITECTURE-ALIGNMENT-MATRIX.md` and ADR-005.
- Cloudflare D1 Worker API / transaction and Workers Secrets docs (see security guide).
