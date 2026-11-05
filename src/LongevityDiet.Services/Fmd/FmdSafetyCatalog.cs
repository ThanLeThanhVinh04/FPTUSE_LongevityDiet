using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Services.Fmd;

public static class FmdSafetyCatalog
{
    public const string CurrentEducationVersion = "FMD-EDU-2026.1";
    public const string CurrentSafetyVersion = "FMD-SAFETY-2026.1";

    public static FmdEducationContent DefaultEducation { get; } = new(
        Version: CurrentEducationVersion,
        Title: "Chương trình Giả nhịn ăn (FMD - Fasting Mimicking Diet) & Giới hạn An toàn",
        Summary: "FMD là quy trình dinh dưỡng mô phỏng trạng thái nhịn ăn trong 5 ngày liên tục do GS. Valter Longo và cộng sự phát triển, nhằm kích hoạt quá trình tái tạo tế bào (autophagy) và cân bằng trao đổi chất mà vẫn cung cấp vi chất thiết yếu.",
        ScopeDisclaimer: "LƯU Ý QUAN TRỌNG: Ứng dụng này chỉ cung cấp kiến thức giáo dục và công cụ nhật ký hỗ trợ ghi nhận cá nhân; KHÔNG PHẢI là thiết bị y tế, KHÔNG đưa ra phác đồ điều trị bệnh hay đơn thuốc nhịn ăn. Bất kỳ người dùng nào có bệnh lý nền bắt buộc phải có chỉ định và giám sát trực tiếp từ bác sĩ chuyên khoa.",
        KeyPrinciples:
        [
            "Chu kỳ 5 ngày có kiểm soát năng lượng và thành phần thực vật chủ đạo.",
            "Ngày 1: Khoảng 1100 kcal (chất béo lành mạnh từ quả hạch/dầu ô-liu và tinh bột phức hợp, lượng đạm thấp).",
            "Ngày 2 đến Ngày 5: Khoảng 750 kcal/ngày, tối ưu hóa trạng thái tế bào.",
            "Uống đủ 2 - 2.5 lít nước lọc mỗi ngày và tránh vận động cường độ cao.",
            "Người dùng có thể DỪNG CHU KỲ BẤT KỲ LÚC NÀO nếu cảm thấy hoa mắt, chóng mặt hoặc không thoải mái."
        ],
        AbsoluteContraindications:
        [
            "Phụ nữ mang thai hoặc đang trong thời kỳ cho con bú.",
            "Trẻ em và thanh thiếu niên dưới 18 tuổi.",
            "Người nhẹ cân, chỉ số khối cơ thể BMI < 18.5 kg/m².",
            "Người trên 70 tuổi có dấu hiệu suy nhược thể lực hoặc nguy cơ teo cơ.",
            "Người có tiền sử rối loạn hành vi ăn uống (biếng ăn tâm thần, ăn ói...).",
            "Bệnh nhân đái tháo đường đang sử dụng insulin hoặc thuốc hạ đường huyết (nguy cơ hạ đường huyết nguy hiểm tính mạng).",
            "Bệnh nhân đang điều trị ung thư mà không có sự đồng thuận và theo dõi trực tiếp từ bác sĩ chuyên khoa ung bướu.",
            "Bệnh nhân suy tim nặng, suy gan, suy thận mạn tính hoặc nhiễm trùng cấp tính."
        ],
        Citations:
        [
            new(
                "Valter Longo Foundation - Adult Longevity Diet & FMD Safety",
                "OfficialFoundation",
                "https://www.fondazionevalterlongo.org/en/diabetes-obesity/",
                "Hướng dẫn chính thức về giới hạn an toàn và đối tượng chống chỉ định FMD."),
            new(
                "Fasting-mimicking diet and markers/risk factors for aging, diabetes, cancer, and CVD (2017)",
                "PeerReviewedTrial",
                "https://pubmed.ncbi.nlm.nih.gov/28202779/",
                "Nghiên cứu lâm sàng ngẫu nhiên có đối chứng về chu kỳ FMD 5 ngày trên người."),
            new(
                "Fasting-mimicking diet cycles and biological aging markers (2024)",
                "PeerReviewedTrial",
                "https://pubmed.ncbi.nlm.nih.gov/38378685/",
                "Phân tích về chu kỳ định kỳ và chỉ số chuyển hóa trong quần thể thử nghiệm.")
        ]);

