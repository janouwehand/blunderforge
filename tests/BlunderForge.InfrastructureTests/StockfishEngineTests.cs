using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Domain.Games;
using BlunderForge.Infrastructure.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlunderForge.InfrastructureTests;

public sealed class StockfishEngineTests
{
    [Fact]
    public async Task StockfishHealthReportsNotReadyWhenBinaryIsMissing()
    {
        await using var engine = CreateEngine(new StockfishOptions { Path = MissingPath() });

        var result = await engine.CheckReadinessAsync(CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal("NotReady", result.Status);
        Assert.Contains("not found", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [RealStockfishFact]
    public async Task StockfishStartsAndReportsReadinessWhenBinaryIsAvailable()
    {
        var path = ResolveStockfishPath()
            ?? throw new InvalidOperationException("Real Stockfish test was selected without an available binary.");

        await using var engine = CreateEngine(new StockfishOptions { Path = path, ProtocolTimeoutMs = 5000 });

        var result = await engine.CheckReadinessAsync(CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Equal("Ready", result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.EngineVersion));
    }

    [RealStockfishFact]
    public async Task StockfishReturnsBestMoveAndCandidateMetadataWhenBinaryIsAvailable()
    {
        var path = ResolveStockfishPath()
            ?? throw new InvalidOperationException("Real Stockfish test was selected without an available binary.");

        await using var engine = CreateEngine(new StockfishOptions
        {
            Path = path,
            AnalysisTimeMs = 100,
            MultiPv = 2,
            ProtocolTimeoutMs = 5000
        });

        var result = await engine.AnalyzeAsync(
            new EngineAnalysisRequest(ChessGame.StandardInitialFen, 100, 2),
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.EngineVersion));
        Assert.Equal(path, result.Settings.EnginePath);
        Assert.NotEmpty(result.Candidates);
        Assert.All(result.Candidates, candidate => Assert.True(UciMove.TryParse(candidate.Move.Value, out _)));
    }

    [RealStockfishFact]
    public async Task StockfishAnalysisObservesCancellationWhenBinaryIsAvailable()
    {
        var path = ResolveStockfishPath()
            ?? throw new InvalidOperationException("Real Stockfish test was selected without an available binary.");

        await using var engine = CreateEngine(new StockfishOptions { Path = path, ProtocolTimeoutMs = 5000 });
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.AnalyzeAsync(new EngineAnalysisRequest(ChessGame.StandardInitialFen, 5000, 1), cts.Token));
    }

    [Fact]
    public async Task StockfishThrowsProtocolExceptionForInvalidBestMove()
    {
        var previous = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE");
        Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", "protocol");
        try
        {
            await using var engine = CreateEngine(new StockfishOptions
            {
                Path = FakeStockfishPath(),
                ProtocolTimeoutMs = 1000
            });

            await Assert.ThrowsAsync<StockfishProtocolException>(() =>
                engine.AnalyzeAsync(new EngineAnalysisRequest(ChessGame.StandardInitialFen, 10, 1), CancellationToken.None));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", previous);
        }
    }

    [Fact]
    public async Task StockfishThrowsProtocolExceptionWhenProcessExitsUnexpectedly()
    {
        var previous = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE");
        Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", "exit");
        try
        {
            await using var engine = CreateEngine(new StockfishOptions
            {
                Path = FakeStockfishPath(),
                ProtocolTimeoutMs = 1000
            });

            await Assert.ThrowsAnyAsync<Exception>(() =>
                engine.AnalyzeAsync(new EngineAnalysisRequest(ChessGame.StandardInitialFen, 10, 1), CancellationToken.None));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", previous);
        }
    }

    [Fact]
    public async Task StockfishLogsLifecycleEventsWithoutRawProtocolOutput()
    {
        var previous = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE");
        Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", "normal");
        var logger = new CapturingLogger<StockfishEngine>();
        try
        {
            await using var engine = new StockfishEngine(
                Options.Create(new StockfishOptions { Path = FakeStockfishPath(), ProtocolTimeoutMs = 1000 }),
                logger);

            await engine.AnalyzeAsync(new EngineAnalysisRequest(ChessGame.StandardInitialFen, 10, 1), CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", previous);
        }

        var joined = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("Starting Stockfish process", joined, StringComparison.Ordinal);
        Assert.Contains("Stockfish UCI initialized", joined, StringComparison.Ordinal);
        Assert.Contains("Stockfish analysis completed", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("bestmove", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position fen", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StockfishLimitedStrengthDoesNotLeakIntoLaterAnalysis()
    {
        var log = Path.GetTempFileName();
        var previousMode = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE");
        var previousLog = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_COMMAND_LOG");
        Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", "normal");
        Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_COMMAND_LOG", log);
        try
        {
            await using var engine = CreateEngine(new StockfishOptions { Path = FakeStockfishPath(), ProtocolTimeoutMs = 1000 });
            var limited = await engine.AnalyzeAsync(new EngineAnalysisRequest(ChessGame.StandardInitialFen, 10, 1, 1320), default);
            var normal = await engine.AnalyzeAsync(new EngineAnalysisRequest(ChessGame.StandardInitialFen, 10, 8), default);
            Assert.True(limited.Settings.UciLimitStrength);
            Assert.False(normal.Settings.UciLimitStrength);
            Assert.Null(normal.Settings.UciElo);
            var commands = await File.ReadAllTextAsync(log);
            var enabled = commands.IndexOf("setoption name UCI_LimitStrength value true", StringComparison.Ordinal);
            var disabled = commands.LastIndexOf("setoption name UCI_LimitStrength value false", StringComparison.Ordinal);
            Assert.True(enabled >= 0 && disabled > enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE", previousMode);
            Environment.SetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_COMMAND_LOG", previousLog);
            File.Delete(log);
        }
    }

    private static StockfishEngine CreateEngine(StockfishOptions options)
    {
        return new StockfishEngine(Options.Create(options), NullLogger<StockfishEngine>.Instance);
    }

    private static string MissingPath()
    {
        return Path.Combine(Path.GetTempPath(), $"missing-stockfish-{Guid.NewGuid():N}");
    }

    internal static string? ResolveStockfishPath()
    {
        var configured = Environment.GetEnvironmentVariable("BLUNDERFORGE_TEST_STOCKFISH_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new[]
        {
            "/app/stockfish/stockfish",
            "/usr/games/stockfish",
            "/usr/bin/stockfish",
            "/opt/homebrew/bin/stockfish",
            "C:\\Program Files\\Stockfish\\stockfish.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string FakeStockfishPath()
    {
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var configuration = "Debug";
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BlunderForge.FakeStockfish",
            "bin",
            configuration,
            "net10.0",
            $"BlunderForge.FakeStockfish{extension}"));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Fake Stockfish test executable was not built.", path);
        }

        return path;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class RealStockfishFactAttribute : FactAttribute
{
    public RealStockfishFactAttribute()
    {
        if (StockfishEngineTests.ResolveStockfishPath() is null)
        {
            Skip = "Stockfish binary is unavailable. Set BLUNDERFORGE_TEST_STOCKFISH_PATH to run this real-engine integration test.";
        }
    }
}
