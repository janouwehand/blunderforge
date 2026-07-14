namespace BlunderForge.Application.Coaching;

public sealed class MoveClassificationOptions
{
    public const string SectionName = "BlunderForge:Classification";

    public int ExcellentMaxCentipawnLoss { get; init; } = 20;

    public int GoodMaxCentipawnLoss { get; init; } = 50;

    public int InaccuracyMaxCentipawnLoss { get; init; } = 100;

    public int MistakeMaxCentipawnLoss { get; init; } = 250;
}
