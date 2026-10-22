using Xunit;

namespace ArchitectureTests;

public sealed class ProjectGraphValidatorTests
{
    private static readonly string[] FinBaseConsumerModules = ["Catalog", "Directory", "Sample"];

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
    public void TaxConsumesCatalogProductIdentityOnlyThroughPublicContracts()
    {
        var tax = Path.Combine(RepositoryRoot, "Modules", "Tax");
        foreach (var sourceFile in Directory.EnumerateFiles(tax, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("Modules.Catalog", source, StringComparison.Ordinal);
            Assert.DoesNotContain("foundation_catalog.", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CatalogDbContext", source, StringComparison.Ordinal);
        }
        var taxProject = File.ReadAllText(Path.Combine(tax, "Tax.csproj"));
        Assert.DoesNotContain("Modules/Catalog/Catalog.csproj", taxProject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shared/Contracts/Contracts.csproj", taxProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Modules.Tax", File.ReadAllText(Path.Combine(RepositoryRoot, "Modules", "Catalog", "Catalog.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingConsumerModulesDoNotAccessFinBaseStorageDirectly()
    {
        var modulesRoot = Path.Combine(RepositoryRoot, "Modules");
        var consumerRoots = FinBaseConsumerModules
            .Select(name => Path.Combine(modulesRoot, name))
            .Where(Directory.Exists)
            .ToArray();

        Assert.Equal(3, consumerRoots.Length);
        foreach (var consumerRoot in consumerRoots)
        {
            var sourceFiles = Directory.EnumerateFiles(consumerRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("\\artifacts\\", StringComparison.OrdinalIgnoreCase));

            foreach (var sourceFile in sourceFiles)
            {
                var source = File.ReadAllText(sourceFile);
                Assert.DoesNotContain("foundation_finbase.", source, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("foundation_finbase\"", source, StringComparison.OrdinalIgnoreCase);
            }
        }

        var projects = ProjectGraphValidator.Load(RepositoryRoot, includeFixtures: false);
        var violations = ProjectGraphValidator.FindViolations(projects);
        Assert.DoesNotContain(violations, violation => violation.Contains("Modules.Catalog", StringComparison.Ordinal)
            || violation.Contains("Modules.Directory", StringComparison.Ordinal)
            || violation.Contains("Modules.Sample", StringComparison.Ordinal));
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
            var configured = Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT");
            foreach (var start in new[] { configured, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                for (var directory = new DirectoryInfo(Path.GetFullPath(start!)); directory is not null; directory = directory.Parent)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Backend.sln"))) return directory.FullName;
                    if (File.Exists(Path.Combine(directory.FullName, "backend", "Backend.sln"))) return Path.Combine(directory.FullName, "backend");
                }
            }
            throw new InvalidOperationException("Unable to locate the backend repository root from the configured root, working directory, or test output.");
        }
    }
}
