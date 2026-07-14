namespace BlunderForge.Web.Contracts;

public sealed record AiProviderSettingsDto(
    string Provider,
    string? BaseUrl,
    string InteractiveModel,
    string ReviewModel,
    int TimeoutSeconds,
    int MaxRetryCount,
    bool Configured,
    bool SecretAvailable,
    string SecretSource);

public sealed record UpdateAiProviderSettingsRequestDto(
    string? Provider,
    string? BaseUrl,
    string? InteractiveModel,
    string? ReviewModel,
    int? TimeoutSeconds,
    int? MaxRetryCount);
