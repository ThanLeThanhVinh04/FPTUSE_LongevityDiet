using Grpc.AspNetCore.Server.Model;
using LongevityDiet.Recommendation.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSingleton<IServiceMethodProvider<RecommendationProtoGrpcService>, RecommendationEnvelopeMethodProvider>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapGrpcService<RecommendationProtoGrpcService>();
app.MapGet("/", () => Results.Ok(new
{
    service = "LongevityDiet.Recommendation.Grpc",
    status = "ready",
    version = "2026-04-v2",
    contract = "longevitydiet.recommendation.v2.RecommendationProtoService"
}));

app.Run();

public partial class Program;
