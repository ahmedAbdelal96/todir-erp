namespace Modules.LayerFixture.Domain;

internal sealed class Rule
{
    // Intentional architecture violation: Domain must not know Infrastructure types.
    private readonly Modules.LayerFixture.Infrastructure.InfrastructureRepository repository = new();
}
