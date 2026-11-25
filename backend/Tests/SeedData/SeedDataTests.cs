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
        var repositoryRoot = FindRepositoryRoot();
        // PostgreSQL roles are cluster-wide. Keep the local runtime credential stable
        // while the test creates and drops its disposable database.
        var runtimePassword = GetRuntimePassword(repositoryRoot);
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var migratorBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        var appBuilder = new NpgsqlConnectionStringBuilder(migratorBuilder.ConnectionString)
        {
            Username = "erp_app",
            Password = runtimePassword
        };
        var databaseCreated = false;

        await using var adminDataSource = NpgsqlDataSource.Create(adminBuilder.ConnectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\" encoding 'UTF8' lc_collate 'C' lc_ctype 'C' template template0", admin))
                await create.ExecuteNonQueryAsync();
            databaseCreated = true;

            var firstRun = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(firstRun.ExitCode == 0, firstRun.Output);
            Assert.Contains("Development Seed Ready", firstRun.Output, StringComparison.Ordinal);
            Assert.Contains("Ledger: 2 legal entities, 74 accounts, 24 periods, invoice rule ready", firstRun.Output, StringComparison.Ordinal);
            Assert.Contains("Invoicing: 120 drafts (60 customer, 60 supplier)", firstRun.Output, StringComparison.Ordinal);

            await using var acceptanceDataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            await using (var migrationIdentityCheck = await acceptanceDataSource.OpenConnectionAsync())
            await using (var invalidNames = new NpgsqlCommand("select count(*) from platform_migration.applied_script where script_name like '%/%'", migrationIdentityCheck))
                Assert.Equal(0L, Convert.ToInt64(await invalidNames.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
            var firstCounts = await ReadCanonicalCountsAsync(acceptanceDataSource);
            await AssertPricingEntityIsolationAsync(acceptanceDataSource);
            await AssertCanonicalCurrencyPrecisionAsync(acceptanceDataSource);
            await AssertFinBaseCanonicalScenariosAsync(acceptanceDataSource);
            await AssertForcedRlsAndRoleBoundaryAsync(acceptanceDataSource);
            await AssertInvoicingPermissionsAsync(acceptanceDataSource);
            await AssertAccountingSeedAsync(acceptanceDataSource);
            await AssertRuntimeTenantIsolationAsync(appBuilder.ConnectionString);

            var secondRun = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(secondRun.ExitCode == 0, secondRun.Output);
            Assert.Contains("Development Seed Ready", secondRun.Output, StringComparison.Ordinal);
            Assert.Contains("Ledger: 2 legal entities, 74 accounts, 24 periods, invoice rule ready", secondRun.Output, StringComparison.Ordinal);
            Assert.Contains("Invoicing: 120 drafts (60 customer, 60 supplier)", secondRun.Output, StringComparison.Ordinal);
            var secondCounts = await ReadCanonicalCountsAsync(acceptanceDataSource);
            Assert.Equal(firstCounts, secondCounts);
            await AssertSingleActiveInventoryAssetMappingAsync(acceptanceDataSource);
            Assert.Equal(2, firstCounts.Tenants);
            Assert.Equal(205, firstCounts.AtlasUsers);
            Assert.Equal(180, firstCounts.AtlasParties);
            Assert.Equal(240, firstCounts.AtlasProducts);
            Assert.Equal(105, firstCounts.DeltaUsers);
            Assert.Equal(120, firstCounts.DeltaParties);
            Assert.Equal(160, firstCounts.DeltaProducts);
            Assert.Equal(4, firstCounts.LegalEntities);
            Assert.Equal(8, firstCounts.EnabledEntityCurrencies);
            Assert.Equal(11, firstCounts.PaymentTerms);
            Assert.Equal(2, firstCounts.AtlasVatIdentifiers);
            Assert.Equal(2, firstCounts.AtlasCommercialRegistrations);
            Assert.Equal(2, firstCounts.DeltaVatIdentifiers);
            Assert.Equal(2, firstCounts.DeltaCommercialRegistrations);
            Assert.Equal(12, firstCounts.AtlasPriceLists + firstCounts.DeltaPriceLists);
            Assert.Equal(302, firstCounts.AtlasPriceEntries + firstCounts.DeltaPriceEntries);
            Assert.Equal(2, firstCounts.AtlasScheduledVersions + firstCounts.DeltaScheduledVersions);
            Assert.Equal(71, firstCounts.AtlasOpenItems);
            Assert.Equal(3, firstCounts.AtlasInstallments);
            Assert.Equal(71, firstCounts.DeltaOpenItems);
            Assert.Equal(3, firstCounts.DeltaInstallments);
            Assert.Equal(120, firstCounts.AtlasInvoices);
            Assert.Equal(120, firstCounts.DeltaInvoices);
            await using var runtimeDataSource = NpgsqlDataSource.Create(appBuilder.ConnectionString);
            await AssertConcurrentSupplierDuplicateProtectionAsync(runtimeDataSource);
            await AssertConcurrentSourceIdentityProtectionAsync(runtimeDataSource);
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

    [Fact]
    public async Task ConcurrentMigratorsSerializeAndPreserveMigrationHistory()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the concurrent migrator regression test.");
        var databaseName = $"todir_seed_{Guid.CreateVersion7():N}";
        var seedPassword = $"Seed-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}a1";
        var repositoryRoot = FindRepositoryRoot();
        var runtimePassword = GetRuntimePassword(repositoryRoot);
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var migratorBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        var databaseCreated = false;

        await using var adminDataSource = NpgsqlDataSource.Create(adminBuilder.ConnectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\" encoding 'UTF8' lc_collate 'C' lc_ctype 'C' template template0", admin))
                await create.ExecuteNonQueryAsync();
            databaseCreated = true;

            var runs = await RunConcurrentMigratorsAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(runs.First.ExitCode == 0, $"First concurrent migrator failed:\n{runs.First.Output}\nSecond output:\n{runs.Second.Output}");
            Assert.True(runs.Second.ExitCode == 0, $"Second concurrent migrator failed:\n{runs.Second.Output}\nFirst output:\n{runs.First.Output}");
            Assert.Contains("Development Seed Ready", runs.First.Output + runs.Second.Output, StringComparison.Ordinal);

            await using var dataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            await AssertCanonicalMigrationHistoryAsync(dataSource, repositoryRoot);
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

    [Fact]
    public async Task MigrationLeaseTimeoutDoesNotMutateAndReleasedLeaseAllowsRetry()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the migration lease timeout regression test.");
        var databaseName = $"todir_seed_{Guid.CreateVersion7():N}";
        var seedPassword = $"Seed-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}a1";
        var repositoryRoot = FindRepositoryRoot();
        var runtimePassword = GetRuntimePassword(repositoryRoot);
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var migratorBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        var databaseCreated = false;

        await using var adminDataSource = NpgsqlDataSource.Create(adminBuilder.ConnectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\" encoding 'UTF8' lc_collate 'C' lc_ctype 'C' template template0", admin))
                await create.ExecuteNonQueryAsync();
            databaseCreated = true;

            await using var holderDataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            await using var holder = await holderDataSource.OpenConnectionAsync();
            await AcquireMigrationLeaseAsync(holder);

            var timedOut = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword, "2");
            Assert.Equal(5, timedOut.ExitCode);
            Assert.Contains("Timed out after 2 seconds waiting for the DbMigrator lease", timedOut.Output, StringComparison.Ordinal);
            Assert.True(await IsMigrationHistoryAbsentAsync(holder));

            await ReleaseMigrationLeaseAsync(holder);
            var retry = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(retry.ExitCode == 0, retry.Output);
            Assert.Contains("Development Seed Ready", retry.Output, StringComparison.Ordinal);

            var secondRun = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(secondRun.ExitCode == 0, secondRun.Output);
            await using var dataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            await AssertCanonicalMigrationHistoryAsync(dataSource, repositoryRoot);
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

    [Fact]
    public async Task TerminatedWaitingMigratorDoesNotHoldLease()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the migration lease recovery regression test.");
        var databaseName = $"todir_seed_{Guid.CreateVersion7():N}";
        var seedPassword = $"Seed-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}a1";
        var repositoryRoot = FindRepositoryRoot();
        var runtimePassword = GetRuntimePassword(repositoryRoot);
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var applicationName = $"p0d-lock-test-{Guid.CreateVersion7():N}";
        var migratorBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
            ApplicationName = applicationName
        };
        var databaseCreated = false;

        await using var adminDataSource = NpgsqlDataSource.Create(adminBuilder.ConnectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\" encoding 'UTF8' lc_collate 'C' lc_ctype 'C' template template0", admin))
                await create.ExecuteNonQueryAsync();
            databaseCreated = true;

            await using var holderDataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            await using var holder = await holderDataSource.OpenConnectionAsync();
            await AcquireMigrationLeaseAsync(holder);

            using var waitingProcess = Process.Start(CreateMigratorStartInfo(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword, "300"))
                ?? throw new InvalidOperationException("Unable to start the waiting DbMigrator.");
            Assert.True(await WaitForAdvisoryLockWaitAsync(admin, databaseName, applicationName), "The migrator did not reach the advisory-lock wait state.");
            waitingProcess.Kill(entireProcessTree: true);
            await waitingProcess.WaitForExitAsync();

            await ReleaseMigrationLeaseAsync(holder);
            var retry = await RunMigratorAsync(repositoryRoot, migratorBuilder.ConnectionString, seedPassword, runtimePassword);
            Assert.True(retry.ExitCode == 0, retry.Output);
            await using var dataSource = NpgsqlDataSource.Create(migratorBuilder.ConnectionString);
            await AssertCanonicalMigrationHistoryAsync(dataSource, repositoryRoot);
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

    private static async Task AssertSingleActiveInventoryAssetMappingAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select count(*) from finance_ledger.ledger_entity_configuration c join finance_ledger.account_role r on r.tenant_id=c.tenant_id and r.legal_entity_id=c.legal_entity_id and r.code='InventoryAsset' join finance_ledger.account_mapping m on m.tenant_id=r.tenant_id and m.legal_entity_id=r.legal_entity_id and m.role_id=r.id join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id and v.accounting_configuration_version_id=c.active_configuration_version_id where c.tenant_id=$1 and c.legal_entity_id=$2 and m.status='Active' and v.status='Active' and v.effective_from <= current_date and (v.effective_to is null or v.effective_to >= current_date) and v.specificity_rank=0 and v.product_category_code is null and v.party_group_code is null and v.tax_code is null", connection);
        command.Parameters.AddWithValue(SeedIds.AtlasTenant);
        command.Parameters.AddWithValue(SeedIds.TenantEntity(SeedIds.AtlasTenant));
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
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

    private static async Task AssertInvoicingPermissionsAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var tenantId in new[] { SeedIds.AtlasTenant, SeedIds.DeltaTenant })
        {
            var rolePermissions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            await using var command = new NpgsqlCommand("select r.code,rp.permission_code from platform.role r join platform.role_permission rp on rp.tenant_id=r.tenant_id and rp.role_id=r.id where r.tenant_id=$1 and r.code=any(array['administrator','accountant','sales','auditor'])", connection);
            command.Parameters.AddWithValue(tenantId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var role = reader.GetString(0);
                if (!rolePermissions.TryGetValue(role, out var permissions)) rolePermissions[role] = permissions = new(StringComparer.Ordinal);
                permissions.Add(reader.GetString(1));
            }
            Assert.Contains("invoicing.customer_invoice.finalize", rolePermissions["administrator"]);
            Assert.Contains("invoicing.supplier_invoice.finalize", rolePermissions["administrator"]);
            Assert.Contains("invoicing.delivery.send", rolePermissions["administrator"]);
            Assert.Contains("invoicing.credit_note.create", rolePermissions["accountant"]);
            Assert.Contains("invoicing.supplier_invoice.finalize", rolePermissions["accountant"]);
            Assert.Contains("invoicing.customer_invoice.create", rolePermissions["sales"]);
            Assert.DoesNotContain("invoicing.customer_invoice.finalize", rolePermissions["sales"]);
            Assert.DoesNotContain("invoicing.supplier_invoice.create", rolePermissions["sales"]);
            Assert.DoesNotContain("invoicing.accounting_preview", rolePermissions["sales"]);
            Assert.Contains("accounting.statement.read", rolePermissions["auditor"]);
            Assert.DoesNotContain("accounting.close.execute", rolePermissions["auditor"]);
            Assert.Contains("accounting.reconciliation.run", rolePermissions["accountant"]);
            Assert.DoesNotContain("accounting.close.execute", rolePermissions["accountant"]);
        }
    }

    private static async Task AssertAccountingSeedAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select (select count(*) from business_accounting.accounting_setup where go_live_status='Ready'), (select count(*) from business_accounting.financial_statement_definition where status='Active'), (select count(*) from business_accounting.financial_statement_line), (select count(*) from platform.role_permission p join platform.role r on r.tenant_id=p.tenant_id and r.id=p.role_id where r.code='administrator' and p.permission_code='accounting.close.execute')", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(4L, reader.GetInt64(1));
        Assert.Equal(12L, reader.GetInt64(2));
        Assert.Equal(2L, reader.GetInt64(3));
        await reader.DisposeAsync();

        const string reconciliationSql = """
            with source as (
              select direction,
                sum(case when (direction='Receivable' and polarity='Debit') or (direction='Payable' and polarity='Credit') then original_base_amount else -original_base_amount end) as balance
              from finance_subledger.party_open_item
              where tenant_id=$1 and legal_entity_id=$2 and posting_date <= date '2026-10-01'
              group by direction
            ), ledger as (
              select 'Receivable' as direction, coalesce(sum(l.base_debit-l.base_credit),0) as balance
              from finance_ledger.journal_entry e
              join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
              join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id
              where e.tenant_id=$1 and e.legal_entity_id=$2 and e.posting_date <= date '2026-10-01' and a.code='110100'
              union all
              select 'Payable', coalesce(-sum(l.base_debit-l.base_credit),0)
              from finance_ledger.journal_entry e
              join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
              join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id
              where e.tenant_id=$1 and e.legal_entity_id=$2 and e.posting_date <= date '2026-10-01' and a.code='210200'
            )
            select s.direction,s.balance,l.balance
            from source s join ledger l on l.direction=s.direction
            order by s.direction
            """;
        await using var reconciliation = new NpgsqlCommand(reconciliationSql, connection);
        reconciliation.Parameters.AddWithValue(SeedIds.AtlasTenant);
        reconciliation.Parameters.AddWithValue(SeedIds.TenantEntity(SeedIds.AtlasTenant));
        await using var reconciliationReader = await reconciliation.ExecuteReaderAsync();
        var controlBalances = new Dictionary<string, (decimal Source, decimal Ledger)>(StringComparer.Ordinal);
        while (await reconciliationReader.ReadAsync())
            controlBalances[reconciliationReader.GetString(0)] = (reconciliationReader.GetDecimal(1), reconciliationReader.GetDecimal(2));
        Assert.Equal(2, controlBalances.Count);
        Assert.Equal(controlBalances["Receivable"].Source, controlBalances["Receivable"].Ledger);
        Assert.Equal(controlBalances["Payable"].Source, controlBalances["Payable"].Ledger);
        Assert.True(controlBalances["Receivable"].Source > 0m);
        Assert.True(controlBalances["Payable"].Source > 0m);
    }

    private static async Task AssertPricingEntityIsolationAsync(NpgsqlDataSource dataSource)
    {
        const string sql = "with active_version as (select distinct on (tenant_id,price_list_id) tenant_id,id,price_list_id from foundation_pricing.price_list_version where status='Active' and effective_from<='2026-10-01' and (effective_to is null or effective_to>'2026-10-01') order by tenant_id,price_list_id,effective_from desc,version_no desc) select l.code,l.legal_entity_id,e.unit_price from foundation_pricing.price_list l join active_version v on v.tenant_id=l.tenant_id and v.price_list_id=l.id join foundation_pricing.price_entry e on e.tenant_id=v.tenant_id and e.price_list_version_id=v.id where l.tenant_id=$1 and l.code in ('RETAIL','RETAIL-SECONDARY') and e.variant_id=$2 and e.uom_id=$3 and e.party_id is null and e.min_quantity=0 order by l.code,l.id";
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(SeedIds.AtlasTenant);
        command.Parameters.AddWithValue(SeedIds.Variant(SeedIds.AtlasTenant, 2, "default"));
        command.Parameters.AddWithValue(SeedIds.Uom(SeedIds.AtlasTenant, "EA"));
        await using var reader = await command.ExecuteReaderAsync();
        var actual = new List<(string Code, Guid LegalEntityId, decimal UnitPrice)>();
        while (await reader.ReadAsync()) actual.Add((reader.GetString(0), reader.GetGuid(1), reader.GetDecimal(2)));
        Assert.Equal(new[]
        {
            ("RETAIL", SeedIds.TenantEntity(SeedIds.AtlasTenant), 1250m),
            ("RETAIL-SECONDARY", SeedIds.TenantSecondaryEntity(SeedIds.AtlasTenant), 1450m)
        }, actual);
        await reader.DisposeAsync();
        await using var purchaseCommand = new NpgsqlCommand("select count(*) from foundation_pricing.price_list l join foundation_pricing.price_list_assignment a on a.tenant_id=l.tenant_id and a.price_list_id=l.id and a.legal_entity_id=l.legal_entity_id and a.direction=l.direction and a.is_default join foundation_pricing.price_list_version v on v.tenant_id=l.tenant_id and v.price_list_id=l.id and v.status='Active' join foundation_pricing.price_entry e on e.tenant_id=v.tenant_id and e.price_list_version_id=v.id where l.tenant_id=$1 and l.code='PURCHASE-SECONDARY' and l.legal_entity_id=$2 and l.direction='Buying' and e.variant_id=$3 and e.uom_id=$4 and e.party_id=$5 and e.unit_price=900", connection);
        purchaseCommand.Parameters.AddWithValue(SeedIds.AtlasTenant);
        purchaseCommand.Parameters.AddWithValue(SeedIds.TenantSecondaryEntity(SeedIds.AtlasTenant));
        purchaseCommand.Parameters.AddWithValue(SeedIds.Variant(SeedIds.AtlasTenant, 2, "default"));
        purchaseCommand.Parameters.AddWithValue(SeedIds.Uom(SeedIds.AtlasTenant, "EA"));
        purchaseCommand.Parameters.AddWithValue(SeedIds.Party(SeedIds.AtlasTenant, 2));
        Assert.Equal(1L, await purchaseCommand.ExecuteScalarAsync());
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
    public async Task InvalidMigrationLeaseTimeoutIsRejectedBeforeDatabaseAccess()
    {
        var repositoryRoot = FindRepositoryRoot();
        var start = CreateMigratorStartInfo(repositoryRoot, "Host=127.0.0.1;Port=1;Database=not-used;Username=not-used;Password=not-used", "unused", "unused", "0");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start DbMigrator for timeout configuration validation.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var combined = $"{await output}{await error}";
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("TODIR_MIGRATOR_LOCK_TIMEOUT_SECONDS must be an integer between 1 and 3600 seconds", combined, StringComparison.Ordinal);
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
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 180 : 120, reader.GetInt64(1));
            Assert.Equal(tenant.Id == SeedIds.AtlasTenant ? 240 : 160, reader.GetInt64(2));
        }
    }

    private static async Task<(int ExitCode, string Output)> RunMigratorAsync(string repositoryRoot, string connectionString, string seedPassword, string runtimePassword, string? lockTimeoutSeconds = null)
    {
        var start = CreateMigratorStartInfo(repositoryRoot, connectionString, seedPassword, runtimePassword, lockTimeoutSeconds);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start DbMigrator for the fresh-database seed regression test.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, $"{await standardOutput}{await standardError}");
    }

    private static async Task<((int ExitCode, string Output) First, (int ExitCode, string Output) Second)> RunConcurrentMigratorsAsync(string repositoryRoot, string connectionString, string seedPassword, string runtimePassword)
    {
        using var first = Process.Start(CreateMigratorStartInfo(repositoryRoot, connectionString, seedPassword, runtimePassword))
            ?? throw new InvalidOperationException("Unable to start the first concurrent DbMigrator.");
        using var second = Process.Start(CreateMigratorStartInfo(repositoryRoot, connectionString, seedPassword, runtimePassword))
            ?? throw new InvalidOperationException("Unable to start the second concurrent DbMigrator.");
        var firstOutput = first.StandardOutput.ReadToEndAsync();
        var firstError = first.StandardError.ReadToEndAsync();
        var secondOutput = second.StandardOutput.ReadToEndAsync();
        var secondError = second.StandardError.ReadToEndAsync();
        await Task.WhenAll(first.WaitForExitAsync(), second.WaitForExitAsync());
        return ((first.ExitCode, $"{await firstOutput}{await firstError}"), (second.ExitCode, $"{await secondOutput}{await secondError}"));
    }

    private static ProcessStartInfo CreateMigratorStartInfo(string repositoryRoot, string connectionString, string seedPassword, string runtimePassword, string? lockTimeoutSeconds = null)
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
        if (lockTimeoutSeconds is not null) start.Environment["TODIR_MIGRATOR_LOCK_TIMEOUT_SECONDS"] = lockTimeoutSeconds;
        else start.Environment.Remove("TODIR_MIGRATOR_LOCK_TIMEOUT_SECONDS");
        return start;
    }

    private static async Task AcquireMigrationLeaseAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("select pg_advisory_lock(hashtextextended('todir.dbmigrator:' || current_database(), 0))", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ReleaseMigrationLeaseAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("select pg_advisory_unlock(hashtextextended('todir.dbmigrator:' || current_database(), 0))", connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<bool> IsMigrationHistoryAbsentAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("select to_regclass('platform_migration.applied_script') is null", connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> WaitForAdvisoryLockWaitAsync(NpgsqlConnection admin, string databaseName, string applicationName)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = new NpgsqlCommand("select exists(select 1 from pg_stat_activity where datname=$1 and application_name=$2 and wait_event='advisory')", admin);
            command.Parameters.AddWithValue(databaseName);
            command.Parameters.AddWithValue(applicationName);
            if ((bool)(await command.ExecuteScalarAsync())!) return true;
            await Task.Delay(50);
        }
        return false;
    }

    private static async Task AssertCanonicalMigrationHistoryAsync(NpgsqlDataSource dataSource, string repositoryRoot)
    {
        var expected = GetExpectedMigrationIdentities(repositoryRoot);
        var actual = new Dictionary<(string ModuleId, string ScriptName), string>();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select module_id, script_name, checksum from platform_migration.applied_script", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = (ModuleId: reader.GetString(0), ScriptName: reader.GetString(1));
            Assert.True(actual.TryAdd(key, reader.GetString(2)), $"Duplicate migration identity returned: {key.ModuleId}/{key.ScriptName}");
        }

        Assert.Equal(expected.Count, actual.Count);
        foreach (var entry in expected)
        {
            Assert.True(actual.TryGetValue(entry.Key, out var checksum), $"Missing migration identity: {entry.Key.ModuleId}/{entry.Key.ScriptName}");
            Assert.Equal(entry.Value, checksum);
        }
    }

    private static Dictionary<(string ModuleId, string ScriptName), string> GetExpectedMigrationIdentities(string repositoryRoot)
    {
        var bootstrapDirectory = Path.Combine(repositoryRoot, "backend", "DbMigrator", "Database", "bootstrap");
        var expected = new Dictionary<(string ModuleId, string ScriptName), string>();
        foreach (var path in Directory.EnumerateFiles(bootstrapDirectory, "*.sql", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
        {
            var scriptName = Path.GetFileName(path);
            var moduleId = scriptName switch
            {
                "runtime-roles-and-schemas.sql" => "runtime",
                "platform-rls-finalization.sql" or "platform-kernel-schema.sql" => "platform-kernel",
                _ when scriptName.EndsWith("-schema.sql", StringComparison.Ordinal) => scriptName[..^"-schema.sql".Length],
                _ when scriptName.Contains("-forward-fix-", StringComparison.Ordinal) => scriptName[..scriptName.IndexOf("-forward-fix-", StringComparison.Ordinal)],
                _ => throw new InvalidOperationException($"Unexpected SQL file in canonical migration directory: {scriptName}")
            };
            var key = (moduleId, scriptName);
            Assert.True(expected.TryAdd(key, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()), $"Duplicate canonical migration identity: {moduleId}/{scriptName}");
        }
        return expected;
    }

    private static async Task<CanonicalSeedCounts> ReadCanonicalCountsAsync(NpgsqlDataSource dataSource)
    {
        const string sql = "select (select count(*) from platform.tenant where id = $1 or id = $2)::int, (select count(*) from platform.identity where tenant_id = $1)::int, (select count(*) from foundation_directory.party where tenant_id = $1)::int, (select count(*) from foundation_catalog.product_template where tenant_id = $1)::int, (select count(*) from platform.identity where tenant_id = $2)::int, (select count(*) from foundation_directory.party where tenant_id = $2)::int, (select count(*) from foundation_catalog.product_template where tenant_id = $2)::int, (select count(*) from platform.legal_entity)::int, (select count(*) from foundation_finbase.currency_reference)::int, (select count(*) from foundation_finbase.payment_term)::int, (select count(*) from foundation_finbase.legal_entity_currency)::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$1 and identifier_type='TaxIdentifier')::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$1 and identifier_type='CommercialRegistration')::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$2 and identifier_type='TaxIdentifier')::int, (select count(*) from foundation_directory.party_identifier where tenant_id=$2 and identifier_type='CommercialRegistration')::int, (select count(*) from foundation_pricing.price_list where tenant_id=$1)::int, (select count(*) from foundation_pricing.price_entry where tenant_id=$1)::int, (select count(*) from foundation_pricing.price_list_version where tenant_id=$1 and status='Scheduled')::int, (select count(*) from foundation_pricing.price_list where tenant_id=$2)::int, (select count(*) from foundation_pricing.price_entry where tenant_id=$2)::int, (select count(*) from foundation_pricing.price_list_version where tenant_id=$2 and status='Scheduled')::int, (select count(*) from finance_subledger.party_open_item where tenant_id=$1)::int, (select count(*) from finance_subledger.party_open_item where tenant_id=$1 and source_schedule_reference is not null)::int, (select count(*) from finance_subledger.party_open_item where tenant_id=$2)::int, (select count(*) from finance_subledger.party_open_item where tenant_id=$2 and source_schedule_reference is not null)::int, (select count(*) from business_invoicing.invoice where tenant_id=$1)::int, (select count(*) from business_invoicing.invoice where tenant_id=$2)::int";
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(SeedIds.AtlasTenant);
        command.Parameters.AddWithValue(SeedIds.DeltaTenant);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13), reader.GetInt32(14), reader.GetInt32(15), reader.GetInt32(16), reader.GetInt32(17), reader.GetInt32(18), reader.GetInt32(19), reader.GetInt32(20), reader.GetInt32(21), reader.GetInt32(22), reader.GetInt32(23), reader.GetInt32(24), reader.GetInt32(25), reader.GetInt32(26));
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
        const string sql = "select (select relrowsecurity from pg_class where oid='platform.tenant'::regclass), (select bool_and(relrowsecurity and relforcerowsecurity) from pg_class where oid in ('platform.legal_entity'::regclass,'platform.identity'::regclass,'foundation_directory.party'::regclass,'foundation_catalog.product_template'::regclass,'foundation_finbase.payment_term'::regclass,'foundation_finbase.financial_rounding_policy_rule'::regclass)), (select bool_and(relrowsecurity and relforcerowsecurity) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='foundation_pricing' and c.relkind='r'), (select bool_and(relrowsecurity and relforcerowsecurity) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='business_invoicing' and c.relkind='r'), (select not rolbypassrls from pg_roles where rolname='erp_app'), (select not rolbypassrls from pg_roles where rolname='erp_owner'), has_table_privilege('erp_app','foundation_finbase.currency_reference','SELECT'), has_table_privilege('erp_app','foundation_finbase.currency_reference','INSERT'), has_table_privilege('erp_app','foundation_finbase.currency_reference','UPDATE'), has_table_privilege('erp_app','foundation_finbase.currency_reference','DELETE')";
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
        Assert.True(reader.GetBoolean(6));
        Assert.False(reader.GetBoolean(7));
        Assert.False(reader.GetBoolean(8));
        Assert.False(reader.GetBoolean(9));
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
            await using var command = new NpgsqlCommand("select (select count(*) from platform.tenant)::int,(select count(*) from platform.legal_entity)::int,(select count(*) from platform.identity)::int,(select count(*) from foundation_directory.party)::int,(select count(*) from foundation_catalog.product_template)::int,(select count(*) from foundation_pricing.price_list)::int,(select count(*) from foundation_pricing.price_entry)::int,(select count(*) from business_invoicing.invoice)::int,(select count(*) from business_accounting.accounting_setup)::int,(select count(*) from business_accounting.financial_statement_definition)::int,(select count(*) from business_accounting.financial_statement_line)::int", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var expectedUsers = tenant.Id == SeedIds.AtlasTenant ? 205 : 105;
            var expectedParties = tenant.Id == SeedIds.AtlasTenant ? 180 : 120;
            var expectedProducts = tenant.Id == SeedIds.AtlasTenant ? 240 : 160;
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(2, reader.GetInt32(1));
            Assert.Equal(expectedUsers, reader.GetInt32(2));
            Assert.Equal(expectedParties, reader.GetInt32(3));
            Assert.Equal(expectedProducts, reader.GetInt32(4));
            Assert.Equal(6, reader.GetInt32(5));
            Assert.Equal(151, reader.GetInt32(6));
            Assert.Equal(120, reader.GetInt32(7));
            Assert.Equal(1, reader.GetInt32(8));
            Assert.Equal(2, reader.GetInt32(9));
            Assert.Equal(6, reader.GetInt32(10));
            await reader.DisposeAsync();
            await transaction.CommitAsync();
        }
    }

    private static async Task AssertConcurrentSupplierDuplicateProtectionAsync(NpgsqlDataSource runtimeDataSource)
    {
        var tenant = SeedIds.AtlasTenant;
        var entity = SeedIds.TenantEntity(tenant);
        var party = SeedIds.Party(tenant, 2);
        var externalNumber = $"RACE-{Guid.CreateVersion7():N}";
        var normalizedNumber = externalNumber.ToUpperInvariant();
        var ready = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> AttemptAsync(Guid invoiceId)
        {
            await using var connection = await runtimeDataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                scope.Parameters.AddWithValue(tenant.ToString("D"));
                await scope.ExecuteNonQueryAsync();
            }
            if (Interlocked.Increment(ref ready) == 2) start.TrySetResult();
            await start.Task;
            try
            {
                await using var insert = new NpgsqlCommand("insert into business_invoicing.invoice(tenant_id,legal_entity_id,id,direction,document_type,lifecycle_state,party_id,document_date,posting_date,tax_date,currency_code,supplier_invoice_number,supplier_invoice_number_key,document_number,gross_total,finalized_at,created_at,updated_at) values($1,$2,$3,'Payable','Invoice','Finalized',$4,date '2026-10-26',date '2026-10-26',date '2026-10-26','EGP',$5,$6,$7,1,now(),now(),now())", connection, transaction);
                insert.Parameters.AddWithValue(tenant); insert.Parameters.AddWithValue(entity); insert.Parameters.AddWithValue(invoiceId); insert.Parameters.AddWithValue(party);
                insert.Parameters.AddWithValue(externalNumber); insert.Parameters.AddWithValue(normalizedNumber); insert.Parameters.AddWithValue($"BILL-RACE-{invoiceId:N}");
                await insert.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (PostgresException exception) when (exception.SqlState == "23505" && exception.ConstraintName == "ux_invoice_supplier_external_number")
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        var results = await Task.WhenAll(AttemptAsync(Guid.CreateVersion7()), AttemptAsync(Guid.CreateVersion7()));
        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(1, results.Count(result => !result));
    }

    private static async Task AssertConcurrentSourceIdentityProtectionAsync(NpgsqlDataSource runtimeDataSource)
    {
        var tenant = SeedIds.AtlasTenant;
        var entity = SeedIds.TenantEntity(tenant);
        var party = SeedIds.Party(tenant, 1);
        var sourceId = Guid.CreateVersion7();
        var hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var ready = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> AttemptAsync(Guid invoiceId)
        {
            await using var connection = await runtimeDataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                scope.Parameters.AddWithValue(tenant.ToString("D"));
                await scope.ExecuteNonQueryAsync();
            }
            if (Interlocked.Increment(ref ready) == 2) start.TrySetResult();
            await start.Task;
            try
            {
                await using var insert = new NpgsqlCommand("insert into business_invoicing.invoice(tenant_id,legal_entity_id,id,direction,document_type,lifecycle_state,party_id,document_date,posting_date,tax_date,currency_code,source_module,source_document_id,source_purpose,source_idempotency_key,source_payload_hash,created_at,updated_at) values($1,$2,$3,'Receivable','Invoice','Draft',$4,date '2026-10-26',date '2026-10-26',date '2026-10-26','EGP','sales',$5,'Invoice','source-race-v1',$6,now(),now())", connection, transaction);
                insert.Parameters.AddWithValue(tenant); insert.Parameters.AddWithValue(entity); insert.Parameters.AddWithValue(invoiceId); insert.Parameters.AddWithValue(party);
                insert.Parameters.AddWithValue(sourceId); insert.Parameters.AddWithValue(hash);
                await insert.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (PostgresException exception) when (exception.SqlState == "23505" && exception.ConstraintName == "ux_invoice_source_document")
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        var results = await Task.WhenAll(AttemptAsync(Guid.CreateVersion7()), AttemptAsync(Guid.CreateVersion7()));
        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(1, results.Count(result => !result));
    }

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT");
        foreach (var start in new[] { configured, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            for (var directory = new DirectoryInfo(Path.GetFullPath(start!)); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not find the repository root from the configured root, working directory, or seed test output.");
    }

    private static string GetRuntimePassword(string repositoryRoot)
    {
        var configured = Environment.GetEnvironmentVariable("TODIR_RUNTIME_PASSWORD");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        var developmentSettings = Path.Combine(repositoryRoot, "backend", "appsettings.Development.json");
        if (File.Exists(developmentSettings))
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(developmentSettings));
            if (document.RootElement.TryGetProperty("TODIR_DATABASE_CONNECTION", out var connection)
                && connection.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var password = new NpgsqlConnectionStringBuilder(connection.GetString()).Password;
                if (!string.IsNullOrWhiteSpace(password)) return password;
            }
        }

        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    }

    private sealed record CanonicalSeedCounts(int Tenants, int AtlasUsers, int AtlasParties, int AtlasProducts, int DeltaUsers, int DeltaParties, int DeltaProducts, int LegalEntities, int ReferenceCurrencies, int PaymentTerms, int EnabledEntityCurrencies, int AtlasVatIdentifiers, int AtlasCommercialRegistrations, int DeltaVatIdentifiers, int DeltaCommercialRegistrations, int AtlasPriceLists, int AtlasPriceEntries, int AtlasScheduledVersions, int DeltaPriceLists, int DeltaPriceEntries, int DeltaScheduledVersions, int AtlasOpenItems, int AtlasInstallments, int DeltaOpenItems, int DeltaInstallments, int AtlasInvoices, int DeltaInvoices);
}
