using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Seed;

public static class ChallengeSeed
{
    public static readonly Guid FoundationTemplateId = Guid.Parse("b2e80cd7-284e-40ac-b11e-f9cb4b242001");

    public static async Task SeedAsync(LongevityDietDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.ChallengeTemplates.AnyAsync(x => x.Code == "LONGEVITY_14", cancellationToken))
            return;

        var template = new ChallengeTemplate
        {
            Id = FoundationTemplateId,
            Code = "LONGEVITY_14",
            Title = "14 ngày xây thói quen trường thọ",
            Description = "Chuỗi 14 ngày tập trung vào ghi nhận bữa ăn, khung giờ ăn và tính nhất quán của thói quen.",
            DurationDays = 14,
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var guidance = new[]
        {
            ("Bắt đầu bằng việc ghi nhận", "Ghi lại đầy đủ các bữa ăn trong ngày."),
            ("Giữ nhịp đều", "Tiếp tục ghi nhận bữa ăn và kiểm tra khung giờ ăn."),
            ("Ưu tiên thực vật", "Chọn thêm thực phẩm có nguồn gốc thực vật khi phù hợp."),
            ("Quan sát khung giờ ăn", "Xem thời điểm bữa đầu và bữa cuối trong ngày."),
            ("Tăng tính nhất quán", "Duy trì việc ghi nhận thay vì cố hoàn hảo."),
            ("Nhìn lại nửa tuần", "Xem lại các ngày đã hoàn thành và điều chỉnh nhịp sinh hoạt."),
            ("Hoàn thành tuần đầu", "Giữ lịch ăn phù hợp với lịch ngủ cá nhân."),
            ("Khởi động tuần hai", "Tiếp tục thói quen đã duy trì trong tuần đầu."),
            ("Chú ý chất lượng bữa ăn", "Ưu tiên lựa chọn phù hợp mục tiêu dinh dưỡng cá nhân."),
            ("Giữ khung giờ ổn định", "Hạn chế thay đổi giờ ăn quá lớn giữa các ngày."),
            ("Theo dõi thay vì phán xét", "Dùng dữ liệu để quan sát xu hướng, không tự chẩn đoán."),
            ("Củng cố thói quen", "Hoàn thành ghi nhận ngay trong ngày để tránh bỏ sót."),
            ("Chuẩn bị về đích", "Xem lại chuỗi ngày liên tiếp và các ngày còn thiếu."),
            ("Tổng kết 14 ngày", "Hoàn tất ngày cuối và xem lại tiến độ tổng thể."),
        };

        for (var index = 0; index < guidance.Length; index++)
        {
            template.Days.Add(new ChallengeDayTemplate
            {
                Id = Guid.Parse($"b2e80cd7-284e-40ac-b11e-f9cb4b242{index + 101:000}"),
                ChallengeTemplateId = template.Id,
                DayNumber = index + 1,
                Title = guidance[index].Item1,
                Guidance = guidance[index].Item2,
            });
        }

        dbContext.ChallengeTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
