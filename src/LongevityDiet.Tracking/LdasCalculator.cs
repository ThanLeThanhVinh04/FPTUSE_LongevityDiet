using System.Text.Json;

namespace LongevityDiet.Tracking;

public sealed record DimensionValue(string Code, decimal Weight, decimal? RawScore, decimal? WeightedScore, string ReasonCode);
public sealed record ScoreOutcome(
    decimal? Score,
    decimal CoveragePercent,
    bool IsPartial,
    IReadOnlyList<string> MissingData,
    IReadOnlyList<DimensionValue> Breakdown,
    bool IsClinical = false,
    string Disclaimer = "LDAS is an educational habit indicator, not a diagnosis or longevity prediction.");

/// <summary>
/// Versioned deterministic educational score; missing observations are NEVER
/// interpreted as failed adherence. Score remains NULL until every dimension
/// has an independently sourced observation.
/// </summary>
public static class LdasCalculator
{
    public static ScoreOutcome Calculate(
        ScoreConfigurationVersion config,
        decimal? activityPercent,
        decimal? mealPercent,
        decimal? eatingWindowPercent)
    {
        if (!config.Published)
            throw new InvalidOperationException("Only published scoring configurations may be used.");

        var weights = new[] { config.ActivityWeight, config.MealWeight, config.EatingWindowWeight };
        if (weights.Any(w => w < 0m || w > 1m) || weights.Sum() != 1m)
            throw new InvalidOperationException("Published score weights must sum to one.");

        var input = new (string Code, decimal Weight, decimal? Value)[]
        {
            ("activity", config.ActivityWeight, activityPercent),
            ("meals", config.MealWeight, mealPercent),
            ("eating_window", config.EatingWindowWeight, eatingWindowPercent),
        };

        var missing = new List<string>();
        var dimensions = new List<DimensionValue>();
        decimal coverage = 0;
        decimal total = 0;

        foreach (var (code, weight, raw) in input)
        {
            if (raw is null)
            {
                missing.Add(code);
                dimensions.Add(new DimensionValue(code, weight, null, null, "not_observed"));
                continue;
            }

            if (raw < 0m || raw > 100m)
                throw new ArgumentOutOfRangeException(nameof(activityPercent), "All score signals must lie between 0 and 100.");

            var weighted = Math.Round(raw.Value * weight, 2, MidpointRounding.AwayFromZero);
            coverage += weight;
            total += weighted;
            dimensions.Add(new DimensionValue(code, weight, raw, weighted, "measured"));
        }

        var complete = missing.Count == 0;
        return new ScoreOutcome(
            complete ? Math.Clamp(Math.Round(total, 2, MidpointRounding.AwayFromZero), 0, 100) : null,
            Math.Round(coverage * 100, 2),
            !complete,
            missing,
            dimensions);
    }

    public static decimal? ActivityPercent(
        IReadOnlyList<ActivityLog> dailyActivities,
        ActivityGoal goal)
    {
        if (dailyActivities.Count == 0)
            return null; // Not observed != no exercise.

        var equivalentMinutes = dailyActivities.Sum(a =>
            a.DurationMinutes * (a.Kind == ActivityKind.Vigorous ? 2 : 1));
        var dailyGoal = Math.Max(1m, goal.WeeklyMinutesTarget / 7m);
        var progress = Math.Min(1m, equivalentMinutes / dailyGoal);
        var hasStrength = dailyActivities.Any(a => a.Kind == ActivityKind.Strength);
        var strengthProgress = goal.WeeklyStrengthSessionsTarget == 0
            ? 1m : hasStrength ? 1m : 0m;

        return Math.Round((progress * 0.85m + strengthProgress * 0.15m) * 100m,
            2, MidpointRounding.AwayFromZero);
    }

    public static string MissingAsJson(ScoreOutcome score) =>
        JsonSerializer.Serialize(score.MissingData);
}
