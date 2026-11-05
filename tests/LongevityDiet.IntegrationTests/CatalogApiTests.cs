using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LongevityDiet.IntegrationTests;

public sealed class CatalogApiTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public CatalogApiTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MemberCatalog_WithoutToken_ReturnsUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/v1/foods");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MemberToken_CannotUseAdminCatalog()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var session = await RegisterAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync("/api/v1/admin/foods");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemberFood_MissingResource_ReturnsProblemDetailsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var session = await RegisterAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync($"/api/v1/foods/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminFood_InvalidInput_ReturnsProblemDetailsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        await AuthenticateAsAdminAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/foods",
            CreateFoodRequest(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminFood_CreateQueryDeactivate_UsesExpectedHttpSemantics()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        await AuthenticateAsAdminAsync(client);

        var create = await client.PostAsJsonAsync(
            "/api/v1/admin/foods",
            CreateFoodRequest("Lentils"));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);

        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var foodId = createdJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/foods/{foodId}", create.Headers.Location!.OriginalString);

        var memberGet = await client.GetAsync($"/api/v1/foods/{foodId}");
        Assert.Equal(HttpStatusCode.OK, memberGet.StatusCode);

        var invalidPage = await client.GetAsync("/api/v1/foods?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        Assert.Equal("application/problem+json", invalidPage.Content.Headers.ContentType?.MediaType);

        var deactivate = await client.DeleteAsync($"/api/v1/admin/foods/{foodId}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var hiddenFromMember = await client.GetAsync($"/api/v1/foods/{foodId}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenFromMember.StatusCode);

        var adminList = await client.GetAsync("/api/v1/admin/foods?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);

        using var adminListJson = JsonDocument.Parse(await adminList.Content.ReadAsStringAsync());
        var item = Assert.Single(adminListJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(foodId, item.GetProperty("id").GetGuid());
        Assert.False(item.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task FoodCollection_SearchFilterSortAndPagination_AreServerSideAndDeterministic()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        await AuthenticateAsAdminAsync(client);

        foreach (var request in new[]
                 {
                     CreateFoodRequest("Lentil Alpha", 110m, isLegume: true),
                     CreateFoodRequest("Lentil Beta", 140m, isLegume: true),
                     CreateFoodRequest("Lentil Fish", 210m, isLegume: false, isFish: true),
                 })
        {
            var create = await client.PostAsJsonAsync("/api/v1/admin/foods", request);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        const string query =
            "/api/v1/foods?page=1&pageSize=1&search=lentil&isLegume=true&sortBy=calories&sortDirection=desc";

        var firstResponse = await client.GetAsync(query);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var root = firstJson.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(1, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, root.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());

        var firstItem = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("Lentil Beta", firstItem.GetProperty("name").GetString());
        var firstId = firstItem.GetProperty("id").GetGuid();

        var repeatResponse = await client.GetAsync(query);
        using var repeatJson = JsonDocument.Parse(await repeatResponse.Content.ReadAsStringAsync());
        var repeatItem = Assert.Single(repeatJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(firstId, repeatItem.GetProperty("id").GetGuid());

        var secondPage = await client.GetAsync(
            "/api/v1/foods?page=2&pageSize=1&search=lentil&isLegume=true&sortBy=calories&sortDirection=desc");
        using var secondJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync());
        var secondItem = Assert.Single(secondJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Lentil Alpha", secondItem.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AdminRecipe_CreateThenUpdateSameIngredient_DoesNotConflictWithEfTracking()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        await AuthenticateAsAdminAsync(client);

        var createFood = await client.PostAsJsonAsync(
            "/api/v1/admin/foods",
            CreateFoodRequest("Recipe Lentils"));
        Assert.Equal(HttpStatusCode.Created, createFood.StatusCode);

        using var foodJson = JsonDocument.Parse(await createFood.Content.ReadAsStringAsync());
        var foodId = foodJson.RootElement.GetProperty("id").GetGuid();

        object RecipePayload(decimal amount, string description) => new
        {
            Name = "Lentil Bowl",
            Description = description,
            Category = "Main",
            Cuisine = "Mediterranean",
            Tags = "legume",
            ServingCount = 2,
            PrepMinutes = 20,
            CaloriesPerServing = 330m,
            ProteinGPerServing = 16m,
            CarbohydrateGPerServing = 52m,
            FatGPerServing = 7m,
            SaturatedFatGPerServing = 1m,
            SugarGPerServing = 4m,
            FiberGPerServing = 11m,
            IsPlantForward = true,
            HasLegume = true,
            HasWholeGrain = false,
            HasFish = false,
            Ingredients = new[]
            {
                new { FoodId = foodId, Amount = amount, Unit = "g", IsOptional = false },
            },
            AllergenIds = Array.Empty<Guid>(),
        };

        var createRecipe = await client.PostAsJsonAsync(
            "/api/v1/admin/recipes",
            RecipePayload(120m, "Initial recipe"));
        Assert.Equal(HttpStatusCode.Created, createRecipe.StatusCode);

        using var recipeJson = JsonDocument.Parse(await createRecipe.Content.ReadAsStringAsync());
        var recipeId = recipeJson.RootElement.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync(
            $"/api/v1/admin/recipes/{recipeId}",
            RecipePayload(180m, "Updated recipe"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var updatedJson = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal("Updated recipe", updatedJson.RootElement.GetProperty("description").GetString());
        var ingredient = Assert.Single(updatedJson.RootElement.GetProperty("ingredients").EnumerateArray());
        Assert.Equal(180m, ingredient.GetProperty("amount").GetDecimal());
        Assert.Equal(foodId, ingredient.GetProperty("foodId").GetGuid());
    }

    [Fact]
    public async Task RulePublish_PublishedVersionBecomesImmutable()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        await AuthenticateAsAdminAsync(client);

        var createRule = await client.PostAsJsonAsync(
            "/api/v1/admin/diet-rules",
            new
            {
                RuleKey = "PLANT_FORWARD",
                Title = "Plant-forward pattern",
                Description = "Prefer a mostly plant-forward dietary pattern.",
                Category = "FoodPattern",
                IsMedicalSensitive = false,
            });

        Assert.Equal(HttpStatusCode.Created, createRule.StatusCode);
        using var ruleJson = JsonDocument.Parse(await createRule.Content.ReadAsStringAsync());
        var ruleId = ruleJson.RootElement.GetProperty("id").GetGuid();

        var versionPayload = new
        {
            ParametersJson = "{\"weight\":1}",
            SourceUrl = "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
            SourceType = 2,
            Citation = "Longo Foundation adult longevity diet guidance",
            EvidenceLevel = 2,
            EffectiveAt = (DateTimeOffset?)null,
        };

        var createVersion = await client.PostAsJsonAsync(
            $"/api/v1/admin/diet-rules/{ruleId}/versions",
            versionPayload);

        Assert.Equal(HttpStatusCode.Created, createVersion.StatusCode);
        using var versionJson = JsonDocument.Parse(await createVersion.Content.ReadAsStringAsync());
        var versionId = versionJson.RootElement.GetProperty("id").GetGuid();

        var publish = await client.PostAsync(
            $"/api/v1/admin/rule-versions/{versionId}/publication",
            content: null);

        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var updatePublished = await client.PutAsJsonAsync(
            $"/api/v1/admin/rule-versions/{versionId}",
            versionPayload with { ParametersJson = "{\"weight\":2}" });

        Assert.Equal(HttpStatusCode.Conflict, updatePublished.StatusCode);

        var memberRules = await client.GetAsync("/api/v1/diet-rules");
        Assert.Equal(HttpStatusCode.OK, memberRules.StatusCode);

        using var rulesJson = JsonDocument.Parse(await memberRules.Content.ReadAsStringAsync());
        var rule = Assert.Single(rulesJson.RootElement.EnumerateArray());
        var publishedVersion = Assert.Single(rule.GetProperty("versions").EnumerateArray());
        Assert.Equal(2, publishedVersion.GetProperty("state").GetInt32());
    }

    [Fact]
    public async Task OpenApi_ContainsCatalogAdminSecurityAndResponseMetadata()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var adminFoodPost = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/foods")
            .GetProperty("post");

        Assert.True(adminFoodPost.GetProperty("security").GetArrayLength() > 0);
        var responses = adminFoodPost.GetProperty("responses");
        Assert.True(responses.TryGetProperty("201", out _));
        Assert.True(responses.TryGetProperty("400", out _));
    }

    private async Task AuthenticateAsAdminAsync(HttpClient client)
    {
        var registration = CreateRegistration();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await _factory.PromoteUserToAdminAsync(registration.Email);

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { registration.Email, registration.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static async Task<AuthSessionResponse> RegisterAsync(HttpClient client)
    {
        var registration = CreateRegistration();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var session = await register.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        return session;
    }

    private static RegistrationRequest CreateRegistration()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new RegistrationRequest(
            $"catalog-{suffix}@example.test",
            "StrongPass123!",
            "Catalog Tester");
    }

    private static object CreateFoodRequest(
        string name,
        decimal calories = 116m,
        bool isLegume = true,
        bool isFish = false) =>
        new
        {
            Name = name,
            Category = isFish ? "Fish" : "Legume",
            Tags = isFish ? "fish" : "plant-forward",
            Cuisine = "Mediterranean",
            DefaultServingAmount = 100m,
            ServingUnit = "g",
            Calories = calories,
            ProteinG = 9m,
            CarbohydrateG = 20m,
            FatG = 0.4m,
            SaturatedFatG = 0.1m,
            SugarG = 1.8m,
            FiberG = 7.9m,
            IsPlantBased = !isFish,
            IsLegume = isLegume,
            IsWholeGrain = false,
            IsFish = isFish,
        };

    private sealed record RegistrationRequest(string Email, string Password, string DisplayName);

    private sealed record AuthSessionResponse(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        SessionUserResponse User);

    private sealed record SessionUserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        string Role,
        bool ProfileCompleted);
}
