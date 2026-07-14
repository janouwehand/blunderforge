using BlunderForge.Application.Games;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlunderForge.Infrastructure.Persistence;

internal sealed class EfActiveGameRepository(BlunderForgeDbContext dbContext) : IActiveGameRepository
{
    public async Task<ChessGame?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.Games
            .AsNoTracking()
            .Include(game => game.Moves)
            .SingleOrDefaultAsync(game => game.Status == nameof(GameStatus.Active), cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task SaveActiveAsync(ChessGame game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existing = await dbContext.Games
            .Include(entity => entity.Moves)
            .SingleOrDefaultAsync(entity => entity.Id == game.Id, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            if (game.Version != 0)
            {
                throw new GameConcurrencyException("The game changed before it could be saved. Reload it and try again.");
            }

            existing = new GameEntity
            {
                Id = game.Id,
                StartedAt = now,
                InitialFen = ChessGame.StandardInitialFen
            };
            dbContext.Games.Add(existing);
        }
        else if (existing.Version != game.Version)
        {
            throw new GameConcurrencyException("The game changed before it could be saved. Reload it and try again.");
        }

        ApplyGameState(existing, game, now);

        foreach (var storedMove in existing.Moves.Where(storedMove => storedMove.Ply > game.Moves.Count).ToArray())
        {
            dbContext.Moves.Remove(storedMove);
        }

        foreach (var move in game.Moves)
        {
            if (existing.Moves.Any(storedMove => storedMove.Ply == move.Ply))
            {
                continue;
            }

            existing.Moves.Add(new MoveEntity
            {
                GameId = game.Id,
                Ply = move.Ply,
                Color = move.Side.ToString(),
                San = move.San,
                Uci = move.Uci.Value,
                FenBefore = move.FenBefore,
                FenAfter = move.FenAfter,
                IsOpponentMove = move.Side != game.Settings.PlayerSide,
                CreatedAt = now
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new GameConcurrencyException("The game changed before it could be saved. Reload it and try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        {
            throw new GameConcurrencyException("The game changed before it could be saved. Reload it and try again.", exception);
        }
    }

    public async Task ReplaceActiveAsync(ChessGame game, CancellationToken cancellationToken)
    {
        await SaveActiveAsync(game, cancellationToken);
    }

    public async Task ClearActiveAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var active = await dbContext.Games
            .SingleOrDefaultAsync(game => game.Status == nameof(GameStatus.Active), cancellationToken);

        if (active is not null)
        {
            dbContext.Games.Remove(active);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await dbContext.Games.SingleOrDefaultAsync(entity => entity.Id == gameId, cancellationToken);
        if (game is null) return false;
        dbContext.Games.Remove(game);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ApplyGameState(GameEntity entity, ChessGame game, DateTimeOffset now)
    {
        var state = game.ToState();
        entity.Status = state.Status.ToString();
        entity.PlayerColorChoice = state.Settings.PlayerColorChoice.ToString();
        entity.PlayerSide = state.Settings.PlayerSide.ToString();
        entity.OpponentElo = state.Settings.OpponentElo.Value;
        entity.Result = state.Result.ToString();
        entity.TerminationReason = state.TerminationReason.ToString();
        entity.CurrentFen = state.CurrentFen;
        entity.CompletedAt = state.Status is GameStatus.Active ? null : now;
        entity.UpdatedAt = now;
        entity.Version++;
    }

    private static ChessGame ToDomain(GameEntity entity)
    {
        var settings = new GameSettings(
            Enum.Parse<PlayerColorChoice>(entity.PlayerColorChoice),
            Enum.Parse<Side>(entity.PlayerSide),
            new OpponentElo(entity.OpponentElo));

        var moves = entity.Moves
            .Where(move => move.Ply > 0)
            .OrderBy(move => move.Ply)
            .Select(move => new MoveRecord(
                move.Ply,
                Enum.Parse<Side>(move.Color),
                move.San,
                UciMove.Parse(move.Uci),
                move.FenBefore,
                move.FenAfter));

        return ChessGame.Rehydrate(
            entity.Id,
            settings,
            entity.CurrentFen,
            moves,
            Enum.Parse<GameStatus>(entity.Status),
            Enum.Parse<GameResult>(entity.Result),
            Enum.Parse<GameTerminationReason>(entity.TerminationReason),
            entity.Version);
    }
}
