using Xunit;

namespace SecurityTests;

[CollectionDefinition("database-security", DisableParallelization = true)]
public sealed class DatabaseSecurityGroup : ICollectionFixture<DatabaseSecurityFixture>;

public sealed class DatabaseSecurityFixture;
