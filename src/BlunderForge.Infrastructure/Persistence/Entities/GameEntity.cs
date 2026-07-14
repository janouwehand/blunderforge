namespace BlunderForge.Infrastructure.Persistence.Entities;

internal sealed class GameEntity
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PlayerColorChoice { get; set; } = string.Empty;

    public string PlayerSide { get; set; } = string.Empty;

    public int OpponentElo { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Result { get; set; } = string.Empty;

    public string TerminationReason { get; set; } = string.Empty;

    public string InitialFen { get; set; } = string.Empty;

    public string CurrentFen { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public uint Version { get; set; }

    public ICollection<MoveEntity> Moves { get; } = new List<MoveEntity>();

    public GameReviewEntity? Review { get; set; }
}
