using BlunderForge.Application.Ai;
using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Reviews;

public sealed record ReviewMove(int Ply, string San, string Uci, string FenBefore, string FenAfter, bool IsOpponentMove);
public sealed record ReviewAnalysis(int Ply, int? EvaluationBefore, int? EvaluationAfter, int CentipawnLoss, MoveClassification Classification, string? BestMoveUci, IReadOnlyList<string> CandidateMoves, IReadOnlyList<string> PrincipalVariation, bool IsCritical);
public sealed record ReviewGame(Guid Id, Side PlayerSide, int OpponentElo, GameStatus Status, GameResult Result, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, IReadOnlyList<ReviewMove> Moves, IReadOnlyList<ReviewAnalysis> Analyses);
public sealed record CriticalPosition(int Ply, string Fen, string PlayedSan, string PlayedUci, string? BestMoveUci, MoveClassification Classification, int CentipawnLoss);
public sealed record GameReview(Guid GameId, string Result, string OverallQuality, IReadOnlyList<CriticalPosition> CriticalMoves, string WentWell, string FutureFocus, bool UsedAi);
public sealed record GameHistoryItem(Guid GameId, DateTimeOffset Date, string Result, Side PlayerSide, int OpponentElo);
public sealed record GameHistoryPage(IReadOnlyList<GameHistoryItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public interface IGameReviewRepository
{
    Task<ReviewGame?> GetAsync(Guid gameId, CancellationToken cancellationToken);
    Task<GameReview?> GetStoredReviewAsync(Guid gameId, CancellationToken cancellationToken);
    Task<GameReview> SaveReviewIfAbsentAsync(GameReview review, CancellationToken cancellationToken);
    Task<GameHistoryPage> ListCompletedAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class GameReviewService(IGameReviewRepository repository, IAiCoachProvider? aiProvider = null)
{
    public async Task<GameReview> GetAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await repository.GetAsync(gameId, cancellationToken) ?? throw new KeyNotFoundException("Game was not found.");
        if (game.Status == GameStatus.Active) throw new InvalidOperationException("A review is only available after the game is completed or resigned.");
        return await repository.GetStoredReviewAsync(gameId, cancellationToken)
            ?? throw new InvalidOperationException("The stored review is not available yet.");
    }

    public async Task<GameReview> GenerateOnceAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var stored = await repository.GetStoredReviewAsync(gameId, cancellationToken);
        if (stored is not null) return stored;
        var game = await repository.GetAsync(gameId, cancellationToken) ?? throw new KeyNotFoundException("Game was not found.");
        if (game.Status == GameStatus.Active) throw new InvalidOperationException("A review is only available after the game is completed or resigned.");

        var review = BuildDeterministic(game);
        if (aiProvider is not null)
        {
            try
            {
                var context = review.CriticalMoves.Select(move => new BlunderForge.Application.Coaching.CompactEngineContext(
                    move.Fen, game.OpponentElo, move.BestMoveUci ?? move.PlayedUci, [], null, [])).ToArray();
                var phrasing = await aiProvider.GenerateGameReviewAsync(
                    AiPromptTemplates.GameReview(new GameReviewRequest(review.Result, context, game.OpponentElo)), cancellationToken);
                review = review with
                {
                    OverallQuality = phrasing.Summary,
                    WentWell = phrasing.LearningMoments.Count == 0 ? review.WentWell : string.Join(" ", phrasing.LearningMoments),
                    FutureFocus = phrasing.Focus,
                    UsedAi = true
                };
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The deterministic review remains authoritative and usable.
            }
        }
        return await repository.SaveReviewIfAbsentAsync(review, cancellationToken);
    }

    public Task<GameHistoryPage> ListAsync(int page, CancellationToken cancellationToken) =>
        repository.ListCompletedAsync(page, 25, cancellationToken);

    private static GameReview BuildDeterministic(ReviewGame game)
    {
        var critical = game.Analyses.Where(analysis => analysis.IsCritical)
            .OrderByDescending(analysis => analysis.CentipawnLoss).ThenBy(analysis => analysis.Ply).Take(3)
            .Select(analysis =>
            {
                var move = game.Moves.Single(item => item.Ply == analysis.Ply);
                return new CriticalPosition(analysis.Ply, move.FenBefore, move.San, move.Uci, analysis.BestMoveUci, analysis.Classification, analysis.CentipawnLoss);
            }).ToArray();
        var averageLoss = game.Analyses.Count == 0 ? 0 : (int)Math.Round(game.Analyses.Average(analysis => analysis.CentipawnLoss));
        var quality = averageLoss switch { <= 20 => "Excellent", <= 50 => "Good", <= 100 => "Inconsistent", _ => "Needs improvement" };
        return new GameReview(game.Id, game.Result.ToString(), quality, critical,
            game.Analyses.Any(analysis => analysis.Classification is MoveClassification.Best or MoveClassification.Excellent)
                ? "You found at least one objectively strong move."
                : "You completed the game and created useful analysis data.",
            critical.Length == 0 ? "Keep comparing candidate moves before committing." : $"Revisit the position before move {critical[0].Ply} and compare the best candidate.", false);
    }
}
