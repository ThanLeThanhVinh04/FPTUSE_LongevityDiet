using System.Text.Json;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;

namespace LongevityDiet.Services.Fmd;

public sealed class FmdService(
    IFmdRepository fmdRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FmdEducationContent GetEducationContent() =>
        FmdSafetyCatalog.DefaultEducation;

    public async Task<ServiceResult<FmdSafetyStatusSummary>> GetSafetyStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure<FmdSafetyStatusSummary>(
                "user_not_found",
                "User was not found.");
        }

        var ack = await fmdRepository.GetLatestAcknowledgementAsync(userId, FmdSafetyCatalog.CurrentEducationVersion, cancellationToken);
        if (ack is null)
        {
            return ServiceResult.Success(new FmdSafetyStatusSummary(
                CurrentStatus: FmdSafetyStatus.EducationNotViewed,
                StatusTitle: "Chưa đọc tài liệu an toàn FMD",
                StatusMessage: "Bạn cần đọc hướng dẫn khoa học và giới hạn an toàn FMD trước khi bắt đầu.",
                IsEligibleForTracking: false,
                RequiresProfessionalReview: false,
                LastAcknowledgedAt: null,
                LastEvaluatedAt: null,
                TriggeredRiskCodes: [],
                Notes: null));
        }

        var assessment = await fmdRepository.GetLatestAssessmentAsync(userId, cancellationToken);
        if (assessment is null)
        {
            return ServiceResult.Success(new FmdSafetyStatusSummary(
                CurrentStatus: FmdSafetyStatus.AssessmentRequired,
                StatusTitle: "Cần thực hiện sàng lọc an toàn",
                StatusMessage: "Bạn đã xem tài liệu giáo dục. Vui lòng hoàn thành bảng câu hỏi kiểm tra chống chỉ định y khoa.",
                IsEligibleForTracking: false,
                RequiresProfessionalReview: false,
                LastAcknowledgedAt: ack.AcknowledgedAt,
                LastEvaluatedAt: null,
                TriggeredRiskCodes: [],
                Notes: null));
        }

        var riskCodes = SplitCsv(assessment.TriggeredRiskCodesCsv);
        var isEligible = assessment.ResultStatus == FmdSafetyStatus.EligibleForTracking;
        var requiresReview = assessment.ResultStatus == FmdSafetyStatus.ProfessionalReviewRequired;

        var title = isEligible
            ? "Đủ điều kiện theo dõi chu kỳ"
            : "Yêu cầu có ý kiến bác sĩ (Bị chặn)";

        return ServiceResult.Success(new FmdSafetyStatusSummary(
            CurrentStatus: assessment.ResultStatus,
            StatusTitle: title,
            StatusMessage: assessment.RecommendationsSummary,
            IsEligibleForTracking: isEligible,
            RequiresProfessionalReview: requiresReview,
            LastAcknowledgedAt: ack.AcknowledgedAt,
            LastEvaluatedAt: assessment.EvaluatedAt,
            TriggeredRiskCodes: riskCodes,
            Notes: assessment.Notes));
    }

    public async Task<ServiceResult<FmdSafetyStatusSummary>> AcknowledgeEducationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure<FmdSafetyStatusSummary>(
                "user_not_found",
                "User was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var ack = new FmdEducationAcknowledgement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EducationVersion = FmdSafetyCatalog.CurrentEducationVersion,
            AcknowledgedAt = now,
            SourceCitation = FmdSafetyCatalog.DefaultEducation.Citations[0].Title,
            DisclaimerAccepted = true,
        };

        await fmdRepository.AddAcknowledgementAsync(ack, cancellationToken);
        await fmdRepository.SaveChangesAsync(cancellationToken);

        return await GetSafetyStatusAsync(userId, cancellationToken);
    }

    public async Task<ServiceResult<FmdSafetyStatusSummary>> SubmitAssessmentAsync(
        Guid userId,
        SubmitFmdAssessmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure<FmdSafetyStatusSummary>(
                "user_not_found",
                "User was not found.");
        }

        // Must acknowledge education first before submitting assessment
        var ack = await fmdRepository.GetLatestAcknowledgementAsync(userId, FmdSafetyCatalog.CurrentEducationVersion, cancellationToken);
        if (ack is null)
        {
            return ServiceResult.Failure<FmdSafetyStatusSummary>(
                "fmd_education_required",
                "Bạn cần xác nhận đã đọc tài liệu hướng dẫn an toàn FMD trước khi nộp bảng kiểm tra y tế.");
        }

        var answers = command.QuestionAnswers ?? new Dictionary<string, bool>();
        var (status, triggeredCodes, message) = FmdSafetyCatalog.EvaluateScreening(answers);
        var now = timeProvider.GetUtcNow();

        var assessment = new FmdSafetyAssessment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AssessmentVersion = FmdSafetyCatalog.CurrentSafetyVersion,
            EvaluatedAt = now,
            ResultStatus = status,
            HasHighRiskFlag = status == FmdSafetyStatus.ProfessionalReviewRequired,
            TriggeredRiskCodesCsv = string.Join(',', triggeredCodes),
            RecommendationsSummary = message,
            AnswersJson = JsonSerializer.Serialize(answers, JsonOptions),
            Notes = command.Notes?.Trim(),
        };

        await fmdRepository.AddAssessmentAsync(assessment, cancellationToken);
        await fmdRepository.SaveChangesAsync(cancellationToken);

        return await GetSafetyStatusAsync(userId, cancellationToken);
    }

    public async Task<ServiceResult<FmdCycleSummary>> StartCycleAsync(
        Guid userId,
        StartFmdCycleCommand command,
        CancellationToken cancellationToken = default)
    {
        var safetyStatusResult = await GetSafetyStatusAsync(userId, cancellationToken);
        if (!safetyStatusResult.Succeeded || safetyStatusResult.Value is null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                safetyStatusResult.ErrorCode ?? "fmd_safety_error",
                safetyStatusResult.ErrorMessage ?? "Could not resolve FMD safety status.");
        }

        var status = safetyStatusResult.Value;
        if (!status.IsEligibleForTracking)
        {
            if (status.RequiresProfessionalReview)
            {
                return ServiceResult.Failure<FmdCycleSummary>(
                    "fmd_professional_review_required",
                    $"Backend đã chặn bắt đầu chu kỳ: Bạn có yếu tố rủi ro cao ({string.Join(", ", status.TriggeredRiskCodes)}). Bắt buộc phải có ý kiến bác sĩ chuyên khoa.");
            }

            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_assessment_required",
                "Bạn cần hoàn thành bảng sàng lọc an toàn FMD và đạt chuẩn trước khi bắt đầu chu kỳ.");
        }

        var active = await fmdRepository.GetActiveCycleAsync(userId, cancellationToken);
        if (active is not null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_active_cycle_exists",
                "Bạn đang có một chu kỳ FMD đang diễn ra. Vui lòng hoàn thành hoặc dừng chu kỳ hiện tại trước khi tạo chu kỳ mới.");
        }

        var allCycles = await fmdRepository.ListCyclesByUserAsync(userId, 100, cancellationToken);
        var cycleNumber = allCycles.Count + 1;
        var now = timeProvider.GetUtcNow();
        var startDate = command.StartDate ?? DateOnly.FromDateTime(now.DateTime);

        var cycle = new FmdCycle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CycleNumber = cycleNumber,
            StartDate = startDate,
            EndDate = startDate.AddDays(4),
            TargetDays = 5,
            Status = FmdCycleStatus.Active,
            Notes = command.Notes?.Trim(),
            StartedAt = now,
        };

        for (var i = 1; i <= 5; i++)
        {
            cycle.Days.Add(new FmdCycleDay
            {
                Id = Guid.NewGuid(),
                FmdCycleId = cycle.Id,
                DayNumber = i,
                PlanDate = startDate.AddDays(i - 1),
                TargetCaloriesKcal = FmdSafetyCatalog.ResolveTargetCaloriesForDay(i),
                IsCompleted = false,
            });
        }

        await fmdRepository.AddCycleAsync(cycle, cancellationToken);
        await fmdRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(ToSummary(cycle));
    }

    public async Task<ServiceResult<FmdCycleSummary>> GetActiveCycleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var active = await fmdRepository.GetActiveCycleAsync(userId, cancellationToken);
        if (active is null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_found",
                "Không có chu kỳ FMD nào đang diễn ra.");
        }

        return ServiceResult.Success(ToSummary(active));
    }

    public async Task<ServiceResult<FmdCycleSummary>> LogCycleDayAsync(
        Guid userId,
        Guid cycleId,
        LogFmdCycleDayCommand command,
        CancellationToken cancellationToken = default)
    {
        var cycle = await fmdRepository.GetCycleByIdAsync(cycleId, userId, cancellationToken);
        if (cycle is null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_found",
                "Chu kỳ FMD không tồn tại hoặc không thuộc về người dùng hiện tại.");
        }

        if (cycle.Status != FmdCycleStatus.Active)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_active",
                "Chỉ có thể ghi nhận nhật ký cho chu kỳ đang hoạt động.");
        }

        var day = cycle.Days.FirstOrDefault(d => d.DayNumber == command.DayNumber);
        if (day is null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_day_not_found",
                $"Ngày thứ {command.DayNumber} không hợp lệ trong chu kỳ 5 ngày.");
        }

        var now = timeProvider.GetUtcNow();
        day.EnergyLevel = command.EnergyLevel.HasValue ? Math.Clamp(command.EnergyLevel.Value, 1, 5) : null;
        day.HungerLevel = command.HungerLevel.HasValue ? Math.Clamp(command.HungerLevel.Value, 1, 5) : null;
        day.WaterIntakeLiters = command.WaterIntakeLiters.HasValue ? Math.Clamp(command.WaterIntakeLiters.Value, 0m, 10m) : null;
        day.Symptoms = string.IsNullOrWhiteSpace(command.Symptoms) ? null : command.Symptoms.Trim();
        day.DailyNotes = string.IsNullOrWhiteSpace(command.DailyNotes) ? null : command.DailyNotes.Trim();
        day.IsCompleted = true;
        day.LoggedAt = now;

        // Auto-complete cycle if all 5 days are logged
        if (cycle.Days.All(d => d.IsCompleted))
        {
            cycle.Status = FmdCycleStatus.Completed;
            cycle.CompletedAt = now;
        }

        await fmdRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(cycle));
    }

    public async Task<ServiceResult<FmdCycleSummary>> StopCycleAsync(
        Guid userId,
        Guid cycleId,
        StopFmdCycleCommand command,
        CancellationToken cancellationToken = default)
    {
        var cycle = await fmdRepository.GetCycleByIdAsync(cycleId, userId, cancellationToken);
        if (cycle is null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_found",
                "Chu kỳ FMD không tồn tại hoặc không thuộc về người dùng hiện tại.");
        }

        if (cycle.Status != FmdCycleStatus.Active)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_active",
                "Chu kỳ này đã kết thúc trước đó.");
        }

        var now = timeProvider.GetUtcNow();
        cycle.Status = FmdCycleStatus.StoppedByUser;
        cycle.StoppedAt = now;
        cycle.StopReason = string.IsNullOrWhiteSpace(command.StopReason)
            ? "Người dùng chủ động dừng chu kỳ vì lý do cá nhân / an toàn"
            : command.StopReason.Trim();

        await fmdRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(cycle));
    }

    public async Task<ServiceResult<FmdCycleSummary>> CompleteCycleAsync(
        Guid userId,
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        var cycle = await fmdRepository.GetCycleByIdAsync(cycleId, userId, cancellationToken);
        if (cycle is null)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_found",
                "Chu kỳ FMD không tồn tại hoặc không thuộc về người dùng hiện tại.");
        }

        if (cycle.Status != FmdCycleStatus.Active)
        {
            return ServiceResult.Failure<FmdCycleSummary>(
                "fmd_cycle_not_active",
                "Chu kỳ này đã kết thúc trước đó.");
        }

        var now = timeProvider.GetUtcNow();
        cycle.Status = FmdCycleStatus.Completed;
        cycle.CompletedAt = now;

        await fmdRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(cycle));
    }

    public async Task<ServiceResult<IReadOnlyList<FmdCycleSummary>>> ListCyclesAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var cycles = await fmdRepository.ListCyclesByUserAsync(userId, limit, cancellationToken);
        var summaries = cycles.Select(ToSummary).ToList();
        return ServiceResult.Success<IReadOnlyList<FmdCycleSummary>>(summaries);
    }

    private static FmdCycleSummary ToSummary(FmdCycle cycle)
    {
        var days = cycle.Days
            .OrderBy(d => d.DayNumber)
            .Select(d => new FmdCycleDaySummary(
                d.Id,
                d.DayNumber,
                d.PlanDate,
                d.TargetCaloriesKcal,
                d.EnergyLevel,
                d.HungerLevel,
                d.WaterIntakeLiters,
                d.Symptoms,
                d.DailyNotes,
                d.IsCompleted,
                d.LoggedAt))
            .ToList();

        return new FmdCycleSummary(
            cycle.Id,
            cycle.UserId,
            cycle.CycleNumber,
            cycle.StartDate,
            cycle.EndDate,
            cycle.TargetDays,
            cycle.Status.ToString(),
            cycle.Notes,
            cycle.StartedAt,
            cycle.CompletedAt,
            cycle.StoppedAt,
            cycle.StopReason,
            days);
    }

    private static List<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

