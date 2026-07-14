using BlunderForge.Application.Coaching;

namespace BlunderForge.Application.Ai;

public enum AiCoachCallType { MoveHelp, GameReview }

public sealed record AiCoachRequest(AiCoachCallType CallType, string PromptVersion, string SystemPrompt, string UserPrompt);

public sealed record GameReviewRequest(string Result, IReadOnlyList<CompactEngineContext> CriticalMoves, int OpponentElo);

public sealed record GameReviewResponse(string Summary, IReadOnlyList<string> LearningMoments, string Focus);

public sealed record ProviderStatus(bool Available, string Detail);

public interface IAiCoachProvider
{
    Task<AiCoachExplanation> GenerateMoveHelpAsync(AiCoachRequest request, CancellationToken cancellationToken);
    Task<GameReviewResponse> GenerateGameReviewAsync(AiCoachRequest request, CancellationToken cancellationToken);
    Task<ProviderStatus> TestConnectionAsync(CancellationToken cancellationToken);
}

public interface IAiProviderSettingsStore
{
    Task<StoredAiProviderSettings?> GetAsync(CancellationToken cancellationToken);
    Task SetAsync(StoredAiProviderSettings settings, CancellationToken cancellationToken);
}

public sealed record StoredAiProviderSettings(string Provider, string? BaseUrl, string InteractiveModel, string ReviewModel, int TimeoutSeconds, int MaxRetryCount);
