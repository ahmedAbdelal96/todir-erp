using Npgsql;
using System.Globalization;
using System.Diagnostics;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Modules.FinanceBase.Repositories;
using Modules.FinanceBase.Services;
using Core.Services;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;
using Xunit.Abstractions;

namespace Modules.FinanceBase.Tests;

public sealed class FinanceBaseDatabaseTests
{
    private readonly ITestOutputHelper output;
    public FinanceBaseDatabaseTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task SchemaHasControlledCurrencyReferenceAndForcedTenantRls()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var currency = new NpgsqlCommand("select (select minor_unit from foundation_finbase.currency_reference where code='JPY' and status='Active'), (select minor_unit from foundation_finbase.currency_reference where code='EGP' and status='Active'), (select minor_unit from foundation_finbase.currency_reference where code='KWD' and status='Active')", connection);
        await using var currencyReader = await currency.ExecuteReaderAsync();
        Assert.True(await currencyReader.ReadAsync());
        Assert.Equal(0, currencyReader.GetInt16(0));
        Assert.Equal(2, currencyReader.GetInt16(1));
        Assert.Equal(3, currencyReader.GetInt16(2));
        await currencyReader.CloseAsync();
        await using var rls = new NpgsqlCommand("select count(*) from pg_class where relnamespace='foundation_finbase'::regnamespace and relrowsecurity and relforcerowsecurity and relname = any(array['exchange_rate','exchange_rate_source_config','financial_policy','financial_policy_version','financial_rounding_policy','financial_rounding_policy_rule','legal_entity_currency','payment_term','payment_term_rule','payment_term_version'])", connection);
        Assert.Equal(10, Convert.ToInt32(await rls.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        await using var runtimeRole = new NpgsqlCommand("select not rolbypassrls from pg_roles where rolname='erp_app'", connection);
        Assert.True((bool)(await runtimeRole.ExecuteScalarAsync())!);
        await using var referencePrivileges = new NpgsqlCommand("select has_table_privilege('erp_app','foundation_finbase.currency_reference','SELECT'),has_table_privilege('erp_app','foundation_finbase.currency_reference','INSERT'),has_table_privilege('erp_app','foundation_finbase.currency_reference','UPDATE'),has_table_privilege('erp_app','foundation_finbase.currency_reference','DELETE')", connection);
        await using var referencePrivilegesReader = await referencePrivileges.ExecuteReaderAsync();
        Assert.True(await referencePrivilegesReader.ReadAsync());
        Assert.True(referencePrivilegesReader.GetBoolean(0));
        Assert.False(referencePrivilegesReader.GetBoolean(1));
        Assert.False(referencePrivilegesReader.GetBoolean(2));
        Assert.False(referencePrivilegesReader.GetBoolean(3));
    }

    [Fact]
    public async Task RuntimeRoleCannotReadAnotherTenantFinBaseRows()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var tenants = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("select slug,id from platform.tenant where slug in ('atlas-trading','delta-supplies') order by slug", admin))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) tenants[reader.GetString(0)] = reader.GetGuid(1);
        }
        Assert.Equal(2, tenants.Count);

        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();
        foreach (var (slug, tenantId) in tenants)
        {
            await using var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime);
            scope.Parameters.AddWithValue(tenantId.ToString("D"));
            await scope.ExecuteNonQueryAsync();

            await using var visible = new NpgsqlCommand("select count(*) from foundation_finbase.legal_entity_currency where tenant_id=$1", runtime);
            visible.Parameters.AddWithValue(tenantId);
            Assert.True(Convert.ToInt32(await visible.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0, $"The runtime role could not read the seeded {slug} currency setup.");

            var otherTenant = tenants.Single(pair => pair.Value != tenantId).Value;
            await using var hidden = new NpgsqlCommand("select count(*) from foundation_finbase.legal_entity_currency where tenant_id=$1", runtime);
            hidden.Parameters.AddWithValue(otherTenant);
            Assert.Equal(0, Convert.ToInt32(await hidden.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public async Task SameTenantEntityRatesAreResolvedFromTheirOwnLegalEntityRows()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        Guid tenantId;
        var entities = new List<(Guid Id, string Code)>();
        await using (var tenantCommand = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin))
            tenantId = (Guid)(await tenantCommand.ExecuteScalarAsync())!;
        await using (var entitiesCommand = new NpgsqlCommand("select id,code from platform.legal_entity where tenant_id=$1 order by code", admin))
        {
            entitiesCommand.Parameters.AddWithValue(tenantId);
            await using var reader = await entitiesCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync()) entities.Add((reader.GetGuid(0), reader.GetString(1)));
        }
        Assert.Equal(2, entities.Count);

        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();
        await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime))
        {
            scope.Parameters.AddWithValue(tenantId.ToString("D"));
            await scope.ExecuteNonQueryAsync();
        }

        var rates = new List<decimal>();
        foreach (var entity in entities)
        {
            await using var command = new NpgsqlCommand("select rate from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and purpose='Accounting' and status='Approved'", runtime);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(entity.Id);
            var value = await command.ExecuteScalarAsync();
            Assert.NotNull(value);
            rates.Add(Convert.ToDecimal(value, CultureInfo.InvariantCulture));
        }
        Assert.NotEqual(rates[0], rates[1]);
    }

    [Fact]
    public async Task FinBasePersistsOneCanonicalBaseCurrencyAndExactNumericColumns()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var baseCurrency = new NpgsqlCommand("select count(*) from information_schema.columns where table_schema='platform' and table_name='legal_entity' and column_name='base_currency_code'", connection);
        Assert.Equal(1, Convert.ToInt32(await baseCurrency.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        await using var duplicate = new NpgsqlCommand("select count(*) from information_schema.columns where table_schema='foundation_finbase' and column_name in ('base_currency','base_currency_code','company_currency','ledger_currency','sales_currency','invoice_base_currency')", connection);
        Assert.Equal(0, Convert.ToInt32(await duplicate.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        await using var numeric = new NpgsqlCommand("select (select numeric_precision from information_schema.columns where table_schema='foundation_finbase' and table_name='exchange_rate' and column_name='rate'), (select numeric_scale from information_schema.columns where table_schema='foundation_finbase' and table_name='exchange_rate' and column_name='rate'), (select numeric_precision from information_schema.columns where table_schema='foundation_finbase' and table_name='payment_term_rule' and column_name='fixed_amount'), (select numeric_scale from information_schema.columns where table_schema='foundation_finbase' and table_name='payment_term_rule' and column_name='fixed_amount')", connection);
        await using var reader = await numeric.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(28, reader.GetInt32(0));
        Assert.Equal(12, reader.GetInt32(1));
        Assert.Equal(28, reader.GetInt32(2));
        Assert.Equal(8, reader.GetInt32(3));
    }

    [Fact]
    public async Task PaymentTermApiPersistsPercentageRatiosWithoutConvertingThemToPercentPoints()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var tenantQuery = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin);
        var tenantId = (Guid)(await tenantQuery.ExecuteScalarAsync())!;

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var termCode = $"RATIO-{Guid.CreateVersion7():N}"[..18];
        var request = new CreatePaymentTermRequest(termCode, "اختبار نسبة", "Ratio test", null, new DateOnly(2026, 1, 1),
        [
            new(1, PaymentAllocation.Percentage, "0.15", null, DueDateBasis.Immediate, 0, null, 0),
            new(2, PaymentAllocation.Balance, null, null, DueDateBasis.DaysAfterDate, 30, null, 0)
        ]);
        var correlation = Guid.CreateVersion7();
        var term = await repository.CreatePaymentTermAsync(new TenantId(tenantId), request, correlation, CancellationToken.None);

        await using var persisted = new NpgsqlCommand("select percentage from foundation_finbase.payment_term_rule where tenant_id=$1 and version_id=(select id from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2)", admin);
        persisted.Parameters.AddWithValue(tenantId);
        persisted.Parameters.AddWithValue(term.Id);
        Assert.Equal(0.15m, Convert.ToDecimal(await persisted.ExecuteScalarAsync(), CultureInfo.InvariantCulture));

        await using var outbox = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and correlation_id=$2 and message_type='FinBasePaymentTermActivatedV1'", admin);
        outbox.Parameters.AddWithValue(tenantId);
        outbox.Parameters.AddWithValue(correlation);
        Assert.Equal(1, Convert.ToInt32(await outbox.ExecuteScalarAsync(), CultureInfo.InvariantCulture));

        var schedule = await FinanceBaseRepository.PreviewScheduleAsync(new("100", new DateOnly(2026, 1, 1), 2, request.Rules), request.Rules);
        Assert.Equal("15", schedule[0].Amount);
        Assert.Equal("85", schedule[1].Amount);
    }

    [Fact]
    public async Task ManualRatesRequireReasonAndAnEntityPolicyThatAllowsOverrides()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var tenantQuery = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin);
        var tenantId = (Guid)(await tenantQuery.ExecuteScalarAsync())!;
        await using var entityQuery = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 order by code limit 1", admin);
        entityQuery.Parameters.AddWithValue(tenantId);
        var entityId = (Guid)(await entityQuery.ExecuteScalarAsync())!;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var request = new CreateExchangeRateRequest(entityId, "USD", "EGP", "50.5", ExchangeRatePurpose.Accounting, new DateOnly(2026, 10, 19), "Manual", null, "approved test reason");
        var missingReason = await Assert.ThrowsAsync<FinanceBaseException>(() => repository.CreateRateAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(), request with { Reason = null }, Guid.CreateVersion7(), CancellationToken.None));
        Assert.Equal("finbase.rate_override_not_allowed", missingReason.Code);
        var exception = await Assert.ThrowsAsync<FinanceBaseException>(() => repository.CreateRateAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(), request, Guid.CreateVersion7(), CancellationToken.None));
        Assert.Equal("finbase.rate_override_not_allowed", exception.Code);
    }

    [Fact]
    public async Task RateLookupCacheIsIsolatedByLegalEntity()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var tenantCommand = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin);
        var tenantId = (Guid)(await tenantCommand.ExecuteScalarAsync())!;
        var entities = new List<Guid>();
        await using (var command = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 order by code", admin))
        {
            command.Parameters.AddWithValue(tenantId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) entities.Add(reader.GetGuid(0));
        }
        Assert.Equal(2, entities.Count);
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        async Task<string> Resolve(Guid entity) => (await repository.ConvertAsync(new TenantId(tenantId), new(entity, "1", "USD", "EGP", ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 2)), CancellationToken.None)).Rate;
        var first = await Resolve(entities[0]);
        var second = await Resolve(entities[1]);
        Assert.NotEqual(first, second);
        Assert.Equal(first, await Resolve(entities[0]));
        Assert.Equal(second, await Resolve(entities[1]));
    }

    [Fact]
    public async Task ProviderSyncImportsIdempotentlyAndRecordsOutageWithoutChangingApprovedTruth()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var tenantCommand = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin);
        var tenantId = (Guid)(await tenantCommand.ExecuteScalarAsync())!;
        await using var entityCommand = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 order by code limit 1", admin);
        entityCommand.Parameters.AddWithValue(tenantId);
        var entityId = (Guid)(await entityCommand.ExecuteScalarAsync())!;
        var providerCode = $"test_{Guid.CreateVersion7():N}";
        var outageCode = $"outage_{Guid.CreateVersion7():N}";
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var tenant = new TenantId(tenantId);
        var entity = new LegalEntityId(entityId);
        var correlation = Guid.CreateVersion7();
        var source = await repository.CreateRateSourceConfigAsync(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, new(providerCode, "USD", "EGP", ExchangeRatePurpose.Accounting, 1, "secret-ref:finbase/test-provider", 60), correlation, CancellationToken.None);
        var eventKey = $"stable-{Guid.CreateVersion7():N}";
        var quote = new ExchangeRateProviderQuote("USD", "EGP", 51.2345m, ExchangeRatePurpose.Accounting, DateOnly.FromDateTime(DateTime.UtcNow), eventKey, "fixture-provider");
        var provider = new DeterministicExchangeRateProvider(providerCode, quote);
        var service = new FinanceBaseRateSyncService(repository, new ExchangeRateProviderRegistry([provider]), TimeProvider.System);
        Assert.Equal(1, await service.SyncDueAsync(CancellationToken.None));

        await using (var dueAgain = new NpgsqlCommand("update foundation_finbase.exchange_rate_source_config set next_attempt_at=now()-interval '1 second' where tenant_id=$1 and id=$2", admin))
        {
            dueAgain.Parameters.AddWithValue(tenantId);
            dueAgain.Parameters.AddWithValue(source.Id);
            Assert.Equal(1, await dueAgain.ExecuteNonQueryAsync());
        }
        Assert.Equal(0, await service.SyncDueAsync(CancellationToken.None));

        var outage = await repository.CreateRateSourceConfigAsync(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), entity, new(outageCode, "USD", "EGP", ExchangeRatePurpose.Commercial, 2, null, 60), correlation, CancellationToken.None);
        await using var deltaTenantQuery = new NpgsqlCommand("select id from platform.tenant where slug='delta-supplies'", admin);
        var deltaTenantId = (Guid)(await deltaTenantQuery.ExecuteScalarAsync())!;
        var entityIds = new List<Guid>();
        await using (var entityList = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 order by code", admin))
        {
            entityList.Parameters.AddWithValue(tenantId);
            await using var entityReader = await entityList.ExecuteReaderAsync();
            while (await entityReader.ReadAsync()) entityIds.Add(entityReader.GetGuid(0));
        }
        Assert.Equal(2, entityIds.Count);
        await using (var runtime = new NpgsqlConnection(runtimeConnectionString))
        {
            await runtime.OpenAsync();
            await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime))
            {
                scope.Parameters.AddWithValue(deltaTenantId.ToString("D"));
                await scope.ExecuteNonQueryAsync();
            }
            await using var hiddenTenantConfig = new NpgsqlCommand("select count(*) from foundation_finbase.exchange_rate_source_config where tenant_id=$1", runtime);
            hiddenTenantConfig.Parameters.AddWithValue(tenantId);
            Assert.Equal(0, Convert.ToInt32(await hiddenTenantConfig.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
            await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime))
            {
                scope.Parameters.AddWithValue(tenantId.ToString("D"));
                await scope.ExecuteNonQueryAsync();
            }
            await using var hiddenEntityConfig = new NpgsqlCommand("select count(*) from foundation_finbase.exchange_rate_source_config where tenant_id=$1 and legal_entity_id=$2", runtime);
            hiddenEntityConfig.Parameters.AddWithValue(tenantId);
            hiddenEntityConfig.Parameters.AddWithValue(entityIds[1]);
            Assert.Equal(0, Convert.ToInt32(await hiddenEntityConfig.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        }
        var beforeApproved = await ApprovedRateCountAsync(admin, tenantId, entityId);
        var failingService = new FinanceBaseRateSyncService(repository, new ExchangeRateProviderRegistry([provider, new DeterministicExchangeRateProvider(outageCode, null, true)]), TimeProvider.System);
        Assert.Equal(0, await failingService.SyncDueAsync(CancellationToken.None));
        Assert.Equal(beforeApproved, await ApprovedRateCountAsync(admin, tenantId, entityId));
        await using var sourceState = new NpgsqlCommand("select failure_count,last_error_code,next_attempt_at > now() from foundation_finbase.exchange_rate_source_config where tenant_id=$1 and id=$2", admin);
        sourceState.Parameters.AddWithValue(tenantId);
        sourceState.Parameters.AddWithValue(outage.Id);
        await using var stateReader = await sourceState.ExecuteReaderAsync();
        Assert.True(await stateReader.ReadAsync());
        Assert.Equal(1, stateReader.GetInt32(0));
        Assert.Equal("provider_sync_failed", stateReader.GetString(1));
        Assert.True(stateReader.GetBoolean(2));
        await stateReader.CloseAsync();
        await using var candidate = new NpgsqlCommand("select count(*),min(status),min(source_type) from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and provider_event_key=$3", admin);
        candidate.Parameters.AddWithValue(tenantId);
        candidate.Parameters.AddWithValue(entityId);
        candidate.Parameters.AddWithValue(eventKey);
        await using var candidateReader = await candidate.ExecuteReaderAsync();
        Assert.True(await candidateReader.ReadAsync());
        Assert.Equal(1, candidateReader.GetInt64(0));
        Assert.Equal("Draft", candidateReader.GetString(1));
        Assert.Equal("Provider", candidateReader.GetString(2));
        await candidateReader.CloseAsync();
        await using var outbox = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and correlation_id=$2 and message_type='FinBaseRateSourceConfiguredV1'", admin);
        outbox.Parameters.AddWithValue(tenantId);
        outbox.Parameters.AddWithValue(correlation);
        Assert.Equal(2, Convert.ToInt32(await outbox.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task PaymentTermLifecycleKeepsVersionHistoryAndArchivedTermsOutOfNewUse()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var tenantCommand = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin);
        var tenantId = (Guid)(await tenantCommand.ExecuteScalarAsync())!;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var tenant = new TenantId(tenantId);
        var term = await repository.CreatePaymentTermAsync(tenant, new($"HIST-{Guid.CreateVersion7():N}"[..18], "شروط محفوظة", "History retained", null, new DateOnly(2026, 1, 1), [new(1, PaymentAllocation.Balance, null, null, DueDateBasis.Immediate, 0, null, 0)]), Guid.CreateVersion7(), CancellationToken.None);
        var draft = await repository.CreatePaymentTermVersionAsync(tenant, term.Id, new(new DateOnly(2026, 2, 1), "New effective version", [new(1, PaymentAllocation.Percentage, "0.3", null, DueDateBasis.Immediate, 0, null, 0), new(2, PaymentAllocation.Balance, null, null, DueDateBasis.DaysAfterDate, 30, null, 0)], 1), Guid.CreateVersion7(), CancellationToken.None);
        var active = await repository.ActivatePaymentTermVersionAsync(tenant, term.Id, draft.Id, draft.TermVersion, Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal("Active", active.Status);
        var history = await repository.GetPaymentTermHistoryAsync(tenant, term.Id, CancellationToken.None);
        Assert.NotNull(history);
        Assert.Equal(["Retired", "Active"], history.Versions.OrderBy(version => version.VersionNumber).Select(version => version.Status));
        Assert.Equal(3, history.Versions[0].Rules.Count + history.Versions[1].Rules.Count);

        var stale = await Assert.ThrowsAsync<FinanceBaseException>(() => repository.CreatePaymentTermVersionAsync(tenant, term.Id, new(new DateOnly(2026, 3, 1), null, [new(1, PaymentAllocation.Balance, null, null, DueDateBasis.Immediate, 0, null, 0)], 2), Guid.CreateVersion7(), CancellationToken.None));
        Assert.Equal("finbase.payment_term_concurrency", stale.Code);
        var archivedVersion = await repository.ArchivePaymentTermAsync(tenant, term.Id, active.TermVersion, Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(active.TermVersion + 1, archivedVersion);
        Assert.Null(await repository.ResolveActivePaymentTermVersionAsync(tenant, term.Id, new DateOnly(2026, 3, 1), CancellationToken.None));
        var archivedHistory = await repository.GetPaymentTermHistoryAsync(tenant, term.Id, CancellationToken.None);
        Assert.Equal("Archived", archivedHistory!.Status);
        Assert.Equal(2, archivedHistory.Versions.Count);
        Assert.All(archivedHistory.Versions, version => Assert.NotEmpty(version.Rules));
        await using var immutableHistory = new NpgsqlCommand("select count(*) from foundation_finbase.payment_term_version where tenant_id=$1 and payment_term_id=$2", admin);
        immutableHistory.Parameters.AddWithValue(tenantId);
        immutableHistory.Parameters.AddWithValue(term.Id);
        Assert.Equal(2, Convert.ToInt32(await immutableHistory.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        await using var committedEvents = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and correlation_id in (select correlation_id from platform.outbox_message where tenant_id=$1 and message_type in ('FinBasePaymentTermChangedV1','FinBasePaymentTermActivatedV1','FinBasePaymentTermArchivedV1') and payload::text like '%' || $2 || '%')", admin);
        committedEvents.Parameters.AddWithValue(tenantId);
        committedEvents.Parameters.AddWithValue(term.Id.ToString("D"));
        Assert.True(Convert.ToInt32(await committedEvents.ExecuteScalarAsync(), CultureInfo.InvariantCulture) >= 4);
    }

    [Fact]
    public async Task ApprovedRateRoundingAndFinancialPolicyHistoryAreProtected()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var tenantCommand = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id where t.slug='atlas-trading' order by e.code limit 1", admin);
        await using var row = await tenantCommand.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        var tenantId = row.GetGuid(0);
        var entityId = row.GetGuid(1);
        await row.CloseAsync();
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var tenant = new TenantId(tenantId);
        var policy = await repository.GetPolicyAsync(tenant, new LegalEntityId(entityId), CancellationToken.None);
        Assert.NotNull(policy);
        await repository.UpdatePolicyAsync(tenant, new LegalEntityId(entityId), new(policy.RoundingPolicyId, policy.ExchangeRateMaxAgeDays, policy.ManualRateOverrideAllowed, policy.Version), Guid.CreateVersion7(), CancellationToken.None);
        await using var history = new NpgsqlCommand("select count(*) from foundation_finbase.financial_policy_version where tenant_id=$1 and legal_entity_id=$2 and version=$3", admin);
        history.Parameters.AddWithValue(tenantId);
        history.Parameters.AddWithValue(entityId);
        history.Parameters.AddWithValue(policy.Version);
        Assert.Equal(1, Convert.ToInt32(await history.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        await using var alterPolicyHistory = new NpgsqlCommand("update foundation_finbase.financial_policy_version set exchange_rate_max_age_days=exchange_rate_max_age_days+1 where tenant_id=$1 and legal_entity_id=$2 and version=$3", admin);
        alterPolicyHistory.Parameters.AddWithValue(tenantId);
        alterPolicyHistory.Parameters.AddWithValue(entityId);
        alterPolicyHistory.Parameters.AddWithValue(policy.Version);
        var policyException = await Assert.ThrowsAsync<PostgresException>(() => alterPolicyHistory.ExecuteNonQueryAsync());
        Assert.Equal("55000", policyException.SqlState);

        await using var protectedRows = new NpgsqlCommand("select (select id from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and status='Approved' limit 1),(select id from foundation_finbase.financial_rounding_policy where tenant_id=$1 and status='Active' limit 1)", admin);
        protectedRows.Parameters.AddWithValue(tenantId);
        protectedRows.Parameters.AddWithValue(entityId);
        await using var protectedReader = await protectedRows.ExecuteReaderAsync();
        Assert.True(await protectedReader.ReadAsync());
        var rateId = protectedReader.GetGuid(0);
        var roundingId = protectedReader.GetGuid(1);
        await protectedReader.CloseAsync();
        await using var alterRate = new NpgsqlCommand("update foundation_finbase.exchange_rate set rate=rate+1 where tenant_id=$1 and id=$2", admin);
        alterRate.Parameters.AddWithValue(tenantId);
        alterRate.Parameters.AddWithValue(rateId);
        var rateException = await Assert.ThrowsAsync<PostgresException>(() => alterRate.ExecuteNonQueryAsync());
        Assert.Equal("55000", rateException.SqlState);
        await using var alterRounding = new NpgsqlCommand("update foundation_finbase.financial_rounding_policy set money_scale=money_scale where tenant_id=$1 and id=$2", admin);
        alterRounding.Parameters.AddWithValue(tenantId);
        alterRounding.Parameters.AddWithValue(roundingId);
        var roundingException = await Assert.ThrowsAsync<PostgresException>(() => alterRounding.ExecuteNonQueryAsync());
        Assert.Equal("55000", roundingException.SqlState);
    }

    [Fact]
    public async Task RoundingPolicyPersistsPerContextModesAndConversionUsesFxContext()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var query = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id where t.slug='atlas-trading' order by e.code limit 1", admin);
        await using var ids = await query.ExecuteReaderAsync();
        Assert.True(await ids.ReadAsync());
        var tenantId = ids.GetGuid(0);
        var entityId = ids.GetGuid(1);
        await ids.CloseAsync();
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var tenant = new TenantId(tenantId);
        var entity = new LegalEntityId(entityId);
        var currentPolicy = await repository.GetPolicyAsync(tenant, entity, CancellationToken.None);
        Assert.NotNull(currentPolicy);
        var contextRules = Enum.GetValues<RoundingContext>().Select(context => new RoundingPolicyContextRuleRequest(context, context == RoundingContext.FXBaseAmount ? RoundingMode.Up : RoundingMode.Down)).ToArray();
        var created = await repository.CreateRoundingPolicyAsync(tenant, new($"CTX-{Guid.CreateVersion7():N}"[..18], "سياسات التقريب", "Context rounding", RoundingMode.HalfUp, 2, "0.01", contextRules, LineDocumentRoundingStrategy.SumExactLinesThenRound), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(8, created.ContextRules!.Count);
        Assert.Equal(LineDocumentRoundingStrategy.SumExactLinesThenRound, created.LineDocumentStrategy);
        var listedPolicy = Assert.Single(await repository.ListRoundingPoliciesAsync(tenant, CancellationToken.None), policyRow => policyRow.Id == created.Id);
        Assert.Equal(8, listedPolicy.ContextRules!.Count);
        await repository.UpdatePolicyAsync(tenant, entity, new(created.Id, currentPolicy.ExchangeRateMaxAgeDays, currentPolicy.ManualRateOverrideAllowed, currentPolicy.Version), Guid.CreateVersion7(), CancellationToken.None);
        var converted = await repository.ConvertAsync(tenant, new(entityId, "1", "USD", "EGP", ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 2)), CancellationToken.None);
        Assert.Equal("50.13", converted.TargetAmount);
        await using var persisted = new NpgsqlCommand("select count(*) from foundation_finbase.financial_rounding_policy_rule where tenant_id=$1 and rounding_policy_id=$2", admin);
        persisted.Parameters.AddWithValue(tenantId);
        persisted.Parameters.AddWithValue(created.Id);
        Assert.Equal(8, Convert.ToInt32(await persisted.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        await using var alterRule = new NpgsqlCommand("update foundation_finbase.financial_rounding_policy_rule set mode=mode where tenant_id=$1 and rounding_policy_id=$2 and rounding_context='FXBaseAmount'", admin);
        alterRule.Parameters.AddWithValue(tenantId);
        alterRule.Parameters.AddWithValue(created.Id);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => alterRule.ExecuteNonQueryAsync());
        Assert.Equal("55000", immutable.SqlState);
    }

    [Fact]
    public async Task BoundedPerformanceChecksMeasurePolicyCurrencyRateConversionScheduleAndConcurrencyPaths()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_FINBASE_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var entityQuery = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id where t.slug='atlas-trading' order by e.code limit 1", admin);
        await using var ids = await entityQuery.ExecuteReaderAsync();
        Assert.True(await ids.ReadAsync());
        var tenantId = ids.GetGuid(0);
        var entityId = ids.GetGuid(1);
        await ids.CloseAsync();
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var tenant = new TenantId(tenantId);
        var entity = new LegalEntityId(entityId);

        var policyWatch = Stopwatch.StartNew();
        for (var index = 0; index < 1000; index++) Assert.NotNull(await repository.GetPolicyAsync(tenant, entity, CancellationToken.None));
        policyWatch.Stop();

        var request = new ConvertMoneyRequest(entityId, "10.25", "USD", "EGP", ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 2));
        _ = await repository.ConvertAsync(tenant, request, CancellationToken.None);
        var conversionTicks = new long[1000];
        var conversionsWatch = Stopwatch.StartNew();
        for (var index = 0; index < conversionTicks.Length; index++)
        {
            var one = Stopwatch.StartNew();
            var converted = await repository.ConvertAsync(tenant, request, CancellationToken.None);
            one.Stop();
            Assert.Equal("USD", converted.SourceCurrency);
            conversionTicks[index] = one.ElapsedTicks;
        }
        conversionsWatch.Stop();

        var coldRateTicks = new long[32];
        for (var index = 0; index < coldRateTicks.Length; index++)
        {
            var coldRepository = new FinanceBaseRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
            var one = Stopwatch.StartNew();
            var converted = await coldRepository.ConvertAsync(tenant, request, CancellationToken.None);
            one.Stop();
            Assert.Equal("50.125", converted.Rate);
            coldRateTicks[index] = one.ElapsedTicks;
        }

        var rateReadsWatch = Stopwatch.StartNew();
        var concurrentReads = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => repository.ConvertAsync(tenant, request, CancellationToken.None)));
        rateReadsWatch.Stop();
        Assert.All(concurrentReads, result => Assert.Equal("USD", result.SourceCurrency));

        var candidate = new ExchangeRateCandidate("USD", "EGP", 50.125m, ExchangeRatePurpose.Accounting, request.BusinessDate, ExchangeRateStatus.Approved, Guid.CreateVersion7(), 1, "Performance", false);
        var snapshot = ExchangeRateSnapshot.FromApprovedRate(tenantId, entityId, candidate, DateTimeOffset.UtcNow);
        var policySnapshot = RoundingPolicySnapshot.Create(Guid.CreateVersion7(), 1, RoundingMode.HalfUp, 2);
        var conversionWatch = Stopwatch.StartNew();
        for (var index = 0; index < 1000; index++) _ = CurrencyConverter.Convert(Money.Create("10.25", "USD"), "EGP", snapshot, policySnapshot);
        conversionWatch.Stop();
        var rules = new[]
        {
            new PaymentTermRuleInput(1, PaymentAllocation.Percentage, 0.3333m, null, DueDateBasis.Immediate, 0, null, 0),
            new PaymentTermRuleInput(2, PaymentAllocation.Percentage, 0.3333m, null, DueDateBasis.DaysAfterDate, 30, null, 0),
            new PaymentTermRuleInput(3, PaymentAllocation.Balance, null, null, DueDateBasis.EndOfMonth, 0, null, 0)
        };
        var scheduleWatch = Stopwatch.StartNew();
        for (var index = 0; index < 1000; index++) Assert.Equal(1000m, PaymentScheduleCalculator.Calculate(1000m, new DateOnly(2026, 1, 31), rules, 2).Sum(line => line.Amount));
        scheduleWatch.Stop();
        Array.Sort(conversionTicks);
        Array.Sort(coldRateTicks);
        var p95Milliseconds = conversionTicks[(int)Math.Ceiling(conversionTicks.Length * 0.95) - 1] * 1000d / Stopwatch.Frequency;
        var coldRateP95Milliseconds = coldRateTicks[(int)Math.Ceiling(coldRateTicks.Length * 0.95) - 1] * 1000d / Stopwatch.Frequency;
        Assert.True(coldRateP95Milliseconds < 50, $"Indexed local rate resolution p95 was {coldRateP95Milliseconds:F3} ms; local target is <50 ms.");
        output.WriteLine($"FinBase bounded check: policy reads 1000={policyWatch.Elapsed.TotalMilliseconds:F2} ms; warm cached rate + currency reference full conversions 1000={conversionsWatch.Elapsed.TotalMilliseconds:F2} ms (p95={p95Milliseconds:F3} ms); cold indexed rate resolution 32 (p95={coldRateP95Milliseconds:F3} ms); concurrent rate reads 64={rateReadsWatch.Elapsed.TotalMilliseconds:F2} ms; pure explicit-snapshot conversions 1000={conversionWatch.Elapsed.TotalMilliseconds:F2} ms; schedule calculations 1000={scheduleWatch.Elapsed.TotalMilliseconds:F2} ms.");
    }

    private static async Task<int> ApprovedRateCountAsync(NpgsqlConnection admin, Guid tenantId, Guid entityId)
    {
        await using var command = new NpgsqlCommand("select count(*) from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and status='Approved'", admin);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(entityId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private sealed class DeterministicExchangeRateProvider(string sourceCode, ExchangeRateProviderQuote? quote, bool fail = false) : IExchangeRateProvider
    {
        public string SourceCode => sourceCode;
        public Task<ExchangeRateProviderQuote?> FetchAsync(ExchangeRateProviderRequest request, CancellationToken cancellationToken)
            => fail ? Task.FromException<ExchangeRateProviderQuote?>(new InvalidOperationException("simulated provider outage")) : Task.FromResult(quote);
    }
}
