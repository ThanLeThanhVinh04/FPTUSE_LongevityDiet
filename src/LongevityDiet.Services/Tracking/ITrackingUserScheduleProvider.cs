namespace LongevityDiet.Services.Tracking;

public interface ITrackingUserScheduleProvider
{
    Task<UserScheduleSnapshot?> GetUserScheduleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

