using DbMigrator.SeedData;
using Core.Identity;
using Npgsql;
using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;

namespace SeedDataTests;

public sealed class DevelopmentSeedTests
{
    [Fact]
    public async Task DevelopmentSeedRunsTwiceOnFreshDatabaseWithForcedRls()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the fresh-database seed regression test.");
        var databaseName = $"todir_seed_{Guid.CreateVersion7():N}";
        var seedPassword = $"Seed-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}a1";
        var runtimePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var migratorBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        var appBuilder = new NpgsqlConnectionStringBuilder(migratorBuilder.ConnectionString)
        {
            Username = "erp_app",
            Password = runtimePassword
        };
        var repositoryRoot = FindRepositoryRoot();
        var databaseCreated = false;

        await using var adminDataSource = NpgsqlDataSource.Create(adminBuilder.ConnectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\"", admin))
                await create.ExecuteNonQueryAsync();
            databaseCreated = true;

            var firstRun = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(firstRun.ExitCode == 0, firstRun.Output);
            Assert.Contains("Development Seed Ready", firstRun.Output, StringComparison.Ordinal);

            await using var acceptanceDataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            var firstCounts = await ReadCanonicalCountsAsync(acceptanceDataSource);
            await AssertPricingEntityIsolationAsync(acceptanceDataSource);
            await AssertCanonicalCurrencyPrecisionAsync(acceptanceDataSource);
            await AssertFinBaseCanonicalScenariosAsync(acceptanceDataSource);
            await AssertForcedRlsAndRoleBoundaryAsync(acceptanceDataSource);
            await AssertRuntimeTenantIsolationAsync(appBuilder.ConnectionString);

            var secondRun = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(secondRun.ExitCode == 0, secondRun.Output);
            Assert.Contains("Development Seed Ready", secondRun.Output, StringComparison.Ordinal);
            var secondCounts = await ReadCanonicalCountsAsync(acceptanceDataSource);
            Assert.Equal(firstCounts, secondCounts);
            Assert.Equal(2, firstCounts.Tenants);
            Assert.Equal(205, firstCounts.AtlasUsers);
            Assert.Equal(80, firstCounts.AtlasParties);
            Assert.Equal(100, firstCounts.AtlasProducts);
            Assert.Equal(105, firstCounts.DeltaUsers);
            Assert.Equal(55, firstCounts.DeltaParties);
            Assert.Equal(60, firstCounts.DeltaProducts);
            Assert.Equal(4, firstCounts.LegalEntities);
            Assert.Equal(8, firstCounts.EnabledEntityCurrencies);
            Assert.Equal(11, firstCounts.PaymentTerms);
            Assert.Equal(2, firstCounts.AtlasVatIdentifiers);
            Assert.Equal(2, firstCounts.AtlasCommercialRegistrations);
            Assert.Equal(2, firstCounts.DeltaVatIdentifiers);
            Assert.Equal(2, firstCounts.DeltaCommercialRegistrations);
            Assert.Equal(10, firstCounts.AtlasPriceLists + firstCounts.DeltaPriceLists);
            Assert.Equal(22, firstCounts.AtlasPriceEntries + firstCounts.DeltaPriceEntries);
            Assert.Equal(2, firstCounts.AtlasScheduledVersions + firstCounts.DeltaScheduledVersions);
        }
        finally
        {
            if (databaseCreated)
            {
                await using var terminate = new NpgsqlCommand("select pg_terminate_backend(pid) from pg_stat_activity where datname = $1 and pid <> pg_backend_pid()", admin);
                terminate.Parameters.AddWithValue(databaseName);
                await terminate.ExecuteNonQueryAsync();
                await using var drop = new NpgsqlCommand($"drop database if exists \"{databaseName}\"", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task AssertCanonicalCurrencyPrecisionAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select code,minor_unit from foundation_finbase.currency_reference where code = any(array['JPY','EGP','KWD']) order by code", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var minorUnits = new Dictionary<string, short>(StringComparer.Ordinal);
        while (await reader.ReadAsync()) minorUnits.Add(reader.GetString(0), reader.GetInt16(1));
        Assert.Equal(0, minorUnits["JPY"]);
        Assert.Equal(2, minorUnits["EGP"]);
        Assert.Equal(3, minorUnits["KWD"]);
    }

    private static async Task AssertPricingEntityIsolationAsync(NpgsqlDataSource dataSource)
    {
        const string sql = "select l.legal_entity_id,e.unit_price from foundation_pricing.price_list l join foundation_pricing.price_list_version v on v.tenant_id=l.tenant_id and v.price_list_id=l.id and v.status='Active' join foundation_pricing.price_entry e on e.tenant_id=v.tenant_id and e.price_list_version_id=v.id where l.tenant_id=$1 and l.code in ('RETAIL','RETAIL-SECONDARY') and e.variant_id=$2 and e.uom_id=$3 and e.party_id is null order by l.code";
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(SeedIds.AtlasTenant);
        command.Parameters.AddWithValue(SeedIds.Variant(SeedIds.AtlasTenant, 2, "default"));
        command.Parameters.AddWithValue(SeedIds.Uom(SeedIds.AtlasTenant, "EA"));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(SeedIds.TenantEntity(SeedIds.AtlasTenant), reader.GetGuid(0));
        Assert.Equal(1250m, reader.GetDecimal(1));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(SeedIds.TenantSecondaryEntity(SeedIds.AtlasTenant), reader.GetGuid(0));
        Assert.Equal(1450m, reader.GetDecimal(1));
        Assert.False(await reader.ReadAsync());
    }

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
            await using var identity = new NpgsqlCommand("select password_hash from platform.identity where tenant_id=$1 and login_prefix='admin'", connection);
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
            await using var contextCounts = new NpgsqlCommand("select (select count(*) from platform.identity),(select count(*) from foundation_directory.party),(select count(*) from foundation_catalog.product_template)", runtimeConnection);
            await using var reader = await contextCounts.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 205 : 105, reader.GetInt64(0));
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 80 : 55, reader.GetInt64(1));
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 100 : 60, reader.GetInt64(2));
        }
    }

    private static async Task<(int ExitCode, string Output)> RunMigratorAsync(string repositoryRoot, string connectionString, string seedPassword, string runtimePassword)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        start.ArgumentList.Add(Path.Combine(repositoryRoot, "backend", "DbMigrator", "bin", configuration, "net10.0", "DbMigrator.dll"));
        start.ArgumentList.Add("--seed");
        start.ArgumentList.Add("development");
        start.Environment["TODIR_MIGRATOR_CONNECTION"] = connectionString;
        start.Environment["TODIR_REPOSITORY_ROOT"] = repositoryRoot;
        start.Environment["TODIR_RUNTIME_PASSWORD"] = runtimePassword;
        start.Environment["SEED_DEFAULT_PASSWORD"] = seedPassword;
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start DbMigrator for the fresh-database seed regression test.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, $"{await standardOutput}{await standardError}");
    }

    private static async Task<CanonicalSeedCounts> ReadCanonicalCountsAsync(NpgsqlDataSource dataSource)
    {
        const string sql = "select (select count(*) from platform.tenant where id = $1 or id = $2)::int, (select count(*) from platform.identity where tenant_id = $1)::int, (select count(*) from foundation_directory.party where tenant_id = $1)::int, (select count(*) from foundation_catalog.product_template where tenant_id = $1)::int, (select count(*) from platform.identity where tenant_id = $2)::int, (select count(*) from foundation_directory.party where tenant_id = $2)::int, (select count(*) from foundation_catalog.product_template where tenant_id = $2)::int, (select count(*) from platform.legal_entity)::int, (select count(*) from foundation_finbase.currency_reference)::int, (select count(*) from foundation_finbase.payment_term)::int, (select count(*) from foundation_finbase.legal_entity_currency)::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$1 and identifier_type='TaxIdentifier')::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$1 and identifier_type='CommercialRegistration')::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$2 and identifier_type='TaxIdentifier')::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$2 and identifier_type='CommercialRegistration')::int, (select count(*) from foundation_pricing.price_list where tenant_id=$1)::int, (select count(*) from foundation_pricing.price_entry where tenant_id=$1)::int, (select count(*) from foundation_pricing.price_list_version where tenant_id=$1 and status='Scheduled')::int, (select count(*) from foundation_pricing.price_list where tenant_id=$2)::int, (select count(*) from foundation_pricing.price_entry where tenant_id=$2)::int, (select count(*) from foundation_pricing.price_list_version where tenant_id=$2 and status='Scheduled')::int";
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(SeedIds.AtlasTenant);
        command.Parameters.AddWithValue(SeedIds.DeltaTenant);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13), reader.GetInt32(14), reader.GetInt32(15), reader.GetInt32(16), reader.GetInt32(17), reader.GetInt32(18), reader.GetInt32(19), reader.GetInt32(20));
    }

    private static async Task AssertFinBaseCanonicalScenariosAsync(NpgsqlDataSource dataSource)
    {
        const string sql = "select t.slug, count(distinct v.id)::int, count(r.id)::int, count(r.id) filter (where r.allocation_type='Percentage')::int, count(r.id) filter (where r.allocation_type='Balance')::int, (select count(*) from foundation_finbase.financial_rounding_policy_rule p where p.tenant_id=t.id and p.rounding_context='FXBaseAmount')::int from platform.tenant t join foundation_finbase.payment_term term on term.tenant_id=t.id and term.code='INSTALL3' join foundation_finbase.payment_term_version v on v.tenant_id=term.tenant_id and v.payment_term_id=term.id and v.status='Active' join foundation_finbase.payment_term_rule r on r.tenant_id=v.tenant_id and r.version_id=v.id where t.id in ($1,$2) group by t.id,t.slug order by t.slug";
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(SeedIds.AtlasTenant);
        command.Parameters.AddWithValue(SeedIds.DeltaTenant);
        await using var reader = await command.ExecuteReaderAsync();
        var tenants = 0;
        while (await reader.ReadAsync())
        {
            Assert.True(reader.GetString(0) is "atlas-trading" or "delta-supplies");
            Assert.Equal(1, reader.GetInt32(1));
            Assert.Equal(3, reader.GetInt32(2));
            Assert.Equal(2, reader.GetInt32(3));
            Assert.Equal(1, reader.GetInt32(4));
            Assert.Equal(1, reader.GetInt32(5));
            tenants++;
        }
        Assert.Equal(2, tenants);
    }

    private static async Task AssertForcedRlsAndRoleBoundaryAsync(NpgsqlDataSource dataSource)
    {
        const string sql = "select (select relrowsecurity from pg_class where oid='platform.tenant'::regclass), (select bool_and(relrowsecurity and relforcerowsecurity) from pg_class where oid in ('platform.legal_entity'::regclass,'platform.identity'::regclass,'foundation_directory.party'::regclass,'foundation_catalog.product_template'::regclass,'foundation_finbase.payment_term'::regclass,'foundation_finbase.financial_rounding_policy_rule'::regclass)), (select bool_and(relrowsecurity and relforcerowsecurity) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='foundation_pricing' and c.relkind='r'), (select not rolbypassrls from pg_roles where rolname='erp_app'), (select not rolbypassrls from pg_roles where rolname='erp_owner'), has_table_privilege('erp_app','foundation_finbase.currency_reference','SELECT'), has_table_privilege('erp_app','foundation_finbase.currency_reference','INSERT'), has_table_privilege('erp_app','foundation_finbase.currency_reference','UPDATE'), has_table_privilege('erp_app','foundation_finbase.currency_reference','DELETE')";
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
        Assert.True(reader.GetBoolean(5));
        Assert.False(reader.GetBoolean(6));
        Assert.False(reader.GetBoolean(7));
        Assert.False(reader.GetBoolean(8));
    }

    private static async Task AssertRuntimeTenantIsolationAsync(string runtimeConnectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        foreach (var tenant in PlatformSeed.Tenants)
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                context.Parameters.AddWithValue(tenant.Id.ToString("D"));
                await context.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand("select (select count(*) from platform.tenant)::int,(select count(*) from platform.legal_entity)::int,(select count(*) from platform.identity)::int,(select count(*) from foundation_directory.party)::int,(select count(*) from foundation_catalog.product_template)::int,(select count(*) from foundation_pricing.price_list)::int,(select count(*) from foundation_pricing.price_entry)::int", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var expectedUsers = tenant.Id == SeedIds.AtlasTenant ? 205 : 105;
            var expectedParties = tenant.Id == SeedIds.AtlasTenant ? 80 : 55;
            var expectedProducts = tenant.Id == SeedIds.AtlasTenant ? 100 : 60;
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(2, reader.GetInt32(1));
            Assert.Equal(expectedUsers, reader.GetInt32(2));
            Assert.Equal(expectedParties, reader.GetInt32(3));
            Assert.Equal(expectedProducts, reader.GetInt32(4));
            Assert.Equal(5, reader.GetInt32(5));
            Assert.Equal(11, reader.GetInt32(6));
            await reader.DisposeAsync();
            await transaction.CommitAsync();
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not find backend/DbMigrator from the seed test assembly location.");
    }

    private sealed record CanonicalSeedCounts(int Tenants, int AtlasUsers, int AtlasParties, int AtlasProducts, int DeltaUsers, int DeltaParties, int DeltaProducts, int LegalEntities, int ReferenceCurrencies, int PaymentTerms, int EnabledEntityCurrencies, int AtlasVatIdentifiers, int AtlasCommercialRegistrations, int DeltaVatIdentifiers, int DeltaCommercialRegistrations, int AtlasPriceLists, int AtlasPriceEntries, int AtlasScheduledVersions, int DeltaPriceLists, int DeltaPriceEntries, int DeltaScheduledVersions);
}
