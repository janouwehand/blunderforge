namespace BlunderForge.Application.Engine;

public sealed record EngineHealthResult(
    bool IsReady,
    string Status,
    string Detail,
    string? EngineVersion);
