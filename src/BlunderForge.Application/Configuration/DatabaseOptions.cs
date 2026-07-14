namespace BlunderForge.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Default { get; init; } = "Data Source=/app/data/blunderforge.db";
}
