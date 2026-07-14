using BlunderForge.Application.Configuration;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Games;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure.Configuration;
using BlunderForge.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlunderForge.InfrastructureTests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task InitialMigrationCreatesOnlySimplifiedTables()
    {
        await using var database = await Database.CreateAsync();
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync();
        var tables = await Strings(connection, "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;");
        Assert.Contains("Games", tables);
        Assert.Contains("Moves", tables);
        Assert.Contains("MoveAnalyses", tables);
        Assert.Contains("GameReviews", tables);
        Assert.Contains("AppSettings", tables);
        Assert.DoesNotContain(tables, table => table.Contains("Profile", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tables, table => table.Contains("Rating", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tables, table => table.Contains("Coach", StringComparison.OrdinalIgnoreCase));
        var columns = await Strings(connection, "SELECT name FROM pragma_table_info('AppSettings') ORDER BY name;");
        Assert.DoesNotContain(columns, column => column.Contains("Secret", StringComparison.OrdinalIgnoreCase) || column.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ActiveGameAndPendingOpponentTurnResumeAfterRestart()
    {
        await using var database = await Database.CreateAsync();
        await using (var first = database.Context())
        {
            var service = new GameSessionService(new EfActiveGameRepository(first));
            await service.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);
            await service.ApplyMoveAsync(UciMove.Parse("e2e4"), default);
        }
        await using var second = database.Context();
        var game = await new EfActiveGameRepository(second).GetActiveAsync(default);
        Assert.NotNull(game);
        Assert.Equal(800, game!.Settings.OpponentElo.Value);
        Assert.Equal(Side.Black, game.ActiveSide);
        Assert.Single(game.Moves);
    }

    [Fact]
    public async Task StaleSaveIsRejectedWithoutDivergingMoveAndFen()
    {
        await using var database = await Database.CreateAsync();
        await using (var setup = database.Context())
        {
            await new GameSessionService(new EfActiveGameRepository(setup))
                .StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);
        }

        await using var firstContext = database.Context();
        await using var staleContext = database.Context();
        var firstRepository = new EfActiveGameRepository(firstContext);
        var staleRepository = new EfActiveGameRepository(staleContext);
        var first = (await firstRepository.GetActiveAsync(default))!;
        var stale = (await staleRepository.GetActiveAsync(default))!;
        var accepted = first.ApplyMove(UciMove.Parse("e2e4"));
        stale.ApplyMove(UciMove.Parse("d2d4"));

        await firstRepository.SaveActiveAsync(first, default);
        await Assert.ThrowsAsync<GameConcurrencyException>(() => staleRepository.SaveActiveAsync(stale, default));

        await using var verification = database.Context();
        var persisted = (await new EfActiveGameRepository(verification).GetActiveAsync(default))!;
        Assert.Single(persisted.Moves);
        Assert.Equal("e2e4", persisted.Moves[0].Uci.Value);
        Assert.Equal(accepted.State.CurrentFen, persisted.CurrentFen);
        Assert.Equal(persisted.Moves[0].FenAfter, persisted.CurrentFen);
    }

    [Fact]
    public async Task ConcurrentActiveGameCreationReturnsDomainConcurrencyError()
    {
        await using var database = await Database.CreateAsync();
        await using var firstContext = database.Context();
        await using var secondContext = database.Context();
        await new EfActiveGameRepository(firstContext).SaveActiveAsync(ChessGame.Start(Settings(800)), default);

        await Assert.ThrowsAsync<GameConcurrencyException>(() =>
            new EfActiveGameRepository(secondContext).SaveActiveAsync(ChessGame.Start(Settings(900)), default));
    }

    [Fact]
    public async Task TakebackPhysicallyRemovesMovesAndHardDeleteCascades()
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Context();
        var repository = new EfActiveGameRepository(context);
        var service = new GameSessionService(repository);
        var started = await service.StartNewGameAsync(new StartGameRequest(PlayerColorChoice.White, new OpponentElo(800)), default);
        await service.ApplyMoveAsync(UciMove.Parse("e2e4"), default);
        await service.ApplyNpcMoveAsync(UciMove.Parse("e7e5"), default);
        await service.TakeBackPlayerTurnAsync(default);
        Assert.Empty(await context.Moves.ToListAsync());
        await service.ApplyMoveAsync(UciMove.Parse("d2d4"), default);
        Assert.Single(await context.Moves.ToListAsync());
        Assert.True(await repository.DeleteAsync(started.GameId, default));
        Assert.Empty(await context.Games.ToListAsync());
        Assert.Empty(await context.Moves.ToListAsync());
    }

    [Fact]
    public async Task MoveAnalysisPersistsEngineVersionAndRelevantSettings()
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Context();
        var game = ChessGame.Start(Settings(1320));
        var move = game.ApplyMove(UciMove.Parse("e2e4")).Move;
        await new EfActiveGameRepository(context).SaveActiveAsync(game, default);
        var candidate = new CandidateMove(UciMove.Parse("e2e4"), EngineScore.FromCentipawns(30), [UciMove.Parse("e2e4")], 1);
        var analysis = new EngineAnalysisResult("Stockfish 18", new EngineSettingsSnapshot("stockfish", 1, 128, 1, 500, true, 1320), [candidate]);
        await new EfMoveAnalysisRepository(context).SaveAsync(game.Id, move, 30, 20, 10, MoveClassification.Excellent, analysis, default);
        var stored = await context.MoveAnalyses.SingleAsync();
        Assert.Equal("Stockfish 18", stored.EngineVersion);
        Assert.Contains("1320", stored.EngineSettingsJson, StringComparison.Ordinal);
        Assert.Contains("UciLimitStrength", stored.EngineSettingsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SecretFileTakesPrecedenceWithoutExposingValue()
    {
        var direct = "BLUNDERFORGE_TEST_DIRECT";
        var file = "BLUNDERFORGE_TEST_FILE";
        var path = Path.GetTempFileName();
        Environment.SetEnvironmentVariable(direct, "fake-direct");
        Environment.SetEnvironmentVariable(file, path);
        File.WriteAllText(path, "fake-file");
        try
        {
            var reference = new SecretReference(direct, file);
            Assert.Equal("fake-file", new EnvironmentSecretResolver().Resolve(reference));
            var availability = new EnvironmentSecretStatusService().GetAvailability(reference);
            Assert.Equal(file, availability.Source);
            Assert.DoesNotContain("fake-file", availability.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(direct, null);
            Environment.SetEnvironmentVariable(file, null);
            File.Delete(path);
        }
    }

    private static GameSettings Settings(int elo) => new(PlayerColorChoice.White, Side.White, new OpponentElo(elo));

    private static async Task<List<string>> Strings(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string directory;
        private Database(string directory) { this.directory = directory; ConnectionString = $"Data Source={Path.Combine(directory, "blunderforge.db")}"; }
        public string ConnectionString { get; }
        public static async Task<Database> CreateAsync()
        {
            var database = new Database(Directory.CreateTempSubdirectory("blunderforge-db-").FullName);
            await using var context = database.Context(); await context.Database.MigrateAsync(); return database;
        }
        public BlunderForgeDbContext Context() => new(new DbContextOptionsBuilder<BlunderForgeDbContext>().UseSqlite(ConnectionString).Options);
        public ValueTask DisposeAsync() { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); return ValueTask.CompletedTask; }
    }
}
