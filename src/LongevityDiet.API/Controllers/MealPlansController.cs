using LongevityDiet.API.Contracts.Planning;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/meal-plans")]
public sealed class MealPlansController(
    MealPlanningService mealPlanningService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("preferences")]
    [ProducesResponseType<DietaryPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DietaryPreferenceResponse>> GetPreferences(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.GetDietaryPreferenceAsync(userId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result, StatusCodes.Status404NotFound);
        }

        return Ok(ToPreferenceResponse(result.Value));
    }

    [HttpPut("preferences")]
    [ProducesResponseType<DietaryPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DietaryPreferenceResponse>> UpdatePreferences(
        UpdateDietaryPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.UpdateDietaryPreferenceAsync(
            userId,
            new UpdateDietaryPreferenceCommand(
                request.DietaryPattern,
                request.Allergies ?? [],
                request.ExcludedFoods ?? [],
                request.PreferredCuisine),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(ToPreferenceResponse(result.Value));
    }

    [HttpGet("candidates")]
    [ProducesResponseType<IReadOnlyList<CandidateMealResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CandidateMealResponse>>> GetCandidates(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.GetActiveCandidatesAsync(cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        var items = result.Value
            .Select(c => new CandidateMealResponse(
                c.CandidateCode,
                c.Title,
                c.Description,
                c.Category,
                c.Cuisine,
                c.SupportedSlots.Select(s => s.ToString()).ToList(),
                c.ServingSize,
                c.ServingUnit,
                c.CaloriesKcal,
                c.ProteinGrams,
                c.CarbsGrams,
                c.FatGrams,
                c.FiberGrams,
                c.IsPlantBased,
                c.HasLegumes,
                c.HasWholeGrains,
                c.ContainsFish,
                c.Allergens,
                c.Ingredients))
            .ToList();

        return Ok(items);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MealPlanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MealPlanResponse>>> ListPlans(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.ListPlansAsync(userId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(result.Value.Select(ToResponse).ToList());
    }

    [HttpGet("current")]
    [ProducesResponseType<MealPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> GetCurrentPlan(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.GetCurrentPlanAsync(userId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result, StatusCodes.Status404NotFound);
        }

        return Ok(ToResponse(result.Value));
    }

    [HttpGet("{planId:guid}")]
    [ProducesResponseType<MealPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> GetPlanById(
        Guid planId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.GetPlanByIdAsync(userId, planId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result, StatusCodes.Status404NotFound);
        }

        return Ok(ToResponse(result.Value));
    }

    [HttpPost]
    [HttpPost("generate")]
    [ProducesResponseType<MealPlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MealPlanResponse>> GeneratePlan(
        GenerateMealPlanRequest? request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var effectiveRequest = request ?? new GenerateMealPlanRequest(null);
        var result = await mealPlanningService.GeneratePlanAsync(
            userId,
            new GenerateMealPlanCommand(
                effectiveRequest.StartDate,
                effectiveRequest.Days,
                effectiveRequest.DietaryPattern,
                effectiveRequest.Allergies,
                effectiveRequest.ExcludedFoods,
                effectiveRequest.PreferredCuisine),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        var response = ToResponse(result.Value);
        return CreatedAtAction(nameof(GetPlanById), new { planId = response.Id }, response);
    }

    [HttpPost("{planId:guid}/regenerate")]
    [ProducesResponseType<MealPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MealPlanResponse>> RegeneratePlan(
        Guid planId,
        RegenerateMealPlanRequest? request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.RegeneratePlanAsync(
            userId,
            planId,
            new RegenerateMealPlanCommand(request?.FromDate),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(ToResponse(result.Value));
    }

    [HttpPost("{planId:guid}/meals/{plannedMealId:guid}/regenerate")]
    [ProducesResponseType<MealPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MealPlanResponse>> RegeneratePlannedMeal(
        Guid planId,
        Guid plannedMealId,
        RegeneratePlannedMealRequest? request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealPlanningService.RegeneratePlannedMealAsync(
            userId,
            planId,
            plannedMealId,
            new RegeneratePlannedMealCommand(request?.AdditionalExcludedCodes),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(ToResponse(result.Value));
    }

    private static DietaryPreferenceResponse ToPreferenceResponse(DietaryPreferenceSummary summary) =>
        new(
            summary.UserId,
            summary.DietaryPattern,
            summary.Allergies,
            summary.ExcludedFoods,
            summary.PreferredCuisine,
            summary.UpdatedAt);

    private static MealPlanResponse ToResponse(MealPlanSummary summary) =>
        new(
            summary.Id,
            summary.UserId,
            summary.RuleSetVersion,
            summary.RevisionNumber,
            summary.PreviousPlanId,
            summary.Status,
            summary.StartDate,
            summary.EndDate,
            summary.MealsPerDay,
            summary.TimeZone,
            summary.DietaryPattern,
            summary.AppliedAllergies,
            summary.AppliedExclusions,
            summary.ReasonCodes,
            summary.PlanExplanation,
            summary.GeneratedAt,
            summary.UpdatedAt,
            summary.Days
                .Select(d => new MealPlanDayResponse(
                    d.Id,
                    d.DayNumber,
                    d.PlanDate,
                    d.TargetCaloriesKcal,
                    d.TotalPlannedCaloriesKcal,
                    d.DailyFocusCode,
                    d.DailyFocusTitle,
                    d.Meals
                        .Select(m => new PlannedMealResponse(
                            m.Id,
                            m.MealPlanDayId,
                            m.SlotOrder,
                            m.SlotType,
                            m.PlannedTime,
                            m.CandidateCode,
                            m.Title,
                            m.Description,
                            m.Cuisine,
                            m.ServingSize,
                            m.ServingUnit,
                            m.CaloriesKcal,
                            m.ProteinGrams,
                            m.CarbsGrams,
                            m.FatGrams,
                            m.FiberGrams,
                            m.IsPlantBased,
                            m.HasLegumes,
                            m.HasWholeGrains,
                            m.ContainsFish,
                            m.Allergens,
                            m.Ingredients,
                            m.DeterministicScore,
                            m.ReasonCodes,
                            m.ExplanationSummary,
                            m.RegenerationCount))
                        .ToList()))
                .ToList());
}

