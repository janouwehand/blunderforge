using System.Diagnostics;
using BlunderForge.Application.Ai;
using BlunderForge.Application.Coaching;
using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Application.Games;
using BlunderForge.Application.Reviews;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure;
using BlunderForge.Infrastructure.Configuration;
using BlunderForge.Infrastructure.Persistence;
using BlunderForge.Web.Contracts;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.AddOptions<ApplicationDataOptions>().Bind(builder.Configuration.GetSection(ApplicationDataOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.DataDirectory), "Data directory is required.").ValidateOnStart();
builder.Services.AddOptions<StockfishOptions>().Bind(builder.Configuration.GetSection(StockfishOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Path), "Stockfish path is required.")
    .Validate(options => options.AnalysisTimeMs > 0 && options.QuickAnalysisTimeMs > 0 && options.OpponentMoveTimeMs > 0, "Stockfish analysis times must be positive.")
    .Validate(options => options.Threads > 0 && options.HashSizeMb > 0 && options.MultiPv > 0 && options.LowEloMultiPv >= 8, "Stockfish resource and MultiPV settings are invalid.")
    .Validate(options => options.ProtocolTimeoutMs > 0, "Stockfish protocol timeout must be positive.").ValidateOnStart();
builder.Services.AddOptions<AiProviderOptions>().Bind(builder.Configuration.GetSection(AiProviderOptions.SectionName))
    .Validate(options => options.TimeoutSeconds > 0 && options.MaxRetryCount is >= 0 and <= 1, "AI timeout or retry count is invalid.").ValidateOnStart();
builder.Services.AddOptions<MoveClassificationOptions>().Bind(builder.Configuration.GetSection(MoveClassificationOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<DatabaseOptions>().Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Default), "Default connection string is required.").ValidateOnStart();

var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var aiOptions = builder.Configuration.GetSection(AiProviderOptions.SectionName).Get<AiProviderOptions>() ?? new AiProviderOptions();
builder.Services.AddBlunderForgeInfrastructure(databaseOptions.Default, aiOptions);
builder.Services.AddScoped<GameSessionService>();

var app = builder.Build();
const long MaxApiRequestBodyBytes = 64 * 1024;
const string CorrelationHeader = "X-Correlation-ID";
const string CorrelationItem = "CorrelationId";

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Json(new ErrorResponseDto("UnexpectedError", "An unexpected error occurred.", context.Items[CorrelationItem]?.ToString() ?? context.TraceIdentifier), statusCode: 500).ExecuteAsync(context);
    }));
}

