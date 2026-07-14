namespace BlunderForge.Infrastructure.Persistence;

public sealed class DatabaseMigrationStatus
{
    private readonly object syncRoot = new();

    public bool HasRun { get; private set; }

    public bool IsReady { get; private set; }

    public string Detail { get; private set; } = "Database migrations have not run yet.";

    public void MarkReady()
    {
        lock (syncRoot)
        {
            HasRun = true;
            IsReady = true;
            Detail = "Database migrations applied successfully.";
        }
    }

    public void MarkFailed(string detail)
    {
        lock (syncRoot)
        {
            HasRun = true;
            IsReady = false;
            Detail = detail;
        }
    }
}
