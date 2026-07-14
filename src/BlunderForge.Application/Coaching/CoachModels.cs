using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Coaching;

public sealed record CompactEngineContext(
    string Fen,
    int OpponentElo,
    string RecommendedMoveUci,
    IReadOnlyList<string> CandidateMoves,
    int? EvaluationCentipawns,
    IReadOnlyList<string> PrincipalVariation);

public sealed record CoachArrow(string From, string To);

public sealed record AiCoachExplanation(string Hint, string Explanation);

public sealed record CoachHelpResult(
    string RecommendedMove,
    string RecommendedMoveUci,
    string TextAlternative,
    IReadOnlyList<string> HighlightSquares,
    CoachArrow Arrow,
    string? Hint,
    string? Explanation,
    string? AiStatus);

public sealed record AnalyzedMoveResult(
    MoveRecord Move,
    GameState State,
    CompactEngineContext Context,
    bool OpponentMovePending);
