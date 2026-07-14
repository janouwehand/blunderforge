using Chess;

namespace BlunderForge.Domain.Games;

public sealed class ChessGame
{
    public const string StandardInitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly ChessBoard board;
    private readonly List<MoveRecord> moves;

    private ChessGame(
        Guid id,
        GameSettings settings,
        ChessBoard board,
        IEnumerable<MoveRecord>? moves = null,
        GameStatus? status = null,
        GameResult? result = null,
        GameTerminationReason? terminationReason = null,
        uint version = 0)
    {
        Id = id;
        Settings = settings;
        this.board = board;
        this.moves = moves?.OrderBy(move => move.Ply).ToList() ?? [];
        Status = status ?? GameStatus.Active;
        Result = result ?? GameResult.None;
        TerminationReason = terminationReason ?? GameTerminationReason.None;
        Version = version;

        if (status is null)
        {
            SyncTerminalState();
        }
    }

    public Guid Id { get; }

    public GameSettings Settings { get; }

    public uint Version { get; }

    public GameStatus Status { get; private set; }

    public GameResult Result { get; private set; }

    public GameTerminationReason TerminationReason { get; private set; }

    public Side ActiveSide => board.Turn.ToDomainSide();

    public string CurrentFen => board.ToFen();

    public IReadOnlyList<MoveRecord> Moves => moves;

    public static ChessGame Start(GameSettings settings)
    {
        var board = new ChessBoard
        {
            AutoEndgameRules = AutoEndgameRules.All
        };

        return new ChessGame(Guid.NewGuid(), settings, board);
    }

    public static ChessGame FromFen(Guid id, GameSettings settings, string fen)
    {
        if (!ChessBoard.TryLoadFromFen(fen, out var board, AutoEndgameRules.All))
        {
            throw new ArgumentException("FEN is not a valid standard chess position.", nameof(fen));
        }

        return new ChessGame(id, settings, board);
    }

    public static ChessGame Rehydrate(
        Guid id,
        GameSettings settings,
        string currentFen,
        IEnumerable<MoveRecord> moves,
        GameStatus status,
        GameResult result,
        GameTerminationReason terminationReason,
        uint version = 0)
    {
        if (!ChessBoard.TryLoadFromFen(currentFen, out var board, AutoEndgameRules.All))
        {
            throw new ArgumentException("FEN is not a valid standard chess position.", nameof(currentFen));
        }

        return new ChessGame(id, settings, board, moves, status, result, terminationReason, version);
    }

    public bool IsLegalMove(UciMove move)
    {
        if (Status is not GameStatus.Active)
        {
            return false;
        }

        return TryFindLegalMove(move, out _);
    }

    public MoveResult ApplyMove(UciMove move)
    {
        if (Status is not GameStatus.Active)
        {
            throw new InvalidOperationException("Cannot apply a move to a game that is not active.");
        }

        var fenBefore = CurrentFen;
        var side = ActiveSide;

        if (!TryFindLegalMove(move, out var legalMove))
        {
            throw new InvalidOperationException($"Move '{move}' is not legal in the current position.");
        }

        if (!board.Move(legalMove!))
        {
            throw new InvalidOperationException($"Move '{move}' is not legal in the current position.");
        }

        var executedMove = board.ExecutedMoves[^1];
        var record = new MoveRecord(
            moves.Count + 1,
            side,
            executedMove.San ?? move.Value,
            move,
            fenBefore,
            CurrentFen);

        moves.Add(record);
        SyncTerminalState();

        return new MoveResult(record, ToState());
    }

    public void Resign(Side side)
    {
        if (Status is not GameStatus.Active)
        {
            throw new InvalidOperationException("Cannot resign a game that is not active.");
        }

        board.Resign(side.ToLibraryColor());
        Status = GameStatus.Resigned;
        Result = side is Side.White ? GameResult.BlackWin : GameResult.WhiteWin;
        TerminationReason = GameTerminationReason.Resignation;
    }

    public GameState ToState()
    {
        return new GameState(
            Id,
            Settings,
            Status,
            Result,
            TerminationReason,
            ActiveSide,
            CurrentFen,
            moves.ToArray(),
            board.WhiteKingChecked,
            board.BlackKingChecked);
    }

    private void SyncTerminalState()
    {
        if (!board.IsEndGame)
        {
            Status = GameStatus.Active;
            Result = GameResult.None;
            TerminationReason = GameTerminationReason.None;

            return;
        }

        var endGame = board.EndGame ?? throw new InvalidOperationException("Chess board reported an endgame without endgame details.");
        TerminationReason = endGame.EndgameType.ToDomainTerminationReason();
        Result = endGame.EndgameType is EndgameType.Checkmate or EndgameType.Resigned or EndgameType.Timeout
            ? (endGame.WonSide ?? throw new InvalidOperationException("Decisive endgame has no winning side.")).ToDomainResult()
            : GameResult.Draw;
        Status = endGame.EndgameType is EndgameType.Resigned ? GameStatus.Resigned : GameStatus.Completed;
    }

    /// <summary>Returns all legal moves in the current position using Gera.Chess built-in generation (O(n) where n = ~30-40 moves).</summary>
    public IReadOnlyList<LegalMoveInfo> GetLegalMoves()
    {
        var result = new List<LegalMoveInfo>();
        foreach (var candidate in board.Moves(false, true))
        {
            var from = candidate.OriginalPosition.ToString().ToLowerInvariant();
            var to = candidate.NewPosition.ToString().ToLowerInvariant();
            string? promotion = null;
            if (candidate.IsPromotion && candidate.Promotion is not null)
            {
                promotion = ToPromotionChar(candidate.Promotion.Type);
            }
            result.Add(new LegalMoveInfo(from, to, promotion));
        }
        return result;
    }

    public sealed record LegalMoveInfo(string From, string To, string? Promotion);

    private bool TryFindLegalMove(UciMove uciMove, out Move? legalMove)
    {
        foreach (var candidate in board.Moves(false, true))
        {
            if (candidate.OriginalPosition.ToString().Equals(uciMove.From, StringComparison.OrdinalIgnoreCase)
                && candidate.NewPosition.ToString().Equals(uciMove.To, StringComparison.OrdinalIgnoreCase)
                && PromotionMatches(candidate, uciMove.Promotion))
            {
                legalMove = candidate;
                return true;
            }
        }

        legalMove = null;
        return false;
    }

    private static bool PromotionMatches(Move candidate, PromotionPiece? promotion)
    {
        if (promotion is null)
        {
            return !candidate.IsPromotion;
        }

        return candidate.IsPromotion
            && candidate.Promotion is not null
            && candidate.Promotion.Type == promotion.Value.ToLibraryPieceType();
    }

    private static string? ToPromotionChar(PieceType type)
    {
        if (type == PieceType.Queen) return "q";
        if (type == PieceType.Rook) return "r";
        if (type == PieceType.Bishop) return "b";
        if (type == PieceType.Knight) return "n";
        return "q";
    }
}
