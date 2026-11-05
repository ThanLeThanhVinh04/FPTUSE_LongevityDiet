using System.Text.Json;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Domain.Recommendation;
using LongevityDiet.Recommendation.Grpc.Protos;

namespace LongevityDiet.Recommendation.Grpc.Services;

public sealed class RecommendationProtoGrpcService : RecommendationProtoService.RecommendationProtoServiceBase
{
    public override Task<RankMealsProtoResponse> RankMeals(
        RankMealsProtoRequest request,
        ServerCallContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var domainRequest = MapToDomainRequest(request);
        var result = RecommendationRankingPipeline.Execute(domainRequest);

        return Task.FromResult(MapToProtoResponse(result));
    }

    public Task<RecommendationRankResult> RankMealsEnvelope(
        RecommendationRankRequest request,
        ServerCallContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var result = RecommendationRankingPipeline.Execute(request);
        return Task.FromResult(result);
    }

    private static RecommendationRankRequest MapToDomainRequest(RankMealsProtoRequest proto)
    {
        if (!Enum.TryParse<MealSlotType>(proto.TargetSlot, ignoreCase: true, out var targetSlot))
        {
            targetSlot = MealSlotType.Lunch;
        }

        var candidates = proto.Candidates
            .Select(c =>
            {
                var supportedSlots = c.SupportedSlots
                    .Select(s => Enum.TryParse<MealSlotType>(s, ignoreCase: true, out var parsed) ? parsed : MealSlotType.Lunch)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateInput(
                    CandidateCode: c.CandidateCode,
                    Title: c.Title,
                    Description: c.Description,
                    Category: c.Category,
                    Cuisine: c.Cuisine,
                    SupportedSlots: supportedSlots,
                    ServingSize: (decimal)c.ServingSize,
                    ServingUnit: c.ServingUnit,
                    CaloriesKcal: c.CaloriesKcal,
                    ProteinGrams: (decimal)c.ProteinGrams,
                    CarbsGrams: (decimal)c.CarbsGrams,
                    FatGrams: (decimal)c.FatGrams,
                    SaturatedFatGrams: (decimal)c.SaturatedFatGrams,
                    SugarGrams: (decimal)c.SugarGrams,
                    FiberGrams: (decimal)c.FiberGrams,
                    IsPlantBased: c.IsPlantBased,
                    HasLegumes: c.HasLegumes,
                    HasWholeGrains: c.HasWholeGrains,
                    ContainsFish: c.ContainsFish,
                    Allergens: c.Allergens.ToList(),
                    Ingredients: c.Ingredients.ToList(),
                    IsActive: c.IsActive,
                    HasSafetyGateViolation: c.HasSafetyGateViolation);
            })
            .ToList();

        return new RecommendationRankRequest(
            ContractVersion: string.IsNullOrWhiteSpace(proto.ContractVersion) ? "2026-04-v2" : proto.ContractVersion,
            RuleSetVersion: proto.RuleSetVersion,
            WeightProfileVersion: string.IsNullOrWhiteSpace(proto.WeightProfileVersion) ? "LDC-WEIGHTS-V2.0" : proto.WeightProfileVersion,
            TargetSlot: targetSlot,
            DietaryPattern: proto.DietaryPattern,
            UserAgeYears: proto.UserAgeYears,
            PreferredCuisine: string.IsNullOrWhiteSpace(proto.PreferredCuisine) ? null : proto.PreferredCuisine,
            Allergies: proto.Allergies.ToList(),
            ExplicitExclusions: proto.ExplicitExclusions.ToList(),
            DislikedTokens: proto.DislikedTokens.ToList(),
            PreferredTokens: proto.PreferredTokens.ToList(),
            CurrentCandidateCode: string.IsNullOrWhiteSpace(proto.CurrentCandidateCode) ? null : proto.CurrentCandidateCode,
            Candidates: candidates);
    }

