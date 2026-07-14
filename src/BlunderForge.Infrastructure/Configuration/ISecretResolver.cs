using BlunderForge.Application.Configuration;

namespace BlunderForge.Infrastructure.Configuration;

public interface ISecretResolver
{
    string? Resolve(SecretReference secretReference);
}
