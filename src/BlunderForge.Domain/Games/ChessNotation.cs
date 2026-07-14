namespace BlunderForge.Domain.Games;

public static class ChessNotation
{
    public static bool IsValidFen(string fen)
    {
        return Chess.ChessBoard.TryLoadFromFen(fen, out _, Chess.AutoEndgameRules.All);
    }

    public static string GetFenAfterMove(string fen, UciMove move)
    {
        var game = ChessGame.FromFen(Guid.NewGuid(), DefaultSettings(), fen);
        return game.ApplyMove(move).State.CurrentFen;
    }

    public static string GetSanAfterMove(string fen, UciMove move)
    {
        var game = ChessGame.FromFen(Guid.NewGuid(), DefaultSettings(), fen);
        return game.ApplyMove(move).Move.San;
    }

    private static GameSettings DefaultSettings()
    {
        return new GameSettings(PlayerColorChoice.White, Side.White, new OpponentElo(OpponentElo.Default));
    }
}
