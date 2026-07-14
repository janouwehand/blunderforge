namespace BlunderForge.Application.Npc;

public sealed class SystemNpcRandom : INpcRandom
{
    public double NextDouble() => Random.Shared.NextDouble();
}
