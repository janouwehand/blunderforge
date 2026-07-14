using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlunderForge.Infrastructure.Persistence;

internal sealed partial class DatabaseMigrationHostedService(
    IServiceProvider serviceProvider,
    DatabaseMigrationStatus status,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BlunderForgeDbContext>();

            var connectionString = dbContext.Database.GetConnectionString();
            var databasePath = TryGetSqliteDatabasePath(connectionString);
            if (databasePath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
            }

            if (databasePath is not null && File.Exists(databasePath))
            {
                var backupPath = $"{databasePath}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.bak";
                File.Copy(databasePath, backupPath, overwrite: false);
                CreatedDatabaseBackup(logger, backupPath);
            }

            await dbContext.Database.MigrateAsync(cancellationToken);
            status.MarkReady();
            DatabaseMigrationsApplied(logger);
        }
        catch (Exception exception)
        {
            status.MarkFailed("Database migration failed.");
            DatabaseMigrationFailed(logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string? TryGetSqliteDatabasePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        const string dataSourcePrefix = "Data Source=";
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return part[dataSourcePrefix.Length..];
            }
        }

        return null;
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Created database backup before applying migrations at {BackupPath}")]
    private static partial void CreatedDatabaseBackup(ILogger logger, string backupPath);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Database migrations applied successfully")]
    private static partial void DatabaseMigrationsApplied(ILogger logger);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error, Message = "Database migration failed")]
    private static partial void DatabaseMigrationFailed(ILogger logger, Exception exception);
}
