using BlunderForge.Application.Configuration;

namespace BlunderForge.Infrastructure.Configuration;

public sealed class EnvironmentSecretStatusService : ISecretStatusService
{
    public SecretAvailability GetAvailability(SecretReference secretReference)
    {
        ArgumentNullException.ThrowIfNull(secretReference);

        var filePath = Environment.GetEnvironmentVariable(secretReference.FileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return new SecretAvailability(File.Exists(filePath), $"{secretReference.FileEnvironmentVariable}");
        }

        var directValue = Environment.GetEnvironmentVariable(secretReference.EnvironmentVariable);
        return new SecretAvailability(!string.IsNullOrWhiteSpace(directValue), secretReference.EnvironmentVariable);
    }
}
