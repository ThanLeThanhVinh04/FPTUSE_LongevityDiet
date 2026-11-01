namespace LongevityDiet.Services.Planning;

public interface IPlanningInputProvider
{
    Task<PlanningUserContextSnapshot?> GetUserContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PlanningRuleSetSnapshot> GetActiveRuleSetAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningCandidateMealSnapshot>> GetCandidateMealsAsync(
        CancellationToken cancellationToken = default);
}

