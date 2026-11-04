using LongevityDiet.Tracking;

namespace LongevityDiet.UnitTests;

public sealed class LdasCalculatorTests
{
    private static ScoreConfigurationVersion V1() => new()
    {
        Version = "ldas-v1",
        Published = true,
        ActivityWeight = 0.35m,
        MealWeight = 0.45m,
        EatingWindowWeight = 0.20m,
    };

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(100, 100, 100, 100)]
    [InlineData(40, 60, 80, 57)]
    public void FullyObservedScore_ProducesBoundedDeterministicTotal(
        int activity, int meals, int window, int expected)
    {
        var version = V1();
        var first = LdasCalculator.Calculate(version, activity, meals, window);
        var second = LdasCalculator.Calculate(version, activity, meals, window);
        Assert.Equal((decimal)expected, first.Score);
        Assert.False(first.IsPartial);
        Assert.Equal(100m, first.CoveragePercent);
        Assert.Empty(first.MissingData);
        Assert.False(first.IsClinical);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Breakdown.ToArray(), second.Breakdown.ToArray());
        Assert.Equal(3, first.Breakdown.Count);
        Assert.Equal(expected, first.Breakdown.Sum(d => d.WeightedScore));
    }

    [Fact]
    public void MissingMealAndWindow_NeverTreatsUnobservedAsZero()
    {
        var incomplete = LdasCalculator.Calculate(V1(), 50, null, null);
        Assert.Null(incomplete.Score);
        Assert.True(incomplete.IsPartial);
        Assert.Equal(35m, incomplete.CoveragePercent);
        Assert.Equal("meals,eating_window", string.Join(",", incomplete.MissingData));
        Assert.Null(incomplete.Breakdown.Single(x => x.Code == "meals").RawScore);
        Assert.Equal("not_observed",
            incomplete.Breakdown.Single(x => x.Code == "eating_window").ReasonCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidScoreSignal_ThrowsRatherThanSilentlyClamping(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LdasCalculator.Calculate(V1(), value, 40, 55));
    }

    [Fact]
    public void UnpublishedOrInvalidVersion_CannotBeUsedForCalculation()
    {
        var unpublished = V1();
        unpublished.Published = false;
        Assert.Throws<InvalidOperationException>(() =>
            LdasCalculator.Calculate(unpublished, 100, 100, 100));
        var invalid = V1();
        invalid.MealWeight = 0.9m;
        Assert.Throws<InvalidOperationException>(() =>
            LdasCalculator.Calculate(invalid, 100, 100, 100));
    }

    [Fact]
    public void ActivityPercent_RequiresObservedEntriesAndCapsAt100()
    {
        var goal = new ActivityGoal
        {
            WeeklyMinutesTarget = 150,
            WeeklyStrengthSessionsTarget = 0,
        };
        Assert.Null(LdasCalculator.ActivityPercent([], goal));
        var activities = new[]
        {
            new ActivityLog { Kind = ActivityKind.Vigorous, DurationMinutes = 100 },
        };
        Assert.Equal(100m, LdasCalculator.ActivityPercent(activities, goal));
    }

    [Fact]
    public void AsiaHoChiMinhDateBoundary_UsesProfileTimezoneInsteadOfUtc()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var before = new DateTimeOffset(2026, 11, 4, 16, 59, 0, TimeSpan.Zero);
        var after = new DateTimeOffset(2026, 11, 4, 17, 1, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 4), TrackingService.LocalDate(before, zone));
        Assert.Equal(new DateOnly(2026, 11, 5), TrackingService.LocalDate(after, zone));

        var bounds = TrackingService.UtcBounds(
            new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6), zone);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 17, 0, 0, TimeSpan.Zero), bounds.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero), bounds.End);
    }

    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public void DstDateBounds_UseActualLocalDayLength(int year, int month, int day, int expectedHours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var local = new DateOnly(year, month, day);
        var bounds = TrackingService.UtcBounds(local, local.AddDays(1), zone);
        Assert.Equal(expectedHours, (bounds.End - bounds.Start).TotalHours);
    }
}
