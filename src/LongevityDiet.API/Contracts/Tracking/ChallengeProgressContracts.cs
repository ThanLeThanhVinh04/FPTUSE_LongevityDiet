namespace LongevityDiet.API.Contracts.Tracking;

public sealed record StartChallengeRequest(Guid TemplateId, DateOnly? StartDate);
public sealed record SetChallengeDayCompletionRequest(bool Completed);
