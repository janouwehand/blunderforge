namespace BlunderForge.Infrastructure.Persistence.Entities;

internal sealed class AppSettingEntity
{
    public string Key { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; }
}
