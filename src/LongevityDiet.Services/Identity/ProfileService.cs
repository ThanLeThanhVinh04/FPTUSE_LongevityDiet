using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Repositories;
using LongevityDiet.Services.Reminders;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Services.Identity;

public sealed class ProfileService(
    IUserRepository users,
    TimeProvider timeProvider,
    LongevityDietDbContext? db = null)
{
    public async Task<ServiceResult<ProfileSummary>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);

        return user is null
            ? ServiceResult.Failure<ProfileSummary>(
                "user_not_found",
                "User was not found.")
            : ServiceResult.Success(ToSummary(user));
    }

    public async Task<ServiceResult<ProfileSummary>> UpdateAsync(
        Guid userId,
        UpdateProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure<ProfileSummary>(
                "user_not_found",
                "User was not found.");
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return ServiceResult.Failure<ProfileSummary>(
                "invalid_display_name",
                "Display name is required.");
        }

        var currentYear = timeProvider.GetUtcNow().Year;
        if (command.BirthYear is < 1900 || command.BirthYear > currentYear)
        {
            return ServiceResult.Failure<ProfileSummary>(
                "invalid_birth_year",
                "Birth year is outside the supported range.");
        }

        if (command.PreferredMealFrequency is < 2 or > 4)
        {
            return ServiceResult.Failure<ProfileSummary>(
                "invalid_meal_frequency",
                "Preferred meal frequency must be between 2 and 4.");
        }

        if (!IsValidTimeZone(command.TimeZone))
        {
            return ServiceResult.Failure<ProfileSummary>(
                "invalid_time_zone",
                "Time zone is not recognized.");
        }

        var previousTimeZone = user.Profile?.TimeZone ?? "Asia/Ho_Chi_Minh";
        var now = timeProvider.GetUtcNow();
        user.DisplayName = command.DisplayName.Trim();
        user.UpdatedAt = now;

        user.Profile ??= new UserProfile
        {
            UserId = user.Id,
            User = user,
        };

        user.Profile.BirthYear = command.BirthYear;
        user.Profile.TimeZone = command.TimeZone.Trim();
        user.Profile.WakeTime = command.WakeTime;
        user.Profile.SleepTime = command.SleepTime;
        user.Profile.PreferredMealFrequency = command.PreferredMealFrequency;
        user.Profile.FoodPreference = string.IsNullOrWhiteSpace(command.FoodPreference)
            ? null
            : command.FoodPreference.Trim();
        user.Profile.ProfileCompleted =
            command.BirthYear.HasValue &&
            command.WakeTime.HasValue &&
            command.SleepTime.HasValue &&
            !string.IsNullOrWhiteSpace(command.TimeZone);
        user.Profile.UpdatedAt = now;

        // Replan existing reminders whenever the profile time zone changes.
        // Store the preference version + Outbox event in the SAME IdentityDb
        // transaction as the profile change; no WorkerDb access here.
        if (db is not null && !string.Equals(
            previousTimeZone, user.Profile.TimeZone, StringComparison.Ordinal))
        {
            var reminder = await db.ReminderPreferences.SingleOrDefaultAsync(
                p => p.UserId == userId, cancellationToken);
            if (reminder is not null)
            {
                reminder.Version++;
                reminder.UpdatedAtUtc = now;
                db.IdentityOutbox.Add(ReminderPreferenceEventFactory.Create(
                    userId, user.Profile.TimeZone,
                    user.Status == AccountStatus.Active, reminder, now));
            }
        }

        await users.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(user));
    }

    private static bool IsValidTimeZone(string timeZone)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static ProfileSummary ToSummary(User user)
    {
        var profile = user.Profile ?? new UserProfile
        {
            UserId = user.Id,
            User = user,
            TimeZone = "Asia/Ho_Chi_Minh",
            PreferredMealFrequency = 3,
        };

        return new ProfileSummary(
            user.Id,
            user.Email,
            user.DisplayName,
            profile.BirthYear,
            profile.TimeZone,
            profile.WakeTime,
            profile.SleepTime,
            profile.PreferredMealFrequency,
            profile.FoodPreference,
            profile.ProfileCompleted);
    }
}
