using BlunderForge.Domain;

namespace BlunderForge.DomainTests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void DomainAssemblyMarkerIsAvailable()
    {
        Assert.Equal("BlunderForge.Domain", typeof(AssemblyMarker).Namespace);
    }
}
