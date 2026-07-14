namespace BlunderForge.Application.Engine;

public interface IChessEngine
{
    Task<EngineAnalysisResult> AnalyzeAsync(EngineAnalysisRequest request, CancellationToken cancellationToken);
}
