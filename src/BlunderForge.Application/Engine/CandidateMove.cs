using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Engine;

public sealed record CandidateMove(
    UciMove Move,
    EngineScore Score,
    IReadOnlyList<UciMove> PrincipalVariation,
    int Rank);
