using LongevityDiet.Repositories.Repositories;

namespace LongevityDiet.Services.Tracking;

public sealed class TrackingUserScheduleProvider(IUserRepository userRepository) : ITrackingUserScheduleProvider
{
    public async Task<UserScheduleSnapshot?> GetUserScheduleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var profile = user.Profile;
        var timeZone = string.IsNullOrWhiteSpace(profile?.TimeZone)
            ? "Asia/Ho_Chi_Minh"
            : profile.TimeZone.Trim();

        return new UserScheduleSnapshot(
            ContractVersion: "v1",
            UserId: user.Id,
            TimeZone: timeZone,
            WakeTime: profile?.WakeTime,
            SleepTime: profile?.SleepTime,
            TargetWindowMinutes: 720);
    }
}

