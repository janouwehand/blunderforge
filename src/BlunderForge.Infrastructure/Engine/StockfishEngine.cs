using System.Collections.Concurrent;
using System.Diagnostics;
using BlunderForge.Application.Configuration;
using BlunderForge.Application.Engine;
using BlunderForge.Domain.Games;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlunderForge.Infrastructure.Engine;

public sealed class StockfishEngine : IChessEngine, IEngineHealthService, IAsyncDisposable
{
    private static readonly Action<ILogger, long, int, Exception?> AnalysisCompleted =
        LoggerMessage.Define<long, int>(
            LogLevel.Information,
            new EventId(1001, nameof(AnalysisCompleted)),
            "Stockfish analysis completed in {DurationMs} ms with {CandidateCount} candidates.");

    private static readonly Action<ILogger, Exception?> ReadinessFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1002, nameof(ReadinessFailed)),
            "Stockfish readiness check failed.");

    private static readonly Action<ILogger, Exception?> ExitNotClean =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1003, nameof(ExitNotClean)),
            "Stockfish did not exit cleanly.");

    private static readonly Action<ILogger, Exception?> ProcessDisposed =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1004, nameof(ProcessDisposed)),
            "Stockfish process disposed.");

    private static readonly Action<ILogger, string, Exception?> StartingProcess =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1005, nameof(StartingProcess)),
            "Starting Stockfish process from {StockfishPath}.");

    private static readonly Action<ILogger, int?, Exception?> ProcessExited =
        LoggerMessage.Define<int?>(
            LogLevel.Warning,
            new EventId(1006, nameof(ProcessExited)),
            "Stockfish process exited with code {ExitCode}.");

    private static readonly Action<ILogger, string, Exception?> UciInitialized =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1007, nameof(UciInitialized)),
            "Stockfish UCI initialized with engine version {EngineVersion}.");

    private readonly StockfishOptions options;
    private readonly ILogger<StockfishEngine> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeSpan startupTimeout = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<int, CandidateMove> latestCandidates = [];

    private Process? process;
    private string? engineVersion;
    private bool disposed;

    public StockfishEngine(IOptions<StockfishOptions> options, ILogger<StockfishEngine> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<EngineAnalysisResult> AnalyzeAsync(EngineAnalysisRequest request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await gate.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await EnsureStartedAsync(cancellationToken);
            latestCandidates.Clear();

            await SendCommandAsync($"setoption name Threads value {options.Threads}", cancellationToken);
            await SendCommandAsync($"setoption name Hash value {options.HashSizeMb}", cancellationToken);
            await SendCommandAsync($"setoption name MultiPV value {request.MultiPv}", cancellationToken);
            if (request.UciElo is not null)
            {
                await SendCommandAsync("setoption name UCI_LimitStrength value true", cancellationToken);
                await SendCommandAsync($"setoption name UCI_Elo value {request.UciElo.Value}", cancellationToken);
            }
            else
            {
                await SendCommandAsync("setoption name UCI_LimitStrength value false", cancellationToken);
            }

            await WaitUntilReadyAsync(cancellationToken);
            await SendCommandAsync($"position fen {request.Fen}", cancellationToken);
            await SendCommandAsync($"go movetime {request.MoveTimeMs}", cancellationToken);

            var bestMove = await ReadUntilBestMoveAsync(TimeSpan.FromMilliseconds(request.MoveTimeMs + options.ProtocolTimeoutMs), cancellationToken);
            var candidates = BuildCandidates(bestMove);
            stopwatch.Stop();
            AnalysisCompleted(logger, stopwatch.ElapsedMilliseconds, candidates.Length, null);

            return new EngineAnalysisResult(
                engineVersion ?? "Stockfish unknown",
                new EngineSettingsSnapshot(
                    options.Path,
                    options.Threads,
                    options.HashSizeMb,
                    request.MultiPv,
                    request.MoveTimeMs,
                    request.UciElo is not null,
                    request.UciElo),
                candidates);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EngineHealthResult> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.Path))
        {
            return new EngineHealthResult(false, "NotReady", $"Stockfish binary was not found at '{options.Path}'.", null);
        }

        try
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await EnsureStartedAsync(cancellationToken);
                await WaitUntilReadyAsync(cancellationToken);
                return new EngineHealthResult(true, "Ready", "Stockfish responded to isready.", engineVersion);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReadinessFailed(logger, ex);
            return new EngineHealthResult(false, "NotReady", "Stockfish did not become ready.", engineVersion);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await gate.WaitAsync();
        try
        {
            if (process is null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    await SendCommandAsync("quit", CancellationToken.None);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(timeout.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or StockfishProtocolException)
            {
                ExitNotClean(logger, ex);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                process.Dispose();
                process = null;
                ProcessDisposed(logger, null);
            }
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (process is { HasExited: false })
        {
            return;
        }

        if (!File.Exists(options.Path))
        {
            throw new FileNotFoundException("Stockfish binary was not found.", options.Path);
        }

        process?.Dispose();
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = options.Path,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) => ProcessExited(logger, TryGetExitCode(), null);

        StartingProcess(logger, options.Path, null);
        if (!process.Start())
        {
            throw new InvalidOperationException("Stockfish process could not be started.");
        }

        await SendCommandAsync("uci", cancellationToken);
        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(startupTimeout);

        while (true)
        {
            var line = await ReadLineAsync(startupCts.Token);
            if (line.StartsWith("id name ", StringComparison.Ordinal))
            {
                engineVersion = line["id name ".Length..];
            }
            else if (line == "uciok")
            {
                UciInitialized(logger, engineVersion ?? "unknown", null);
                return;
            }
        }
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        await SendCommandAsync("isready", cancellationToken);
        using var readyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readyCts.CancelAfter(TimeSpan.FromMilliseconds(options.ProtocolTimeoutMs));

        while (true)
        {
            var line = await ReadLineAsync(readyCts.Token);
            if (line == "readyok")
            {
                return;
            }
        }
    }

    private async Task<string> ReadUntilBestMoveAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var analysisCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        analysisCts.CancelAfter(timeout);

        while (true)
        {
            var line = await ReadLineAsync(analysisCts.Token);
            if (line.StartsWith("info ", StringComparison.Ordinal))
            {
                TryCaptureCandidate(line);
            }
            else if (line.StartsWith("bestmove ", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    throw new StockfishProtocolException($"Stockfish returned an invalid bestmove line: {line}");
                }

                // Stockfish returns "bestmove (none)" or "bestmove 0000" for terminal positions
                // (checkmate, stalemate) where no legal moves exist.
                if (parts[1] is "(none)" or "0000")
                {
                    return parts[1];
                }

                if (!UciMove.TryParse(parts[1], out _))
                {
                    throw new StockfishProtocolException($"Stockfish returned an invalid bestmove line: {line}");
                }

                return parts[1];
            }
        }
    }

    private CandidateMove[] BuildCandidates(string bestMove)
    {
        // Terminal position (checkmate/stalemate) — Stockfish returns "(none)" or "0000".
        if (bestMove is "(none)" or "0000")
        {
            return [];
        }

        if (latestCandidates.IsEmpty)
        {
            return [new CandidateMove(UciMove.Parse(bestMove), EngineScore.FromCentipawns(0), [UciMove.Parse(bestMove)], 1)];
        }

        return latestCandidates.Values
            .Where(candidate => candidate.Move.Value != "0000")
            .OrderBy(candidate => candidate.Rank)
            .ToArray();
    }

    private void TryCaptureCandidate(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var multipv = ReadIntAfter(parts, "multipv") ?? 1;
        var scoreIndex = Array.IndexOf(parts, "score");
        var pvIndex = Array.IndexOf(parts, "pv");

        if (scoreIndex < 0 || pvIndex < 0 || pvIndex + 1 >= parts.Length)
        {
            return;
        }

        EngineScore? score = null;
        if (scoreIndex + 2 < parts.Length && parts[scoreIndex + 1] == "cp" && int.TryParse(parts[scoreIndex + 2], out var cp))
        {
            score = EngineScore.FromCentipawns(cp);
        }
        else if (scoreIndex + 2 < parts.Length && parts[scoreIndex + 1] == "mate" && int.TryParse(parts[scoreIndex + 2], out var mate))
        {
            score = EngineScore.FromMate(mate);
        }

        if (score is null || !UciMove.TryParse(parts[pvIndex + 1], out var move))
        {
            return;
        }

        var pv = parts[(pvIndex + 1)..]
            .Where(part => UciMove.TryParse(part, out _))
            .Select(UciMove.Parse)
            .ToArray();

        latestCandidates[multipv] = new CandidateMove(move, score, pv, multipv);
    }

    private static int? ReadIntAfter(string[] parts, string token)
    {
        var index = Array.IndexOf(parts, token);
        return index >= 0 && index + 1 < parts.Length && int.TryParse(parts[index + 1], out var value)
            ? value
            : null;
    }

    private async Task SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        var runningProcess = GetRunningProcess();
        await runningProcess.StandardInput.WriteLineAsync(command.AsMemory(), cancellationToken);
        await runningProcess.StandardInput.FlushAsync(cancellationToken);
    }

    private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        var runningProcess = GetRunningProcess();
        var line = await runningProcess.StandardOutput.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            throw new StockfishProtocolException("Stockfish closed stdout unexpectedly.");
        }

        return line;
    }

    private Process GetRunningProcess()
    {
        if (process is null)
        {
            throw new InvalidOperationException("Stockfish process is not started.");
        }

        if (process.HasExited)
        {
            throw new StockfishProtocolException($"Stockfish process exited unexpectedly with code {TryGetExitCode()}.");
        }

        return process;
    }

    private int? TryGetExitCode()
    {
        try
        {
            return process?.HasExited == true ? process.ExitCode : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
