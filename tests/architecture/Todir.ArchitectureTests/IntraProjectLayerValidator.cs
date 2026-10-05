using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Todir.ArchitectureTests;

internal static class IntraProjectLayerValidator
{
    public static IReadOnlyList<string> FindDomainInfrastructureViolations(IReadOnlyList<ProjectNode> projects)
    {
        var violations = new List<string>();

        foreach (var project in projects.Where(IsImplementationProject))
        {
            violations.AddRange(FindDomainInfrastructureViolations(project));
        }

        return violations;
    }

    private static string[] FindDomainInfrastructureViolations(ProjectNode project)
    {
        var projectDirectory = Path.GetDirectoryName(project.Path)
            ?? throw new InvalidOperationException($"Project '{project.Path}' has no directory.");
        var sourcePaths = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourcePaths.Length == 0)
        {
            return [];
        }

        var syntaxTrees = sourcePaths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)).ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName: project.Name,
            syntaxTrees: syntaxTrees,
            references: TrustedPlatformReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var moduleNamespace = project.Name;
        var domainNamespace = $"{moduleNamespace}.Domain";
        var infrastructureNamespace = $"{moduleNamespace}.Infrastructure";
        var violations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var syntaxTree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var identifier in syntaxTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var sourceNamespace = semanticModel.GetEnclosingSymbol(identifier.SpanStart)?.ContainingNamespace?.ToDisplayString();
                if (!IsWithin(sourceNamespace, domainNamespace))
                {
                    continue;
                }

                var targetSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                var targetNamespace = targetSymbol?.ContainingNamespace?.ToDisplayString();
                if (!IsWithin(targetNamespace, infrastructureNamespace))
                {
                    continue;
                }

                var sourcePath = Path.GetRelativePath(projectDirectory, syntaxTree.FilePath).Replace('\\', '/');
                violations.Add($"Domain namespace '{sourceNamespace}' in project '{project.Name}' depends on Infrastructure symbol '{targetSymbol!.ToDisplayString()}' in '{targetNamespace}' ({sourcePath}).");
            }
        }

        return violations.OrderBy(violation => violation, StringComparer.Ordinal).ToArray();
    }

    private static bool IsImplementationProject(ProjectNode project) =>
        project.Kind is ProjectKind.FoundationImplementation or ProjectKind.ModuleImplementation;

    private static bool IsWithin(string? namespaceName, string expectedNamespace) =>
        namespaceName is not null &&
        (string.Equals(namespaceName, expectedNamespace, StringComparison.Ordinal) ||
         namespaceName.StartsWith($"{expectedNamespace}.", StringComparison.Ordinal));

    private static bool IsGeneratedOrBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<MetadataReference> TrustedPlatformReferences { get; } =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray()
        ?? throw new InvalidOperationException("The .NET trusted platform assemblies list is unavailable.");
}
