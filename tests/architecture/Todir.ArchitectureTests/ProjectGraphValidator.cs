using System.Xml.Linq;

namespace Todir.ArchitectureTests;

internal enum ProjectKind
{
    SharedKernel,
    Contracts,
    BuildingBlocks,
    PlatformImplementation,
    FoundationImplementation,
    ModuleImplementation,
    Domain,
    Infrastructure,
    Host,
    Test,
    Other
}

internal sealed record ProjectNode(
    string Name,
    string Path,
    ProjectKind Kind,
    IReadOnlyList<string> References);

internal static class ProjectGraphValidator
{
    private static readonly string[] ForbiddenSharedKernelBusinessConcepts =
    [
        "Invoice",
        "Customer",
        "Product",
        "LedgerAccount",
        "Warehouse",
        "Employee",
        "TaxRule",
        "PriceList"
    ];

    public static IReadOnlyList<ProjectNode> Load(string repositoryRoot, bool includeFixtures)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsExcluded(path, includeFixtures))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectPaths = projects.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return projects.Select(path => CreateNode(path, projectPaths)).ToArray();
    }

    public static IReadOnlyList<string> FindViolations(IReadOnlyList<ProjectNode> projects)
    {
        var violations = new List<string>();
        var byPath = projects.ToDictionary(project => project.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            foreach (var referencePath in project.References)
            {
                if (!byPath.TryGetValue(referencePath, out var target))
                {
                    continue;
                }

                AddBoundaryViolations(project, target, violations);
            }
        }

        AddCycleViolations(projects, byPath, violations);
        return violations;
    }

    public static IReadOnlyList<string> FindSharedKernelBusinessConceptViolations(string repositoryRoot)
    {
        var sharedKernelDirectory = Path.Combine(repositoryRoot, "src", "backend", "shared", "Todir.SharedKernel");
        if (!Directory.Exists(sharedKernelDirectory))
        {
            return [];
        }

        var violations = new List<string>();
        foreach (var sourcePath in Directory.EnumerateFiles(sharedKernelDirectory, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsExcluded(path, includeFixtures: false)))
        {
            var source = File.ReadAllText(sourcePath);
            foreach (var concept in ForbiddenSharedKernelBusinessConcepts)
            {
                if (source.Contains(concept, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"SharedKernel source '{Path.GetRelativePath(repositoryRoot, sourcePath)}' must not contain module-owned business concept '{concept}'.");
                }
            }
        }

        return violations;
    }

    private static ProjectNode CreateNode(string projectPath, ISet<string> knownProjectPaths)
    {
        var document = XDocument.Load(projectPath, LoadOptions.None);
        var references = document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include!)))
            .Where(knownProjectPaths.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var name = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "AssemblyName")?.Value
            ?? Path.GetFileNameWithoutExtension(projectPath);

        return new ProjectNode(name, projectPath, Classify(projectPath, name), references);
    }

    private static void AddBoundaryViolations(ProjectNode source, ProjectNode target, List<string> violations)
    {
        if (source.Kind == ProjectKind.SharedKernel)
        {
            violations.Add($"SharedKernel project '{source.Name}' must not reference '{target.Name}'.");
        }

        if (source.Kind == ProjectKind.Contracts && target.Kind != ProjectKind.SharedKernel)
        {
            violations.Add($"Contracts project '{source.Name}' may only reference Todir.SharedKernel but references '{target.Name}'.");
        }

        if (source.Kind == ProjectKind.PlatformImplementation &&
            target.Kind is ProjectKind.FoundationImplementation or ProjectKind.ModuleImplementation)
        {
            violations.Add($"Platform project '{source.Name}' must not reference {Describe(target.Kind)} '{target.Name}'.");
        }

        if ((source.Kind is ProjectKind.FoundationImplementation or ProjectKind.ModuleImplementation) &&
            (target.Kind is ProjectKind.FoundationImplementation or ProjectKind.ModuleImplementation) &&
            !target.Name.EndsWith(".Contracts", StringComparison.Ordinal))
        {
            violations.Add($"Module implementation '{source.Name}' must not reference implementation '{target.Name}'; use an approved Contracts, capability, event, or port boundary.");
        }

        if (source.Kind == ProjectKind.Domain && target.Kind == ProjectKind.Infrastructure)
        {
            violations.Add($"Domain project '{source.Name}' must not reference Infrastructure project '{target.Name}'.");
        }

        if (source.Kind == ProjectKind.Domain && target.Kind == ProjectKind.BuildingBlocks)
        {
            violations.Add($"Domain project '{source.Name}' must not reference technical BuildingBlocks project '{target.Name}'.");
        }

        if (source.Kind != ProjectKind.Test && target.Kind == ProjectKind.Test)
        {
            violations.Add($"Production project '{source.Name}' must not reference test project '{target.Name}'.");
        }
    }

    private static void AddCycleViolations(
        IReadOnlyList<ProjectNode> projects,
        Dictionary<string, ProjectNode> byPath,
        List<string> violations)
    {
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var path = new List<ProjectNode>();
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            Visit(project);
        }

        return;

        void Visit(ProjectNode project)
        {
            if (state.TryGetValue(project.Path, out var existingState))
            {
                if (existingState != 1)
                {
                    return;
                }

                var start = path.FindIndex(node => string.Equals(node.Path, project.Path, StringComparison.OrdinalIgnoreCase));
                var cycle = path.Skip(start).Append(project).Select(node => node.Name).ToArray();
                var description = string.Join(" -> ", cycle);
                if (reportedCycles.Add(description))
                {
                    violations.Add($"Project dependency cycle detected: {description}.");
                }

                return;
            }

            state[project.Path] = 1;
            path.Add(project);
            foreach (var referencePath in project.References)
            {
                if (byPath.TryGetValue(referencePath, out var target))
                {
                    Visit(target);
                }
            }

            path.RemoveAt(path.Count - 1);
            state[project.Path] = 2;
        }
    }

    private static bool IsExcluded(string path, bool includeFixtures)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/artifacts/", StringComparison.OrdinalIgnoreCase)
            || (!includeFixtures && normalized.Contains("/tests/architecture/fixtures/", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectKind Classify(string projectPath, string projectName)
    {
        var path = Normalize(projectPath);
        // Architecture fixtures intentionally model production project names, so classify them
        // by their declared assembly name rather than by their test-directory location.
        var isArchitectureFixture = path.Contains("/tests/architecture/fixtures/", StringComparison.OrdinalIgnoreCase);
        if (!isArchitectureFixture && path.Contains("/tests/", StringComparison.OrdinalIgnoreCase)) return ProjectKind.Test;
        if (string.Equals(projectName, "Todir.SharedKernel", StringComparison.Ordinal)) return ProjectKind.SharedKernel;
        if (string.Equals(projectName, "Todir.BuildingBlocks", StringComparison.Ordinal)) return ProjectKind.BuildingBlocks;
        if (projectName.EndsWith(".Contracts", StringComparison.Ordinal) || projectName.StartsWith("Todir.Contracts.", StringComparison.Ordinal)) return ProjectKind.Contracts;
        if (projectName.EndsWith(".Domain", StringComparison.Ordinal)) return ProjectKind.Domain;
        if (projectName.EndsWith(".Infrastructure", StringComparison.Ordinal)) return ProjectKind.Infrastructure;
        if (path.Contains("/src/backend/platform/", StringComparison.OrdinalIgnoreCase) || projectName.StartsWith("Todir.Platform.", StringComparison.Ordinal)) return ProjectKind.PlatformImplementation;
        if (path.Contains("/src/backend/foundation/", StringComparison.OrdinalIgnoreCase) || projectName.StartsWith("Todir.Foundation.", StringComparison.Ordinal)) return ProjectKind.FoundationImplementation;
        if (path.Contains("/src/backend/modules/", StringComparison.OrdinalIgnoreCase) || projectName.StartsWith("Todir.Modules.", StringComparison.Ordinal)) return ProjectKind.ModuleImplementation;
        if (path.Contains("/src/backend/hosts/", StringComparison.OrdinalIgnoreCase)) return ProjectKind.Host;
        return ProjectKind.Other;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string Describe(ProjectKind kind) => kind switch
    {
        ProjectKind.FoundationImplementation => "Foundation implementation",
        ProjectKind.ModuleImplementation => "business module implementation",
        _ => kind.ToString()
    };
}
