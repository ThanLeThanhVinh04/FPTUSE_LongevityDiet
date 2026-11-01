using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using LongevityDiet.Domain.Recommendation;
using LongevityDiet.Services.Recommendation;

namespace LongevityDiet.API.Recommendation;

public sealed class GrpcRecommendationEngineClient : IRecommendationEngineClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Method<RecommendationRankRequest, RecommendationRankResult> RankMealsEnvelopeMethod =
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

    private static readonly Action<ILogger, StatusCode, Exception?> LogRemoteRpcFallback =
        LoggerMessage.Define<StatusCode>(
            LogLevel.Information,
            new EventId(1, nameof(LogRemoteRpcFallback)),
            "Remote Recommendation gRPC host returned {StatusCode}; using deterministic pipeline fallback.");

    private static readonly Action<ILogger, StatusCode, Exception?> LogRemoteRpcFailed =
        LoggerMessage.Define<StatusCode>(
            LogLevel.Warning,
            new EventId(2, nameof(LogRemoteRpcFailed)),
            "Remote Recommendation gRPC host failed with {StatusCode}.");

    private static readonly Action<ILogger, string, Exception?> LogRemoteUnreachableFallback =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, nameof(LogRemoteUnreachableFallback)),
            "Remote Recommendation gRPC host unreachable ({Message}); using deterministic pipeline fallback.");

    private readonly GrpcChannel? _channel;
    private readonly CallInvoker? _callInvoker;
    private readonly TimeSpan _timeout;
    private readonly bool _requireRemoteHost;
    private readonly ILogger<GrpcRecommendationEngineClient> _logger;

    public GrpcRecommendationEngineClient(
        IConfiguration configuration,
        ILogger<GrpcRecommendationEngineClient> logger)
    {
        _logger = logger;
        var grpcUrl = configuration["Grpc:RecommendationUrl"];
        var timeoutMs = configuration.GetValue("Grpc:TimeoutMilliseconds", 1500);
        _timeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 200, 10000));
        _requireRemoteHost = configuration.GetValue("Grpc:RequireRemoteHost", false);

        if (!string.IsNullOrWhiteSpace(grpcUrl) && Uri.TryCreate(grpcUrl, UriKind.Absolute, out var address))
        {
            _channel = GrpcChannel.ForAddress(address);
            _callInvoker = _channel.CreateCallInvoker();
        }
    }

    public async Task<RecommendationRankResult> RankMealsAsync(
        RecommendationRankRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_callInvoker is not null)
        {
            try
            {
                var deadline = DateTime.UtcNow.Add(_timeout);
                var callOptions = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);
                var call = _callInvoker.AsyncUnaryCall(RankMealsEnvelopeMethod, host: null, callOptions, request);
                var response = await call.ResponseAsync.ConfigureAwait(false);
                return response;
            }
            catch (RpcException ex) when (!_requireRemoteHost)
            {
                LogRemoteRpcFallback(_logger, ex.StatusCode, null);
            }
            catch (RpcException ex)
            {
                LogRemoteRpcFailed(_logger, ex.StatusCode, ex);
                return new RecommendationRankResult(
                    Succeeded: false,
                    ErrorCode: "recommendation_service_unavailable",
                    ErrorMessage: "Recommendation gRPC service is currently unavailable.",
                    ContractVersion: request.ContractVersion,
                    RuleSetVersion: request.RuleSetVersion,
                    WeightProfileVersion: request.WeightProfileVersion,
                    TargetSlot: request.TargetSlot,
                    AcceptedCandidates: [],
                    RejectedCandidates: []);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !_requireRemoteHost)
            {
                LogRemoteUnreachableFallback(_logger, ex.Message, null);
            }
        }
        else if (_requireRemoteHost)
        {
            return new RecommendationRankResult(
                Succeeded: false,
                ErrorCode: "recommendation_service_unavailable",
                ErrorMessage: "Recommendation gRPC address is not configured.",
                ContractVersion: request.ContractVersion,
                RuleSetVersion: request.RuleSetVersion,
                WeightProfileVersion: request.WeightProfileVersion,
                TargetSlot: request.TargetSlot,
                AcceptedCandidates: [],
                RejectedCandidates: []);
        }

        return RecommendationRankingPipeline.Execute(request);
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}

