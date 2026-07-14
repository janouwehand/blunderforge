using BlunderForge.Domain.Games;

namespace BlunderForge.DomainTests;

public sealed class ChessGameRulesTests
{
    [Fact]
    public void NewGameStartsFromStandardFen()
    {
        var game = ChessGame.Start(DefaultSettings());

        Assert.Equal(Side.White, game.ActiveSide);
        Assert.StartsWith("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w", game.CurrentFen, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalMoveUpdatesFenSanAndActiveSide()
    {
        var game = ChessGame.Start(DefaultSettings());

        var result = game.ApplyMove(UciMove.Parse("e2e4"));

        Assert.Equal("e4", result.Move.San);
        Assert.Equal(Side.Black, result.State.ActiveSide);
        Assert.Contains(" b ", result.State.CurrentFen, StringComparison.Ordinal);
    }

    [Fact]
    public void IllegalMoveIsRejected()
    {
        var game = ChessGame.Start(DefaultSettings());

        Assert.False(game.IsLegalMove(UciMove.Parse("e2e5")));
        Assert.Throws<InvalidOperationException>(() => game.ApplyMove(UciMove.Parse("e2e5")));
    }

    [Fact]
    public void CastlingIsLegalWhenPathIsClear()
    {
        var game = ChessGame.Start(DefaultSettings());

        Apply(game, "e2e4", "e7e5", "g1f3", "b8c6", "f1c4", "f8c5");
        var result = game.ApplyMove(UciMove.Parse("e1g1"));

        Assert.Equal("O-O", result.Move.San);
        Assert.Contains("K", result.State.CurrentFen, StringComparison.Ordinal);
    }

    [Fact]
    public void EnPassantCapturesThePassedPawn()
    {
        var game = ChessGame.Start(DefaultSettings());

        Apply(game, "e2e4", "a7a6", "e4e5", "d7d5");
        var result = game.ApplyMove(UciMove.Parse("e5d6"));

        Assert.Equal("exd6", result.Move.San);
        Assert.DoesNotContain("3p4", result.State.CurrentFen, StringComparison.Ordinal);
    }

    [Fact]
    public void PawnCanPromoteToQueenRookBishopOrKnight()
    {
        foreach (var suffix in new[] { "q", "r", "b", "n" })
        {
            var game = ChessGame.FromFen(Guid.NewGuid(), DefaultSettings(), "8/P7/8/8/8/8/8/k6K w - - 0 1");

            var result = game.ApplyMove(UciMove.Parse($"a7a8{suffix}"));

            Assert.Contains("=Q", suffix == "q" ? result.Move.San : result.Move.San.Replace("=R", "=Q", StringComparison.Ordinal).Replace("=B", "=Q", StringComparison.Ordinal).Replace("=N", "=Q", StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FoolMateEndsByCheckmate()
    {
        var game = ChessGame.Start(DefaultSettings());

        Apply(game, "f2f3", "e7e5", "g2g4");
        var result = game.ApplyMove(UciMove.Parse("d8h4"));

        Assert.Equal(GameStatus.Completed, result.State.Status);
        Assert.Equal(GameResult.BlackWin, result.State.Result);
        Assert.Equal(GameTerminationReason.Checkmate, result.State.TerminationReason);
        Assert.True(result.Move.San.EndsWith('#'));
    }

    [Fact]
    public void StalemateFenIsTerminalDraw()
    {
        var game = ChessGame.FromFen(Guid.NewGuid(), DefaultSettings(), "7k/5K2/6Q1/8/8/8/8/8 b - - 0 1");

        var state = game.ToState();

        Assert.Equal(GameStatus.Completed, state.Status);
        Assert.Equal(GameResult.Draw, state.Result);
        Assert.Equal(GameTerminationReason.Stalemate, state.TerminationReason);
    }

    [Fact]
    public void ThreefoldRepetitionIsTerminalDraw()
    {
        var game = ChessGame.Start(DefaultSettings());

        Apply(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1");
        var result = game.ApplyMove(UciMove.Parse("f6g8"));

        Assert.Equal(GameStatus.Completed, result.State.Status);
        Assert.Equal(GameTerminationReason.ThreefoldRepetition, result.State.TerminationReason);
        Assert.Equal(GameResult.Draw, result.State.Result);
    }

    [Fact]
    public void FiftyMoveRuleIsTerminalDraw()
    {
        var game = ChessGame.FromFen(Guid.NewGuid(), DefaultSettings(), "8/8/8/8/8/8/6k1/KR6 w - - 99 50");

        var result = game.ApplyMove(UciMove.Parse("b1b2"));

        Assert.Equal(GameStatus.Completed, result.State.Status);
        Assert.Equal(GameTerminationReason.FiftyMoveRule, result.State.TerminationReason);
        Assert.Equal(GameResult.Draw, result.State.Result);
    }

    [Fact]
    public void InsufficientMaterialFenIsTerminalDraw()
    {
        var game = ChessGame.FromFen(Guid.NewGuid(), DefaultSettings(), "8/8/8/8/8/8/2k5/K7 w - - 0 1");

        var state = game.ToState();

        Assert.Equal(GameStatus.Completed, state.Status);
        Assert.Equal(GameTerminationReason.InsufficientMaterial, state.TerminationReason);
        Assert.Equal(GameResult.Draw, state.Result);
    }

    [Fact]
    public void ResignationEndsGameWithOpponentWin()
    {
        var game = ChessGame.Start(DefaultSettings());

        game.Resign(Side.White);

        var state = game.ToState();
        Assert.Equal(GameStatus.Resigned, state.Status);
        Assert.Equal(GameResult.BlackWin, state.Result);
        Assert.Equal(GameTerminationReason.Resignation, state.TerminationReason);
    }

    private static void Apply(ChessGame game, params string[] moves)
    {
        foreach (var move in moves)
        {
            game.ApplyMove(UciMove.Parse(move));
        }
    }

    private static GameSettings DefaultSettings()
    {
        return new GameSettings(PlayerColorChoice.White, Side.White, new OpponentElo(OpponentElo.Default));
    }
}
