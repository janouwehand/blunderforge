using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;

namespace BlunderForge.ApplicationTests;

public sealed class AiProviderTests
{
    [Fact]
    public void ValidatesOnlyBoundedHintAndExplanation()
    {
        var value = AiResponseValidator.ValidateMoveHelp("""{"hint":"Consider a forcing move.","explanation":"The engine move improves piece activity."}""");
        Assert.Equal("Consider a forcing move.", value.Hint);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"hint\":\"x\"}")]
    [InlineData("{\"hint\":\"x\",\"explanation\":\"y\",\"arrows\":[]}")]
    public void RejectsMalformedMissingOrVisualFields(string json) => Assert.Throws<AiResponseValidationException>(() => AiResponseValidator.ValidateMoveHelp(json));

    [Fact]
    public void PromptUsesOpponentEloAndProhibitsVisualInstructions()
    {
        var request = AiPromptTemplates.MoveHelp(new CompactEngineContext("fen", 800, "e2e4", ["e2e4"], 25, ["e2e4"]));
        Assert.Equal("coach-v4", request.PromptVersion);
        Assert.Contains("\"opponentElo\":800", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("never determine", request.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("arrow", request.UserPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
