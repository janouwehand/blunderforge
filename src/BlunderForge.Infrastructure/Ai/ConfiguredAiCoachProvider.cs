using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Configuration;
using Microsoft.Extensions.Options;

namespace BlunderForge.Infrastructure.Ai;

internal sealed class ConfiguredAiCoachProvider(DeepSeekAiCoachProvider deepSeek, GenericOpenAiCoachProvider generic, IOptions<AiProviderOptions> options, IAiProviderSettingsStore settings) : IAiCoachProvider
{
    private async Task<IAiCoachProvider> CurrentAsync(CancellationToken cancellationToken)
    {
        var provider = (await settings.GetAsync(cancellationToken))?.Provider ?? options.Value.Provider;
        return provider.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase) ? generic : deepSeek;
    }
    public async Task<AiCoachExplanation> GenerateMoveHelpAsync(AiCoachRequest request, CancellationToken cancellationToken) => await (await CurrentAsync(cancellationToken)).GenerateMoveHelpAsync(request, cancellationToken);
    public async Task<GameReviewResponse> GenerateGameReviewAsync(AiCoachRequest request, CancellationToken cancellationToken) => await (await CurrentAsync(cancellationToken)).GenerateGameReviewAsync(request, cancellationToken);
    public async Task<ProviderStatus> TestConnectionAsync(CancellationToken cancellationToken) => await (await CurrentAsync(cancellationToken)).TestConnectionAsync(cancellationToken);
}
