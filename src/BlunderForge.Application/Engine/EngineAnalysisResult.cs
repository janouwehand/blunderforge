namespace BlunderForge.Application.Engine;

public sealed record EngineAnalysisResult(
    string EngineVersion,
    EngineSettingsSnapshot Settings,
    IReadOnlyList<CandidateMove> Candidates)
{
    public CandidateMove BestMove => Candidates.Count > 0
        ? Candidates.OrderBy(candidate => candidate.Rank).First()
        : throw new InvalidOperationException("Engine analysis did not return any candidate moves.");
}
