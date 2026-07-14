namespace BlunderForge.Domain.Games;

public sealed record GameSettings(
    PlayerColorChoice PlayerColorChoice,
    Side PlayerSide,
    OpponentElo OpponentElo);
