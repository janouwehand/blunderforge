using BlunderForge.Application.Ai;
using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Games;
using BlunderForge.Application.Npc;
using BlunderForge.Domain.Games;
using Microsoft.Extensions.Options;

namespace BlunderForge.Application.Coaching;

public sealed class CoachFlowService(
    GameSessionService gameSession,
    IChessEngine engine,
    INpcMoveSelector opponentMoveSelector,
    IMoveClassifier classifier,
    IMoveAnalysisRepository analyses,
    IAiCoachProvider aiProvider,
    IOptions<StockfishOptions> stockfishOptions)
{
    public async Task<MoveResult> SubmitPlayerMoveAsync(UciMove move, CancellationToken cancellationToken)
    {
        var state = await RequiredStateAsync(cancellationToken);
        if (state.ActiveSide != state.Settings.PlayerSide)
        {
            if (state.Moves.Count > 0 && state.Moves[^1].Side == state.Settings.PlayerSide && state.Moves[^1].Uci == move)
            {
                return new MoveResult(state.Moves[^1], state);
            }

            throw new InvalidOperationException("It is not the player's turn.");
        }

        return await gameSession.ApplyMoveAsync(move, cancellationToken);
    }

    public async Task<AnalyzedMoveResult> CompleteNpcTurnAsync(CancellationToken cancellationToken)
    {
        var state = await RequiredStateAsync(cancellationToken);
        if (state.Status == GameStatus.Active && state.ActiveSide == state.Settings.PlayerSide)
        {
            throw new InvalidOperationException("No opponent turn is pending.");
        }

        if (state.Moves.Count == 0)
        {
            var opening = await opponentMoveSelector.SelectMoveAsync(state, cancellationToken);
            var applied = await gameSession.ApplyNpcMoveAsync(opening.Move, cancellationToken);
            return new AnalyzedMoveResult(
                applied.Move,
                applied.State,
                new CompactEngineContext(state.CurrentFen, state.Settings.OpponentElo.Value, opening.Move.Value,
                    opening.Candidates.Select(candidate => candidate.Move.Value).Take(8).ToArray(),
                    opening.Candidates.Count == 0 ? null : opening.Candidates[0].Score.ToWhiteCentipawnEstimate(),
                    opening.Candidates.Count == 0 ? [] : opening.Candidates[0].PrincipalVariation.Select(move => move.Value).Take(8).ToArray()),
                false);
        }

        var playerMove = state.Moves[^1];
        if (playerMove.Side != state.Settings.PlayerSide)
        {
            throw new InvalidOperationException("The latest move was not made by the player.");
        }

        var before = await AnalyzeAsync(playerMove.FenBefore, stockfishOptions.Value.QuickAnalysisTimeMs, cancellationToken);
        var after = state.Status == GameStatus.Active
            ? await AnalyzeAsync(state.CurrentFen, stockfishOptions.Value.AnalysisTimeMs, cancellationToken)
            : before;
        int? beforeEval = before.Candidates.Count == 0 ? null : before.BestMove.Score.ToWhiteCentipawnEstimate();
        var afterEval = after.Candidates.Count == 0 ? beforeEval : after.BestMove.Score.ToWhiteCentipawnEstimate();
        var loss = CalculateLoss(beforeEval, afterEval, state.Settings.PlayerSide);
        var classification = classifier.Classify(loss);
        await analyses.SaveAsync(state.GameId, playerMove, beforeEval, afterEval, loss, classification, before, cancellationToken);

        if (state.Status == GameStatus.Active && state.ActiveSide != state.Settings.PlayerSide)
        {
            var selection = await opponentMoveSelector.SelectMoveAsync(state, cancellationToken);
            state = (await gameSession.ApplyNpcMoveAsync(selection.Move, cancellationToken)).State;
        }

        var context = BuildContext(playerMove.FenBefore, state.Settings.OpponentElo.Value, before);
        return new AnalyzedMoveResult(playerMove, state, context, false);
    }

    public async Task<CoachHelpResult> RequestCoachAsync(bool useAiExplanation, CancellationToken cancellationToken)
    {
        var state = await RequiredStateAsync(cancellationToken);
        if (state.Status != GameStatus.Active || state.ActiveSide != state.Settings.PlayerSide)
        {
            throw new InvalidOperationException("Coach me is available only during the player's turn in an active game.");
        }

        var analysis = await AnalyzeAsync(state.CurrentFen, stockfishOptions.Value.AnalysisTimeMs, cancellationToken);
        var best = analysis.BestMove;
        var preview = ChessGame.FromFen(state.GameId, state.Settings, state.CurrentFen);
        var notation = preview.ApplyMove(best.Move).Move.San;
        var context = BuildContext(state.CurrentFen, state.Settings.OpponentElo.Value, analysis);
        AiCoachExplanation? ai = null;
        string? aiStatus = null;
        if (useAiExplanation)
        {
            try
            {
                ai = await aiProvider.GenerateMoveHelpAsync(AiPromptTemplates.MoveHelp(context), cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                aiStatus = "AI explanation is unavailable. Stockfish help is still available.";
            }
        }

        return new CoachHelpResult(
            notation,
            best.Move.Value,
            $"Stockfish recommends {notation} ({best.Move.From} to {best.Move.To}).",
            [best.Move.From, best.Move.To],
            new CoachArrow(best.Move.From, best.Move.To),
            ai?.Hint,
            ai?.Explanation,
            aiStatus);
    }

    public Task<GameState> TakeBackPlayerTurnAsync(CancellationToken cancellationToken) =>
        gameSession.TakeBackPlayerTurnAsync(cancellationToken);

    private async Task<GameState> RequiredStateAsync(CancellationToken cancellationToken) =>
        await gameSession.GetActiveGameAsync(cancellationToken) ?? throw new InvalidOperationException("No active game exists.");

    private Task<EngineAnalysisResult> AnalyzeAsync(string fen, int moveTimeMs, CancellationToken cancellationToken) =>
        engine.AnalyzeAsync(new EngineAnalysisRequest(fen, moveTimeMs, stockfishOptions.Value.MultiPv), cancellationToken);

    private static CompactEngineContext BuildContext(string fen, int opponentElo, EngineAnalysisResult analysis)
    {
        var best = analysis.BestMove;
        return new CompactEngineContext(
            fen,
            opponentElo,
            best.Move.Value,
            analysis.Candidates.OrderBy(candidate => candidate.Rank).Select(candidate => candidate.Move.Value).Take(8).ToArray(),
            best.Score.ToWhiteCentipawnEstimate(),
            best.PrincipalVariation.Select(move => move.Value).Take(8).ToArray());
    }

    private static int CalculateLoss(int? before, int? after, Side playerSide)
    {
        if (before is null || after is null) return 0;
        return Math.Max(0, playerSide is Side.White ? before.Value - after.Value : after.Value - before.Value);
    }
}
