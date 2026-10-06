using Xunit;

namespace Todir.Database.Security.Tests;

[CollectionDefinition("database-security", DisableParallelization = true)]
public sealed class DatabaseSecurityGroup : ICollectionFixture<DatabaseSecurityFixture>;

public sealed class DatabaseSecurityFixture;
