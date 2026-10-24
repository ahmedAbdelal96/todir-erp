using System.Diagnostics;
using System.Reflection;
using Core.Services;
using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Npgsql;
using Shared.Contracts.Ledger;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerPass3AcceptanceTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    [Fact]
    public async Task ManagedSetupIsDeterministicAndModeChangePreservesHistory()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
            var setup = CreateSetup(dataSource);
            var actor = Guid.CreateVersion7();
            var first = await setup.InitializeAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(), actor, Guid.CreateVersion7(), default);
            var firstCounts = await CountsAsync(dataSource, "select count(*) from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.account_mapping where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2");
            var entryIdsBeforeMode = await IdsAsync(dataSource, "select id from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 order by id");
            var mappingIdsBeforeMode = await IdsAsync(dataSource, "select id from finance_ledger.account_mapping where tenant_id=$1 and legal_entity_id=$2 order by id");

            var second = await setup.InitializeAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(), actor, Guid.CreateVersion7(), default);
            var secondCounts = await CountsAsync(dataSource, "select count(*) from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.account_mapping where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2");
            Assert.Equal(first.ConfigurationVersionId, second.ConfigurationVersionId);
            Assert.Equal(firstCounts, secondCounts);
            Assert.Equal(LedgerReadinessState.Ready, first.Readiness.State);

            var changed = await setup.ChangeModeAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, LedgerMode.Full, actor, Guid.CreateVersion7(), default);
            Assert.Equal(LedgerMode.Full, changed.Mode);
            Assert.Equal(entryIdsBeforeMode, await IdsAsync(dataSource, "select id from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 order by id"));
            Assert.Equal(mappingIdsBeforeMode, await IdsAsync(dataSource, "select id from finance_ledger.account_mapping where tenant_id=$1 and legal_entity_id=$2 order by id"));
            await setup.ChangeModeAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, LedgerMode.Managed, actor, Guid.CreateVersion7(), default);
        }
        finally { Gate.Release(); }
    }

    [Fact]
    public async Task ReadinessExplainsMissingMappingAndNeverCreatesSuspense()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
            var setup = CreateSetup(dataSource);
            await setup.InitializeAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
            await using var connection = await dataSource.OpenConnectionAsync();
            await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
            await ExecuteAsync(connection, "update finance_ledger.account_mapping set status='Archived' where tenant_id=$1 and legal_entity_id=$2 and role_id in (select id from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2 and code in ('AccountsReceivableControl','Receivable'))", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value);
            try
            {
                var readiness = await setup.GetReadinessAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, default);
                var invoices = Assert.Single(readiness.Capabilities, item => item.Capability == "CanPostInvoices");
                Assert.Equal(LedgerReadinessState.Blocked, invoices.State);
                Assert.Contains(invoices.Reasons, reason => reason.Code == "ledger.account_mapping_missing");
                Assert.Equal(0, await ScalarAsync(connection, "select count(*) from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and subtype ilike '%Suspense%'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value));
            }
            finally
            {
                await ExecuteAsync(connection, "update finance_ledger.account_mapping set status='Active' where tenant_id=$1 and legal_entity_id=$2 and role_id in (select id from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2 and code in ('AccountsReceivableControl','Receivable'))", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value);
            }
        }
        finally { Gate.Release(); }
    }

    [Fact]
    public async Task AccountingWorkspaceSearchCloseReadinessAndPreviewAreBounded()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
            var posting = LedgerTestSupport.CreateRepository(dataSource);
            var setup = CreateSetup(dataSource);
            var ux = new LedgerUxRepository(dataSource, setup);
            await setup.InitializeAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(), Guid.CreateVersion7(), Guid.CreateVersion7(), default);

            Assert.Contains((await ux.SearchAccountsAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, "110000", null, 10, default)).Items, item => item.Code == "110000");
            Assert.NotEmpty((await ux.SearchAccountsAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, "النقدية", null, 10, default)).Items);
            Assert.NotEmpty((await ux.SearchAccountsAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, "Cash", null, 10, default)).Items);
            var accountId = await GuidAsync(dataSource, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code='110000'");
            var journalId = await GuidAsync(dataSource, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'");
            var periodId = await GuidAsync(dataSource, "select id from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and start_date='2026-01-01'");
            Assert.NotNull(await ux.GetAccountWorkspaceAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, accountId, default));
            Assert.NotNull(await ux.GetPeriodCloseReadinessAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, periodId, default));

            var revenueAccountId = await GuidAsync(dataSource, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code='410000'");
            var request = new PostingPreviewRequest(new(2026, 1, 15), journalId, "Preview", "EGP", [new(accountId, 100m, 0m, "EGP", 100m, 0m, 100m, 0m, 1m, [], "Debit"), new(revenueAccountId, 0m, 100m, "EGP", 0m, 100m, 0m, 100m, 1m, [], "Credit")]);
            var before = await CountsAsync(dataSource, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2", "select count(*) from platform.outbox_message where tenant_id=$1");
            var preview = new LedgerPreviewService(new NpgsqlTransactionCoordinator(dataSource), new LedgerTransactionCapability(posting));
            var result = await preview.PreviewExplicitAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, request, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
            var repeated = await preview.PreviewExplicitAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, request, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
            Assert.Equal(result.JournalId, repeated.JournalId); Assert.Equal(result.PostingDate, repeated.PostingDate); Assert.Equal(result.PostingRuleVersionId, repeated.PostingRuleVersionId); Assert.Equal(result.AccountingConfigurationVersionId, repeated.AccountingConfigurationVersionId); Assert.Equal(result.BaseCurrencyCode, repeated.BaseCurrencyCode); Assert.Equal(result.TotalDebit, repeated.TotalDebit); Assert.Equal(result.TotalCredit, repeated.TotalCredit);
            Assert.Equal(100m, result.TotalDebit);
            Assert.Equal(100m, result.TotalCredit);
            Assert.Equal(result.Lines.ToArray(), repeated.Lines.ToArray());
            Assert.Equal(before, await CountsAsync(dataSource, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2", "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2", "select count(*) from platform.outbox_message where tenant_id=$1"));

            var explicitPayload = new ExplicitLedgerPostingPayload(journalId, request.Description, request.BaseCurrencyCode, request.Lines.Select(line => new LedgerPostingLinePayload(line.AccountId, line.Debit, line.Credit, line.TransactionCurrencyCode, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, line.ExchangeRate, [], line.Description, null)).ToArray());
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.ManualGeneralJournalPosted, 1, "ledger", $"preview-post:{Guid.CreateVersion7():N}", $"preview-post:{Guid.CreateVersion7():N}", request.PostingDate, request.PostingDate, explicitPayload, Guid.CreateVersion7(), Guid.CreateVersion7());
            var posted = await posting.PostAsync(fact);
            await using var connection = await dataSource.OpenConnectionAsync();
            await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
            await using var entry = new NpgsqlCommand("select journal_id,posting_date,posting_rule_version_id,accounting_configuration_version_id,base_currency_code from finance_ledger.journal_entry where tenant_id=$1 and id=$2", connection);
            entry.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); entry.Parameters.AddWithValue(posted.JournalEntryId);
            await using var entryReader = await entry.ExecuteReaderAsync();
            Assert.True(await entryReader.ReadAsync());
            Assert.Equal(result.JournalId, entryReader.GetGuid(0)); Assert.Equal(result.PostingDate, DateOnly.FromDateTime(entryReader.GetDateTime(1))); Assert.Equal(result.PostingRuleVersionId, entryReader.GetGuid(2)); Assert.Equal(result.AccountingConfigurationVersionId, entryReader.GetGuid(3)); Assert.Equal(result.BaseCurrencyCode, entryReader.GetString(4));
            await entryReader.CloseAsync();
            await using var actualLines = new NpgsqlCommand("select sequence_no,account_id,debit,credit,base_debit,base_credit,currency_code,transaction_debit,transaction_credit,exchange_rate,account_role,account_mapping_id,account_mapping_version_id,description from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2 and journal_entry_id=$3 order by sequence_no", connection);
            actualLines.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); actualLines.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); actualLines.Parameters.AddWithValue(posted.JournalEntryId);
            await using var actualReader = await actualLines.ExecuteReaderAsync();
            foreach (var expected in result.Lines)
            {
                Assert.True(await actualReader.ReadAsync());
                Assert.Equal(expected.Sequence, actualReader.GetInt32(0)); Assert.Equal(expected.AccountId, actualReader.GetGuid(1)); Assert.Equal(expected.Debit, actualReader.GetDecimal(2)); Assert.Equal(expected.Credit, actualReader.GetDecimal(3)); Assert.Equal(expected.BaseDebit, actualReader.GetDecimal(4)); Assert.Equal(expected.BaseCredit, actualReader.GetDecimal(5)); Assert.Equal(expected.CurrencyCode, actualReader.GetString(6)); Assert.Equal(expected.TransactionDebit, actualReader.GetDecimal(7)); Assert.Equal(expected.TransactionCredit, actualReader.GetDecimal(8)); Assert.Equal(expected.ExchangeRate, actualReader.GetDecimal(9)); Assert.Equal(expected.AccountRole, actualReader.GetString(10)); Assert.Equal(expected.MappingId, actualReader.IsDBNull(11) ? null : actualReader.GetGuid(11)); Assert.Equal(expected.MappingVersionId, actualReader.IsDBNull(12) ? null : actualReader.GetGuid(12)); Assert.Equal(expected.Description, actualReader.IsDBNull(13) ? null : actualReader.GetString(13)); Assert.Empty(expected.Dimensions);
            }
            Assert.False(await actualReader.ReadAsync());
        }
        finally { Gate.Release(); }
    }

    [Fact]
    public async Task PeriodLifecycleEmitsOneAuditedCloseAndReopenEvent()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
            var posting = LedgerTestSupport.CreateRepository(dataSource);
            var periodId = await GuidAsync(dataSource, "select id from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and start_date='2026-12-01'");
            await posting.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, periodId, PeriodStatus.Open, default);
            var actor = Guid.CreateVersion7(); var closeCorrelation = Guid.CreateVersion7(); var reopenCorrelation = Guid.CreateVersion7();
            Assert.True(await posting.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, periodId, PeriodStatus.Closed, actor, closeCorrelation, default));
            Assert.False(await posting.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, periodId, PeriodStatus.Closed, actor, Guid.CreateVersion7(), default));
            Assert.True(await posting.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, periodId, PeriodStatus.Open, actor, reopenCorrelation, default));
            await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
            Assert.Equal(1, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and correlation_id=$2 and message_type='PeriodClosedV1'", LedgerTestSupport.AtlasTenant.Value, closeCorrelation));
            Assert.Equal(1, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and correlation_id=$2 and message_type='PeriodReopenedV1'", LedgerTestSupport.AtlasTenant.Value, reopenCorrelation));
            Assert.Equal(1, await ScalarAsync(connection, "select count(*) from platform.audit_entry where tenant_id=$1 and correlation_id=$2 and action='period.reopened'", LedgerTestSupport.AtlasTenant.Value, reopenCorrelation));
        }
        finally { Gate.Release(); }
    }

    [Fact]
    public void CrossModuleFactsAreSemanticVersionedAndFinanciallySafe()
    {
        IAccountingFactPayload[] samples =
        [
            new CustomerInvoicePostedPayload(100m, 14m, 114m, "EGP", "CUSTOMER", "GENERAL", [new("VAT14", 14m, 14m, "OutputTax")]),
            new PaymentFactPayload(114m, "EGP", "CUSTOMER", "PaymentReceived"),
            new InventoryValuationFactPayload(50m, "EGP", "RAW_MATERIAL", "InventoryReceiptValued"),
            new PayrollPostedPayload(100m, 80m, "EGP"),
            new TaxAdjustmentPayload(14m, "EGP", "VAT14", "OutputTax")
        ];
        foreach (var sample in samples)
        {
            Assert.DoesNotContain(sample.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance), property => property.Name.Contains("AccountId", StringComparison.OrdinalIgnoreCase) || property.PropertyType == typeof(double) || property.PropertyType == typeof(float));
            Assert.NotEqual(string.Empty, AccountingFactFingerprint.Compute(sample));
        }
        Assert.Contains(AccountingFactTypes.CustomerInvoicePosted, AccountingFactTypes.Known);
        Assert.Contains(AccountingFactTypes.PaymentReceived, AccountingFactTypes.Known);
        Assert.Contains(AccountingFactTypes.InventoryReceiptValued, AccountingFactTypes.Known);
        Assert.Contains(AccountingFactTypes.PayrollPosted, AccountingFactTypes.Known);
        Assert.Contains(AccountingFactTypes.TaxAdjustment, AccountingFactTypes.Known);
        var v1 = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.CustomerInvoicePosted, 1, "sales", "sample", "sample", new(2026, 1, 1), new(2026, 1, 1), (CustomerInvoicePostedPayload)samples[0], Guid.CreateVersion7(), Guid.CreateVersion7());
        var v2 = v1 with { FactVersion = 2 };
        Assert.Equal(1, v1.FactVersion);
        Assert.Equal(2, v2.FactVersion);
        Assert.DoesNotContain(typeof(CustomerInvoicePostedPayload).Assembly.GetTypes(), type => type.Name.Contains("DebitCreditFact", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProducerRegistryAllowsOnlyRegisteredFactVersionAndProducer()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
            var posting = LedgerTestSupport.CreateRepository(dataSource);
            var baseline = await CountsAsync(dataSource,
                "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2",
                "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2",
                "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2",
                "select count(*) from platform.outbox_message where tenant_id=$1");

            var valid = await posting.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"REGISTRY-VALID-{Guid.CreateVersion7():N}"));
            Assert.False(valid.AlreadyPosted);

            var rejected = new List<(string Case, AccountingFactEnvelope<IAccountingFactPayload> Fact)>
            {
                ("wrong producer", AsGeneric(LedgerTestSupport.Invoice($"REGISTRY-WRONG-PRODUCER-{Guid.CreateVersion7():N}") with { SourceModule = "payments" })),
                ("unknown producer", AsGeneric(LedgerTestSupport.Invoice($"REGISTRY-UNKNOWN-PRODUCER-{Guid.CreateVersion7():N}") with { SourceModule = "untrusted-external" })),
                ("wrong fact", AsGeneric(LedgerTestSupport.Invoice($"REGISTRY-WRONG-FACT-{Guid.CreateVersion7():N}") with { FactType = AccountingFactTypes.PaymentReceived })),
                ("unregistered version", AsGeneric(LedgerTestSupport.Invoice($"REGISTRY-WRONG-VERSION-{Guid.CreateVersion7():N}") with { FactVersion = 2 }))
            };

            foreach (var item in rejected)
            {
                var exception = await Assert.ThrowsAsync<LedgerDomainException>(() => posting.PostAsync(item.Fact));
                Assert.True(exception.Code is "ledger.fact_producer_unregistered" or "ledger.fact_unsupported" or "ledger.posting_rule_missing");
                Assert.Equal(0, await ScalarByIdAsync(dataSource, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3", item.Fact.FactId));
                Assert.Equal(0, await ScalarByIdAsync(dataSource, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 and fact_id=$3", item.Fact.FactId));
                Assert.Equal(0, await ScalarByIdAsync(dataSource, "select count(*) from finance_ledger.journal_entry_line l join finance_ledger.journal_entry e on e.tenant_id=l.tenant_id and e.legal_entity_id=l.legal_entity_id and e.id=l.journal_entry_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_id=$3", item.Fact.FactId));
                Assert.Equal(0, await ScalarByIdAsync(dataSource, "select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$3", item.Fact.FactId));
            }

            var after = await CountsAsync(dataSource,
                "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2",
                "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2",
                "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2",
                "select count(*) from platform.outbox_message where tenant_id=$1");
            Assert.Equal(baseline[0] + 1, after[0]);
            Assert.Equal(baseline[1] + 1, after[1]);
            Assert.Equal(baseline[2] + 3, after[2]);
            Assert.Equal(baseline[3] + 1, after[3]);
        }
        finally { Gate.Release(); }
    }

    [Fact]
    public async Task RealWorkerPublishesFreshLedgerOutboxAfterBrokerRecovery()
    {
        await Gate.WaitAsync();
        Process? worker = null;
        try
        {
            var connectionString = LedgerTestSupport.RequireConnection("TODIR_LEDGER_WORKER_CONNECTION");
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            var posting = LedgerTestSupport.CreateRepository(dataSource);
            await using (var cleanup = await dataSource.OpenConnectionAsync())
            {
                await LedgerTestSupport.SetTenantAsync(cleanup, LedgerTestSupport.AtlasTenant);
                await ExecuteAsync(cleanup, "delete from platform.outbox_message where tenant_id=$1", LedgerTestSupport.AtlasTenant.Value);
            }
            var posted = await posting.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"WORKER-RECOVERY-{Guid.CreateVersion7():N}"));
            await using var connection = await dataSource.OpenConnectionAsync();
            await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
            var outbox = await ReadOutboxAsync(connection, posted.FactId);
            Assert.NotEqual(Guid.Empty, outbox.MessageId);
            Assert.Equal(LedgerEventTypes.JournalEntryPostedV1, outbox.MessageType);
            Assert.Equal("Pending", outbox.Status);
            Assert.Equal(0, outbox.Attempts);

            worker = StartWorker(connectionString, "5679");
            await Task.Delay(TimeSpan.FromSeconds(7));
            Assert.Equal("Pending", await ReadOutboxStatusAsync(connection, outbox.MessageId));
            await StopWorkerAsync(worker);
            worker = StartWorker(connectionString, Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT") ?? "5672");
            var published = false;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                if (string.Equals(await ReadOutboxStatusAsync(connection, outbox.MessageId), "Published", StringComparison.Ordinal)) { published = true; break; }
            }
            Assert.True(published, "The real Worker did not publish the fresh pending Ledger event after broker recovery.");
            await StopWorkerAsync(worker); worker = null;
            Assert.Equal(1, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and id=$2", LedgerTestSupport.AtlasTenant.Value, outbox.MessageId));
            Assert.Equal(1, await ScalarAsync(connection, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and fact_id=$2", LedgerTestSupport.AtlasTenant.Value, posted.FactId));
            Assert.Equal(3, await ScalarAsync(connection, "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", LedgerTestSupport.AtlasTenant.Value, posted.JournalEntryId));
            Console.WriteLine($"PASS3_LEDGER_WORKER outbox_id={outbox.MessageId:D} event={outbox.MessageType} broker_down=Pending attempts_before={outbox.Attempts} broker_restored=Published duplicate_entries=0 duplicate_outbox_rows=0");
        }
        finally
        {
            if (worker is not null) await StopWorkerAsync(worker);
            Gate.Release();
        }
    }

    [Fact]
    public async Task PostingAndProjectionMeasurementsAreCapturedWithoutChangingTruthRules()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
            var posting = LedgerTestSupport.CreateRepository(dataSource);
            var operations = new LedgerOperationsRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), posting, TimeProvider.System);
            for (var i = 0; i < 2; i++)
                await posting.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"PASS3-PERF-WARMUP-{Guid.CreateVersion7():N}"));
            var samples = new List<double>();
            for (var i = 0; i < 20; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await posting.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"PASS3-PERF-{Guid.CreateVersion7():N}"));
                stopwatch.Stop(); samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            samples.Sort();
            var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await posting.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"PASS3-PERF-CONCURRENT-{Guid.CreateVersion7():N}"));
                    stopwatch.Stop();
                    return (Elapsed: stopwatch.Elapsed.TotalMilliseconds, Error: (Exception?)null);
                }
                catch (Exception exception)
                {
                    stopwatch.Stop();
                    return (Elapsed: stopwatch.Elapsed.TotalMilliseconds, Error: exception);
                }
            }));
            var glWatch = Stopwatch.StartNew(); _ = await operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(2026, 1, 1), new(2026, 12, 31), null, null, 100, default); glWatch.Stop();
            var trialWatch = Stopwatch.StartNew(); _ = await operations.GetTrialBalanceAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(2026, 1, 1), new(2026, 12, 31), 500, default); trialWatch.Stop();
            var reconcileWatch = Stopwatch.StartNew(); var reconciliation = await operations.ReconcileBalancesAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, null, null, default); reconcileWatch.Stop();
            var rebuildWatch = Stopwatch.StartNew(); var rebuilt = await operations.RebuildBalancesAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, null, null, default); rebuildWatch.Stop();
            var concurrentSamples = concurrent.Select(item => item.Elapsed).OrderBy(value => value).ToList();
            Console.WriteLine($"PASS3_LEDGER_PERF dataset=30 sequential invoice postings p50={Percentile(samples, .50):F2}ms p95={Percentile(samples, .95):F2}ms p99={Percentile(samples, .99):F2}ms throughput={1000d / samples.Average():F2}/s concurrency=8 errors={concurrent.Count(item => item.Error is not null)} concurrent_p50={Percentile(concurrentSamples, .50):F2}ms concurrent_p95={Percentile(concurrentSamples, .95):F2}ms concurrent_p99={Percentile(concurrentSamples, .99):F2}ms gl={glWatch.Elapsed.TotalMilliseconds:F2}ms trial_balance={trialWatch.Elapsed.TotalMilliseconds:F2}ms reconciliation={reconcileWatch.Elapsed.TotalMilliseconds:F2}ms rebuild={rebuildWatch.Elapsed.TotalMilliseconds:F2}ms rebuilt_rows={rebuilt.RowsRebuilt} healthy={reconciliation.Healthy && rebuilt.Reconciled}");
            Assert.True(reconciliation.Healthy);
            Assert.All(concurrent, item => Assert.Null(item.Error));
            Assert.True(rebuilt.Reconciled);
        }
        finally { Gate.Release(); }
    }

    [Fact]
    public async Task QueryCountInstrumentationCoversPostingDeterminationAndReports()
    {
        await Gate.WaitAsync();
        try
        {
            await using var dataSource = CreateTracedDataSource(LedgerTestSupport.RequireConnection());
            var posting = LedgerTestSupport.CreateRepository(dataSource);
            var operations = new LedgerOperationsRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), posting, TimeProvider.System);
            var accounts = await AccountIdsAsync(dataSource, 12);
            var journal = await GuidAsync(dataSource, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL'");

            var threeLines = BuildExplicitLines(accounts.Take(3).ToArray(), 3, 100m);
            var tenLines = BuildExplicitLines(accounts.Take(10).ToArray(), 10, 100m);
            var threeCount = await CountDbCommandsAsync(() => posting.PostAsync(ExplicitFact(journal, threeLines, "QUERY-3")));
            var tenCount = await CountDbCommandsAsync(() => posting.PostAsync(ExplicitFact(journal, tenLines, "QUERY-10")));
            var determinationCount = await CountDbCommandsAsync(() => posting.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"QUERY-DETERMINATION-{Guid.CreateVersion7():N}")));
            var trialCount = await CountDbCommandsAsync(() => operations.GetTrialBalanceAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(2026, 1, 1), new(2026, 12, 31), 100, default));
            var glFirstCount = await CountDbCommandsAsync(() => operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(2026, 1, 1), new(2026, 12, 31), null, null, 10, default));
            var glNextCount = await CountDbCommandsAsync(async () =>
            {
                var first = await operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(2026, 1, 1), new(2026, 12, 31), null, null, 10, default);
                if (first.NextCursor is not null) _ = await operations.GetGeneralLedgerAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(2026, 1, 1), new(2026, 12, 31), null, first.NextCursor, 10, default);
            });
            var reconciliationCount = await CountDbCommandsAsync(() => operations.ReconcileBalancesAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, null, null, default));
            Console.WriteLine($"PASS3_LEDGER_QUERY_COUNT posting_3_lines={threeCount} posting_10_lines={tenCount} posting_3_write_adjusted={threeCount - 3} posting_10_write_adjusted={tenCount - 10} account_determination={determinationCount} trial_balance={trialCount} gl_first={glFirstCount} gl_next={glNextCount} reconciliation={reconciliationCount}");
            Assert.True(threeCount > 0 && tenCount > 0 && determinationCount > 0 && trialCount > 0 && glFirstCount > 0 && reconciliationCount > 0);
            Assert.True(tenCount - 10 <= threeCount - 3 + 2, "Posting command growth exceeds the fixed orchestration budget after accounting for line writes.");
        }
        finally { Gate.Release(); }
    }

    private static LedgerSetupRepository CreateSetup(NpgsqlDataSource dataSource) => new(dataSource, new NpgsqlTransactionCoordinator(dataSource), new LedgerConfigurationCache(), TimeProvider.System);
    private static NpgsqlDataSource CreateTracedDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConfigureTracing(options => options.EnablePhysicalOpenTracing(false));
        return builder.Build();
    }

    private static async Task<long> CountDbCommandsAsync(Func<Task> operation)
    {
        long count = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, "Npgsql", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = _ => Interlocked.Increment(ref count)
        };
        ActivitySource.AddActivityListener(listener);
        await operation();
        return count;
    }

    private static async Task<IReadOnlyList<Guid>> AccountIdsAsync(NpgsqlDataSource dataSource, int limit)
    {
        await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using var command = new NpgsqlCommand("select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and status='Active' and allow_direct_posting=true and subtype not in ('Receivable','Payable','Inventory','OutputTax','InputTax','Tax') order by code limit $3", connection); command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); command.Parameters.AddWithValue(limit);
        await using var reader = await command.ExecuteReaderAsync(); var result = new List<Guid>(); while (await reader.ReadAsync()) result.Add(reader.GetGuid(0)); Assert.True(result.Count >= limit); return result;
    }

    private static LedgerPostingLinePayload[] BuildExplicitLines(IReadOnlyList<Guid> accounts, int count, decimal total)
    {
        var debitCount = count / 2; var creditCount = count - debitCount; var debitAmount = total / debitCount; var creditAmount = total / creditCount;
        return accounts.Select((account, index) => index < count / 2
            ? new LedgerPostingLinePayload(account, debitAmount, 0m, "EGP", debitAmount, 0m, debitAmount, 0m, 1m, [], $"debit-{index}", null)
            : new LedgerPostingLinePayload(account, 0m, creditAmount, "EGP", 0m, creditAmount, 0m, creditAmount, 1m, [], $"credit-{index}", null)).ToArray();
    }

    private static AccountingFactEnvelope<IAccountingFactPayload> ExplicitFact(Guid journal, IReadOnlyList<LedgerPostingLinePayload> lines, string key) =>
        new(Guid.CreateVersion7(), LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.ManualGeneralJournalPosted, 1, "ledger", $"query:{key}:{Guid.CreateVersion7():N}", $"query:{key}:{Guid.CreateVersion7():N}", new(2026, 1, 15), new(2026, 1, 15), new ExplicitLedgerPostingPayload(journal, key, "EGP", lines), Guid.CreateVersion7(), Guid.CreateVersion7());
    private static double Percentile(List<double> values, double percentile) => values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile) - 1)];

    private static async Task<IReadOnlyList<long>> CountsAsync(NpgsqlDataSource dataSource, params string[] sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant); var values = new List<long>();
        foreach (var statement in sql)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
            if (statement.Contains("$2", StringComparison.Ordinal)) command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value);
            values.Add(Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }
        return values;
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant); await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); await using var reader = await command.ExecuteReaderAsync(); var result = new List<Guid>(); while (await reader.ReadAsync()) result.Add(reader.GetGuid(0)); return result;
    }

    private static async Task<Guid> GuidAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync(); await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant); await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a Ledger identifier."));
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection); foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter); return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> ScalarByIdAsync(NpgsqlDataSource dataSource, string sql, Guid factId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
        command.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value);
        command.Parameters.AddWithValue(factId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Process StartWorker(string connectionString, string rabbitPort)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("The repository root could not be located for the Worker acceptance process.");
        var worker = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", "run --project backend/Worker/Worker.csproj --no-build --no-restore")
            {
                WorkingDirectory = repository.FullName, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        worker.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        worker.StartInfo.Environment["TODIR_DATABASE_CONNECTION"] = connectionString;
        worker.StartInfo.Environment["TODIR_RABBITMQ_HOST"] = "127.0.0.1";
        worker.StartInfo.Environment["TODIR_RABBITMQ_PORT"] = rabbitPort;
        worker.StartInfo.Environment["TODIR_RABBITMQ_USER"] = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USER") ?? "todir";
        worker.StartInfo.Environment["TODIR_RABBITMQ_PASSWORD"] = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? "todir-dev-only";
        worker.StartInfo.Environment["TODIR_RABBITMQ_VHOST"] = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev";
        worker.Start();
        return worker;
    }

    private static async Task StopWorkerAsync(Process worker)
    {
        if (!worker.HasExited)
        {
            try { worker.Kill(true); } catch (InvalidOperationException) { }
            await worker.WaitForExitAsync();
        }
        worker.Dispose();
    }

    private static async Task<(Guid MessageId, string MessageType, string Status, int Attempts)> ReadOutboxAsync(NpgsqlConnection connection, Guid factId)
    {
        await using var command = new NpgsqlCommand("select id,message_type,status,attempts from platform.outbox_message where tenant_id=$1 and causation_id=$2 order by occurred_at desc limit 1", connection);
        command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(factId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3));
    }

    private static async Task<string> ReadOutboxStatusAsync(NpgsqlConnection connection, Guid messageId)
    {
        await using var command = new NpgsqlCommand("select status from platform.outbox_message where tenant_id=$1 and id=$2", connection);
        command.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); command.Parameters.AddWithValue(messageId);
        return (string)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Outbox message disappeared."));
    }

    private static AccountingFactEnvelope<IAccountingFactPayload> AsGeneric(AccountingFactEnvelope<CustomerInvoicePostedPayload> fact) =>
        new(fact.FactId, fact.TenantId, fact.LegalEntityId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, fact.Payload, fact.CorrelationId, fact.ActorId);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection); foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter); await command.ExecuteNonQueryAsync();
    }
}
