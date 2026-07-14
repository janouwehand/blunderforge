using System.Text.Json;
using BlunderForge.Application.Reviews;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlunderForge.Infrastructure.Persistence;

internal sealed class EfGameReviewRepository(BlunderForgeDbContext db) : IGameReviewRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReviewGame?> GetAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await db.Games.AsNoTracking().Include(g => g.Moves).ThenInclude(m => m.Analysis)
            .SingleOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null) return null;
        var moves = game.Moves.OrderBy(m => m.Ply)
            .Select(m => new ReviewMove(m.Ply, m.San, m.Uci, m.FenBefore, m.FenAfter, m.IsOpponentMove)).ToArray();
        var analyses = game.Moves.Where(m => m.Analysis is not null).Select(m => m.Analysis!)
            .Select(a => new ReviewAnalysis(a.Move!.Ply, a.EvaluationBefore, a.EvaluationAfter, a.CentipawnLoss ?? 0,
                Enum.TryParse<MoveClassification>(a.Classification, out var classification) ? classification : MoveClassification.Good,
                a.BestMoveUci, DeserializeStrings(a.CandidateMovesJson), DeserializeStrings(a.PrincipalVariationJson), a.IsCritical)).ToArray();
        return new ReviewGame(game.Id, Enum.Parse<Side>(game.PlayerSide), game.OpponentElo,
            Enum.Parse<GameStatus>(game.Status), Enum.Parse<GameResult>(game.Result), game.StartedAt, game.CompletedAt, moves, analyses);
    }

    public async Task<GameReview?> GetStoredReviewAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var entity = await db.GameReviews.AsNoTracking().SingleOrDefaultAsync(review => review.GameId == gameId, cancellationToken);
        return entity is null ? null : ToReview(entity);
    }

    public async Task<GameReview> SaveReviewIfAbsentAsync(GameReview review, CancellationToken cancellationToken)
    {
        var existing = await db.GameReviews.SingleOrDefaultAsync(item => item.GameId == review.GameId, cancellationToken);
        if (existing is not null) return ToReview(existing);
        var entity = new GameReviewEntity
        {
            GameId = review.GameId,
            Result = review.Result,
            OverallQuality = review.OverallQuality,
            CriticalMovesJson = JsonSerializer.Serialize(review.CriticalMoves, JsonOptions),
            WentWell = review.WentWell,
            FutureFocus = review.FutureFocus,
            UsedAi = review.UsedAi,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.GameReviews.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return review;
    }

    public async Task<GameHistoryPage> ListCompletedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Games.AsNoTracking().Where(game => game.Status != nameof(GameStatus.Active));
        var allCompleted = await query.ToListAsync(cancellationToken);
        var total = allCompleted.Count;
        var entities = allCompleted.OrderByDescending(game => game.CompletedAt).ThenByDescending(game => game.Id)
            .Skip((page - 1) * pageSize).Take(pageSize);
        var items = entities.Select(game => new GameHistoryItem(game.Id, game.CompletedAt ?? game.StartedAt, game.Result,
            Enum.Parse<Side>(game.PlayerSide), game.OpponentElo)).ToArray();
        return new GameHistoryPage(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    private static GameReview ToReview(GameReviewEntity entity) => new(entity.GameId, entity.Result, entity.OverallQuality,
        DeserializeCritical(entity.CriticalMovesJson), entity.WentWell, entity.FutureFocus, entity.UsedAi);

    private static string[] DeserializeStrings(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static CriticalPosition[] DeserializeCritical(string json)
    {
        try { return JsonSerializer.Deserialize<CriticalPosition[]>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }
}
