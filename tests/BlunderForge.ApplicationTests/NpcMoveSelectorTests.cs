using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Npc;
using BlunderForge.Domain.Games;
using Microsoft.Extensions.Options;

namespace BlunderForge.ApplicationTests;

public sealed class NpcMoveSelectorTests
{
    private static readonly CandidateMove[] Candidates =
    [
        Candidate("e2e4", 100, 1), Candidate("d2d4", 40, 2), Candidate("g1f3", -20, 3), Candidate("a2a3", -300, 4)
    ];

    [Theory]
    [InlineData(199)]
    [InlineData(3001)]
    public void RejectsInvalidElo(int elo) => Assert.Throws<ArgumentOutOfRangeException>(() => new OpponentElo(elo));

    [Fact]
    public void LowEloCanChooseBoundedWeakerMoveWhile1319PrefersStrength()
    {
        var low = OpponentMoveSelector.SelectCalibratedCandidate(Candidates, 200, Side.White, .2);
        var high = OpponentMoveSelector.SelectCalibratedCandidate(Candidates, 1319, Side.White, .2);
        Assert.NotEqual(Candidates[0].Move, low.Move);
        Assert.Equal(Candidates[0].Move, high.Move);
        Assert.NotEqual(Candidates[^1].Move, low.Move);
    }

    [Fact]
    public void LowEloSelectionVariesWithRandomRoll()
    {
        var first = OpponentMoveSelector.SelectCalibratedCandidate(Candidates, 800, Side.White, .05);
        var second = OpponentMoveSelector.SelectCalibratedCandidate(Candidates, 800, Side.White, .75);
        Assert.NotEqual(first.Move, second.Move);
    }

    [Theory]
    [InlineData(200, 8, null)]
    [InlineData(800, 8, null)]
    [InlineData(1319, 8, null)]
    [InlineData(1320, 1, 1320)]
    [InlineData(3000, 1, 3000)]
    public async Task ConfiguresCorrectStockfishRegime(int elo, int multiPv, int? uciElo)
    {
        var engine = new RecordingEngine(Candidates);
        var selector = new OpponentMoveSelector(engine, new FixedRandom(.2), Options.Create(new StockfishOptions()));
        await selector.SelectMoveAsync(ChessGame.Start(new GameSettings(PlayerColorChoice.Black, Side.Black, new OpponentElo(elo))).ToState(), default);
        Assert.Equal(multiPv, engine.Request!.MultiPv);
        Assert.Equal(uciElo, engine.Request.UciElo);
    }

    private static CandidateMove Candidate(string move, int cp, int rank) => new(UciMove.Parse(move), EngineScore.FromCentipawns(cp), [UciMove.Parse(move)], rank);
    private sealed class FixedRandom(double value) : INpcRandom { public double NextDouble() => value; }
    private sealed class RecordingEngine(IReadOnlyList<CandidateMove> candidates) : IChessEngine
    {
        public EngineAnalysisRequest? Request { get; private set; }
        public Task<EngineAnalysisResult> AnalyzeAsync(EngineAnalysisRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new EngineAnalysisResult("Stockfish test", new EngineSettingsSnapshot("fake", 1, 16, request.MultiPv, request.MoveTimeMs, request.UciElo is not null, request.UciElo), candidates));
        }
    }
}
