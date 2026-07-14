using BlunderForge.Application.Engine;
using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Coaching;

public interface IMoveAnalysisRepository
{
    Task SaveAsync(
        Guid gameId,
        MoveRecord move,
        int? evaluationBefore,
        int? evaluationAfter,
        int centipawnLoss,
        MoveClassification classification,
        EngineAnalysisResult analysis,
        CancellationToken cancellationToken);
}
