using LongevityDiet.Services.Catalog;
using LongevityDiet.Services.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Errors;

public static class ControllerProblemExtensions
{
    private static readonly Dictionary<string, int> StatusByErrorCode =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["email_exists"] = StatusCodes.Status409Conflict,
            ["weak_password"] = StatusCodes.Status400BadRequest,
            ["invalid_credentials"] = StatusCodes.Status401Unauthorized,
            ["invalid_refresh_token"] = StatusCodes.Status401Unauthorized,
            ["account_disabled"] = StatusCodes.Status403Forbidden,
            ["user_not_found"] = StatusCodes.Status404NotFound,
            ["invalid_display_name"] = StatusCodes.Status400BadRequest,
            ["invalid_birth_year"] = StatusCodes.Status400BadRequest,
            ["invalid_meal_frequency"] = StatusCodes.Status400BadRequest,
            ["invalid_time_zone"] = StatusCodes.Status400BadRequest,
            ["meal_plan_not_found"] = StatusCodes.Status404NotFound,
            ["planned_meal_not_found"] = StatusCodes.Status404NotFound,
            ["meal_log_not_found"] = StatusCodes.Status404NotFound,
            ["no_safe_candidates"] = StatusCodes.Status422UnprocessableEntity,
            ["invalid_plan_days"] = StatusCodes.Status400BadRequest,
            ["invalid_dietary_pattern"] = StatusCodes.Status400BadRequest,
            ["invalid_meal_log"] = StatusCodes.Status400BadRequest,
            ["invalid_meal_type"] = StatusCodes.Status400BadRequest,
            ["invalid_meal_items"] = StatusCodes.Status400BadRequest,
            ["invalid_date_range"] = StatusCodes.Status400BadRequest,
            ["invalid_slot_type"] = StatusCodes.Status400BadRequest,
            ["invalid_candidate_code"] = StatusCodes.Status400BadRequest,
            ["invalid_feedback_type"] = StatusCodes.Status400BadRequest,
            ["invalid_food_token"] = StatusCodes.Status400BadRequest,
            ["invalid_preference_kind"] = StatusCodes.Status400BadRequest,
            ["food_preference_not_found"] = StatusCodes.Status404NotFound,
            ["replacement_candidate_not_found"] = StatusCodes.Status404NotFound,
            ["unsafe_replacement_candidate"] = StatusCodes.Status422UnprocessableEntity,
            ["meal_plan_conflict"] = StatusCodes.Status409Conflict,
            ["recommendation_service_unavailable"] = StatusCodes.Status503ServiceUnavailable,
            ["fmd_education_required"] = StatusCodes.Status400BadRequest,
            ["fmd_assessment_required"] = StatusCodes.Status400BadRequest,
            ["fmd_not_eligible"] = StatusCodes.Status422UnprocessableEntity,
            ["fmd_professional_review_required"] = StatusCodes.Status422UnprocessableEntity,
            ["fmd_active_cycle_exists"] = StatusCodes.Status409Conflict,
            ["fmd_cycle_not_found"] = StatusCodes.Status404NotFound,
            ["fmd_day_not_found"] = StatusCodes.Status404NotFound,
            ["fmd_cycle_not_active"] = StatusCodes.Status400BadRequest,
            ["food_not_found"] = StatusCodes.Status404NotFound,
            ["recipe_not_found"] = StatusCodes.Status404NotFound,
            ["rule_not_found"] = StatusCodes.Status404NotFound,
            ["rule_version_not_found"] = StatusCodes.Status404NotFound,
            ["active_rule_set_not_found"] = StatusCodes.Status404NotFound,
            ["published_rule_immutable"] = StatusCodes.Status409Conflict,
            ["rule_version_conflict"] = StatusCodes.Status409Conflict,
        };

    public static ObjectResult ToProblem<T>(
        this ControllerBase controller,
        ServiceResult<T> result,
        int defaultStatusCode = StatusCodes.Status400BadRequest) =>
        ToProblemCore(controller, result.ErrorCode, result.ErrorMessage, defaultStatusCode);

    public static ObjectResult ToProblem<T>(
        this ControllerBase controller,
        CatalogServiceResult<T> result,
        int defaultStatusCode = StatusCodes.Status400BadRequest) =>
        ToProblemCore(controller, result.ErrorCode, result.ErrorMessage, defaultStatusCode);

    private static ObjectResult ToProblemCore(
        ControllerBase controller,
        string? errorCode,
        string? errorMessage,
        int defaultStatusCode)
    {
        var statusCode = errorCode is not null &&
                         StatusByErrorCode.TryGetValue(errorCode, out var mappedStatus)
            ? mappedStatus
            : defaultStatusCode;

        return controller.Problem(
            statusCode: statusCode,
            title: errorCode ?? "request_failed",
            detail: errorMessage ?? "The request could not be completed.");
    }
}
