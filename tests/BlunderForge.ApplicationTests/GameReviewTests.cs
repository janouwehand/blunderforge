using BlunderForge.Application.Reviews;
using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Domain.Games;

namespace BlunderForge.ApplicationTests;

public sealed class GameReviewTests
{
    [Fact]
    public async Task ReviewContainsAtMostThreeCriticalMoves()
    {
        var game = CompletedGame();
        var review = await new GameReviewService(new Repository(game)).GenerateOnceAsync(game.Id, default);
        Assert.True(review.CriticalMoves.Count <= 3);
        Assert.DoesNotContain("rating", review.FutureFocus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewFallsBackAndIsStoredWhenAiTimesOut()
    {
        var game = CompletedGame();
        var repository = new Repository(game);

        var review = await new GameReviewService(repository, new TimingOutAiProvider()).GenerateOnceAsync(game.Id, default);

        Assert.False(review.UsedAi);
        Assert.Same(review, await repository.GetStoredReviewAsync(game.Id, default));
    }

    [Fact]
    public void PgnContainsOpponentEloWithoutCoachingMetadata()
    {
        var pgn = PgnExporter.Export(CompletedGame());
        Assert.Contains("[OpponentElo \"800\"]", pgn, StringComparison.Ordinal);
        Assert.DoesNotContain("Coach", pgn, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewAiContractRejectsUnexpectedFields()
    {
        Assert.Throws<AiResponseValidationException>(() => AiResponseValidator.ValidateGameReview(
            "{\"summary\":\"Good\",\"learningMoments\":[],\"focus\":\"Compare moves\",\"rating\":900}"));
    }

    private static ReviewGame CompletedGame()
    {
        var id = Guid.NewGuid();
        var moves = new[] { new ReviewMove(1, "e4", "e2e4", ChessGame.StandardInitialFen, "fen", false) };
        var analyses = Enumerable.Range(1, 4).Select(index => new ReviewAnalysis(1, 20, 0, index * 50, MoveClassification.Mistake, "d2d4", ["d2d4"], ["d2d4"], true)).ToArray();
        return new ReviewGame(id, Side.White, 800, GameStatus.Completed, GameResult.BlackWin, new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow, moves, analyses);
    }

    private sealed class Repository(ReviewGame game) : IGameReviewRepository
    {
        private GameReview? stored;
        public Task<ReviewGame?> GetAsync(Guid gameId, CancellationToken cancellationToken) => Task.FromResult<ReviewGame?>(gameId == game.Id ? game : null);
        public Task<GameReview?> GetStoredReviewAsync(Guid gameId, CancellationToken cancellationToken) => Task.FromResult(gameId == game.Id ? stored : null);
        public Task<GameReview> SaveReviewIfAbsentAsync(GameReview review, CancellationToken cancellationToken)
        {
            stored ??= review;
            return Task.FromResult(stored);
        }
        public Task<GameHistoryPage> ListCompletedAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new GameHistoryPage([], page, pageSize, 0, 0));
    }

    private sealed class TimingOutAiProvider : IAiCoachProvider
    {
        public Task<AiCoachExplanation> GenerateMoveHelpAsync(AiCoachRequest request, CancellationToken cancellationToken) =>
            Task.FromException<AiCoachExplanation>(new TimeoutException());

        public Task<GameReviewResponse> GenerateGameReviewAsync(AiCoachRequest request, CancellationToken cancellationToken) =>
            Task.FromException<GameReviewResponse>(new TimeoutException());

        public Task<ProviderStatus> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderStatus(false, "Timed out."));
    }
}
