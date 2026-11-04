using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Tracking;

namespace LongevityDiet.API.Security;

/// <summary>
/// Transitional current-runtime adapter. Tracking sees only the time-zone
/// contract, not the Identity DbContext. Target: versioned Identity API/events.
/// </summary>
public sealed class IdentityTrackingUserContext(IUserRepository users) : ITrackingUserContext
{
    public async Task<string?> GetActiveUserTimeZoneAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        return user is { Status: AccountStatus.Active }
            ? user.Profile?.TimeZone ?? "Asia/Ho_Chi_Minh"
            : null;
    }
}
