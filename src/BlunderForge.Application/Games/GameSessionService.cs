using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Games;

public sealed class GameSessionService(IActiveGameRepository repository)
{
    public async Task<GameState> StartNewGameAsync(StartGameRequest request, CancellationToken cancellationToken)
    {
        if (await repository.GetActiveAsync(cancellationToken) is not null)
        {
            throw new InvalidOperationException("An active game already exists.");
        }

        var playerSide = ResolvePlayerSide(request.PlayerColorChoice);
        var settings = new GameSettings(request.PlayerColorChoice, playerSide, request.OpponentElo);
        var game = ChessGame.Start(settings);

        await repository.SaveActiveAsync(game, cancellationToken);

        return game.ToState();
    }

    public async Task<GameState?> GetActiveGameAsync(CancellationToken cancellationToken)
    {
        var game = await repository.GetActiveAsync(cancellationToken);
        return game?.ToState();
    }

    public async Task<MoveResult> ApplyMoveAsync(UciMove move, CancellationToken cancellationToken)
    {
        var game = await GetRequiredActiveGameAsync(cancellationToken);
        if (!game.IsLegalMove(move) && TryGetDuplicateMove(game, move, out var duplicate))
        {
            return new MoveResult(duplicate!, game.ToState());
        }

        var result = game.ApplyMove(move);

        try
        {
            await repository.SaveActiveAsync(game, cancellationToken);
        }
        catch (GameConcurrencyException)
        {
            var current = await GetRequiredActiveGameAsync(cancellationToken);
            if (TryGetDuplicateMove(current, move, out duplicate))
            {
                return new MoveResult(duplicate!, current.ToState());
            }

            throw;
        }

        return result;
    }

    public async Task<bool> ValidateMoveAsync(UciMove move, CancellationToken cancellationToken)
    {
        var game = await GetRequiredActiveGameAsync(cancellationToken);
        return game.IsLegalMove(move);
    }

    public async Task<IReadOnlyList<ChessGame.LegalMoveInfo>> GetLegalMovesAsync(CancellationToken cancellationToken)
    {
        var game = await GetRequiredActiveGameAsync(cancellationToken);
        return game.GetLegalMoves();
    }

    public async Task<GameState> ResignAsync(Side side, CancellationToken cancellationToken)
    {
        var game = await GetRequiredActiveGameAsync(cancellationToken);
        game.Resign(side);

        await repository.SaveActiveAsync(game, cancellationToken);

        return game.ToState();
    }

    public async Task<MoveResult> ApplyNpcMoveAsync(UciMove move, CancellationToken cancellationToken)
    {
        return await ApplyMoveAsync(move, cancellationToken);
    }

    public async Task<GameState> TakeBackLastMoveAsync(CancellationToken cancellationToken)
    {
        var game = await GetRequiredActiveGameAsync(cancellationToken);
        if (game.Moves.Count == 0)
        {
            throw new InvalidOperationException("No move is available to take back.");
        }

        var lastMove = game.Moves[^1];

        var remainingMoves = game.Moves.Take(game.Moves.Count - 1).ToArray();
        var rewound = ChessGame.Rehydrate(
            game.Id,
            game.Settings,
            lastMove.FenBefore,
            remainingMoves,
            GameStatus.Active,
            GameResult.None,
            GameTerminationReason.None,
            game.Version);

        await repository.ReplaceActiveAsync(rewound, cancellationToken);

        return rewound.ToState();
    }

    public async Task<GameState> TakeBackPlayerTurnAsync(CancellationToken cancellationToken)
    {
        var game = await GetRequiredActiveGameAsync(cancellationToken);
        var playerMoveIndex = game.Moves
            .Select((move, index) => (move, index))
            .LastOrDefault(entry => entry.move.Side == game.Settings.PlayerSide).index;
        if (game.Moves.Count == 0 || game.Moves[playerMoveIndex].Side != game.Settings.PlayerSide)
            throw new InvalidOperationException("No player move is available to take back.");

        var playerMove = game.Moves[playerMoveIndex];
        var remainingMoves = game.Moves.Take(playerMoveIndex).ToArray();
        var rewound = ChessGame.Rehydrate(game.Id, game.Settings, playerMove.FenBefore, remainingMoves,
            GameStatus.Active, GameResult.None, GameTerminationReason.None, game.Version);
        await repository.ReplaceActiveAsync(rewound, cancellationToken);
        return rewound.ToState();
    }

    public async Task<GameState?> DeleteActiveGameAsync(CancellationToken cancellationToken)
    {
        var game = await repository.GetActiveAsync(cancellationToken);
        if (game is null)
        {
            return null;
        }

        await repository.ClearActiveAsync(cancellationToken);
        return game.ToState();
    }

    private async Task<ChessGame> GetRequiredActiveGameAsync(CancellationToken cancellationToken)
    {
        return await repository.GetActiveAsync(cancellationToken)
            ?? throw new InvalidOperationException("No active game exists.");
    }

    private static Side ResolvePlayerSide(PlayerColorChoice choice)
    {
        return choice switch
        {
            PlayerColorChoice.White => Side.White,
            PlayerColorChoice.Black => Side.Black,
            PlayerColorChoice.Random => Random.Shared.Next(0, 2) == 0 ? Side.White : Side.Black,
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unsupported player color choice.")
        };
    }

    private static bool TryGetDuplicateMove(ChessGame game, UciMove move, out MoveRecord? duplicate)
    {
        duplicate = game.Moves.Count > 0 && game.Moves[^1].Uci == move
            ? game.Moves[^1]
            : null;
        return duplicate is not null;
    }
}
