using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Configuration;
using BlunderForge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BlunderForge.Infrastructure.Ai;

internal abstract partial class OpenAiCompatibleCoachProviderBase(
    HttpClient httpClient,
    IOptions<AiProviderOptions> options,
    IAiProviderSettingsStore settingsStore,
    ISecretResolver secretResolver,
    AiResiliencePipelineProvider resilience,
    ILogger logger,
    bool deepSeek) : IAiCoachProvider
{
    private readonly AiProviderOptions _options = options.Value;

    public Task<AiCoachExplanation> GenerateMoveHelpAsync(AiCoachRequest request, CancellationToken cancellationToken) =>
        SendConfiguredAsync(request, false, AiResponseValidator.ValidateMoveHelp, cancellationToken);

    public Task<GameReviewResponse> GenerateGameReviewAsync(AiCoachRequest request, CancellationToken cancellationToken) =>
        SendConfiguredAsync(request, true, AiResponseValidator.ValidateGameReview, cancellationToken);

    public async Task<ProviderStatus> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configured = await GetSettingsAsync(cancellationToken);
            using var response = await SendWithResilienceAsync(
                () => CreateRequest(HttpMethod.Get, "models", configured.BaseUrl), configured, cancellationToken);
            return response.IsSuccessStatusCode
                ? new ProviderStatus(true, "Connection succeeded.")
                : new ProviderStatus(false, SafeFailure(response.StatusCode));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or AiProviderException or TimeoutRejectedException or BrokenCircuitException)
        {
            return new ProviderStatus(false, "Provider is unreachable.");
        }
    }

    private async Task<T> SendConfiguredAsync<T>(AiCoachRequest request, bool review, Func<string, T> validate, CancellationToken cancellationToken)
    {
        var configured = await GetSettingsAsync(cancellationToken);
        var model = review ? configured.ReviewModel : configured.InteractiveModel;
        var payload = new ChatCompletionRequest(model,
            [
                new ChatMessage("system", request.SystemPrompt),
                new ChatMessage("user", request.UserPrompt)
            ], new ResponseFormat("json_object"));

        var started = DateTimeOffset.UtcNow;
        // Read headers first, then explicitly bound body deserialization. Relying on
        // HttpClient buffering would move body reading outside the resilience handler
        // while preventing this provider-level timeout from taking effect.
        var attempts = 0;
        using var response = await SendWithResilienceAsync(() =>
        {
            var message = CreateRequest(HttpMethod.Post, "chat/completions", configured.BaseUrl);
            message.Content = JsonContent.Create(payload);
            return message;
        }, configured, cancellationToken, () => attempts++);
        if (!response.IsSuccessStatusCode) throw new AiProviderException(SafeFailure(response.StatusCode), response.StatusCode);
        using var bodyTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bodyTimeout.CancelAfter(TimeSpan.FromSeconds(configured.TimeoutSeconds));
        ChatCompletionResponse responsePayload;
        try
        {
            responsePayload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: bodyTimeout.Token)
                ?? throw new AiResponseValidationException("Provider response is empty.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Provider response body timed out.", exception);
        }
        var content = responsePayload.Choices.Count > 0
            ? responsePayload.Choices[0].Message.Content
            : throw new AiResponseValidationException("Provider response has no assistant content.");
        if (logger.IsEnabled(LogLevel.Information))
        {
            var retryCount = Math.Max(0, attempts - 1);
            LogAiOperation(logger, "GenerateCoachContent", deepSeek ? "DeepSeek" : "OpenAICompatible", model,
                request.PromptVersion, request.CallType, (DateTimeOffset.UtcNow - started).TotalMilliseconds,
                "Success", retryCount, responsePayload.Usage?.TotalTokens, null);
        }
        return validate(content);
    }

    private async Task<HttpResponseMessage> SendWithResilienceAsync(
        Func<HttpRequestMessage> requestFactory,
        StoredAiProviderSettings settings,
        CancellationToken cancellationToken,
        Action? onAttempt = null)
    {
        return await resilience.Get(settings).ExecuteAsync(async token =>
        {
            onAttempt?.Invoke();
            using var message = requestFactory();
            return await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        }, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string? baseUrl)
    {
        var secret = secretResolver.Resolve(deepSeek ? _options.DeepSeekApiKey : _options.OpenAiCompatibleApiKey);
        if (string.IsNullOrWhiteSpace(secret)) throw new AiProviderException("The required provider secret is unavailable.", HttpStatusCode.Unauthorized);
        if (baseUrl is null) throw new AiProviderException("Provider base URL is missing.", HttpStatusCode.BadRequest);
        var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return request;
    }

    private async Task<StoredAiProviderSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await settingsStore.GetAsync(cancellationToken) ?? new StoredAiProviderSettings(_options.Provider, _options.BaseUrl, _options.InteractiveModel, _options.ReviewModel, _options.TimeoutSeconds, _options.MaxRetryCount);

    private static string SafeFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Provider authentication failed.",
        HttpStatusCode.TooManyRequests => "Provider rate limit reached.",
        _ when (int)statusCode >= 500 => "Provider is temporarily unavailable.",
        _ => $"Provider request was rejected ({(int)statusCode})."
    };

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);
    private sealed record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record ResponseFormat([property: JsonPropertyName("type")] string Type);
    private sealed record ChatCompletionResponse([property: JsonPropertyName("choices")] IReadOnlyList<Choice> Choices, [property: JsonPropertyName("usage")] Usage? Usage);
    private sealed record Choice([property: JsonPropertyName("message")] ChatMessage Message);
    private sealed record Usage([property: JsonPropertyName("total_tokens")] int TotalTokens);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, SkipEnabledCheck = true, Message = "AI operation {OperationName} completed for provider {Provider}, model {Model}, prompt {PromptVersion}, type {CallType}, duration {DurationMs}ms, outcome {OutcomeStatus}, retries {RetryCount}, tokens {TokenCount}, cost {ReportedCost}")]
    private static partial void LogAiOperation(ILogger logger, string operationName, string provider, string model,
        string promptVersion, AiCoachCallType callType, double durationMs, string outcomeStatus, int retryCount,
        int? tokenCount, decimal? reportedCost);
}

public sealed class AiProviderException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
