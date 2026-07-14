namespace BlunderForge.Infrastructure.Persistence.Entities;

internal sealed class GameReviewEntity
{
    public Guid GameId { get; set; }
    public GameEntity? Game { get; set; }
    public string Result { get; set; } = string.Empty;
    public string OverallQuality { get; set; } = string.Empty;
    public string CriticalMovesJson { get; set; } = "[]";
    public string WentWell { get; set; } = string.Empty;
    public string FutureFocus { get; set; } = string.Empty;
    public bool UsedAi { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
