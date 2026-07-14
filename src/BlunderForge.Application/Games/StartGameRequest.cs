using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Games;

public sealed record StartGameRequest(
    PlayerColorChoice PlayerColorChoice,
    OpponentElo OpponentElo);
