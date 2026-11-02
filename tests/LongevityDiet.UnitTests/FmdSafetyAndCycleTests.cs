using LongevityDiet.Domain.Entities;
using LongevityDiet.Services.Fmd;

namespace LongevityDiet.UnitTests;

public sealed class FmdSafetyAndCycleTests
{
    [Fact]
    public void FmdSafetyCatalog_EvaluateScreening_AllNegative_ReturnsEligibleForTracking()
    {
        // Arrange
        var answers = new Dictionary<string, bool>
        {
            ["PREGNANT_OR_BREASTFEEDING"] = false,
            ["AGE_OUT_OF_BOUNDS"] = false,
            ["UNDERWEIGHT_BMI"] = false,
            ["EATING_DISORDER_HISTORY"] = false,
            ["DIABETES_MEDICATION"] = false,
            ["ACTIVE_CHEMOTHERAPY_OR_CANCER"] = false,
            ["ORGAN_FAILURE_OR_INFECTION"] = false,
        };

        // Act
        var (status, triggeredCodes, message) = FmdSafetyCatalog.EvaluateScreening(answers);

        // Assert
        Assert.Equal(FmdSafetyStatus.EligibleForTracking, status);
        Assert.Empty(triggeredCodes);
        Assert.Contains("đủ điều kiện", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PREGNANT_OR_BREASTFEEDING")]
    [InlineData("DIABETES_MEDICATION")]
    [InlineData("UNDERWEIGHT_BMI")]
    [InlineData("ACTIVE_CHEMOTHERAPY_OR_CANCER")]
    [InlineData("ORGAN_FAILURE_OR_INFECTION")]
    public void FmdSafetyCatalog_EvaluateScreening_SingleHighRiskFlag_BlocksAndRequiresProfessionalReview(string riskCode)
    {
        // Arrange
        var answers = new Dictionary<string, bool>
        {
            ["PREGNANT_OR_BREASTFEEDING"] = false,
            ["AGE_OUT_OF_BOUNDS"] = false,
            ["UNDERWEIGHT_BMI"] = false,
            ["EATING_DISORDER_HISTORY"] = false,
            ["DIABETES_MEDICATION"] = false,
            ["ACTIVE_CHEMOTHERAPY_OR_CANCER"] = false,
            ["ORGAN_FAILURE_OR_INFECTION"] = false,
        };
        answers[riskCode] = true;

        // Act
        var (status, triggeredCodes, message) = FmdSafetyCatalog.EvaluateScreening(answers);

        // Assert
        Assert.Equal(FmdSafetyStatus.ProfessionalReviewRequired, status);
        Assert.Single(triggeredCodes);
        Assert.Contains(riskCode, triggeredCodes);
        Assert.Contains("tham vấn bác sĩ", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FmdSafetyCatalog_EvaluateScreening_MultipleHighRiskFlags_CollectsAllCodes()
    {
        // Arrange
        var answers = new Dictionary<string, bool>
        {
            ["PREGNANT_OR_BREASTFEEDING"] = true,
            ["AGE_OUT_OF_BOUNDS"] = false,
            ["UNDERWEIGHT_BMI"] = false,
            ["EATING_DISORDER_HISTORY"] = false,
            ["DIABETES_MEDICATION"] = true,
            ["ACTIVE_CHEMOTHERAPY_OR_CANCER"] = false,
            ["ORGAN_FAILURE_OR_INFECTION"] = false,
        };

        // Act
        var (status, triggeredCodes, _) = FmdSafetyCatalog.EvaluateScreening(answers);

        // Assert
        Assert.Equal(FmdSafetyStatus.ProfessionalReviewRequired, status);
        Assert.Equal(2, triggeredCodes.Count);
        Assert.Contains("PREGNANT_OR_BREASTFEEDING", triggeredCodes);
        Assert.Contains("DIABETES_MEDICATION", triggeredCodes);
    }

    [Theory]
    [InlineData(1, 1100)]
    [InlineData(2, 750)]
    [InlineData(3, 750)]
    [InlineData(4, 750)]
    [InlineData(5, 750)]
    public void FmdSafetyCatalog_ResolveTargetCaloriesForDay_MatchesValterLongoProtocols(int dayNumber, int expectedCalories)
    {
        // Act
        var targetKcal = FmdSafetyCatalog.ResolveTargetCaloriesForDay(dayNumber);

        // Assert
        Assert.Equal(expectedCalories, targetKcal);
    }

    [Fact]
    public void FmdSafetyCatalog_ResolveTargetCaloriesForDay_OutOfRangeDays_DefaultsTo750()
    {
        Assert.Equal(750, FmdSafetyCatalog.ResolveTargetCaloriesForDay(6));
        Assert.Equal(750, FmdSafetyCatalog.ResolveTargetCaloriesForDay(0));
    }

    [Fact]
    public void FmdSafetyCatalog_EducationContent_ContainsRequiredMedicalDisclaimersAndCitations()
    {
        // Arrange
        var education = FmdSafetyCatalog.DefaultEducation;

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(education.Version));
        Assert.Contains("KHÔNG PHẢI là thiết bị y tế", education.ScopeDisclaimer);
        Assert.NotEmpty(education.KeyPrinciples);
        Assert.NotEmpty(education.AbsoluteContraindications);
        Assert.True(education.Citations.Count >= 2);
    }
}
