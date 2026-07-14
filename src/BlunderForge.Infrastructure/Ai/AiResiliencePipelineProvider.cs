using System.Collections.Concurrent;
using BlunderForge.Application.Ai;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace BlunderForge.Infrastructure.Ai;

internal sealed class AiResiliencePipelineProvider
{
    private readonly ConcurrentDictionary<PipelineKey, ResiliencePipeline<HttpResponseMessage>> pipelines = new();

    public ResiliencePipeline<HttpResponseMessage> Get(StoredAiProviderSettings settings) =>
        pipelines.GetOrAdd(new PipelineKey(settings.TimeoutSeconds, settings.MaxRetryCount), Build);

    private static ResiliencePipeline<HttpResponseMessage> Build(PipelineKey key)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TimeSpan.FromSeconds((key.TimeoutSeconds * (key.MaxRetryCount + 1)) + 5));

        if (key.MaxRetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = key.MaxRetryCount,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return default;
                }
            });
        }

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
            SamplingDuration = TimeSpan.FromSeconds((key.TimeoutSeconds * 2) + 1),
            MinimumThroughput = 5,
            FailureRatio = 0.5,
            BreakDuration = TimeSpan.FromSeconds(30)
        });
        builder.AddTimeout(TimeSpan.FromSeconds(key.TimeoutSeconds));
        return builder.Build();
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
        DependencyInjection.IsTransient(outcome.Exception, outcome.Result);

    private readonly record struct PipelineKey(int TimeoutSeconds, int MaxRetryCount);
}
