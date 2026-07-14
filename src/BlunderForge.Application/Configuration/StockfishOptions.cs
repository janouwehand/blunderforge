namespace BlunderForge.Application.Configuration;

public sealed class StockfishOptions
{
    public const string SectionName = "BlunderForge:Stockfish";

    public string Path { get; init; } = "/app/stockfish/stockfish";

    /// <summary>Analysis time in ms for post-move position evaluation. Default 800 ms is sufficient for accurate centipawn-loss calculation.</summary>
    public int AnalysisTimeMs { get; init; } = 800;

    /// <summary>Fast analysis time in ms for pre-move evaluation. We only need the best move and approximate score.</summary>
    public int QuickAnalysisTimeMs { get; init; } = 250;

    public int Threads { get; init; } = 1;

    public int HashSizeMb { get; init; } = 128;

    public int MultiPv { get; init; } = 8;

    public int OpponentMoveTimeMs { get; init; } = 500;

    public int LowEloMultiPv { get; init; } = 8;

    public int ProtocolTimeoutMs { get; init; } = 5000;
}
