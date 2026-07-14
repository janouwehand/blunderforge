namespace BlunderForge.Infrastructure.Engine;

public sealed class StockfishProtocolException : Exception
{
    public StockfishProtocolException(string message)
        : base(message)
    {
    }
}
