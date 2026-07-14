namespace BlunderForge.Application.Engine;

public interface IEngineHealthService
{
    Task<EngineHealthResult> CheckReadinessAsync(CancellationToken cancellationToken);
}
