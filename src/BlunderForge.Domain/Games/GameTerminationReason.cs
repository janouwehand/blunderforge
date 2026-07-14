namespace BlunderForge.Domain.Games;

public enum GameTerminationReason
{
    None = 0,
    Checkmate = 1,
    Stalemate = 2,
    ThreefoldRepetition = 3,
    FiftyMoveRule = 4,
    InsufficientMaterial = 5,
    Resignation = 6
}
