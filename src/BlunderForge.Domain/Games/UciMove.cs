using System.Text.RegularExpressions;

namespace BlunderForge.Domain.Games;

public sealed partial record UciMove
{
    private UciMove(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string From => Value[..2];

    public string To => Value.Substring(2, 2);

    public PromotionPiece? Promotion => Value.Length == 5 ? ToPromotionPiece(Value[4]) : null;

    public static UciMove Parse(string value)
    {
        if (!TryParse(value, out var move))
        {
            throw new ArgumentException("UCI move must use long algebraic form such as e2e4 or e7e8q.", nameof(value));
        }

        return move;
    }

    public static bool TryParse(string? value, out UciMove move)
    {
        if (value is not null && UciRegex().IsMatch(value))
        {
            move = new UciMove(value.ToLowerInvariant());
            return true;
        }

        move = new UciMove("a1a1");
        return false;
    }

    public override string ToString() => Value;

    private static PromotionPiece ToPromotionPiece(char value) => value switch
    {
        'q' => PromotionPiece.Queen,
        'r' => PromotionPiece.Rook,
        'b' => PromotionPiece.Bishop,
        'n' => PromotionPiece.Knight,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported promotion piece.")
    };

    [GeneratedRegex("^[a-h][1-8][a-h][1-8][qrbn]?$", RegexOptions.CultureInvariant)]
    private static partial Regex UciRegex();
}
