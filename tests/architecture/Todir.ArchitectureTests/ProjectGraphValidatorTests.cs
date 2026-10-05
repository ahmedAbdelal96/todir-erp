using Xunit;

namespace Todir.ArchitectureTests;

public sealed class ProjectGraphValidatorTests
{
    [Fact]
    public void ProductionProjectGraphHasNoArchitectureViolations()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: false));

        Assert.Empty(violations);
    }

    [Fact]
    public void SharedKernelContainsNoModuleOwnedBusinessConcepts()
    {
        var violations = ProjectGraphValidator.FindSharedKernelBusinessConceptViolations(RepositoryRoot);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectsDirectModuleImplementationReferenceFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Todir.Modules.FixtureA", StringComparison.Ordinal) && violation.Contains("Todir.Modules.FixtureB", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsDomainToInfrastructureReferenceFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Todir.Modules.Fixture.Domain", StringComparison.Ordinal) && violation.Contains("Todir.Modules.Fixture.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsContractToInfrastructureReferenceFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.Contains("Todir.Contracts.Fixture", StringComparison.Ordinal) && violation.Contains("Todir.Modules.Fixture.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsPlatformToBusinessImplementationReference()
    {
        var violations = ValidateDirectReference(ProjectKind.PlatformImplementation, "Todir.Platform.Fixture", ProjectKind.ModuleImplementation, "Todir.Modules.Sales.Fixture");

        Assert.Contains(violations, violation => violation.Contains("Todir.Platform.Fixture", StringComparison.Ordinal) && violation.Contains("Todir.Modules.Sales.Fixture", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsSharedKernelReference()
    {
        var violations = ValidateDirectReference(ProjectKind.SharedKernel, "Todir.SharedKernel", ProjectKind.Contracts, "Todir.Contracts.Fixture");

        Assert.Contains(violations, violation => violation.Contains("Todir.SharedKernel", StringComparison.Ordinal) && violation.Contains("Todir.Contracts.Fixture", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsDomainToBuildingBlocksReference()
    {
        var violations = ValidateDirectReference(ProjectKind.Domain, "Todir.Modules.Sales.Domain", ProjectKind.BuildingBlocks, "Todir.BuildingBlocks");

        Assert.Contains(violations, violation => violation.Contains("Todir.Modules.Sales.Domain", StringComparison.Ordinal) && violation.Contains("Todir.BuildingBlocks", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsProductionToTestReference()
    {
        var violations = ValidateDirectReference(ProjectKind.ModuleImplementation, "Todir.Modules.Sales", ProjectKind.Test, "Todir.Modules.Sales.Tests");

        Assert.Contains(violations, violation => violation.Contains("Todir.Modules.Sales", StringComparison.Ordinal) && violation.Contains("Todir.Modules.Sales.Tests", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectsProjectCycleFixture()
    {
        var violations = ProjectGraphValidator.FindViolations(ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: true));

        Assert.Contains(violations, violation => violation.StartsWith("Project dependency cycle detected: Todir.Modules.CycleA", StringComparison.Ordinal));
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
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Todir.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Unable to locate the TODIR repository root.");
        }
    }
}
