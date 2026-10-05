namespace Todir.Modules.LayerFixture.Domain;

internal sealed class Rule
{
    // Intentional architecture violation: Domain must not know Infrastructure types.
    private readonly Todir.Modules.LayerFixture.Infrastructure.InfrastructureRepository repository = new();
}
