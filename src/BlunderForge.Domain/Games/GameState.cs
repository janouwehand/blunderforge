namespace BlunderForge.Domain.Games;

public sealed record GameState(
    Guid GameId,
    GameSettings Settings,
    GameStatus Status,
    GameResult Result,
    GameTerminationReason TerminationReason,
    Side ActiveSide,
    string CurrentFen,
    IReadOnlyList<MoveRecord> Moves,
    bool WhiteKingInCheck,
    bool BlackKingInCheck);
