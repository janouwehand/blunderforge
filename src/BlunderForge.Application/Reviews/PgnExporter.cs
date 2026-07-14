using System.Text;
using System.Globalization;
using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Reviews;

public static class PgnExporter
{
    public static string Export(ReviewGame game)
    {
        var result = game.Result switch { GameResult.WhiteWin => "1-0", GameResult.BlackWin => "0-1", GameResult.Draw => "1/2-1/2", _ => "*" };
        var builder = new StringBuilder()
            .AppendLine("[Event \"BlunderForge local game\"]")
            .AppendLine(CultureInfo.InvariantCulture, $"[Date \"{game.StartedAt.UtcDateTime:yyyy.MM.dd}\"]")
            .AppendLine(CultureInfo.InvariantCulture, $"[White \"{(game.PlayerSide is Side.White ? "Player" : "BlunderForge NPC")}\"]")
            .AppendLine(CultureInfo.InvariantCulture, $"[Black \"{(game.PlayerSide is Side.Black ? "Player" : "BlunderForge NPC")}\"]")
            .AppendLine(CultureInfo.InvariantCulture, $"[Result \"{result}\"]")
            .AppendLine(CultureInfo.InvariantCulture, $"[PlayerColor \"{game.PlayerSide}\"]")
            .AppendLine(CultureInfo.InvariantCulture, $"[OpponentElo \"{game.OpponentElo}\"]").AppendLine();
        foreach (var move in game.Moves.OrderBy(m => m.Ply))
        {
            if (move.Ply % 2 == 1) builder.Append((move.Ply + 1) / 2).Append(". ");
            builder.Append(move.San).Append(' ');
        }
        return builder.Append(result).AppendLine().ToString();
    }
}
