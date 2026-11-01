using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;

namespace LongevityDiet.Services.Planning;

public sealed class PlanningCatalogSnapshotProvider(
    IUserRepository userRepository,
    IMealPlanRepository mealPlanRepository) : IPlanningInputProvider
{
    private static readonly PlanningRuleSetSnapshot DefaultRuleSet = new(
        ContractVersion: "v1",
        RuleSetVersion: "LDC-RULES-2026.1",
        PublishedAt: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        Rules:
        [
            new PlanningDietRuleSnapshot(
                "PLANT_FORWARD",
                "Ưu tiên nền tảng thực vật, rau xanh và dầu lành mạnh",
                "FoundationOfficial",
                "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                "FivePillars-EpidemiologyAndClinical",
                25m),
            new PlanningDietRuleSnapshot(
                "LEGUME_PROTEIN",
                "Sử dụng các loại đậu và hạt làm nguồn đạm chính hằng ngày",
                "FoundationOfficial",
                "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                "FivePillars-CentenarianAndClinical",
                20m),
            new PlanningDietRuleSnapshot(
                "COMPLEX_CARBS",
                "Ưu tiên tinh bột phức hợp và ngũ cốc nguyên hạt giàu chất xơ",
                "FoundationOfficial",
                "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                "FivePillars-BasicAndClinical",
                15m),
            new PlanningDietRuleSnapshot(
                "LOW_SUGAR_SATFAT",
                "Hạn chế đường tinh luyện và chất béo bão hòa trong bữa ăn",
                "FoundationOfficial",
                "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                "FivePillars-Epidemiology",
                15m),
            new PlanningDietRuleSnapshot(
                "MODERATE_FISH",
                "Kết hợp cá giàu omega-3 ở mức vừa phải theo chế độ pescatarian",
                "FoundationOfficial",
                "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                "FivePillars-CentenarianStudies",
                10m),
            new PlanningDietRuleSnapshot(
                "AGE_PROTEIN_FIT",
                "Điều chỉnh lượng đạm vừa phải theo độ tuổi sinh học",
                "FoundationOfficial",
                "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                "FivePillars-Juventology",
                10m)
        ]);

    private static readonly IReadOnlyList<PlanningCandidateMealSnapshot> CandidateCatalog =
    [
        new(
            "v1",
            "BF-OAT-WALNUT-01",
            "Cháo yến mạch nguyên cám, hạt óc chó và việt quất",
            "Yến mạch nguyên hạt nấu chậm cùng hạt óc chó, hạt chia và quả mọng tươi giàu chất xơ.",
            "WholeGrainBreakfast",
            "Mediterranean",
            [MealSlotType.Breakfast],
            1m,
            "tô (320g)",
            360,
            11m,
            52m,
            12m,
            1.5m,
            5m,
            9m,
            IsPlantBased: true,
            HasLegumes: false,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["tree-nut", "walnut", "gluten"],
            Ingredients: ["oat", "walnut", "chia-seed", "blueberry", "cinnamon"],
            IsActive: true),
        new(
            "v1",
            "BF-BROWN-RICE-MUNG-02",
            "Cháo gạo lứt đậu xanh nấm hương",
            "Cháo gạo lứt đỏ hầm đậu xanh nguyên vỏ, nấm hương và hành lá giàu đạm thực vật.",
            "LegumeGrainBreakfast",
            "Vietnamese",
            [MealSlotType.Breakfast],
            1m,
            "tô (350g)",
            340,
            12.5m,
            58m,
            6m,
            0.9m,
            2m,
            8.5m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["sesame"],
            Ingredients: ["brown-rice", "mung-bean", "shiitake-mushroom", "sesame-oil", "scallion"],
            IsActive: true),
        new(
            "v1",
            "BF-SWEET-POTATO-BLACKBEAN-03",
            "Khoai lang hấp, đậu đen hầm thảo mộc và bơ tươi",
            "Khoai lang mật ăn kèm đậu đen ninh mềm, lát bơ và hạt bí xanh không chứa gluten.",
            "PlantForwardBreakfast",
            "Vietnamese",
            [MealSlotType.Breakfast],
            1m,
            "đĩa (300g)",
            355,
            11.8m,
            54m,
            10.5m,
            1.4m,
            4.5m,
            10m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["sweet-potato", "black-bean", "avocado", "pumpkin-seed", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "BF-CHICKPEA-TOAST-04",
            "Bánh mì nguyên cám sốt đậu gà nghiền và cà chua bi",
            "Bánh mì lúa mạch đen nguyên cám phủ hummus đậu gà, dầu ô-liu nguyên chất và rau mầm.",
            "MediterraneanBreakfast",
            "Mediterranean",
            [MealSlotType.Breakfast, MealSlotType.Snack],
            1m,
            "phần (240g)",
            330,
            12m,
            46m,
            11m,
            1.5m,
            3.2m,
            8m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["gluten", "sesame"],
            Ingredients: ["whole-wheat-bread", "chickpea", "tahini", "sesame", "cherry-tomato", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "BF-QUINOA-BERRY-05",
            "Bát hạt diêm mạch ấm, táo xanh và hạt lanh",
            "Hạt diêm mạch nấu ấm cùng táo xanh, hạt hướng dương và hạt lanh giàu chất chống oxy hóa.",
            "HypoallergenicBreakfast",
            "Mediterranean",
            [MealSlotType.Breakfast],
            1m,
            "tô (300g)",
            345,
            11m,
            53m,
            9.5m,
            1.1m,
            6m,
            8.2m,
            IsPlantBased: true,
            HasLegumes: false,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["quinoa", "apple", "flaxseed", "sunflower-seed", "cinnamon"],
            IsActive: true),
        new(
            "v1",
            "LN-BROWN-RICE-LENTIL-01",
            "Cơm gạo lứt, đậu lăng hầm cà chua và bông cải xanh",
            "Gạo lứt hạt tròn dùng kèm đậu lăng nâu sốt cà chua, bông cải xanh hấp và dầu ô-liu.",
            "PlantForwardLunch",
            "Vietnamese",
            [MealSlotType.Lunch, MealSlotType.Dinner],
            1m,
            "suất (420g)",
            510,
            18.5m,
            72m,
            14m,
            2m,
            4.5m,
            12.5m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["brown-rice", "lentil", "tomato", "broccoli", "olive-oil", "garlic"],
            IsActive: true),
        new(
            "v1",
            "LN-TOFU-LOTUS-ROOT-02",
            "Đậu phụ non om củ sen, đậu Hà Lan và gạo lứt đỏ",
            "Đậu phụ non om thanh đạm cùng củ sen, đậu Hà Lan, cà rốt và cơm gạo lứt đỏ.",
            "VietnameseLegumeLunch",
            "Vietnamese",
            [MealSlotType.Lunch, MealSlotType.Dinner],
            1m,
            "suất (400g)",
            490,
            19.2m,
            66m,
            14.5m,
            2.1m,
            3.8m,
            10.2m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["soy"],
            Ingredients: ["tofu", "soy", "lotus-root", "green-pea", "brown-rice", "carrot"],
            IsActive: true),
        new(
            "v1",
            "LN-MACKEREL-GREENS-03",
            "Cá thu nướng thảo mộc, đậu trắng và cải bó xôi",
            "Cá thu biển nướng dầu ô-liu dùng kèm đậu trắng hầm mềm, cải bó xôi và khoai tây bi.",
            "PescatarianLunch",
            "Mediterranean",
            [MealSlotType.Lunch, MealSlotType.Dinner],
            1m,
            "suất (410g)",
            540,
            26m,
            48m,
            21m,
            3.2m,
            2.8m,
            9.5m,
            IsPlantBased: false,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: true,
            Allergens: ["fish"],
            Ingredients: ["mackerel", "fish", "white-bean", "spinach", "potato", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "LN-CHICKPEA-QUINOA-BOWL-04",
            "Bát diêm mạch, đậu gà nướng và rau củ Địa Trung Hải",
            "Hạt diêm mạch trộn đậu gà nướng nghệ, bí ngòi, ớt chuông và sốt chanh dầu ô-liu.",
            "MediterraneanBowl",
            "Mediterranean",
            [MealSlotType.Lunch, MealSlotType.Dinner],
            1m,
            "tô (400g)",
            505,
            17.5m,
            68m,
            16m,
            2.2m,
            4.2m,
            11.8m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["quinoa", "chickpea", "zucchini", "bell-pepper", "olive-oil", "lemon"],
            IsActive: true),
        new(
            "v1",
            "LN-SOBANOODLE-PEANUT-05",
            "Mì kiều mạch trộn rau củ và sốt đậu phộng",
            "Mì soba kiều mạch nguyên cám trộn đậu cô-ve, bắp cải tím và sốt bơ đậu phộng.",
            "WholeGrainNoodle",
            "Vietnamese",
            [MealSlotType.Lunch],
            1m,
            "tô (380g)",
            520,
            18m,
            69m,
            17.5m,
            2.6m,
            5m,
            9m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["peanut", "gluten", "soy"],
            Ingredients: ["buckwheat-noodle", "peanut", "green-bean", "red-cabbage", "soy-sauce"],
            IsActive: true),
        new(
            "v1",
            "DN-MINESTRONE-BEAN-01",
            "Súp rau củ Minestrone đậu đỏ và lúa mạch nguyên hạt",
            "Súp rau củ hầm chậm với đậu đỏ, cần tây, cải xoăn, cà chua và hạt lúa mạch.",
            "LightLongevityDinner",
            "Mediterranean",
            [MealSlotType.Dinner, MealSlotType.Lunch],
            1m,
            "tô (420g)",
            430,
            16m,
            62m,
            11m,
            1.5m,
            4.5m,
            12m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["gluten"],
            Ingredients: ["kidney-bean", "barley", "kale", "celery", "tomato", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "DN-MUNG-BEAN-PUMPKIN-02",
            "Canh bí đỏ hầm đậu xanh, rau cải luộc và cơm gạo lứt",
            "Bữa tối nhẹ bụng với bí đỏ hầm đậu xanh, cải ngọt luộc và khẩu phần nhỏ gạo lứt.",
            "VietnameseLightDinner",
            "Vietnamese",
            [MealSlotType.Dinner],
            1m,
            "suất (390g)",
            420,
            14.8m,
            64m,
            10m,
            1.3m,
            4.2m,
            10.5m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["pumpkin", "mung-bean", "bok-choy", "brown-rice", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "DN-SALMON-FENNEL-03",
            "Cá hồi áp chảo thì là, đậu lăng xanh và măng tây",
            "Cá hồi khẩu phần vừa phải áp chảo cùng đậu lăng xanh, măng tây và chanh vàng.",
            "PescatarianDinner",
            "Mediterranean",
            [MealSlotType.Dinner, MealSlotType.Lunch],
            1m,
            "suất (380g)",
            485,
            25m,
            42m,
            20m,
            3.1m,
            2.5m,
            9m,
            IsPlantBased: false,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: true,
            Allergens: ["fish"],
            Ingredients: ["salmon", "fish", "green-lentil", "asparagus", "fennel", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "DN-BLACKBEAN-CAULIFLOWER-04",
            "Đậu đen hầm nấm đông cô, súp lơ trắng và hạt kê",
            "Đậu đen ninh mềm cùng nấm đông cô, súp lơ trắng áp chảo và hạt kê nguyên cám.",
            "PlantForwardDinner",
            "Vietnamese",
            [MealSlotType.Dinner],
            1m,
            "suất (390g)",
            435,
            16.2m,
            61m,
            11.5m,
            1.4m,
            3.1m,
            11.2m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["black-bean", "millet", "cauliflower", "shiitake-mushroom", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "DN-SHRIMP-GREENPAPAYA-05",
            "Gỏi đu đủ xanh tôm hấp và đậu Hà Lan",
            "Đu đủ xanh bào sợi trộn tôm hấp, đậu Hà Lan và rau thơm thanh mát.",
            "SeafoodDinner",
            "Vietnamese",
            [MealSlotType.Dinner],
            1m,
            "đĩa (360g)",
            395,
            23m,
            44m,
            11m,
            1.6m,
            5.5m,
            8m,
            IsPlantBased: false,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: ["shellfish", "shrimp"],
            Ingredients: ["shrimp", "shellfish", "green-papaya", "green-pea", "mint", "lime"],
            IsActive: true),
        new(
            "v1",
            "SN-EDAMAME-OLIVE-01",
            "Đậu nành lông luộc và hạt bí xanh rang mộc",
            "Đậu edamame hấp nguyên vỏ kết hợp hạt bí xanh cung cấp đạm thực vật và kẽm.",
            "PlantSnack",
            "Vietnamese",
            [MealSlotType.Snack],
            1m,
            "phần (150g)",
            195,
            12m,
            16m,
            9m,
            1.1m,
            2m,
            6m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: ["soy"],
            Ingredients: ["edamame", "soy", "pumpkin-seed"],
            IsActive: true),
        new(
            "v1",
            "SN-ROASTED-CHICKPEA-02",
            "Đậu gà rang thảo mộc và quả lê tươi",
            "Đậu gà rang giòn với hương thảo, dầu ô-liu dùng kèm nửa quả lê tươi giàu chất xơ.",
            "LegumeSnack",
            "Mediterranean",
            [MealSlotType.Snack],
            1m,
            "phần (170g)",
            210,
            8.5m,
            32m,
            6m,
            0.8m,
            6.5m,
            7.5m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["chickpea", "pear", "rosemary", "olive-oil"],
            IsActive: true),
        new(
            "v1",
            "SN-ALMOND-APPLE-03",
            "Hạnh nhân rang mộc và táo đỏ",
            "Khẩu phần hạnh nhân nguyên vỏ giàu chất béo không bão hòa cùng táo tươi.",
            "NutFruitSnack",
            "Mediterranean",
            [MealSlotType.Snack],
            1m,
            "phần (140g)",
            205,
            6m,
            22m,
            11.5m,
            1m,
            7m,
            5.5m,
            IsPlantBased: true,
            HasLegumes: false,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: ["tree-nut", "almond"],
            Ingredients: ["almond", "apple"],
            IsActive: true),
        new(
            "v1",
            "SN-LOTUS-SEED-MUNG-04",
            "Hạt sen hầm đậu xanh ít ngọt",
            "Hạt sen tươi nấu mềm cùng đậu xanh nguyên vỏ, thanh mát và không chứa các hạt gây dị ứng phổ biến.",
            "VietnameseSnack",
            "Vietnamese",
            [MealSlotType.Snack],
            1m,
            "chén (180g)",
            185,
            7.5m,
            34m,
            2.2m,
            0.4m,
            4m,
            6.2m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["lotus-seed", "mung-bean"],
            IsActive: true),
        new(
            "v1",
            "INACTIVE-SUGARY-PASTRY-99",
            "Bánh ngọt bơ đường tinh luyện (Ngưng kích hoạt)",
            "Món ăn thử nghiệm đã ngừng kích hoạt trong danh mục.",
            "InactiveTest",
            "Any",
            [MealSlotType.Breakfast, MealSlotType.Lunch, MealSlotType.Dinner, MealSlotType.Snack],
            1m,
            "phần (200g)",
            650,
            6m,
            78m,
            34m,
            18m,
            42m,
            1m,
            IsPlantBased: false,
            HasLegumes: false,
            HasWholeGrains: false,
            ContainsFish: false,
            Allergens: ["gluten"],
            Ingredients: ["refined-sugar", "butter", "white-flour"],
            IsActive: false)
    ];

    public async Task<PlanningUserContextSnapshot?> GetUserContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var profile = user.Profile;
        var dietaryPreference = await mealPlanRepository.GetDietaryPreferenceAsync(userId, cancellationToken);

        var allergies = SplitTokens(dietaryPreference?.AllergiesCsv);
        var excludedFoods = SplitTokens(dietaryPreference?.ExcludedFoodsCsv);
        var dietaryPattern = string.IsNullOrWhiteSpace(dietaryPreference?.DietaryPattern)
            ? InferDietaryPattern(profile?.FoodPreference)
            : dietaryPreference.DietaryPattern;
        var preferredCuisine = !string.IsNullOrWhiteSpace(dietaryPreference?.PreferredCuisine)
            ? dietaryPreference.PreferredCuisine
            : InferCuisinePreference(profile?.FoodPreference);

        return new PlanningUserContextSnapshot(
            ContractVersion: "v1",
            UserId: user.Id,
            BirthYear: profile?.BirthYear,
            TimeZone: string.IsNullOrWhiteSpace(profile?.TimeZone) ? "Asia/Ho_Chi_Minh" : profile.TimeZone,
            WakeTime: profile?.WakeTime,
            SleepTime: profile?.SleepTime,
            PreferredMealFrequency: profile?.PreferredMealFrequency is >= 2 and <= 4
                ? profile.PreferredMealFrequency
                : 3,
            FoodPreference: profile?.FoodPreference,
            DietaryPattern: dietaryPattern,
            Allergies: allergies,
            ExcludedFoods: excludedFoods,
            PreferredCuisine: preferredCuisine);
    }

    public Task<PlanningRuleSetSnapshot> GetActiveRuleSetAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DefaultRuleSet);

    public Task<IReadOnlyList<PlanningCandidateMealSnapshot>> GetCandidateMealsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CandidateCatalog);

    public static IReadOnlyList<string> SplitTokens(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split([',', ';', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string NormalizeToken(string value) =>
        value.Trim().ToLowerInvariant();

    private static string InferDietaryPattern(string? foodPreference)
    {
        if (string.IsNullOrWhiteSpace(foodPreference))
        {
            return "PlantForward";
        }

        var normalized = foodPreference.ToLowerInvariant();
        if (normalized.Contains("vegan", StringComparison.Ordinal) ||
            normalized.Contains("thuần chay", StringComparison.Ordinal) ||
            normalized.Contains("strictly plant", StringComparison.Ordinal))
        {
            return "StrictlyPlantBased";
        }

        if (normalized.Contains("pescatarian", StringComparison.Ordinal) ||
            normalized.Contains("cá", StringComparison.Ordinal))
        {
            return "Pescatarian";
        }

        return "PlantForward";
    }

    private static string? InferCuisinePreference(string? foodPreference)
    {
        if (string.IsNullOrWhiteSpace(foodPreference))
        {
            return null;
        }

        var normalized = foodPreference.ToLowerInvariant();
        if (normalized.Contains("việt", StringComparison.Ordinal) ||
            normalized.Contains("viet", StringComparison.Ordinal))
        {
            return "Vietnamese";
        }

        if (normalized.Contains("mediterranean", StringComparison.Ordinal) ||
            normalized.Contains("địa trung hải", StringComparison.Ordinal))
        {
            return "Mediterranean";
        }

        return null;
    }
}

