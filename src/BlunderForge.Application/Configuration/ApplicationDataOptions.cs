namespace BlunderForge.Application.Configuration;

public sealed class ApplicationDataOptions
{
    public const string SectionName = "BlunderForge";

    public string DataDirectory { get; init; } = "/app/data";
}
