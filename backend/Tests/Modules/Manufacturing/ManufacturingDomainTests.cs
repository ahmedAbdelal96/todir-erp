using Modules.Manufacturing.Entities;
using Xunit;

namespace Modules.Manufacturing.Tests;

public sealed class ManufacturingDomainTests
{
    [Fact]
    public void MaterialConservationAcceptsStagedIssueConsumeAndReturn()
    {
        ManufacturingRules.ValidateMaterialConservation(100m, 60m, 40m, 0m);
    }

    [Fact]
    public void MaterialConservationRejectsDoubleConsumption()
    {
        var exception = Assert.Throws<ManufacturingException>(() => ManufacturingRules.ValidateMaterialConservation(100m, 70m, 40m, 0m));
        Assert.Equal("manufacturing.material_conservation_violation", exception.Code);
    }

    [Fact]
    public void OutputAllowsPartialCompletionButNeverOverProduces()
    {
        ManufacturingRules.ValidateOutput(100m, 0m, 60m);
        var exception = Assert.Throws<ManufacturingException>(() => ManufacturingRules.ValidateOutput(100m, 60m, 41m));
        Assert.Equal("manufacturing.output_exceeds_order", exception.Code);
    }

    [Fact]
    public void ManufacturingModeRequiresItsAuthoritativeDefinition()
    {
        ManufacturingRules.ValidateMode("Discrete", Guid.CreateVersion7(), null);
        ManufacturingRules.ValidateMode("ProcessBatch", null, Guid.CreateVersion7());
        Assert.Throws<ManufacturingException>(() => ManufacturingRules.ValidateMode("Discrete", null, null));
        Assert.Throws<ManufacturingException>(() => ManufacturingRules.ValidateMode("ProcessBatch", Guid.CreateVersion7(), null));
    }

    [Fact]
    public void ProductionOrderLifecyclePreservesExplicitHoldAndCloseTransitions()
    {
        Assert.Equal("Released", ManufacturingRules.NextOrderState("Draft", "release"));
        Assert.Equal("InProgress", ManufacturingRules.NextOrderState("Released", "start"));
        Assert.Equal("OnHold", ManufacturingRules.NextOrderState("InProgress", "hold"));
        Assert.Equal("InProgress", ManufacturingRules.NextOrderState("OnHold", "resume"));
        Assert.Equal("Completed", ManufacturingRules.NextOrderState("InProgress", "complete"));
        Assert.Equal("Closed", ManufacturingRules.NextOrderState("Completed", "close"));
        Assert.Throws<ManufacturingException>(() => ManufacturingRules.NextOrderState("Draft", "complete"));
    }

    [Fact]
    public void BomRejectsDuplicateComponentsThatWouldMakeRequirementResolutionAmbiguous()
    {
        var product = Guid.CreateVersion7();
        var lines = new[]
        {
            new Modules.Manufacturing.DTOs.BomLineRequest(product, 1, "EA"),
            new Modules.Manufacturing.DTOs.BomLineRequest(product, 2, "EA")
        };
        var exception = Assert.Throws<ManufacturingException>(() => ManufacturingRules.ValidateBomLines(lines));
        Assert.Equal("manufacturing.bom_component_duplicate", exception.Code);
    }
}
