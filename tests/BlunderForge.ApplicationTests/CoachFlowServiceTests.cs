using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Games;
using BlunderForge.Application.Npc;
using BlunderForge.Domain.Games;
using Microsoft.Extensions.Options;

namespace BlunderForge.ApplicationTests;

public sealed class CoachFlowServiceTests
{
    [Fact]
    public async Task StockfishOnlyHelpSkipsAiAndReturnsValidatedVisuals()
    {
        var fixture = await Fixture.CreateAsync();
        var result = await fixture.Coach.RequestCoachAsync(false, default);
        Assert.Equal("e2e4", result.RecommendedMoveUci);
        Assert.Equal(["e2", "e4"], result.HighlightSquares);
        Assert.Equal(new CoachArrow("e2", "e4"), result.Arrow);
        Assert.Null(result.Hint);
        Assert.Equal(0, fixture.Ai.Calls);
    }

    [Fact]
    public async Task AiSuccessAddsTextWithoutControllingVisuals()
    {
        var fixture = await Fixture.CreateAsync();
        var result = await fixture.Coach.RequestCoachAsync(true, default);
        Assert.Equal("Look at the center.", result.Hint);
        Assert.Equal(new CoachArrow("e2", "e4"), result.Arrow);
        Assert.Equal(1, fixture.Ai.Calls);
    }

    [Fact]
    public async Task AiFailurePreservesStockfishHelp()
    {
        var fixture = await Fixture.CreateAsync(aiFailure: true);
        var result = await fixture.Coach.RequestCoachAsync(true, default);
        Assert.Equal("e2e4", result.RecommendedMoveUci);
        Assert.Contains("Stockfish help", result.AiStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlayerMovePersistsBeforePendingOpponentTurnAndAnalysis()
    {
        var fixture = await Fixture.CreateAsync();
        var submitted = await fixture.Coach.SubmitPlayerMoveAsync(UciMove.Parse("e2e4"), default);
        Assert.True(submitted.State.ActiveSide != submitted.State.Settings.PlayerSide);
        Assert.Single((await fixture.Repository.GetActiveAsync(default))!.Moves);
        var completed = await fixture.Coach.CompleteNpcTurnAsync(default);
        Assert.Equal(2, completed.State.Moves.Count);
        Assert.Equal(1, fixture.Analyses.Saves);
    }

    [Fact]
    public async Task BlackGameCanCompleteInitialOpponentTurn()
    {
        var repository = new MemoryRepository();
        var session = new GameSessionService(repository);
        await session.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.Black, new OpponentElo(800)), default);
        var engine = new PositionEngine();
        var ai = new RecordingAi(false);
        var coach = new CoachFlowService(session, engine,
            new OpponentMoveSelector(engine, new FixedRandom(), Options.Create(new StockfishOptions())),
            new CentipawnMoveClassifier(Options.Create(new MoveClassificationOptions())), new RecordingAnalyses(), ai, Options.Create(new StockfishOptions()));
        var result = await coach.CompleteNpcTurnAsync(default);
        Assert.Single(result.State.Moves);
        Assert.Equal(Side.Black, result.State.ActiveSide);
    }

    private sealed class Fixture
    {
        public required CoachFlowService Coach { get; init; }
        public required MemoryRepository Repository { get; init; }
        public required RecordingAi Ai { get; init; }
        public required RecordingAnalyses Analyses { get; init; }

        public static async Task<Fixture> CreateAsync(bool aiFailure = false)
        {
            var repository = new MemoryRepository();
            var session = new GameSessionService(repository);
            await session.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);
            var engine = new PositionEngine();
            var opponent = new OpponentMoveSelector(engine, new FixedRandom(), Options.Create(new StockfishOptions()));
            var ai = new RecordingAi(aiFailure);
            var analyses = new RecordingAnalyses();
            return new Fixture
            {
                Repository = repository,
                Ai = ai,
                Analyses = analyses,
                Coach = new CoachFlowService(session, engine, opponent, new CentipawnMoveClassifier(Options.Create(new MoveClassificationOptions())), analyses, ai, Options.Create(new StockfishOptions()))
            };
        }
    }

    private sealed class PositionEngine : IChessEngine
    {
        public Task<EngineAnalysisResult> AnalyzeAsync(EngineAnalysisRequest request, CancellationToken cancellationToken)
        {
            var move = request.Fen.Split(' ')[1] == "w" ? "e2e4" : "e7e5";
            var candidate = new CandidateMove(UciMove.Parse(move), EngineScore.FromCentipawns(20), [UciMove.Parse(move)], 1);
            return Task.FromResult(new EngineAnalysisResult("Stockfish test", new EngineSettingsSnapshot("fake", 1, 16, request.MultiPv, request.MoveTimeMs, request.UciElo is not null, request.UciElo), [candidate]));
        }
    }
    private sealed class FixedRandom : INpcRandom { public double NextDouble() => 0; }
    private sealed class RecordingAi(bool fail) : IAiCoachProvider
    {
        public int Calls { get; private set; }
        public Task<AiCoachExplanation> GenerateMoveHelpAsync(AiCoachRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? Task.FromException<AiCoachExplanation>(new HttpRequestException("offline")) : Task.FromResult(new AiCoachExplanation("Look at the center.", "This move claims space."));
        }
        public Task<GameReviewResponse> GenerateGameReviewAsync(AiCoachRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProviderStatus> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(new ProviderStatus(!fail, "test"));
    }
    private sealed class RecordingAnalyses : IMoveAnalysisRepository
    {
        public int Saves { get; private set; }
        public Task SaveAsync(Guid gameId, MoveRecord move, int? evaluationBefore, int? evaluationAfter, int centipawnLoss, MoveClassification classification, EngineAnalysisResult analysis, CancellationToken cancellationToken) { Saves++; return Task.CompletedTask; }
    }
    private sealed class MemoryRepository : IActiveGameRepository
    {
        private ChessGame? game;
        public Task<ChessGame?> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult(game);
        public Task SaveActiveAsync(ChessGame value, CancellationToken cancellationToken) { game = value; return Task.CompletedTask; }
        public Task ReplaceActiveAsync(ChessGame value, CancellationToken cancellationToken) { game = value; return Task.CompletedTask; }
        public Task ClearActiveAsync(CancellationToken cancellationToken) { game = null; return Task.CompletedTask; }
        public Task<bool> DeleteAsync(Guid gameId, CancellationToken cancellationToken) { var found = game?.Id == gameId; if (found) game = null; return Task.FromResult(found); }
    }
}
