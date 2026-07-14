using BlunderForge.Application.Reviews;

namespace BlunderForge.Web.Contracts;

public sealed record StartGameRequestDto(string PlayerColorChoice, int OpponentElo = 800);

public sealed record GameStateDto(
    string GameId,
    GameSettingsDto Settings,
    string Status,
    string Result,
    string TerminationReason,
    string ActiveSide,
    string CurrentFen,
    List<MoveRecordDto> Moves,
    bool WhiteKingInCheck,
    bool BlackKingInCheck);

public sealed record GameSettingsDto(string PlayerColorChoice, string PlayerSide, int OpponentElo);
public sealed record MoveRecordDto(int Ply, string Side, string San, string Uci, string FenBefore, string FenAfter);
public sealed record SubmitMoveRequestDto(string Uci);
public sealed record MoveResultDto(MoveRecordDto Move, GameStateDto State, bool OpponentMovePending = false);
public sealed record LegalMoveDto(string Uci, string From, string To, string? Promotion);
public sealed record LegalMovesResponseDto(string CurrentFen, List<LegalMoveDto> Moves);
public sealed record ResignRequestDto(string Side);
public sealed record CoachRequestDto(bool UseAiExplanation);
public sealed record CoachArrowDto(string From, string To);
public sealed record CoachHelpDto(
    string RecommendedMove,
    string RecommendedMoveUci,
    string TextAlternative,
    IReadOnlyList<string> HighlightSquares,
    CoachArrowDto Arrow,
    string? Hint,
    string? Explanation,
    string? AiStatus);
public sealed record GameHistoryItemDto(string GameId, DateTimeOffset Date, string Result, string PlayerColor, int OpponentElo);
public sealed record GameHistoryPageDto(IReadOnlyList<GameHistoryItemDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record HistoricalGameDto(
    string GameId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string Result,
    string PlayerColor,
    int OpponentElo,
    string InitialFen,
    IReadOnlyList<MoveRecordDto> Moves,
    GameReview? Review);
public sealed record ErrorResponseDto(string Error, string Detail, string? CorrelationId);
