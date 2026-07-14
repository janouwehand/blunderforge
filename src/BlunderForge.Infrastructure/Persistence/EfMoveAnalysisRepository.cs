using System.Text.Json;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Engine;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlunderForge.Infrastructure.Persistence;

internal sealed class EfMoveAnalysisRepository(BlunderForgeDbContext db) : IMoveAnalysisRepository
{
    public async Task SaveAsync(
        Guid gameId,
        MoveRecord move,
        int? evaluationBefore,
        int? evaluationAfter,
        int centipawnLoss,
        MoveClassification classification,
        EngineAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var storedMove = await db.Moves.SingleAsync(
            candidate => candidate.GameId == gameId && candidate.Ply == move.Ply,
            cancellationToken);
        if (await db.MoveAnalyses.AnyAsync(candidate => candidate.MoveId == storedMove.Id, cancellationToken))
        {
            return;
        }

        db.MoveAnalyses.Add(new MoveAnalysisEntity
        {
            MoveId = storedMove.Id,
            EngineVersion = analysis.EngineVersion,
            EngineSettingsJson = JsonSerializer.Serialize(analysis.Settings),
            EvaluationBefore = evaluationBefore,
            EvaluationAfter = evaluationAfter,
            CentipawnLoss = centipawnLoss,
            Classification = classification.ToString(),
            BestMoveUci = analysis.Candidates.Count == 0 ? null : analysis.BestMove.Move.Value,
            CandidateMovesJson = JsonSerializer.Serialize(analysis.Candidates.Select(candidate => candidate.Move.Value).Take(8)),
            PrincipalVariationJson = JsonSerializer.Serialize(analysis.Candidates.Count == 0 ? [] : analysis.BestMove.PrincipalVariation.Select(move => move.Value).Take(8)),
            IsCritical = classification is MoveClassification.Inaccuracy or MoveClassification.Mistake or MoveClassification.Blunder,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