    public static IReadOnlyList<FmdSafetyQuestionDto> ScreeningQuestions { get; } =
    [
        new(
            "PREGNANT_OR_BREASTFEEDING",
            "Sinh lý & Thai kỳ",
            "Bạn hiện có đang mang thai, dự định có thai trong tháng này hoặc đang cho con bú không?",
            "Thai kỳ và cho con bú đòi hỏi nguồn năng lượng và dưỡng chất liên tục cho thai nhi/trẻ nhỏ.",
            IsHighRiskIfTrue: true),
        new(
            "AGE_OUT_OF_BOUNDS",
            "Độ tuổi & Thể trạng",
            "Bạn có dưới 18 tuổi, hoặc trên 70 tuổi kèm dấu hiệu suy nhược thể lực/suy giảm khối cơ không?",
            "Trẻ vị thành niên cần dinh dưỡng để phát triển; người cao tuổi suy nhược đối mặt nguy cơ mất cơ nghiêm trọng.",
            IsHighRiskIfTrue: true),
        new(
            "UNDERWEIGHT_BMI",
            "Chỉ số thể trọng",
            "Chỉ số BMI của bạn hiện có dưới 18.5 kg/m² hoặc bạn đang bị sụt cân ngoài ý muốn gần đây không?",
            "FMD gây thâm hụt calo và chống chỉ định tuyệt đối với người nhẹ cân hoặc suy dinh dưỡng.",
            IsHighRiskIfTrue: true),
        new(
            "EATING_DISORDER_HISTORY",
            "Tâm lý dinh dưỡng",
            "Bạn có từng có tiền sử hoặc đang được điều trị rối loạn ăn uống (chán ăn tâm thần, cuồng ăn...) không?",
            "Các chế độ hạn chế calo nghiêm ngặt có thể kích hoạt tái phát rối loạn ăn uống tâm lý.",
            IsHighRiskIfTrue: true),
        new(
            "DIABETES_MEDICATION",
            "Thuốc & Đường huyết",
            "Bạn có đang dùng Insulin, Sulfonylurea hoặc bất kỳ thuốc hạ đường huyết nào do bác sĩ kê đơn không?",
            "Nhịn ăn kết hợp thuốc hạ đường huyết có nguy cơ dẫn đến sốc hạ đường huyết cấp tính đe dọa tính mạng.",
            IsHighRiskIfTrue: true),
        new(
            "ACTIVE_CHEMOTHERAPY_OR_CANCER",
            "Bệnh lý mạn tính",
            "Bạn có đang trong quá trình điều trị ung thư, hóa trị hoặc xạ trị mà chưa có chỉ định văn bản từ bác sĩ ung bướu không?",
            "Bệnh nhân ung thư chỉ được can thiệp FMD khi có sự giám sát y tế chặt chẽ tại bệnh viện.",
            IsHighRiskIfTrue: true),
        new(
            "ORGAN_FAILURE_OR_INFECTION",
            "Bệnh lý cơ quan",
            "Bạn có tiền sử suy tim nặng, suy thận, suy gan hoặc đang bị nhiễm trùng cấp tính có sốt không?",
            "Cơ thể đang suy kiệt cơ quan hoặc nhiễm trùng cấp cần chuyển hóa ổn định, không được tạo áp lực thâm hụt năng lượng.",
            IsHighRiskIfTrue: true)
    ];

    public static (FmdSafetyStatus Status, List<string> TriggeredCodes, string Message) EvaluateScreening(
        IReadOnlyDictionary<string, bool> answers)
    {
        var triggered = new List<string>();

        foreach (var q in ScreeningQuestions)
        {
            if (answers.TryGetValue(q.QuestionId, out var answeredYes) && answeredYes && q.IsHighRiskIfTrue)
            {
                triggered.Add(q.QuestionId);
            }
        }

        if (triggered.Count > 0)
        {
            return (
                FmdSafetyStatus.ProfessionalReviewRequired,
                triggered,
                "Hệ thống phát hiện ít nhất một yếu tố rủi ro sức khỏe cao. Bạn BẮT BUỘC phải tham vấn bác sĩ chuyên khoa và KHÔNG ĐƯỢC tự ý thực hiện chu kỳ nhịn ăn FMD.");
        }

        return (
            FmdSafetyStatus.EligibleForTracking,
            triggered,
            "Bạn đã hoàn thành sàng lọc an toàn và đủ điều kiện sức khỏe ban đầu để kích hoạt chu kỳ theo dõi FMD 5 ngày.");
    }

    public static int ResolveTargetCaloriesForDay(int dayNumber)
    {
        return dayNumber switch
        {
            1 => 1100,
            _ => 750
        };
    }
}

