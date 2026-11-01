using LongevityDiet.Domain.Recommendation;

namespace LongevityDiet.Services.Recommendation;

public sealed class SafeExplanationRewriter(
    IRecommendationExplanationTextGenerator? optionalGenerator = null,
    TimeSpan? rewriteTimeout = null)
{
    private static readonly string[] ProhibitedMedicalPhrases =
    [
        "chữa khỏi",
        "điều trị bệnh",
        "thay thế thuốc",
        "kéo dài tuổi thọ thêm",
        "chẩn đoán",
        "cure",
        "treat disease",
        "medical prescription",
        "guaranteed lifespan"
    ];

    private readonly TimeSpan _timeout = rewriteTimeout ?? TimeSpan.FromMilliseconds(800);

    public async Task<RecommendationRankResult> ApplyAsync(
        RecommendationRankResult rankResult,
        string dietaryPattern,
        CancellationToken cancellationToken = default)
    {
        if (!rankResult.Succeeded || rankResult.AcceptedCandidates.Count == 0)
        {
            return rankResult;
        }

        if (optionalGenerator is null)
        {
            return rankResult;
        }

        var updatedCandidates = new List<RankedRecommendationCandidate>(rankResult.AcceptedCandidates.Count);

        foreach (var candidate in rankResult.AcceptedCandidates)
        {
            var rewritten = await TryRewriteSingleAsync(candidate, dietaryPattern, cancellationToken);
            // CRITICAL SAFETY INVARIANT: Never mutate Rank, FinalScore, ScoreComponents, or ReasonCodes.
            updatedCandidates.Add(candidate with
            {
                Rank = candidate.Rank,
                FinalScore = candidate.FinalScore,
                ScoreComponents = candidate.ScoreComponents,
                ReasonCodes = candidate.ReasonCodes,
                ReasonDetails = candidate.ReasonDetails,
                DeterministicExplanation = candidate.DeterministicExplanation,
                ExplanationText = rewritten.Text,
                ExplanationSource = rewritten.Source,
            });
        }

        return rankResult with
        {
            AcceptedCandidates = updatedCandidates,
        };
    }

    private async Task<(string Text, string Source)> TryRewriteSingleAsync(
        RankedRecommendationCandidate candidate,
        string dietaryPattern,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            var rawText = await optionalGenerator!.TryRewriteExplanationAsync(
                candidate,
                dietaryPattern,
                timeoutCts.Token);

            if (!IsValidRewrittenExplanation(rawText, candidate))
            {
                return (candidate.DeterministicExplanation, "DETERMINISTIC_FALLBACK");
            }

            return (rawText!.Trim(), "AI_REWRITTEN_VALIDATED");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (candidate.DeterministicExplanation, "DETERMINISTIC_FALLBACK_TIMEOUT");
        }
        catch (TimeoutException)
        {
            return (candidate.DeterministicExplanation, "DETERMINISTIC_FALLBACK_TIMEOUT");
        }
        catch (Exception)
        {
            return (candidate.DeterministicExplanation, "DETERMINISTIC_FALLBACK_ERROR");
        }
    }

    public static bool IsValidRewrittenExplanation(
        string? rewrittenText,
        RankedRecommendationCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(rewrittenText))
        {
            return false;
        }

        var trimmed = rewrittenText.Trim();
        if (trimmed.Length is < 15 or > 480)
        {
            return false;
        }

        foreach (var phrase in ProhibitedMedicalPhrases)
        {
            if (trimmed.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!trimmed.Contains(candidate.Title, StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains(candidate.CandidateCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

