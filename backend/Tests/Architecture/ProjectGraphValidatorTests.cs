using Xunit;

namespace ArchitectureTests;

public sealed class ProjectGraphValidatorTests
{
    [Fact]
    public void ProductionProjectGraphHasNoArchitectureViolations()
    {
        var projects = ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: false);
        var violations = ProjectGraphValidator.FindViolations(projects)
            .Concat(IntraProjectLayerValidator.FindDomainInfrastructureViolations(projects));

        Assert.Empty(violations);
    }

    [Fact]
    public void SharedKernelContainsNoModuleOwnedBusinessConcepts()
    {
        var violations = ProjectGraphValidator.FindSharedKernelBusinessConceptViolations(RepositoryRoot);

        Assert.Empty(violations);
    }

    [Fact]
    public void RepositoryStructureHasNoRetiredPathsOrLayerViolations()
    {
        var repositoryRoot = Directory.GetParent(RepositoryRoot)?.FullName
            ?? throw new InvalidOperationException("Unable to locate the repository root.");
        var violations = RepositoryStructureValidator.FindViolations(repositoryRoot);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectsDirectModuleImplementationReferenceFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Modules.FixtureA", StringComparison.Ordinal) && violation.Contains("Modules.FixtureB", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsDomainToInfrastructureReferenceFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Modules.Fixture.Domain", StringComparison.Ordinal) && violation.Contains("Modules.Fixture.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsCanonicalSingleProjectDomainToInfrastructureDependency()
    {
        var projects = ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true);
        var violations = IntraProjectLayerValidator.FindDomainInfrastructureViolations(projects);

        Assert.Contains(violations, violation => violation.Contains("Modules.LayerFixture.Domain", StringComparison.Ordinal) && violation.Contains("Modules.LayerFixture.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsNoncanonicalSplitModuleLayerProjectShape()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Modules.Fixture.Domain", StringComparison.Ordinal) && violation.Contains("canonical single implementation assembly shape", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsContractToInfrastructureReferenceFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Shared.Contracts.Fixture", StringComparison.Ordinal) && violation.Contains("Modules.Fixture.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsPlatformToBusinessImplementationReference()
    {
        var violations = ValidateDirectReference(ProjectKind.PlatformImplementation, "Core.Fixture", ProjectKind.ModuleImplementation, "Modules.Sales.Fixture");

        Assert.Contains(violations, violation => violation.Contains("Core.Fixture", StringComparison.Ordinal) && violation.Contains("Modules.Sales.Fixture", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsSharedKernelReference()
    {
        var violations = ValidateDirectReference(ProjectKind.SharedKernel, "Shared.Primitives", ProjectKind.Contracts, "Shared.Contracts.Fixture");

        Assert.Contains(violations, violation => violation.Contains("Shared.Primitives", StringComparison.Ordinal) && violation.Contains("Shared.Contracts.Fixture", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsDomainToBuildingBlocksReference()
    {
        var violations = ValidateDirectReference(ProjectKind.Domain, "Modules.Sales.Domain", ProjectKind.BuildingBlocks, "Shared.BuildingBlocks");

        Assert.Contains(violations, violation => violation.Contains("Modules.Sales.Domain", StringComparison.Ordinal) && violation.Contains("Shared.BuildingBlocks", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsBuildingBlocksToUpperImplementationReference()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Shared.BuildingBlocks.Fixture", StringComparison.Ordinal) && violation.Contains("Modules.FixtureB", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsProductionToTestReference()
    {
        var violations = ValidateDirectReference(ProjectKind.ModuleImplementation, "Modules.Sales", ProjectKind.Test, "Modules.Sales.Tests");

        Assert.Contains(violations, violation => violation.Contains("Modules.Sales", StringComparison.Ordinal) && violation.Contains("Modules.Sales.Tests", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsProjectCycleFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.StartsWith("Project dependency cycle detected: Modules.CycleA", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ValidateDirectReference(ProjectKind sourceKind, string sourceName, ProjectKind targetKind, string targetName)
    {
        const string sourcePath = "C:\\architecture-tests\\source.csproj";
        const string targetPath = "C:\\architecture-tests\\target.csproj";
        var source = new ProjectNode(sourceName, sourcePath, sourceKind, [targetPath]);
        var target = new ProjectNode(targetName, targetPath, targetKind, []);

        return ProjectGraphValidator.FindViolations([source, target]);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Backend.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Unable to locate the TODIR repository root.");
        }
    }
}
