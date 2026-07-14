using System.Net;
using System.Net.Http.Headers;
using BlunderForge.Application.Ai;
using BlunderForge.Application.Configuration;
using BlunderForge.Infrastructure.Ai;
using BlunderForge.Infrastructure.Configuration;
using BlunderForge.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BlunderForge.InfrastructureTests;

public sealed class AiProviderTests
{
    [Fact] public async Task AiProviderUsesBearerSecretAndInteractiveModel()
    {
        var handler = new StubHandler(new Queue<HttpResponseMessage>([JsonResponse()]));
        var provider = Create(handler);
        var content = await provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "system", "compact"), CancellationToken.None);
        Assert.Equal("Hint", content.Hint);
        Assert.Equal("Bearer fake-secret", handler.Authorization);
        Assert.Contains("interactive-test", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("fake-secret", handler.Body, StringComparison.Ordinal);
    }

    [Fact] public async Task AiProviderAuthenticationFailureIsSanitizedAndNotRetriedByProvider()
    {
        var handler = new StubHandler(new Queue<HttpResponseMessage>([new HttpResponseMessage(HttpStatusCode.Unauthorized)]));
        var exception = await Assert.ThrowsAsync<AiProviderException>(() => Create(handler).GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), CancellationToken.None));
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain("fake-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiProviderRejectsMalformedMoveHelp()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"choices":[{"message":{"role":"assistant","content":"{\"hint\":\"Only a hint\"}"}}]}""", System.Text.Encoding.UTF8, "application/json")
        };
        await Assert.ThrowsAsync<AiResponseValidationException>(() => Create(new StubHandler(new Queue<HttpResponseMessage>([response])))
            .GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), default));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public void ResilienceRetriesOnlyTransientStatuses(HttpStatusCode status, bool expected) =>
        Assert.Equal(expected, DependencyInjection.IsTransient(null, new HttpResponseMessage(status)));

    [Fact] public async Task ResilienceRetriesHttp429OnceThenSucceeds()
    {
        var handler = new StubHandler(new Queue<HttpResponseMessage>([new(HttpStatusCode.TooManyRequests), JsonResponse()]));
        await WithEnvironmentSecret(async () =>
        {
            var provider = CreateThroughPipeline(handler, timeout: 2, retries: 1);
            await provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), CancellationToken.None);
            Assert.Equal(2, handler.Calls);
        });
    }

    [Fact] public async Task ResilienceTimeoutStopsAttempt()
    {
        var handler = new DelayingHandler();
        await WithEnvironmentSecret(async () =>
        {
            var provider = CreateThroughPipeline(handler, timeout: 1, retries: 0);
            await Assert.ThrowsAsync<TimeoutRejectedException>(() => provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), CancellationToken.None));
        });
    }

    [Fact] public async Task ProviderTimeoutIncludesResponseBody()
    {
        var handler = new StallingBodyHandler();
        await WithEnvironmentSecret(async () =>
        {
            var provider = CreateThroughPipeline(handler, timeout: 1, retries: 0);
            await Assert.ThrowsAsync<TimeoutException>(() => provider.GenerateGameReviewAsync(
                new AiCoachRequest(AiCoachCallType.GameReview, "v1", "s", "u"), CancellationToken.None));
        });
    }

    [Fact] public async Task ResilienceCircuitBreakerOpensAfterRepeatedTransientFailures()
    {
        var handler = new AlwaysFailingHandler();
        await WithEnvironmentSecret(async () =>
        {
            var provider = CreateThroughPipeline(handler, timeout: 1, retries: 0);
            for (var i = 0; i < 5; i++) await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), CancellationToken.None));
            await Assert.ThrowsAsync<BrokenCircuitException>(() => provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), CancellationToken.None));
        });
    }

    [Fact]
    public async Task RuntimeRetrySettingChangeAppliesWithoutRestart()
    {
        var handler = new StubHandler(new Queue<HttpResponseMessage>(
            [new(HttpStatusCode.TooManyRequests), new(HttpStatusCode.TooManyRequests), JsonResponse()]));
        var settings = new MutableSettings(new StoredAiProviderSettings(
            "DeepSeek", "https://stub.test", "interactive-test", "review-test", 2, 0));
        var provider = new DeepSeekAiCoachProvider(new HttpClient(handler), Options.Create(new AiProviderOptions()),
            settings, new FakeSecrets(), new AiResiliencePipelineProvider(), NullLogger<DeepSeekAiCoachProvider>.Instance);

        await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateMoveHelpAsync(
            new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), default));
        Assert.Equal(1, handler.Calls);

        settings.Value = settings.Value with { MaxRetryCount = 1 };
        await provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), default);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task RuntimeTimeoutSettingChangeAppliesWithoutRestart()
    {
        var handler = new FirstFastThenSlowHandler();
        var settings = new MutableSettings(new StoredAiProviderSettings(
            "DeepSeek", "https://stub.test", "interactive-test", "review-test", 5, 0));
        var provider = new DeepSeekAiCoachProvider(new HttpClient(handler), Options.Create(new AiProviderOptions()),
            settings, new FakeSecrets(), new AiResiliencePipelineProvider(), NullLogger<DeepSeekAiCoachProvider>.Instance);

        await provider.GenerateMoveHelpAsync(new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), default);
        settings.Value = settings.Value with { TimeoutSeconds = 1 };

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => provider.GenerateMoveHelpAsync(
            new AiCoachRequest(AiCoachCallType.MoveHelp, "v1", "s", "u"), default));
    }

    private static DeepSeekAiCoachProvider Create(HttpMessageHandler handler) => new(new HttpClient(handler), Options.Create(new AiProviderOptions { BaseUrl = "https://stub.test", InteractiveModel = "interactive-test", ReviewModel = "review-test" }), new FakeSettings(), new FakeSecrets(), new AiResiliencePipelineProvider(), NullLogger<DeepSeekAiCoachProvider>.Instance);
    private static DeepSeekAiCoachProvider CreateThroughPipeline(HttpMessageHandler handler, int timeout, int retries)
    {
        var options = new AiProviderOptions { BaseUrl = "https://stub.test", InteractiveModel = "interactive-test", ReviewModel = "review-test", TimeoutSeconds = timeout, MaxRetryCount = retries };
        var services = new ServiceCollection();
        services.AddLogging(); services.AddOptions(); services.AddSingleton<IOptions<AiProviderOptions>>(Options.Create(options));
        services.AddBlunderForgeInfrastructure("Data Source=:memory:", options);
        services.RemoveAll<IAiProviderSettingsStore>(); services.AddSingleton<IAiProviderSettingsStore>(new FakeSettings());
        services.AddHttpClient<DeepSeekAiCoachProvider>().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider().GetRequiredService<DeepSeekAiCoachProvider>();
    }

    private static async Task WithEnvironmentSecret(Func<Task> action)
    {
        const string name = "BLUNDERFORGE_DEEPSEEK_API_KEY";
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, "fake-secret");
        try { await action(); } finally { Environment.SetEnvironmentVariable(name, previous); }
    }
    private static HttpResponseMessage JsonResponse() => new(HttpStatusCode.OK) { Content = new StringContent("""{"choices":[{"message":{"role":"assistant","content":"{\"hint\":\"Hint\",\"explanation\":\"Explanation\"}"}}],"usage":{"total_tokens":12}}""", System.Text.Encoding.UTF8, "application/json") };

    private sealed class FakeSecrets : ISecretResolver { public string Resolve(SecretReference reference) => "fake-secret"; }
    private sealed class FakeSettings : IAiProviderSettingsStore { public Task<StoredAiProviderSettings?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<StoredAiProviderSettings?>(null); public Task SetAsync(StoredAiProviderSettings settings, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class MutableSettings(StoredAiProviderSettings value) : IAiProviderSettingsStore
    {
        public StoredAiProviderSettings Value { get; set; } = value;
        public Task<StoredAiProviderSettings?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<StoredAiProviderSettings?>(Value);
        public Task SetAsync(StoredAiProviderSettings settings, CancellationToken cancellationToken) { Value = settings; return Task.CompletedTask; }
    }
    private sealed class StubHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        public int Calls { get; private set; } public string? Authorization { get; private set; } public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; Authorization = request.Headers.Authorization?.ToString(); Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken); return responses.Dequeue(); }
    }
    private sealed class DelayingHandler : HttpMessageHandler { protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); return JsonResponse(); } }
    private sealed class FirstFastThenSlowHandler : HttpMessageHandler
    {
        private int calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1) return JsonResponse();
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return JsonResponse();
        }
    }
    private sealed class StallingBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StreamContent(new StallingStream());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class AlwaysFailingHandler : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)); }
}