app.Use(async (context, next) =>
{
    var suppliedCorrelationId = context.Request.Headers.TryGetValue(CorrelationHeader, out var values) ? values.FirstOrDefault() : null;
    var correlationId = string.IsNullOrWhiteSpace(suppliedCorrelationId) ? context.TraceIdentifier : suppliedCorrelationId.Trim();
    context.Items[CorrelationItem] = correlationId;
    context.Response.OnStarting(() => { context.Response.Headers[CorrelationHeader] = correlationId; return Task.CompletedTask; });
    await next(context);
});
app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    await next(context);
    if (app.Logger.IsEnabled(LogLevel.Information))
    {
        var operationName = context.GetEndpoint()?.DisplayName ?? "UnmatchedRequest";
        var durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var correlationId = Correlation(context);
        Program.LogHttpOperation(app.Logger, operationName, durationMs, context.Response.StatusCode, correlationId);
    }
});
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && context.Request.ContentLength > MaxApiRequestBodyBytes)
    {
        await Results.Json(new ErrorResponseDto("RequestTooLarge", $"Request body must be {MaxApiRequestBodyBytes} bytes or smaller.", Correlation(context)), statusCode: 413).ExecuteAsync(context);
        return;
    }
    await next(context);
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy", "Application liveness is OK.")));
app.MapGet("/health/live", () => Results.Ok(new HealthResponse("Healthy", "Application liveness is OK.")));
app.MapGet("/ready", async (IOptions<ApplicationDataOptions> data, DatabaseMigrationStatus migration, IEngineHealthService engine, CancellationToken token) =>
{
    var pathReady = Directory.Exists(data.Value.DataDirectory) || CanCreateDirectory(data.Value.DataDirectory);
    var databaseReady = pathReady && migration.IsReady;
    var stockfish = await engine.CheckReadinessAsync(token);
    var ready = databaseReady && stockfish.IsReady;
    return Results.Json(new ReadinessResponse(ready ? "Ready" : "Degraded",
        new ComponentStatus("Database", databaseReady ? "Ready" : "NotReady", pathReady ? migration.Detail : "SQLite data directory is not available."),
        new ComponentStatus("Stockfish", stockfish.Status, stockfish.Detail)), statusCode: ready ? 200 : 503);
});
app.MapGet("/health/ai", async (IAiCoachProvider provider, CancellationToken token) =>
{
    var status = await provider.TestConnectionAsync(token);
    return Results.Ok(new HealthResponse(status.Available ? "Available" : "OptionalUnavailable", status.Detail));
});
app.MapGet("/api/system/info", (IOptions<ApplicationDataOptions> data, IOptions<StockfishOptions> stockfish) => Results.Ok(new SystemInfoResponse(
    "BlunderForge", typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0", !string.IsNullOrWhiteSpace(data.Value.DataDirectory), !string.IsNullOrWhiteSpace(stockfish.Value.Path))));

app.MapPost("/api/games", async (StartGameRequestDto request, GameSessionService games, HttpContext context, CancellationToken token) =>
{
    if (!Enum.TryParse<PlayerColorChoice>(request.PlayerColorChoice, true, out var color))
        return Error(context, "InvalidRequest", "PlayerColorChoice must be White, Black, or Random.");
    OpponentElo elo;
    try { elo = new OpponentElo(request.OpponentElo); }
    catch (ArgumentOutOfRangeException ex) { return Error(context, "InvalidOpponentElo", ex.Message); }
    try { return Results.Created("/api/games/active", MapState(await games.StartNewGameAsync(new StartGameRequest(color, elo), token))); }
    catch (InvalidOperationException ex) { return Error(context, "ActiveGameExists", ex.Message, 409); }
});

app.MapGet("/api/games/active", async (GameSessionService games, HttpContext context, CancellationToken token) =>
    await games.GetActiveGameAsync(token) is { } state ? Results.Ok(MapState(state)) : Error(context, "NotFound", "No active game exists.", 404));

app.MapPost("/api/games/active/moves", async (SubmitMoveRequestDto request, CoachFlowService coach, HttpContext context, CancellationToken token) =>
{
    if (!UciMove.TryParse(request.Uci, out var move)) return Error(context, "InvalidMove", "Move must use UCI notation such as e2e4 or e7e8q.");
    try
    {
        var result = await coach.SubmitPlayerMoveAsync(move, token);
        return Results.Ok(new MoveResultDto(MapMove(result.Move), MapState(result.State), result.State.Status == GameStatus.Active && result.State.ActiveSide != result.State.Settings.PlayerSide));
    }
    catch (GameConcurrencyException ex) { return Error(context, "ConcurrentGameUpdate", ex.Message, 409); }
    catch (InvalidOperationException ex) { return Error(context, "IllegalMove", ex.Message); }
});

app.MapPost("/api/games/active/opponent-turn", async (CoachFlowService coach, HttpContext context, CancellationToken token) =>
{
    try
    {
        var result = await coach.CompleteNpcTurnAsync(token);
        return Results.Ok(new MoveResultDto(MapMove(result.Move), MapState(result.State)));
    }
    catch (InvalidOperationException ex) { return Error(context, "InvalidState", ex.Message); }
    catch (Exception ex) when (!token.IsCancellationRequested)
    {
        Program.LogEngineFailure(app.Logger, ex, "opponent-turn");
        return Error(context, "EngineUnavailable", "The opponent move could not be completed. Your move remains saved; try again.", 503);
    }
});

app.MapPost("/api/games/active/coach", async (CoachRequestDto request, CoachFlowService coach, HttpContext context, CancellationToken token) =>
{
    try
    {
        var result = await coach.RequestCoachAsync(request.UseAiExplanation, token);
        return Results.Ok(new CoachHelpDto(result.RecommendedMove, result.RecommendedMoveUci, result.TextAlternative, result.HighlightSquares,
            new CoachArrowDto(result.Arrow.From, result.Arrow.To), result.Hint, result.Explanation, result.AiStatus));
    }
    catch (InvalidOperationException ex) { return Error(context, "InvalidState", ex.Message); }
    catch (Exception ex) when (!token.IsCancellationRequested)
    {
        Program.LogEngineFailure(app.Logger, ex, "coach");
        return Error(context, "EngineUnavailable", "Stockfish coaching is temporarily unavailable. The game remains valid.", 503);
    }
});

app.MapPost("/api/games/active/takeback", async (CoachFlowService coach, HttpContext context, CancellationToken token) =>
{
    try { return Results.Ok(MapState(await coach.TakeBackPlayerTurnAsync(token))); }
    catch (InvalidOperationException ex) { return Error(context, "InvalidState", ex.Message); }
});

app.MapGet("/api/games/active/moves/legal", async (GameSessionService games, HttpContext context, CancellationToken token) =>
{
    var state = await games.GetActiveGameAsync(token);
    if (state is null) return Error(context, "NotFound", "No active game exists.", 404);
    var moves = (await games.GetLegalMovesAsync(token)).Select(move => new LegalMoveDto(move.From + move.To + (move.Promotion ?? ""), move.From, move.To, move.Promotion)).ToList();
    return Results.Ok(new LegalMovesResponseDto(state.CurrentFen, moves));
});

app.MapPost("/api/games/active/resign", async (ResignRequestDto request, GameSessionService games, HttpContext context, CancellationToken token) =>
{
    if (!Enum.TryParse<Side>(request.Side, true, out var side)) return Error(context, "InvalidRequest", "Side must be White or Black.");
    var active = await games.GetActiveGameAsync(token);
    if (active is null) return Error(context, "NotFound", "No active game exists.", 404);
    if (side != active.Settings.PlayerSide) return Error(context, "InvalidRequest", "Only the player's side can resign.");
    try
    {
        return Results.Ok(MapState(await games.ResignAsync(side, token)));
    }
    catch (InvalidOperationException ex) { return Error(context, "InvalidState", ex.Message); }
});

app.MapDelete("/api/games/active", async (GameSessionService games, HttpContext context, CancellationToken token) =>
    await games.DeleteActiveGameAsync(token) is null ? Error(context, "NotFound", "No active game exists.", 404) : Results.NoContent());
app.MapDelete("/api/games/{gameId:guid}", async (Guid gameId, IActiveGameRepository repository, HttpContext context, CancellationToken token) =>
    await repository.DeleteAsync(gameId, token) ? Results.NoContent() : Error(context, "NotFound", "Game was not found.", 404));

app.MapGet("/api/games", async (int? page, GameReviewService reviews, HttpContext context, CancellationToken token) =>
{
    var requestedPage = page ?? 1;
    if (requestedPage < 1) return Error(context, "InvalidPage", "Page must be 1 or greater.");
    var result = await reviews.ListAsync(requestedPage, token);
    return Results.Ok(new GameHistoryPageDto(result.Items.Select(item => new GameHistoryItemDto(item.GameId.ToString(), item.Date,
        item.Result, item.PlayerSide.ToString(), item.OpponentElo)).ToArray(), result.Page, result.PageSize, result.TotalCount, result.TotalPages));
});
app.MapGet("/api/games/{gameId:guid}", async (Guid gameId, IGameReviewRepository repository, HttpContext context, CancellationToken token) =>
{
    var game = await repository.GetAsync(gameId, token);
    if (game is null) return Error(context, "NotFound", "Game was not found.", 404);
    var review = await repository.GetStoredReviewAsync(gameId, token);
    return Results.Ok(new HistoricalGameDto(game.Id.ToString(), game.StartedAt, game.CompletedAt, game.Status.ToString(), game.Result.ToString(),
        game.PlayerSide.ToString(), game.OpponentElo, ChessGame.StandardInitialFen,
        game.Moves.Select(move => new MoveRecordDto(move.Ply, move.IsOpponentMove ? Opposite(game.PlayerSide).ToString() : game.PlayerSide.ToString(),
            move.San, move.Uci, move.FenBefore, move.FenAfter)).ToArray(), review));
});

app.MapGet("/api/games/{gameId:guid}/review", async (Guid gameId, GameReviewService reviews, HttpContext context, CancellationToken token) =>
{
    try { return Results.Ok(await reviews.GetAsync(gameId, token)); }
    catch (KeyNotFoundException ex) { return Error(context, "NotFound", ex.Message, 404); }
    catch (InvalidOperationException ex) { return Error(context, "InvalidState", ex.Message); }
});
app.MapPost("/api/games/{gameId:guid}/review", async (Guid gameId, GameReviewService reviews, IGameReviewRepository repository,
    IOptions<AiProviderOptions> configured, IAiProviderSettingsStore settings, ISecretStatusService secrets, HttpContext context, CancellationToken token) =>
{
    var game = await repository.GetAsync(gameId, token);
    if (game is null) return Error(context, "NotFound", "Game was not found.", 404);
    if (game.Status == GameStatus.Active) return Error(context, "InvalidState", "A review is only available after the game is completed or resigned.");
    var stored = await repository.GetStoredReviewAsync(gameId, token);
    if (stored is not null) return Results.Ok(stored);
    if (!await IsAiAvailableAsync(configured.Value, settings, secrets, token))
        return Error(context, "AiUnavailable", "Configure the AI provider and its required API key before generating a review.", 409);
    return Results.Ok(await reviews.GenerateOnceAsync(gameId, token));
});
app.MapGet("/api/games/{gameId:guid}/pgn", async (Guid gameId, IGameReviewRepository repository, HttpContext context, CancellationToken token) =>
{
    var game = await repository.GetAsync(gameId, token);
    if (game is null) return Error(context, "NotFound", "Game was not found.", 404);
    if (game.Status == GameStatus.Active) return Error(context, "InvalidState", "PGN export is available after the game is completed or resigned.");
    return Results.Text(PgnExporter.Export(game), "application/x-chess-pgn", System.Text.Encoding.UTF8, 200);
});

app.MapGet("/api/settings/ai-provider", async (IOptions<AiProviderOptions> configured, ISecretStatusService secrets, IAiProviderSettingsStore store, CancellationToken token) =>
{
    var stored = await store.GetAsync(token);
    var provider = stored?.Provider ?? configured.Value.Provider;
    var secret = provider.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase) ? configured.Value.OpenAiCompatibleApiKey : configured.Value.DeepSeekApiKey;
    var availability = secrets.GetAvailability(secret);
    var baseUrl = stored?.BaseUrl ?? configured.Value.BaseUrl;
    var interactiveModel = stored?.InteractiveModel ?? configured.Value.InteractiveModel;
    var reviewModel = stored?.ReviewModel ?? configured.Value.ReviewModel;
    var isConfigured = Uri.TryCreate(baseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(interactiveModel) && !string.IsNullOrWhiteSpace(reviewModel) && availability.IsConfigured;
    return Results.Ok(new AiProviderSettingsDto(provider, baseUrl, interactiveModel,
        reviewModel, stored?.TimeoutSeconds ?? configured.Value.TimeoutSeconds, stored?.MaxRetryCount ?? configured.Value.MaxRetryCount,
        isConfigured, availability.IsConfigured, availability.Source));
});
app.MapPut("/api/settings/ai-provider", async (UpdateAiProviderSettingsRequestDto request, IOptions<AiProviderOptions> configured, IAiProviderSettingsStore store, ISecretStatusService secrets, HttpContext context, CancellationToken token) =>
{
    var current = await store.GetAsync(token);
    var value = new StoredAiProviderSettings(request.Provider ?? current?.Provider ?? configured.Value.Provider, request.BaseUrl ?? current?.BaseUrl ?? configured.Value.BaseUrl,
        request.InteractiveModel ?? current?.InteractiveModel ?? configured.Value.InteractiveModel, request.ReviewModel ?? current?.ReviewModel ?? configured.Value.ReviewModel,
        request.TimeoutSeconds ?? current?.TimeoutSeconds ?? configured.Value.TimeoutSeconds, request.MaxRetryCount ?? current?.MaxRetryCount ?? configured.Value.MaxRetryCount);
    if (value.Provider is not ("DeepSeek" or "OpenAICompatible") || !Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out _) || value.TimeoutSeconds <= 0 || value.MaxRetryCount is < 0 or > 1 || string.IsNullOrWhiteSpace(value.InteractiveModel) || string.IsNullOrWhiteSpace(value.ReviewModel))
        return Error(context, "InvalidProviderSettings", "Check the provider, base URL, models, timeout, and retry count.");
    await store.SetAsync(value, token);
    var secret = value.Provider == "OpenAICompatible" ? configured.Value.OpenAiCompatibleApiKey : configured.Value.DeepSeekApiKey;
    var availability = secrets.GetAvailability(secret);
    return Results.Ok(new AiProviderSettingsDto(value.Provider, value.BaseUrl, value.InteractiveModel, value.ReviewModel, value.TimeoutSeconds, value.MaxRetryCount, availability.IsConfigured, availability.IsConfigured, availability.Source));
});
app.MapPost("/api/settings/ai-provider/test", async (IAiCoachProvider provider, CancellationToken token) => Results.Ok(await provider.TestConnectionAsync(token)));
app.MapGet("/api/settings/ai-provider/status", (IOptions<AiProviderOptions> configured, ISecretStatusService secrets) =>
{
    var secret = configured.Value.Provider.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase) ? configured.Value.OpenAiCompatibleApiKey : configured.Value.DeepSeekApiKey;
    var availability = secrets.GetAvailability(secret);
    return Results.Ok(new AiProviderStatusResponse(configured.Value.Provider, configured.Value.BaseUrl, configured.Value.InteractiveModel, configured.Value.ReviewModel, availability.IsConfigured, availability.Source));
});

app.MapFallbackToFile("index.html");
app.Run();

static GameStateDto MapState(GameState state) => new(state.GameId.ToString(), new GameSettingsDto(state.Settings.PlayerColorChoice.ToString(), state.Settings.PlayerSide.ToString(), state.Settings.OpponentElo.Value),
    state.Status.ToString(), state.Result.ToString(), state.TerminationReason.ToString(), state.ActiveSide.ToString(), state.CurrentFen, state.Moves.Select(MapMove).ToList(), state.WhiteKingInCheck, state.BlackKingInCheck);
static MoveRecordDto MapMove(MoveRecord move) => new(move.Ply, move.Side.ToString(), move.San, move.Uci.Value, move.FenBefore, move.FenAfter);
static Side Opposite(Side side) => side is Side.White ? Side.Black : Side.White;
static IResult Error(HttpContext context, string code, string detail, int status = 400) => Results.Json(new ErrorResponseDto(code, detail, Correlation(context)), statusCode: status);
static string Correlation(HttpContext context) => context.Items.TryGetValue("CorrelationId", out var value) ? value?.ToString() ?? context.TraceIdentifier : context.TraceIdentifier;
static bool CanCreateDirectory(string path) { try { Directory.CreateDirectory(path); return true; } catch { return false; } }
static async Task<bool> IsAiAvailableAsync(AiProviderOptions configured, IAiProviderSettingsStore settings, ISecretStatusService secrets, CancellationToken token)
{
    var stored = await settings.GetAsync(token);
    var provider = stored?.Provider ?? configured.Provider;
    var secret = provider.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)
        ? configured.OpenAiCompatibleApiKey
        : configured.DeepSeekApiKey;
    var baseUrl = stored?.BaseUrl ?? configured.BaseUrl;
    var interactiveModel = stored?.InteractiveModel ?? configured.InteractiveModel;
    var reviewModel = stored?.ReviewModel ?? configured.ReviewModel;
    return Uri.TryCreate(baseUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(interactiveModel)
        && !string.IsNullOrWhiteSpace(reviewModel)
        && secrets.GetAvailability(secret).IsConfigured;
}

public partial class Program
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, SkipEnabledCheck = true, Message = "HTTP operation {OperationName} completed in {DurationMs}ms with outcome {OutcomeStatus} and correlation {CorrelationId}")]
    internal static partial void LogHttpOperation(ILogger logger, string operationName, double durationMs, int outcomeStatus, string correlationId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Stockfish operation failed during {OperationName}")]
    internal static partial void LogEngineFailure(ILogger logger, Exception exception, string operationName);
}
