using LongevityDiet.API.Contracts.Recommendation;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Planning;
using LongevityDiet.Services.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/recommendations")]
public sealed class RecommendationsController(
    RecommendationService recommendationService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecommendations(
        [FromQuery] string? slotType,
        [FromQuery] Guid? mealPlanId,
        [FromQuery] Guid? plannedMealId,
        [FromQuery] string? excludedCodes,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var extraExcluded = string.IsNullOrWhiteSpace(excludedCodes)
            ? null
            : PlanningCatalogSnapshotProvider.SplitTokens(excludedCodes);

        var result = await recommendationService.GetRecommendationsAsync(
            userId,
            new GetMealRecommendationsQuery(slotType, mealPlanId, plannedMealId, extraExcluded),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("feedback")]
    public async Task<IActionResult> SubmitFeedback(
        [FromBody] SubmitRecommendationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.SubmitFeedbackAsync(
            userId,
            new SubmitRecommendationFeedbackCommand(
                request.CandidateCode,
                request.FeedbackType,
                request.ReasonCode,
                request.Notes,
                request.PlannedMealId),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> ListFeedback(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.ListFeedbackAsync(userId, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> ListFoodPreferences(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.ListFoodPreferencesAsync(userId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> UpsertFoodPreference(
        [FromBody] UpsertFoodPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.UpsertFoodPreferenceAsync(
            userId,
            new UpsertFoodPreferenceCommand(
                request.FoodToken,
                request.PreferenceKind,
                request.DisplayLabel,
                request.Notes),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpDelete("preferences/{foodToken}")]
    public async Task<IActionResult> RemoveFoodPreference(
        [FromRoute] string foodToken,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.RemoveFoodPreferenceAsync(userId, foodToken, cancellationToken);
        return result.Succeeded ? NoContent() : this.ToProblem(result);
    }

    [HttpPost("replacements")]
    public async Task<IActionResult> ReplacePlannedMeal(
        [FromBody] ReplacePlannedMealRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.ReplacePlannedMealAsync(
            userId,
            new ReplacePlannedMealCommand(
                request.MealPlanId,
                request.PlannedMealId,
                request.TargetCandidateCode,
                request.ReplacementReasonCode,
                request.ReasonNotes,
                request.AdditionalExcludedCodes),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("replacements")]
    public async Task<IActionResult> ListReplacements(
        [FromQuery] Guid? mealPlanId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await recommendationService.ListReplacementHistoryAsync(userId, mealPlanId, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }
}
