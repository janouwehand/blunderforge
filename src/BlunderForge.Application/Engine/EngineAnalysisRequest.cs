namespace BlunderForge.Application.Engine;

public sealed record EngineAnalysisRequest(
    string Fen,
    int MoveTimeMs,
    int MultiPv,
    int? UciElo = null);
