using Xunit;

namespace CatalogTests;

[CollectionDefinition("catalog-database", DisableParallelization = true)]
public sealed class CatalogDatabaseGroup
{
    public const string Name = "catalog-database";
}
