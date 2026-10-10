using DbMigrator.SeedData;
using Core.Identity;
using Npgsql;
using Xunit;

namespace SeedDataTests;

public sealed class DevelopmentSeedTests
{
    [Fact]
    public void ProductionAndUnspecifiedEnvironmentsAreRejected()
    {
        var originalDotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var originalAspNet = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            Assert.Throws<InvalidOperationException>(DevelopmentSeedEnvironment.RequireSupported);

            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            Assert.Throws<InvalidOperationException>(DevelopmentSeedEnvironment.RequireSupported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotnet);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNet);
        }
    }

    [Fact]
    public void CanonicalIdsAndTenantsAreStable()
    {
        Assert.Equal(SeedIds.AtlasTenant, SeedIds.Stable("tenant:atlas"));
        Assert.Equal(SeedIds.DeltaTenant, SeedIds.Stable("tenant:delta"));
        Assert.Equal("atlas.com", PlatformSeed.Tenants.Single(tenant => tenant.Id == SeedIds.AtlasTenant).LoginDomain);
        Assert.Equal("delta.com", PlatformSeed.Tenants.Single(tenant => tenant.Id == SeedIds.DeltaTenant).LoginDomain);
        Assert.NotEqual(SeedIds.AtlasTenant, SeedIds.DeltaTenant);
    }

    [Fact]
    public void SeedOutputDoesNotContainCredentialValues()
    {
        var output = new StringWriter();
        new SeedRunSummary("test", []).Print(output);
        Assert.DoesNotContain("password", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalCanonicalCredentialsAndTenantCountsAreValid()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_MIGRATOR_CONNECTION");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(password)) return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var tenant in PlatformSeed.Tenants)
        {
            await using var identity = new NpgsqlCommand("select password_hash from platform_iam.identity where tenant_id=$1 and login_prefix='admin'", connection);
            identity.Parameters.AddWithValue(tenant.Id);
            var hash = (string?)await identity.ExecuteScalarAsync();
            Assert.NotNull(hash);
            Assert.True(IdentityConventions.VerifyPassword(password, hash));

            var runtimeConnectionString = Environment.GetEnvironmentVariable("SEED_RUNTIME_CONNECTION");
            if (!string.IsNullOrWhiteSpace(runtimeConnectionString))
            {
                await using var runtimeDataSource = NpgsqlDataSource.Create(runtimeConnectionString);
                var authenticated = await new NpgsqlIdentityStore(runtimeDataSource).AuthenticateAsync($"admin@{tenant.LoginDomain}", password);
                Assert.NotNull(authenticated);
            }

            if (string.IsNullOrWhiteSpace(runtimeConnectionString)) continue;
            await using var runtime = NpgsqlDataSource.Create(runtimeConnectionString);
            await using var runtimeConnection = await runtime.OpenConnectionAsync();
            await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtimeConnection))
            {
                context.Parameters.AddWithValue(tenant.Id.ToString("D"));
                await context.ExecuteNonQueryAsync();
            }
            await using var contextCounts = new NpgsqlCommand("select (select count(*) from platform_iam.identity),(select count(*) from foundation_directory.party),(select count(*) from foundation_catalog.product_template)", runtimeConnection);
            await using var reader = await contextCounts.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 205 : 105, reader.GetInt64(0));
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 80 : 55, reader.GetInt64(1));
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 100 : 60, reader.GetInt64(2));
        }
    }
}
