namespace BlunderForge.Domain.Games;

public sealed record MoveRecord(
    int Ply,
    Side Side,
    string San,
    UciMove Uci,
    string FenBefore,
    string FenAfter);
