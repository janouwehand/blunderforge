namespace BlunderForge.Application.Engine;

public sealed record EngineSettingsSnapshot(
    string EnginePath,
    int Threads,
    int HashSizeMb,
    int MultiPv,
    int MoveTimeMs,
    bool UciLimitStrength,
    int? UciElo);
