using Chess;

namespace BlunderForge.Domain.Games;

internal static class ChessLibraryMapping
{
    public static Side ToDomainSide(this PieceColor color)
    {
        return color == PieceColor.White ? Side.White : Side.Black;
    }

    public static PieceColor ToLibraryColor(this Side side)
    {
        return side == Side.White ? PieceColor.White : PieceColor.Black;
    }

    public static GameResult ToDomainResult(this PieceColor winningSide)
    {
        return winningSide == PieceColor.White ? GameResult.WhiteWin : GameResult.BlackWin;
    }

    public static GameTerminationReason ToDomainTerminationReason(this EndgameType type)
    {
        return type switch
        {
            EndgameType.Checkmate => GameTerminationReason.Checkmate,
            EndgameType.Resigned => GameTerminationReason.Resignation,
            EndgameType.Stalemate => GameTerminationReason.Stalemate,
            EndgameType.InsufficientMaterial => GameTerminationReason.InsufficientMaterial,
            EndgameType.FiftyMoveRule => GameTerminationReason.FiftyMoveRule,
            EndgameType.Repetition => GameTerminationReason.ThreefoldRepetition,
            EndgameType.DrawDeclared => GameTerminationReason.Stalemate,
            EndgameType.Timeout => GameTerminationReason.None,
            _ => GameTerminationReason.None
        };
    }

    public static PieceType ToLibraryPieceType(this PromotionPiece piece)
    {
        return piece switch
        {
            PromotionPiece.Queen => PieceType.Queen,
            PromotionPiece.Rook => PieceType.Rook,
            PromotionPiece.Bishop => PieceType.Bishop,
            PromotionPiece.Knight => PieceType.Knight,
            _ => throw new ArgumentOutOfRangeException(nameof(piece), piece, "Unsupported promotion piece.")
        };
    }
}
