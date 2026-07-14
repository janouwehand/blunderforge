namespace BlunderForge.Infrastructure.Persistence.Entities;

internal sealed class MoveAnalysisEntity
{
    public long Id { get; set; }

    public long MoveId { get; set; }

    public MoveEntity? Move { get; set; }

    public string EngineVersion { get; set; } = string.Empty;

    public string EngineSettingsJson { get; set; } = "{}";

    public int? EvaluationBefore { get; set; }

    public int? EvaluationAfter { get; set; }

    public int? CentipawnLoss { get; set; }

    public string Classification { get; set; } = string.Empty;

    public string? BestMoveUci { get; set; }

    public string CandidateMovesJson { get; set; } = "[]";

    public string PrincipalVariationJson { get; set; } = "[]";

    public bool IsCritical { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
