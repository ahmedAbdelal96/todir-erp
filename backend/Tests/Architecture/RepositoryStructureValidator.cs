namespace ArchitectureTests;

internal static class RepositoryStructureValidator
{
    private static readonly string[] RetiredPaths =
    [
        "src/backend",
        "src/frontend",
        "src/frontend/src",
        "frontend/src",
        "frontend/platform",
        "backend/hosts",
        "backend/platform",
        "backend/platform-kernel"
    ];

    private static readonly string[] ForbiddenDataAccessTokens =
    [
        "DbContext",
        "Npgsql",
        "SqlCommand",
        "DbConnection",
        "ExecuteSql",
        "FromSql"
    ];

    public static IReadOnlyList<string> FindViolations(string repositoryRoot)
    {
        var violations = new List<string>();
        AddRetiredPathViolations(repositoryRoot, violations);
        AddBrandedActivePathViolations(repositoryRoot, violations);
        AddLayerViolations(repositoryRoot, violations);
        AddPersistencePlacementViolations(repositoryRoot, violations);
        AddCoreBoundaryViolations(repositoryRoot, violations);
        return violations;
    }

    private static void AddRetiredPathViolations(string root, List<string> violations)
    {
        foreach (var relativePath in RetiredPaths)
        {
            if (Directory.Exists(Path.Combine(root, relativePath)))
            {
                violations.Add($"Retired runtime path '{relativePath}' must not exist.");
            }
        }
    }

    private static void AddBrandedActivePathViolations(string root, List<string> violations)
    {
        foreach (var sourceRoot in new[] { "backend", "frontend", "scripts", "tools", "ops", ".github" })
        {
            var path = Path.Combine(root, sourceRoot);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
                         .Where(entry => !IsGenerated(entry)))
            {
                if (Path.GetFileName(entry).Contains("todir", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"Active technical path must not use product branding: '{Path.GetRelativePath(root, entry)}'.");
                }
            }
        }
    }

    private static void AddLayerViolations(string root, List<string> violations)
    {
        var modulesRoot = Path.Combine(root, "backend", "Modules");
        if (!Directory.Exists(modulesRoot))
        {
            return;
        }

        foreach (var module in Directory.EnumerateDirectories(modulesRoot))
        {
            AddForbiddenTokens(module, "Controllers", violations);
            AddForbiddenTokens(module, "Services", violations);
            AddForbiddenTokens(module, "Repositories", violations);
            AddForbiddenTokens(module, "Entities", violations);
        }
    }

    private static void AddForbiddenTokens(string modulePath, string layer, List<string> violations)
    {
        var layerPath = Path.Combine(modulePath, layer);
        if (!Directory.Exists(layerPath))
        {
            return;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(layerPath, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourcePath);
            foreach (var token in layer is "Repositories" ? Array.Empty<string>() : ForbiddenDataAccessTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(modulePath, sourcePath)} must not contain direct data-access token '{token}'.");
                }
            }

            if (layer is "Controllers" &&
                (!source.Contains(".Services", StringComparison.Ordinal) || !source.Contains("Service", StringComparison.Ordinal)))
            {
                violations.Add($"{Path.GetRelativePath(modulePath, sourcePath)} must call the module Services boundary.");
            }

            if (layer is "Services" or "Repositories" && source.Contains(".Controllers", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(modulePath, sourcePath)} must not depend on Controllers.");
            }

            if (layer is "Entities" &&
                (source.Contains(".Controllers", StringComparison.Ordinal) ||
                 source.Contains(".Repositories", StringComparison.Ordinal) ||
                 source.Contains(".Data", StringComparison.Ordinal) ||
                 source.Contains("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                 source.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)))
            {
                violations.Add($"{Path.GetRelativePath(modulePath, sourcePath)} must remain independent of transport, persistence and framework layers.");
            }
        }
    }

    private static void AddPersistencePlacementViolations(string root, List<string> violations)
    {
        var modulesRoot = Path.Combine(root, "backend", "Modules");
        if (!Directory.Exists(modulesRoot)) return;

        foreach (var sourcePath in Directory.EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modulesRoot, sourcePath).Replace('\\', '/');
            var source = File.ReadAllText(sourcePath);
            if (!ForbiddenDataAccessTokens.Any(token => source.Contains(token, StringComparison.Ordinal))) continue;
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && (segments[0] == "Sample" || segments[1] is "Data" or "Repositories")) continue;
            violations.Add($"Module persistence source '{Path.GetRelativePath(root, sourcePath)}' must live under Repositories/ or Data/.");
        }
    }

    private static void AddCoreBoundaryViolations(string root, List<string> violations)
    {
        var corePath = Path.Combine(root, "frontend", "core");
        if (!Directory.Exists(corePath))
        {
            violations.Add("Frontend core directory is missing.");
            return;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(corePath, "*.ts*", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourcePath);
            if (source.Contains("@platform", StringComparison.Ordinal) || source.Contains("frontend/platform", StringComparison.Ordinal))
            {
                violations.Add($"Frontend core source '{Path.GetRelativePath(root, sourcePath)}' uses the retired platform path.");
            }

            if (source.Contains("/modules/", StringComparison.Ordinal) || source.Contains("@modules", StringComparison.Ordinal))
            {
                violations.Add($"Frontend core source '{Path.GetRelativePath(root, sourcePath)}' must not depend on business modules.");
            }
        }
    }

    private static bool IsGenerated(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/dist/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/coverage/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/test-results/", StringComparison.OrdinalIgnoreCase);
    }
}
