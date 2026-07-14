using BlunderForge.Application.Configuration;
using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Games;
using BlunderForge.Application.Npc;
using BlunderForge.Application.Reviews;
using BlunderForge.Infrastructure.Configuration;
using BlunderForge.Infrastructure.Engine;
using BlunderForge.Infrastructure.Ai;
using BlunderForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace BlunderForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBlunderForgeInfrastructure(this IServiceCollection services, string databaseConnectionString, AiProviderOptions aiOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);

        services.AddDbContext<BlunderForgeDbContext>(options => options.UseSqlite(databaseConnectionString));
        services.AddSingleton<DatabaseMigrationStatus>();
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddScoped<IActiveGameRepository, EfActiveGameRepository>();
        services.AddScoped<IMoveAnalysisRepository, EfMoveAnalysisRepository>();
        services.AddScoped<IMoveClassifier, CentipawnMoveClassifier>();
        services.AddSingleton<AiResiliencePipelineProvider>();
        services.AddHttpClient<DeepSeekAiCoachProvider>();
        services.AddHttpClient<GenericOpenAiCoachProvider>();
        services.AddScoped<IAiCoachProvider, ConfiguredAiCoachProvider>();
        services.AddScoped<CoachFlowService>();
        services.AddSingleton<ISecretStatusService, EnvironmentSecretStatusService>();
        services.AddSingleton<ISecretResolver, EnvironmentSecretResolver>();
        services.AddSingleton<StockfishEngine>();
        services.AddSingleton<IChessEngine>(provider => provider.GetRequiredService<StockfishEngine>());
        services.AddSingleton<IEngineHealthService>(provider => provider.GetRequiredService<StockfishEngine>());
        services.AddSingleton<INpcRandom, SystemNpcRandom>();
        services.AddScoped<INpcMoveSelector, OpponentMoveSelector>();
        services.AddScoped<IAiProviderSettingsStore, EfAiProviderSettingsStore>();
        services.AddScoped<IGameReviewRepository, EfGameReviewRepository>();
        services.AddScoped<GameReviewService>();
        return services;
    }

    internal static bool IsTransient(Exception? exception, HttpResponseMessage? response) =>
        exception is HttpRequestException or TimeoutRejectedException ||
        response?.StatusCode is System.Net.HttpStatusCode.TooManyRequests ||
        (int?)response?.StatusCode >= 500;
}
