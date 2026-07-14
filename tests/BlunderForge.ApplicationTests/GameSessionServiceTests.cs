using BlunderForge.Application.Games;
using BlunderForge.Domain.Games;

namespace BlunderForge.ApplicationTests;

public sealed class GameSessionServiceTests
{
    [Fact]
    public async Task StartsSingleGameWithOpponentEloAndPersistsMoves()
    {
        var repository = new MemoryRepository();
        var service = new GameSessionService(repository);
        var state = await service.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);
        Assert.Equal(800, state.Settings.OpponentElo.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.Black, new OpponentElo(1200)), default));
        var moved = await service.ApplyMoveAsync(UciMove.Parse("e2e4"), default);
        Assert.Single(moved.State.Moves);
        Assert.Single((await repository.GetActiveAsync(default))!.Moves);
    }

    [Fact]
    public async Task TakebackRemovesPlayerTurnAndDeleteIsHard()
    {
        var repository = new MemoryRepository();
        var service = new GameSessionService(repository);
        await service.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);
        await service.ApplyMoveAsync(UciMove.Parse("e2e4"), default);
        await service.ApplyNpcMoveAsync(UciMove.Parse("e7e5"), default);
        var state = await service.TakeBackPlayerTurnAsync(default);
        Assert.Empty(state.Moves);
        await service.DeleteActiveGameAsync(default);
        Assert.Null(await repository.GetActiveAsync(default));
    }

    [Fact]
    public async Task RepeatedMoveSubmissionReturnsThePersistedMoveWithoutDuplicatingIt()
    {
        var repository = new MemoryRepository();
        var service = new GameSessionService(repository);
        await service.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);

        var first = await service.ApplyMoveAsync(UciMove.Parse("e2e4"), default);
        var duplicate = await service.ApplyMoveAsync(UciMove.Parse("e2e4"), default);

        Assert.Equal(first.Move, duplicate.Move);
        Assert.Single(duplicate.State.Moves);
    }

    private sealed class MemoryRepository : IActiveGameRepository
    {
        private ChessGame? game;
        public Task<ChessGame?> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult(game);
        public Task SaveActiveAsync(ChessGame value, CancellationToken cancellationToken) { game = value; return Task.CompletedTask; }
        public Task ClearActiveAsync(CancellationToken cancellationToken) { game = null; return Task.CompletedTask; }
        public Task ReplaceActiveAsync(ChessGame value, CancellationToken cancellationToken) { game = value; return Task.CompletedTask; }
        public Task<bool> DeleteAsync(Guid gameId, CancellationToken cancellationToken) { var found = game?.Id == gameId; if (found) game = null; return Task.FromResult(found); }
    }
}
