namespace BlunderForge.Domain.Games;

public readonly record struct OpponentElo
{
    public const int Minimum = 200;
    public const int NativeStockfishMinimum = 1320;
    public const int Maximum = 3000;
    public const int Default = 800;

    public OpponentElo(int value)
    {
        if (value is < Minimum or > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Opponent Elo must be between {Minimum} and {Maximum}.");
        }

        Value = value;
    }

    public int Value { get; }

    public bool UsesNativeStockfishLimit => Value >= NativeStockfishMinimum;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
