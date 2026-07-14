namespace BlunderForge.Infrastructure.Configuration;

public sealed record SecretAvailability(bool IsConfigured, string Source);
