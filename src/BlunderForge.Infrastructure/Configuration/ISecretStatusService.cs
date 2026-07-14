using BlunderForge.Application.Configuration;

namespace BlunderForge.Infrastructure.Configuration;

public interface ISecretStatusService
{
    SecretAvailability GetAvailability(SecretReference secretReference);
}
