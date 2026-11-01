# Task 3 - Deterministic Meal Planning, Meal Logging & Eating-Window Core Loop

**Owner:** Thành viên 3  
**Reviewer:** Thành viên 1  
**Branch:** `feature/w1-t3-plan-log-window`  
**Status:** Done

## Architecture alignment (ADR-005)

- **Target owners:** Planning Service (`:8083`) owns MealPlan/MealPlanDay/PlannedMeal; Tracking & Progress Service (`:8084`) owns MealLog/MealLogItem/EatingWindowSnapshot.
- **Owned data:** `LongevityPlanningDb` and `LongevityTrackingDb`; no cross-service SQL FK/join/transaction.
- **Cross-boundary data:** stable IDs plus API/versioned events/read models.
- **Implementation rule:** split the core loop by target ownership even when one user journey spans both services.


## Mục tiêu
Tạo core product loop: **Profile + Catalog + Rules -> Meal Plan -> Meal Log -> Eating Window**.

## Phạm vi
### Domain & Data
Tạo:
- MealPlan;
- MealPlanDay;
- PlannedMeal;
- MealLog;
- MealLogItem;
- EatingWindowSnapshot;
- user food exclusions/preferences nếu chưa có.

Mọi plan/log phải có UserId ownership rõ ràng.

### Planner v1
Pipeline:
1. Load UserProfile.
2. Load active RuleSetVersion.
3. Load active Food/Recipe candidates.
4. Apply hard constraints: allergy, excluded food, inactive data.
5. Apply preference/rule scoring.
6. Generate minimum 7-day plan.
7. Persist reason codes/basic explanation metadata.

Planner phải deterministic/reproducible. AI không tham gia quyết định.

**Boundary rule:** UserProfile, active RuleSetVersion và Food/Recipe candidates phải đi vào Planning qua versioned service contracts, APIs hoặc event-driven read models/snapshots. Planning Service không được query trực tiếp `LongevityIdentityDb` hoặc `LongevityCatalogDb`.

### REST API
- Generate plan.
- Current/list/detail plan.
- Regenerate planned meal.
- Regenerate plan theo contract rõ.
- MealLog create/update/delete.
- MealLog date-range list.
- Eating-window summary.

### Eating Window
Tính:
- first meal;
- last meal;
- duration;
- last meal -> bedtime gap;
- timezone/local date.

Xử lý midnight, edit/delete log và timezone boundary.

### Frontend
- Current/7-day plan.
- Day navigation.
- Planned meal cards.
- Regenerate action.
- Meal log form/history.
- Eating-window summary.
- Loading/empty/error states.

## Testing
- Allergy hard exclusion.
- User exclusion.
- Ownership.
- Deterministic plan generation.
- Regeneration.
- MealLog CRUD.
- Timezone/midnight.
- Recalculation after edit/delete.

## Deliverables
- Schema + migration.
- Planner service.
- Plan/Log/EatingWindow APIs.
- Planner/log UI.
- Unit + integration tests.

## Definition of Done
Một user đã onboarding generate được plan 7 ngày từ catalog thật, regenerate meal, log bữa ăn và xem eating-window summary; không có candidate vi phạm allergy/exclusion.
