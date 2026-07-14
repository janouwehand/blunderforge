namespace BlunderForge.Application.Engine;

public sealed record EngineScore(int? Centipawns, int? MateIn)
{
    public static EngineScore FromCentipawns(int centipawns) => new(centipawns, null);

    public static EngineScore FromMate(int mateIn) => new(null, mateIn);

    public int ToWhiteCentipawnEstimate()
    {
        if (Centipawns is not null)
        {
            return Centipawns.Value;
        }

        if (MateIn is null)
        {
            return 0;
        }

        var distancePenalty = Math.Min(Math.Abs(MateIn.Value), 20) * 10;
        return Math.Sign(MateIn.Value) * (100_000 - distancePenalty);
    }
}
