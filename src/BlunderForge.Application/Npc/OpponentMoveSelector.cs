using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Domain.Games;
using Microsoft.Extensions.Options;

namespace BlunderForge.Application.Npc;

public sealed class OpponentMoveSelector(
    IChessEngine engine,
    INpcRandom random,
    IOptions<StockfishOptions> options) : INpcMoveSelector
{
    public async Task<NpcMoveSelection> SelectMoveAsync(GameState game, CancellationToken cancellationToken)
    {
        if (game.Status is not GameStatus.Active)
        {
            throw new InvalidOperationException("Cannot select an opponent move for a game that is not active.");
        }

        var elo = game.Settings.OpponentElo;
        var request = elo.UsesNativeStockfishLimit
            ? new EngineAnalysisRequest(game.CurrentFen, options.Value.OpponentMoveTimeMs, 1, elo.Value)
            : new EngineAnalysisRequest(game.CurrentFen, options.Value.OpponentMoveTimeMs, Math.Max(8, options.Value.LowEloMultiPv));
        var analysis = await engine.AnalyzeAsync(request, cancellationToken);
        var selected = elo.UsesNativeStockfishLimit
            ? analysis.BestMove
            : SelectCalibratedCandidate(analysis.Candidates, elo.Value, game.ActiveSide, random.NextDouble());

        return new NpcMoveSelection(selected.Move, "Stockfish", analysis.EngineVersion, analysis.Settings, analysis.Candidates);
    }

    public bool ShouldResign(GameState game, int evaluationCentipawnsFromWhite) => false;

    public static CandidateMove SelectCalibratedCandidate(
        IReadOnlyList<CandidateMove> candidates,
        int elo,
        Side sideToMove,
        double randomValue)
    {
        _ = new OpponentElo(elo);
        if (elo >= OpponentElo.NativeStockfishMinimum)
        {
            return candidates.OrderBy(candidate => candidate.Rank).FirstOrDefault()
                ?? throw new InvalidOperationException("Opponent move selection requires at least one engine candidate.");
        }

        var ordered = candidates.OrderBy(candidate => candidate.Rank).ToArray();
        if (ordered.Length == 0)
        {
            throw new InvalidOperationException("Opponent move selection requires at least one engine candidate.");
        }

        var strength = (elo - OpponentElo.Minimum) / (double)(OpponentElo.NativeStockfishMinimum - 1 - OpponentElo.Minimum);
        var maximumLoss = (int)Math.Round(650 - (500 * strength));
        var best = ordered[0];
        var plausible = ordered
            .Select(candidate => (candidate, loss: LossFromBest(best, candidate, sideToMove)))
            .Where(item => item.loss >= 0 && item.loss <= maximumLoss)
            .ToArray();

        if (plausible.Length == 0)
        {
            return best;
        }

        var weights = plausible.Select(item =>
        {
            var strongPreference = Math.Exp(-(strength * item.loss) / 55.0);
            var weakPreference = 1.0 + ((1.0 - strength) * item.loss / 90.0);
            return (item.candidate, weight: Math.Max(0.0001, strongPreference * weakPreference));
        }).ToArray();
        var total = weights.Sum(item => item.weight);
        var roll = Math.Clamp(randomValue, 0, 0.999999999) * total;
        var cumulative = 0.0;
        foreach (var item in weights)
        {
            cumulative += item.weight;
            if (roll < cumulative)
            {
                return item.candidate;
            }
        }

        return weights[^1].candidate;
    }

    private static int LossFromBest(CandidateMove best, CandidateMove candidate, Side sideToMove)
    {
        var bestScore = best.Score.ToWhiteCentipawnEstimate();
        var candidateScore = candidate.Score.ToWhiteCentipawnEstimate();
        return Math.Max(0, sideToMove is Side.White ? bestScore - candidateScore : candidateScore - bestScore);
    }
}
