using System.Net;
using System.Net.Http.Json;
using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Reviews;
using BlunderForge.Application.Configuration;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure.Configuration;
using BlunderForge.Web.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlunderForge.WebTests;

[Trait("Category", "EndToEnd")]
public sealed class GameEndpointTests
{
    [Theory]
    [InlineData(199)]
    [InlineData(3001)]
    public async Task RejectsInvalidOpponentElo(int elo)
    {
        using var fixture = new Fixture();
        using var response = await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", elo));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartsOneGamePersistsPendingTurnAndResumes()
    {
        using var fixture = new Fixture();
        using var started = await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800));
        started.EnsureSuccessStatusCode();
        var state = await started.Content.ReadFromJsonAsync<GameStateDto>();
        Assert.Equal(800, state!.Settings.OpponentElo);
        using var duplicate = await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("Black", 900));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var move = await fixture.Client.PostAsJsonAsync("/api/games/active/moves", new SubmitMoveRequestDto("e2e4"));
        var moved = await move.Content.ReadFromJsonAsync<MoveResultDto>();
        Assert.True(moved!.OpponentMovePending);
        using var repeatedMove = await fixture.Client.PostAsJsonAsync("/api/games/active/moves", new SubmitMoveRequestDto("e2e4"));
        repeatedMove.EnsureSuccessStatusCode();
        var repeated = await repeatedMove.Content.ReadFromJsonAsync<MoveResultDto>();
        Assert.Single(repeated!.State.Moves);
        var resumed = await fixture.Client.GetFromJsonAsync<GameStateDto>("/api/games/active");
        Assert.Single(resumed!.Moves);
    }

    [Fact]
    public async Task CoachOptOutSkipsAiAndReturnsEngineVisuals()
    {
        using var fixture = new Fixture();
        await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800));
        using var response = await fixture.Client.PostAsJsonAsync("/api/games/active/coach", new CoachRequestDto(false));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CoachHelpDto>();
        Assert.Equal("e2e4", result!.RecommendedMoveUci);
        Assert.Equal(["e2", "e4"], result.HighlightSquares);
        Assert.Equal(new CoachArrowDto("e2", "e4"), result.Arrow);
        Assert.Equal(0, fixture.Ai.Calls);
    }

    [Fact]
    public async Task CoachWithAiReturnsValidatedExplanation()
    {
        using var fixture = new Fixture();
        await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800));

        using var response = await fixture.Client.PostAsJsonAsync("/api/games/active/coach", new CoachRequestDto(true));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CoachHelpDto>();
        Assert.Equal("e2e4", result!.RecommendedMoveUci);
        Assert.Equal("Hint", result.Hint);
        Assert.Equal("Explanation", result.Explanation);
        Assert.Equal(1, fixture.Ai.Calls);
    }

    [Fact]
    public async Task AiFailureStillReturnsStockfishHelp()
    {
        using var fixture = new Fixture(aiFailure: true);
        await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800));
        using var response = await fixture.Client.PostAsJsonAsync("/api/games/active/coach", new CoachRequestDto(true));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CoachHelpDto>();
        Assert.Equal("e2e4", result!.RecommendedMoveUci);
        Assert.Contains("Stockfish help", result.AiStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveGameDeletionIsHard()
    {
        using var fixture = new Fixture();
        await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800));
        using var deleted = await fixture.Client.DeleteAsync("/api/games/active");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await fixture.Client.GetAsync("/api/games/active");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task CustomBlackGameCompletesPendingMoveAndTakesBackLatestPlayerTurn()
    {
        using var fixture = new Fixture(opponentMoves: ["e2e4"]);
        var started = await (await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("Black", 1320)))
            .Content.ReadFromJsonAsync<GameStateDto>();
        Assert.Equal("Black", started!.Settings.PlayerSide);
        Assert.Equal(1320, started.Settings.OpponentElo);

        using var opening = await fixture.Client.PostAsync("/api/games/active/opponent-turn", null);
        opening.EnsureSuccessStatusCode();
        var afterOpening = await opening.Content.ReadFromJsonAsync<MoveResultDto>();
        Assert.Equal("e2e4", afterOpening!.Move.Uci);

        using var move = await fixture.Client.PostAsJsonAsync("/api/games/active/moves", new SubmitMoveRequestDto("e7e5"));
        move.EnsureSuccessStatusCode();
        using var takeback = await fixture.Client.PostAsync("/api/games/active/takeback", null);
        takeback.EnsureSuccessStatusCode();
        var restored = await takeback.Content.ReadFromJsonAsync<GameStateDto>();
        Assert.Single(restored!.Moves);
        Assert.Equal("e2e4", restored.Moves[0].Uci);
        Assert.Equal("Black", restored.ActiveSide);
    }

    [Fact]
    public async Task CompletedGameStoresReviewHistoryAndPgn()
    {
        using var fixture = new Fixture(opponentMoves: ["e7e5", "d8h4"]);
        var started = await (await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800)))
            .Content.ReadFromJsonAsync<GameStateDto>();

        await fixture.Client.PostAsJsonAsync("/api/games/active/moves", new SubmitMoveRequestDto("f2f3"));
        (await fixture.Client.PostAsync("/api/games/active/opponent-turn", null)).EnsureSuccessStatusCode();
        await fixture.Client.PostAsJsonAsync("/api/games/active/moves", new SubmitMoveRequestDto("g2g4"));
        using var mate = await fixture.Client.PostAsync("/api/games/active/opponent-turn", null);

        mate.EnsureSuccessStatusCode();
        var completed = await mate.Content.ReadFromJsonAsync<MoveResultDto>();
        Assert.Equal("Completed", completed!.State.Status);
        Assert.Equal("Checkmate", completed.State.TerminationReason);
        Assert.Equal(0, fixture.Ai.ReviewCalls);
        var detail = await fixture.Client.GetFromJsonAsync<HistoricalGameDto>($"/api/games/{started!.GameId}");
        Assert.Null(detail!.Review);
        Assert.Equal(4, detail.Moves.Count);
        using var generated = await fixture.Client.PostAsync($"/api/games/{started.GameId}/review", null);
        generated.EnsureSuccessStatusCode();
        Assert.Equal(1, fixture.Ai.ReviewCalls);
        using var repeated = await fixture.Client.PostAsync($"/api/games/{started.GameId}/review", null);
        repeated.EnsureSuccessStatusCode();
        Assert.Equal(1, fixture.Ai.ReviewCalls);
        var pgn = await fixture.Client.GetStringAsync($"/api/games/{started.GameId}/pgn");
        Assert.Contains("1. f3 e5 2. g4 Qh4#", pgn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingApiKeyPreventsReviewProviderCall()
    {
        using var fixture = new Fixture(secretAvailable: false);
        var started = await (await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800)))
            .Content.ReadFromJsonAsync<GameStateDto>();
        await fixture.Client.PostAsJsonAsync("/api/games/active/resign", new ResignRequestDto("White"));

        using var response = await fixture.Client.PostAsync($"/api/games/{started!.GameId}/review", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, fixture.Ai.ReviewCalls);
        var detail = await fixture.Client.GetFromJsonAsync<HistoricalGameDto>($"/api/games/{started.GameId}");
        Assert.Null(detail!.Review);
    }

    [Fact]
    public async Task RequestedReviewFallsBackDeterministicallyWhenAiFails()
    {
        using var fixture = new Fixture(aiFailure: true);
        var started = await (await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 800)))
            .Content.ReadFromJsonAsync<GameStateDto>();
        await fixture.Client.PostAsJsonAsync("/api/games/active/resign", new ResignRequestDto("White"));

        using var generated = await fixture.Client.PostAsync($"/api/games/{started!.GameId}/review", null);

        generated.EnsureSuccessStatusCode();
        var review = await generated.Content.ReadFromJsonAsync<GameReview>();
        Assert.False(review!.UsedAi);
        Assert.Equal(1, fixture.Ai.ReviewCalls);
    }

    [Fact]
    public async Task ProviderSettingsRoundTripAndExplicitTestDoNotExposeASecret()
    {
        using var fixture = new Fixture();
        using var updated = await fixture.Client.PutAsJsonAsync("/api/settings/ai-provider", new
        {
            provider = "OpenAICompatible",
            baseUrl = "https://provider.invalid/v1",
            interactiveModel = "fake-interactive",
            reviewModel = "fake-review",
            timeoutSeconds = 12,
            maxRetryCount = 0
        });
        updated.EnsureSuccessStatusCode();
        var updateText = await updated.Content.ReadAsStringAsync();
        Assert.DoesNotContain("apiKey", updateText, StringComparison.OrdinalIgnoreCase);

        var settings = await fixture.Client.GetFromJsonAsync<AiProviderSettingsDto>("/api/settings/ai-provider");
        Assert.Equal("OpenAICompatible", settings!.Provider);
        Assert.Equal(12, settings.TimeoutSeconds);
        using var tested = await fixture.Client.PostAsync("/api/settings/ai-provider/test", null);
        tested.EnsureSuccessStatusCode();
        Assert.Contains("available", await tested.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResignedGameIsListedWithStoredReviewReplayPgnAndHardDeletion()
    {
        using var fixture = new Fixture();
        var started = await (await fixture.Client.PostAsJsonAsync("/api/games", new StartGameRequestDto("White", 900)))
            .Content.ReadFromJsonAsync<GameStateDto>();
        using var resigned = await fixture.Client.PostAsJsonAsync("/api/games/active/resign", new ResignRequestDto("White"));
        resigned.EnsureSuccessStatusCode();
        Assert.Equal(0, fixture.Ai.ReviewCalls);
        using var generated = await fixture.Client.PostAsync($"/api/games/{started!.GameId}/review", null);
        generated.EnsureSuccessStatusCode();
        Assert.Equal(1, fixture.Ai.ReviewCalls);

        var history = await fixture.Client.GetFromJsonAsync<GameHistoryPageDto>("/api/games?page=1");
        Assert.NotNull(history);
        Assert.Equal(25, history.PageSize);
        Assert.Single(history.Items);
        Assert.Equal(900, history.Items[0].OpponentElo);

        var detail = await fixture.Client.GetFromJsonAsync<HistoricalGameDto>($"/api/games/{started!.GameId}");
        Assert.NotNull(detail!.Review);
        Assert.True(detail.Review.UsedAi);
        _ = await fixture.Client.GetFromJsonAsync<GameReview>($"/api/games/{started.GameId}/review");
        Assert.Equal(1, fixture.Ai.ReviewCalls);

        var pgn = await fixture.Client.GetStringAsync($"/api/games/{started.GameId}/pgn");
        Assert.Contains("[OpponentElo \"900\"]", pgn, StringComparison.Ordinal);
        Assert.Contains("[PlayerColor \"White\"]", pgn, StringComparison.Ordinal);
        using var deleted = await fixture.Client.DeleteAsync($"/api/games/{started.GameId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var empty = await fixture.Client.GetFromJsonAsync<GameHistoryPageDto>("/api/games?page=1");
        Assert.Empty(empty!.Items);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Directory.CreateTempSubdirectory("blunderforge-web-").FullName;
        private readonly WebApplicationFactory<Program> factory;
        public Fixture(bool aiFailure = false, IReadOnlyList<string>? opponentMoves = null, bool secretAvailable = true)
        {
            Ai = new FakeAi(aiFailure);
            var path = Path.Combine(directory, "blunderforge.db");
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("BlunderForge:DataDirectory", directory);
                builder.UseSetting("ConnectionStrings:Default", $"Data Source={path}");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChessEngine>();
                    services.RemoveAll<IEngineHealthService>();
                    services.RemoveAll<IAiCoachProvider>();
                    services.RemoveAll<ISecretStatusService>();
                    services.AddSingleton<IChessEngine>(new FakeEngine(opponentMoves));
                    services.AddSingleton<IEngineHealthService>(new FakeHealth());
                    services.AddSingleton<IAiCoachProvider>(Ai);
                    services.AddSingleton<ISecretStatusService>(new FakeSecretStatus(secretAvailable));
                });
            });
            Client = factory.CreateClient();
        }
        public HttpClient Client { get; }
        public FakeAi Ai { get; }
        public void Dispose()
        {
            Client.Dispose(); factory.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(directory, true);
        }
    }

    private sealed class FakeEngine(IReadOnlyList<string>? opponentMoves) : IChessEngine
    {
        private readonly Queue<string> opponentMoves = new(opponentMoves ?? []);

        public Task<EngineAnalysisResult> AnalyzeAsync(EngineAnalysisRequest request, CancellationToken cancellationToken)
        {
            var move = request.MoveTimeMs == 500 && opponentMoves.TryDequeue(out var scripted)
                ? scripted
                : request.Fen.Contains(" b ", StringComparison.Ordinal) ? "e7e5" : "e2e4";
            var candidate = new CandidateMove(UciMove.Parse(move), EngineScore.FromCentipawns(20), [UciMove.Parse(move)], 1);
            return Task.FromResult(new EngineAnalysisResult("Fakefish", new EngineSettingsSnapshot("fake", 1, 16, request.MultiPv, request.MoveTimeMs, request.UciElo is not null, request.UciElo), [candidate]));
        }
    }
    private sealed class FakeHealth : IEngineHealthService
    {
        public Task<EngineHealthResult> CheckReadinessAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineHealthResult(true, "Ready", "Fake Stockfish is ready.", "Fakefish"));
    }
    private sealed class FakeSecretStatus(bool available) : ISecretStatusService
    {
        public SecretAvailability GetAvailability(SecretReference secretReference) => new(available, secretReference.EnvironmentVariable);
    }
    private sealed class FakeAi(bool fail) : IAiCoachProvider
    {
        public int Calls { get; private set; }
        public int ReviewCalls { get; private set; }
        public Task<AiCoachExplanation> GenerateMoveHelpAsync(AiCoachRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? Task.FromException<AiCoachExplanation>(new HttpRequestException("offline")) : Task.FromResult(new AiCoachExplanation("Hint", "Explanation"));
        }
        public Task<GameReviewResponse> GenerateGameReviewAsync(AiCoachRequest request, CancellationToken cancellationToken)
        {
            ReviewCalls++;
            return fail ? Task.FromException<GameReviewResponse>(new HttpRequestException("offline")) :
                Task.FromResult(new GameReviewResponse("A steady game", ["You kept playing after setbacks."], "Compare two candidates before moving."));
        }
        public Task<ProviderStatus> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(new ProviderStatus(!fail, "test"));
    }
}
