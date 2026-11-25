using Modules.Planning;
using Xunit;

namespace PlanningTests;

public sealed class PlanningFormulaTests
{
    [Fact]
    public void RejectsFormulaCycles()
    {
        var graph = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["Revenue"] = ["Volume"],
            ["Volume"] = ["Revenue"]
        };

        var exception = Assert.Throws<PlanningException>(() => PlanningFormulaEngine.EnsureAcyclic(graph));

        Assert.Equal("planning.formula_cycle", exception.Code);
    }

    [Fact]
    public void AcceptsADeterministicDag()
    {
        var graph = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["Revenue"] = ["Volume", "Price"],
            ["Margin"] = ["Revenue", "Opex"],
            ["Volume"] = [], ["Price"] = [], ["Opex"] = []
        };

        PlanningFormulaEngine.EnsureAcyclic(graph);
    }
}
