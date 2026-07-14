using BlunderForge.Application.Configuration;
using BlunderForge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlunderForge.Infrastructure.Ai;

internal sealed class DeepSeekAiCoachProvider(HttpClient client, IOptions<AiProviderOptions> options, BlunderForge.Application.Ai.IAiProviderSettingsStore settings, ISecretResolver secrets, AiResiliencePipelineProvider resilience, ILogger<DeepSeekAiCoachProvider> logger)
    : OpenAiCompatibleCoachProviderBase(client, options, settings, secrets, resilience, logger, true);
