using BlunderForge.Application.Configuration;

namespace BlunderForge.Infrastructure.Configuration;

internal sealed class EnvironmentSecretResolver : ISecretResolver
{
    public string? Resolve(SecretReference secretReference)
    {
        ArgumentNullException.ThrowIfNull(secretReference);

        var filePath = Environment.GetEnvironmentVariable(secretReference.FileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath).Trim() : null;
        }

        var directValue = Environment.GetEnvironmentVariable(secretReference.EnvironmentVariable);
        return string.IsNullOrWhiteSpace(directValue) ? null : directValue;
    }
}