    private static RankMealsProtoResponse MapToProtoResponse(RecommendationRankResult result)
    {
        var response = new RankMealsProtoResponse
        {
            Succeeded = result.Succeeded,
            ErrorCode = result.ErrorCode ?? string.Empty,
            ErrorMessage = result.ErrorMessage ?? string.Empty,
            ContractVersion = result.ContractVersion,
            RuleSetVersion = result.RuleSetVersion,
            WeightProfileVersion = result.WeightProfileVersion,
            TargetSlot = result.TargetSlot.ToString(),
        };

        foreach (var accepted in result.AcceptedCandidates)
        {
            var protoCandidate = new RankedCandidateProto
            {
                Rank = accepted.Rank,
                CandidateCode = accepted.CandidateCode,
                Title = accepted.Title,
                Description = accepted.Description,
                Cuisine = accepted.Cuisine,
                ServingSize = (double)accepted.ServingSize,
                ServingUnit = accepted.ServingUnit,
                CaloriesKcal = accepted.CaloriesKcal,
                ProteinGrams = (double)accepted.ProteinGrams,
                CarbsGrams = (double)accepted.CarbsGrams,
                FatGrams = (double)accepted.FatGrams,
                FiberGrams = (double)accepted.FiberGrams,
                IsPlantBased = accepted.IsPlantBased,
                HasLegumes = accepted.HasLegumes,
                HasWholeGrains = accepted.HasWholeGrains,
                ContainsFish = accepted.ContainsFish,
                FinalScore = (double)accepted.FinalScore,
                IsDisliked = accepted.IsDisliked,
                IsPreferred = accepted.IsPreferred,
                DeterministicExplanation = accepted.DeterministicExplanation,
            };

            protoCandidate.Allergens.AddRange(accepted.Allergens);
            protoCandidate.Ingredients.AddRange(accepted.Ingredients);
            protoCandidate.ReasonCodes.AddRange(accepted.ReasonCodes);

            foreach (var comp in accepted.ScoreComponents)
            {
                protoCandidate.ScoreComponents.Add(new ScoreComponentProto
                {
                    ComponentCode = comp.ComponentCode,
                    Label = comp.Label,
                    NormalizedValue = (double)comp.NormalizedValue,
                    Weight = (double)comp.Weight,
                    WeightedDelta = (double)comp.WeightedDelta,
                    IsPenalty = comp.IsPenalty,
                });
            }

            foreach (var detail in accepted.ReasonDetails)
            {
                protoCandidate.ReasonDetails.Add(new ReasonDetailProto
                {
                    ReasonCode = detail.ReasonCode,
                    Title = detail.Title,
                    Explanation = detail.Explanation,
                });
            }

            response.AcceptedCandidates.Add(protoCandidate);
        }

        foreach (var rejected in result.RejectedCandidates)
        {
            var protoRejected = new RejectedCandidateProto
            {
                CandidateCode = rejected.CandidateCode,
                Title = rejected.Title,
                RejectionExplanation = rejected.RejectionExplanation,
            };
            protoRejected.RejectionReasonCodes.AddRange(rejected.RejectionReasonCodes);
            response.RejectedCandidates.Add(protoRejected);
        }

        return response;
    }
}

public sealed class RecommendationEnvelopeMethodProvider : IServiceMethodProvider<RecommendationProtoGrpcService>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly Method<RecommendationRankRequest, RecommendationRankResult> RankMealsEnvelopeMethod =
        new(
            MethodType.Unary,
            "longevitydiet.recommendation.v2.RecommendationEnvelopeService",
            "RankMealsEnvelope",
            Marshallers.Create(
                serializer: req => JsonSerializer.SerializeToUtf8Bytes(req, JsonOptions),
                deserializer: bytes => JsonSerializer.Deserialize<RecommendationRankRequest>(bytes, JsonOptions)!),
            Marshallers.Create(
                serializer: res => JsonSerializer.SerializeToUtf8Bytes(res, JsonOptions),
                deserializer: bytes => JsonSerializer.Deserialize<RecommendationRankResult>(bytes, JsonOptions)!));

    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<RecommendationProtoGrpcService> context)
    {
        context.AddUnaryMethod(
            RankMealsEnvelopeMethod,
            [],
            (service, request, callContext) => service.RankMealsEnvelope(request, callContext));
    }
}

