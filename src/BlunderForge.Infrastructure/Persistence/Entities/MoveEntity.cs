namespace BlunderForge.Infrastructure.Persistence.Entities;

internal sealed class MoveEntity
{
    public long Id { get; set; }

    public Guid GameId { get; set; }

    public GameEntity? Game { get; set; }

    public int Ply { get; set; }

    public string Color { get; set; } = string.Empty;

    public string San { get; set; } = string.Empty;

    public string Uci { get; set; } = string.Empty;

    public string FenBefore { get; set; } = string.Empty;

    public string FenAfter { get; set; } = string.Empty;

    public bool IsOpponentMove { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public MoveAnalysisEntity? Analysis { get; set; }
}
