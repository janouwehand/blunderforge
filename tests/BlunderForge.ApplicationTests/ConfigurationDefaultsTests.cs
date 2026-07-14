using BlunderForge.Application.Configuration;

namespace BlunderForge.ApplicationTests;

public sealed class ConfigurationDefaultsTests
{
    [Fact]
    public void AiProviderDefaultsDoNotContainRealSecrets()
    {
        var options = new AiProviderOptions();

        Assert.Equal("DeepSeek", options.Provider);
        Assert.Equal("fake-interactive-model", options.InteractiveModel);
        Assert.Equal("fake-review-model", options.ReviewModel);
        Assert.Equal("BLUNDERFORGE_DEEPSEEK_API_KEY", options.DeepSeekApiKey.EnvironmentVariable);
        Assert.Equal("BLUNDERFORGE_DEEPSEEK_API_KEY_FILE", options.DeepSeekApiKey.FileEnvironmentVariable);
    }

    [Fact]
    public void StockfishDefaultsTargetContainerPath()
    {
        var options = new StockfishOptions();

        Assert.Equal("/app/stockfish/stockfish", options.Path);
        Assert.True(options.AnalysisTimeMs > 0);
    }
}
