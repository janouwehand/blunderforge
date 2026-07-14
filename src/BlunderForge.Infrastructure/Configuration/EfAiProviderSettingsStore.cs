using System.Text.Json;
using BlunderForge.Application.Ai;
using BlunderForge.Infrastructure.Persistence;
using BlunderForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlunderForge.Infrastructure.Configuration;

internal sealed class EfAiProviderSettingsStore(BlunderForgeDbContext dbContext) : IAiProviderSettingsStore
{
    private const string Key = "ai-provider-settings";

    public async Task<StoredAiProviderSettings?> GetAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Key, cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<StoredAiProviderSettings>(entity.ValueJson);
    }

    public async Task SetAsync(StoredAiProviderSettings settings, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AppSettings.SingleOrDefaultAsync(x => x.Key == Key, cancellationToken);
        if (entity is null)
        {
            entity = new AppSettingEntity { Key = Key };
            dbContext.AppSettings.Add(entity);
        }
        entity.ValueJson = JsonSerializer.Serialize(settings);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
