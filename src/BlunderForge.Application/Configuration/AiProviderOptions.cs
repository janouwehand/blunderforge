namespace BlunderForge.Application.Configuration;

public sealed class AiProviderOptions
{
    public const string SectionName = "BlunderForge:AiProvider";

    public string Provider { get; init; } = "DeepSeek";

    public string? BaseUrl { get; init; }

    public string InteractiveModel { get; init; } = "fake-interactive-model";

    public string ReviewModel { get; init; } = "fake-review-model";

    public int TimeoutSeconds { get; init; } = 30;

    public int MaxRetryCount { get; init; } = 1;

    public SecretReference DeepSeekApiKey { get; init; } = new("BLUNDERFORGE_DEEPSEEK_API_KEY", "BLUNDERFORGE_DEEPSEEK_API_KEY_FILE");

    public SecretReference OpenAiCompatibleApiKey { get; init; } = new("BLUNDERFORGE_OPENAI_COMPATIBLE_API_KEY", "BLUNDERFORGE_OPENAI_COMPATIBLE_API_KEY_FILE");
}
