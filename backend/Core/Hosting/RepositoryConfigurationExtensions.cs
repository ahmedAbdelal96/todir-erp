using Microsoft.Extensions.Configuration;

namespace Core.Hosting;

public static class RepositoryConfigurationExtensions
{
    public static string? GetRuntimeConnectionString(this IConfiguration configuration) =>
        configuration["TODIR_DATABASE_CONNECTION"] ?? configuration.GetConnectionString("Todir");

    public static void AddRepositoryConfiguration(this IConfigurationManager configuration, string contentRootPath, string environmentName)
    {
        var repositoryRoot = FindRepositoryRoot(contentRootPath, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
        if (repositoryRoot is null) return;

        var sharedConfiguration = Path.Combine(repositoryRoot, "backend");
        AddBelowExistingProviders(configuration, Path.Combine(sharedConfiguration, $"appsettings.{environmentName}.json"));
        AddBelowExistingProviders(configuration, Path.Combine(sharedConfiguration, "appsettings.json"));
    }

    private static void AddBelowExistingProviders(IConfigurationManager configuration, string path)
    {
        if (!File.Exists(path)) return;

        configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
        var source = configuration.Sources[^1];
        configuration.Sources.RemoveAt(configuration.Sources.Count - 1);
        configuration.Sources.Insert(0, source);
    }

    private static string? FindRepositoryRoot(params string[] startingPaths)
    {
        foreach (var startingPath in startingPaths)
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "backend", "DbMigrator", "DbMigrator.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        return null;
    }
}
