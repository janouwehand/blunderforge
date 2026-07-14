using System.Text.Json;
using BlunderForge.Application.Coaching;

namespace BlunderForge.Application.Ai;

public static class AiPromptTemplates
{
    public const string Version = "coach-v4";
    public const string SystemPrompt = """
        You are a concise chess coach. Use only the supplied Stockfish context; never determine legality, evaluation, the best move, squares, arrows, or other visual instructions yourself. Adjust vocabulary and detail to opponentElo.
        Return JSON with exactly two string fields: {"hint":"…","explanation":"…"}.
        hint must be at most 200 characters and explanation at most 1000 characters. Do not include HTML or additional fields.
        """;
    public const string ReviewSystemPrompt = """
        Provide a concise game review based on Stockfish context. Never change the result, evaluation, or best move.
        Return JSON with exactly these fields: {"summary":"…","learningMoments":["…"],"focus":"…"}. Use at most 3 learning moments. summary≤100 characters, each moment≤300 characters, focus≤300 characters. Do not include HTML or additional fields.
        """;

    public static AiCoachRequest MoveHelp(CompactEngineContext context) => Build(AiCoachCallType.MoveHelp, new
    {
        task = "move-help",
        context
    });

    public static AiCoachRequest GameReview(GameReviewRequest request) =>
        new(AiCoachCallType.GameReview, Version, ReviewSystemPrompt, JsonSerializer.Serialize(new { task = "game-review", context = request }, JsonSerializerOptions.Web));

    private static AiCoachRequest Build(AiCoachCallType type, object payload) =>
        new(type, Version, SystemPrompt, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web));
}
