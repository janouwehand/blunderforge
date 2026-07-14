namespace BlunderForge.Web.Contracts;

public sealed record HealthResponse(string Status, string Detail);

public sealed record ComponentStatus(string Name, string Status, string Detail);

public sealed record ReadinessResponse(string Status, ComponentStatus Database, ComponentStatus Stockfish);

public sealed record AiProviderStatusResponse(
    string Provider,
    string? BaseUrl,
    string InteractiveModel,
    string ReviewModel,
    bool SecretAvailable,
    string SecretSource);

public sealed record SystemInfoResponse(string Name, string Version, bool PersistentDataConfigured, bool StockfishConfigured);
