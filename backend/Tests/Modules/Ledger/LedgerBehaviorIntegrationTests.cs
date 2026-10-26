using Modules.Ledger.Entities;
using Modules.Ledger.DTOs;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Core.Transactions;
using Npgsql;
using Shared.Contracts.Ledger;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerBehaviorIntegrationTests
{
    private static readonly string[] RejectedSqlStates = ["55000", "42501"];
    [Fact]
    public async Task AccountsEnforceCodesHierarchyCyclesAndScopeBoundaries()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var root = await repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"9{suffix}", "مجموعة", "Group", AccountType.Asset, "Current", null, false), default);
        var leaf = await repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"8{suffix}", "حساب", "Leaf", AccountType.Asset, "Current", root.Id, true), default);
        await Assert.ThrowsAsync<PostgresException>(() => repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(root.Code, "مكرر", "Duplicate", AccountType.Asset, "Current", null), default));

        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using (var indirect = new NpgsqlCommand("update finance_ledger.account set parent_account_id=$1 where tenant_id=$2 and id=$3", connection))
        {
            indirect.Parameters.AddWithValue(leaf.Id); indirect.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); indirect.Parameters.AddWithValue(root.Id);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => indirect.ExecuteNonQueryAsync());
            Assert.Equal("23514", exception.SqlState);
        }

        var selfId = Guid.CreateVersion7();
        await using (var self = new NpgsqlCommand("insert into finance_ledger.account (tenant_id,legal_entity_id,id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,status,version,created_at,updated_at) values ($1,$2,$3,$4,'ذاتي','Self','Asset','Current','Debit',$3,true,'Active',1,now(),now())", connection))
        {
            self.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); self.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); self.Parameters.AddWithValue(selfId); self.Parameters.AddWithValue($"7{suffix}");
            var exception = await Assert.ThrowsAsync<PostgresException>(() => self.ExecuteNonQueryAsync());
            Assert.Equal("23514", exception.SqlState);
        }

        var deltaTenant = new Shared.Primitives.Identifiers.TenantId(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
        await using (var crossTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            crossTenant.Parameters.AddWithValue(deltaTenant.Value.ToString("D"));
            await crossTenant.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<PostgresException>(() => repository.CreateAccountAsync(deltaTenant, new(Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d")), new($"6{suffix}", "عبر مستأجر", "Cross tenant", AccountType.Asset, "Current", root.Id), default));
        await Assert.ThrowsAsync<PostgresException>(() => repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, new(Guid.Parse("d8eb39bb-b048-7c5d-bacd-e3a923eee21c")), new($"5{suffix}", "عبر كيان", "Cross entity", AccountType.Asset, "Current", root.Id), default));
    }

    [Fact]
    public async Task FiscalPeriodsSupportCustomCalendarsOverlapAndStatusBoundaries()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var year = await repository.CreateFiscalYearAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"FY-{Guid.NewGuid():N}", new(2031, 4, 1), new(2032, 3, 31)), default);
        for (var index = 0; index < 13; index++)
        {
            var start = new DateOnly(2031, 4, 1).AddDays(index * 28);
            await repository.CreatePeriodAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(year.Id, $"P{index + 1:00}", start, start.AddDays(27), index + 1), default);
        }
        var periods = await repository.ListPeriodsAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, default);
        Assert.Contains(periods, period => period.FiscalYearId == year.Id && period.Sequence == 13);
        await Assert.ThrowsAsync<PostgresException>(() => repository.CreatePeriodAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(year.Id, "OVERLAP", new(2031, 4, 10), new(2031, 4, 20), 99), default));

        var seeded = periods.Single(period => period.Code == "01" && period.FiscalYearId != year.Id);
        await repository.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, seeded.Id, PeriodStatus.SoftClosed, default);
        await Assert.ThrowsAsync<LedgerDomainException>(() => LedgerTestSupport.CreateRepository(dataSource).PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"SOFT-{Guid.NewGuid():N}", new(2026, 1, 15)), default));
        await repository.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, seeded.Id, PeriodStatus.Closed, default);
        await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"CLOSED-{Guid.NewGuid():N}", new(2026, 1, 15)), default));
        await repository.SetPeriodStatusAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, seeded.Id, PeriodStatus.Open, default);
    }

    [Fact]
    public async Task ExplicitBackdatedAndFuturePostingDatesResolveTheirPeriods()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var backdated = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"BACKDATED-{Guid.NewGuid():N}", new(2026, 1, 1)));
        var future = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"FUTURE-{Guid.NewGuid():N}", new(2026, 12, 31)));
        Assert.False(backdated.AlreadyPosted);
        Assert.False(future.AlreadyPosted);
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using var periods = new NpgsqlCommand("select p.start_date,p.end_date from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id where e.tenant_id=$1 and e.id = any($2)", connection);
        periods.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); periods.Parameters.AddWithValue(new[] { backdated.JournalEntryId, future.JournalEntryId });
        await using var reader = await periods.ExecuteReaderAsync(); var resolved = 0; while (await reader.ReadAsync()) { Assert.True(reader.GetDateTime(0) <= reader.GetDateTime(1)); resolved++; } Assert.Equal(2, resolved);
    }

    [Fact]
    public async Task TypedFactCatalogAndMissingRuleAreExplicit()
    {
        Assert.Contains(AccountingFactTypes.CustomerInvoicePosted, AccountingFactTypes.Known);
        Assert.Contains(AccountingFactTypes.CustomerCreditNotePosted, AccountingFactTypes.Known);
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var invoice = LedgerTestSupport.Invoice($"KNOWN-{Guid.NewGuid():N}");
        var accepted = await repository.PostAsync(new AccountingFactEnvelope<IAccountingFactPayload>(invoice.FactId, invoice.TenantId, invoice.LegalEntityId, invoice.FactType, invoice.FactVersion, invoice.SourceModule, invoice.SourceReference, invoice.IdempotencyKey, invoice.BusinessDate, invoice.PostingDate, invoice.Payload, invoice.CorrelationId, invoice.ActorId));
        Assert.False(accepted.AlreadyPosted);
        var unknown = invoice with { FactType = "UncataloguedFact", SourceReference = $"UNKNOWN-{Guid.NewGuid():N}", IdempotencyKey = $"UNKNOWN-{Guid.NewGuid():N}" };
        var unsupported = await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostAsync(new AccountingFactEnvelope<IAccountingFactPayload>(unknown.FactId, unknown.TenantId, unknown.LegalEntityId, unknown.FactType, unknown.FactVersion, unknown.SourceModule, unknown.SourceReference, unknown.IdempotencyKey, unknown.BusinessDate, unknown.PostingDate, unknown.Payload, unknown.CorrelationId, unknown.ActorId)));
        Assert.Equal("ledger.fact_unsupported", unsupported.Code);
        var versioned = invoice with { FactVersion = 2, SourceReference = $"VERSION-2-{Guid.NewGuid():N}", IdempotencyKey = $"VERSION-2-{Guid.NewGuid():N}" };
        var versionException = await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostAsync(new AccountingFactEnvelope<IAccountingFactPayload>(versioned.FactId, versioned.TenantId, versioned.LegalEntityId, versioned.FactType, versioned.FactVersion, versioned.SourceModule, versioned.SourceReference, versioned.IdempotencyKey, versioned.BusinessDate, versioned.PostingDate, versioned.Payload, versioned.CorrelationId, versioned.ActorId)));
        Assert.Equal("ledger.fact_unsupported", versionException.Code);
        var missingRule = invoice with { FactType = AccountingFactTypes.CustomerCreditNotePosted, SourceReference = $"CREDIT-{Guid.NewGuid():N}", IdempotencyKey = $"CREDIT-{Guid.NewGuid():N}" };
        var ruleException = await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostAsync(new AccountingFactEnvelope<IAccountingFactPayload>(missingRule.FactId, missingRule.TenantId, missingRule.LegalEntityId, missingRule.FactType, 1, missingRule.SourceModule, missingRule.SourceReference, missingRule.IdempotencyKey, missingRule.BusinessDate, missingRule.PostingDate, missingRule.Payload, missingRule.CorrelationId, missingRule.ActorId)));
        Assert.Equal("ledger.posting_rule_missing", ruleException.Code);
    }

    [Fact]
    public async Task JournalsAreEntityOwnedUniqueAndNotNumberedFromMaxEntry()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var code = $"TEST-{Guid.NewGuid():N}";
        var journal = await repository.CreateJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(code, "اختبار", "Test", JournalType.General), default);
        Assert.Equal(LedgerTestSupport.AtlasEntity.Value, journal.LegalEntityId);
        await Assert.ThrowsAsync<PostgresException>(() => repository.CreateJournalAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(code, "مكرر", "Duplicate", JournalType.General), default));
        var entry = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"NUMBER-{Guid.NewGuid():N}"));
        Assert.StartsWith("JE-", entry.EntryNumber, StringComparison.Ordinal);
        Assert.Contains(entry.FactId.ToString("N"), entry.EntryNumber, StringComparison.Ordinal);
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using (var inactive = new NpgsqlCommand("update finance_ledger.journal set status='Archived' where tenant_id=$1 and legal_entity_id=$2 and code='SALES'", connection))
        {
            inactive.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); inactive.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); await inactive.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"INACTIVE-JOURNAL-{Guid.NewGuid():N}")));
        await using (var restore = new NpgsqlCommand("update finance_ledger.journal set status='Active' where tenant_id=$1 and legal_entity_id=$2 and code='SALES'", connection))
        {
            restore.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); restore.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); await restore.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CrossEntityJournalReferenceFailsCompositeForeignKey()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction)) { context.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value.ToString("D")); await context.ExecuteNonQueryAsync(); }
        var factId = Guid.CreateVersion7();
        await using (var fact = new NpgsqlCommand("insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,received_at) values ($1,$2,$3,'ManualGeneralJournalPosted',1,'test',$4,$5,'2026-01-01','2026-01-01','{}','test','Received',$6,$7)", connection, transaction))
        {
            fact.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); fact.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); fact.Parameters.AddWithValue(factId); fact.Parameters.AddWithValue($"cross-{factId:N}"); fact.Parameters.AddWithValue($"cross-{factId:N}"); fact.Parameters.AddWithValue(Guid.CreateVersion7()); fact.Parameters.AddWithValue(DateTimeOffset.UtcNow); await fact.ExecuteNonQueryAsync();
        }
        var secondary = new Guid("d8eb39bb-b048-7c5d-bacd-e3a923eee21c");
        await using var invalid = new NpgsqlCommand("insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,posted_at) values ($1,$2,$3,(select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$4 and code='SALES'),$5,'test','cross',$6,'2026-01-01',(select id from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and code='01'), 'ManualGeneralJournalPosted',1,'EGP','EGP',(select v.id from finance_ledger.posting_rule_version v where v.tenant_id=$1 and v.legal_entity_id=$2 limit 1),(select c.id from finance_ledger.accounting_configuration_version c where c.tenant_id=$1 and c.legal_entity_id=$2 limit 1),'cross',$7,now())", connection, transaction);
        invalid.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); invalid.Parameters.AddWithValue(secondary); invalid.Parameters.AddWithValue(Guid.CreateVersion7()); invalid.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); invalid.Parameters.AddWithValue(factId); invalid.Parameters.AddWithValue($"cross-{factId:N}"); invalid.Parameters.AddWithValue(Guid.CreateVersion7());
        var exception = await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync());
        Assert.Equal("23503", exception.SqlState);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task FingerprintIsCanonicalAndAtomicityLeavesNoResidue()
    {
        var first = LedgerTestSupport.Invoice("HASH-A").Payload;
        var second = first with { Taxes = [new InvoiceTaxComponent("VAT14", 14m, 14m, "OutputTax")] };
        Assert.Equal(AccountingFactFingerprint.Compute(first), AccountingFactFingerprint.Compute(second));
        Assert.NotEqual(AccountingFactFingerprint.Compute(first), AccountingFactFingerprint.Compute(first with { NetAmount = 101m, GrossAmount = 115m }));

        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var key = $"ATOMIC-{Guid.NewGuid():N}";
        var fact = LedgerTestSupport.Invoice(key, taxes: [new("VAT14", 14m, -1m, "OutputTax"), new("VAT14", 14m, 15m, "OutputTax")]);
        await Assert.ThrowsAsync<PostgresException>(() => repository.PostCustomerInvoiceAsync(fact));
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        Assert.Equal(0, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and source_reference=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("reference", fact.SourceReference)));
        Assert.Equal(0, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and source_reference=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("reference", fact.SourceReference)));
        Assert.Equal(0, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='JournalEntryPostedV1' and payload->>'sourceReference'=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("reference", fact.SourceReference)));
    }

    [Fact]
    public async Task OneCentImbalanceIsRejectedBeforeAnyPostingRowsAreCommitted()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var fact = LedgerTestSupport.Invoice($"UNBALANCED-{Guid.NewGuid():N}", taxes: [new("VAT14", 14m, 13.99m, "OutputTax")]);
        var exception = await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostCustomerInvoiceAsync(fact));
        Assert.Equal("ledger.entry_unbalanced", exception.Code);
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        Assert.Equal(0, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and source_reference=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("reference", fact.SourceReference)));
        Assert.Equal(0, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and source_reference=$2", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("reference", fact.SourceReference)));
    }

    [Fact]
    public async Task MappingSpecificityWinsAndSameRankConflictsAreDeterministic()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var suffix = Guid.NewGuid().ToString("N");
        var specificAccount = await repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"61{suffix[..8]}", "محدد", "Specific", AccountType.Revenue, "Sales", null), default);
        var moreSpecificAccount = await repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"62{suffix[..8]}", "أكثر تحديداً", "More specific", AccountType.Revenue, "Sales", null), default);
        var conflictAccount = await repository.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"63{suffix[..8]}", "متعارض", "Conflict", AccountType.Revenue, "Sales", null), default);
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        var roleId = await ScalarGuid(connection, "select id from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2 and code='SalesRevenue'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value);
        var mappingIds = new List<Guid>();
        try
        {
            var first = await AddCandidateAsync(connection, roleId, specificAccount.Id, 1, $"specific-{suffix}", $"GENERAL-{suffix}-1"); mappingIds.Add(first.MappingId);
            var specificResult = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"SPECIFIC-{suffix}"));
            Assert.Contains(specificResult.Determinations!, item => item.AccountRole == "SalesRevenue" && item.SelectedAccountId == specificAccount.Id && item.SpecificityRank == 1);
            var second = await AddCandidateAsync(connection, roleId, moreSpecificAccount.Id, 2, $"more-specific-{suffix}", $"GENERAL-{suffix}-2"); mappingIds.Add(second.MappingId);
            var moreSpecificResult = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"MORE-SPECIFIC-{suffix}"));
            Assert.Contains(moreSpecificResult.Determinations!, item => item.AccountRole == "SalesRevenue" && item.SelectedAccountId == moreSpecificAccount.Id && item.SpecificityRank == 2);
            var conflictA = await AddCandidateAsync(connection, roleId, specificAccount.Id, 3, $"conflict-a-{suffix}", $"GENERAL-{suffix}-3"); mappingIds.Add(conflictA.MappingId);
            var conflictB = await AddCandidateAsync(connection, roleId, conflictAccount.Id, 3, $"conflict-b-{suffix}", $"GENERAL-{suffix}-4"); mappingIds.Add(conflictB.MappingId);
            var conflict = await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"CONFLICT-{suffix}")));
            Assert.Equal("ledger.account_mapping_conflict", conflict.Code);
        }
        finally
        {
            await using var cleanupSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection("TODIR_P04_ADMIN_CONNECTION"));
            await using var cleanup = await cleanupSource.OpenConnectionAsync();
            await using var archiveMappings = new NpgsqlCommand("update finance_ledger.account_mapping set status='Archived' where tenant_id=$1 and id = any($2)", cleanup); archiveMappings.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); archiveMappings.Parameters.AddWithValue(mappingIds.ToArray()); await archiveMappings.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task InvalidLineShapesAndArchivedAccountsAreRejectedWithoutChangingHistory()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var result = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"IMM-{Guid.NewGuid():N}"));
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        var accountId = await ScalarGuid(connection, "select account_id from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 order by sequence_no limit 1", LedgerTestSupport.AtlasTenant.Value, result.JournalEntryId);
        foreach (var values in new[] { (-1m, 0m), (1m, 1m), (0m, 0m) })
        {
            await using var line = new NpgsqlCommand("insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role) values ($1,$2,$3,$4,$5,$6,$7,$8,'EGP',1,'Invalid')", connection);
            line.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); line.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); line.Parameters.AddWithValue(Guid.CreateVersion7()); line.Parameters.AddWithValue(result.JournalEntryId); line.Parameters.AddWithValue(100 + values.GetHashCode() & int.MaxValue); line.Parameters.AddWithValue(accountId); line.Parameters.AddWithValue(values.Item1); line.Parameters.AddWithValue(values.Item2);
            await Assert.ThrowsAsync<PostgresException>(() => line.ExecuteNonQueryAsync());
        }
        await using (var archive = new NpgsqlCommand("update finance_ledger.account set status='Archived' where tenant_id=$1 and id=$2", connection))
        {
            archive.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); archive.Parameters.AddWithValue(accountId); await archive.ExecuteNonQueryAsync();
        }
        Assert.Equal(1, await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 and account_id=$3", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("entry", result.JournalEntryId), ("account", accountId)));
        await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"ARCHIVED-{Guid.NewGuid():N}")));
        await using (var restore = new NpgsqlCommand("update finance_ledger.account set status='Active' where tenant_id=$1 and id=$2", connection)) { restore.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); restore.Parameters.AddWithValue(accountId); await restore.ExecuteNonQueryAsync(); }
        await using (var nonPostable = new NpgsqlCommand("update finance_ledger.account set allow_direct_posting=false where tenant_id=$1 and id=$2", connection)) { nonPostable.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); nonPostable.Parameters.AddWithValue(accountId); await nonPostable.ExecuteNonQueryAsync(); }
        await Assert.ThrowsAsync<LedgerDomainException>(() => repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"NONPOSTABLE-{Guid.NewGuid():N}")));
        await using (var restorePosting = new NpgsqlCommand("update finance_ledger.account set allow_direct_posting=true where tenant_id=$1 and id=$2", connection)) { restorePosting.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); restorePosting.Parameters.AddWithValue(accountId); await restorePosting.ExecuteNonQueryAsync(); }
        var lineId = await ScalarGuid(connection, "select id from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 order by sequence_no limit 1", LedgerTestSupport.AtlasTenant.Value, result.JournalEntryId);
        await using var deleteLine = new NpgsqlCommand("delete from finance_ledger.journal_entry_line where tenant_id=$1 and id=$2", connection); deleteLine.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); deleteLine.Parameters.AddWithValue(lineId); var lineDeleteException = await Assert.ThrowsAsync<PostgresException>(() => deleteLine.ExecuteNonQueryAsync()); Assert.Contains(lineDeleteException.SqlState, RejectedSqlStates);
        await using var deleteEntry = new NpgsqlCommand("delete from finance_ledger.journal_entry where tenant_id=$1 and id=$2", connection); deleteEntry.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); deleteEntry.Parameters.AddWithValue(result.JournalEntryId); var deleteException = await Assert.ThrowsAsync<PostgresException>(() => deleteEntry.ExecuteNonQueryAsync()); Assert.Contains(deleteException.SqlState, RejectedSqlStates);
    }

    [Fact]
    public async Task UsedRuleAndConfigurationVersionsCannotBeMutatedInPlace()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var repository = LedgerTestSupport.CreateRepository(dataSource);
        var result = await repository.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"RULE-IMM-{Guid.NewGuid():N}"));
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        await using var refs = new NpgsqlCommand("select posting_rule_version_id,accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and id=$2", connection); refs.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); refs.Parameters.AddWithValue(result.JournalEntryId);
        await using var reader = await refs.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); var ruleVersion = reader.GetGuid(0); var configuration = reader.GetGuid(1); await reader.DisposeAsync();
        await using var mutateRule = new NpgsqlCommand("update finance_ledger.posting_rule_version set status='Retired' where tenant_id=$1 and id=$2", connection); mutateRule.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); mutateRule.Parameters.AddWithValue(ruleVersion); var ruleException = await Assert.ThrowsAsync<PostgresException>(() => mutateRule.ExecuteNonQueryAsync()); Assert.Equal("55000", ruleException.SqlState);
        await using var mutateConfiguration = new NpgsqlCommand("update finance_ledger.accounting_configuration_version set snapshot_hash='tampered' where tenant_id=$1 and id=$2", connection); mutateConfiguration.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); mutateConfiguration.Parameters.AddWithValue(configuration); var configurationException = await Assert.ThrowsAsync<PostgresException>(() => mutateConfiguration.ExecuteNonQueryAsync()); Assert.Equal("55000", configurationException.SqlState);
    }

    [Fact]
    public async Task MappingConfigurationActivationKeepsPostingAndSettlementBoundToTheirHistoricalContext()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        var ledger = LedgerTestSupport.CreateRepository(dataSource);
        var lifecycle = new LedgerMappingConfigurationRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), new LedgerConfigurationCache(), TimeProvider.System);
        var initial = await ledger.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"CONFIG-A-{Guid.NewGuid():N}"));

        await using var adminDataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection("TODIR_P04_ADMIN_CONNECTION"));
        await using var connection = await adminDataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        var configurationA = await ScalarGuid(connection, "select accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and id=$2", LedgerTestSupport.AtlasTenant.Value, initial.JournalEntryId);
        var receivableA = await ScalarGuid(connection, "select v.target_account_id from finance_ledger.account_mapping_version v join finance_ledger.account_mapping m on m.tenant_id=v.tenant_id and m.legal_entity_id=v.legal_entity_id and m.id=v.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id where v.tenant_id=$1 and v.legal_entity_id=$2 and v.accounting_configuration_version_id=$3 and r.code='Receivable' and v.status='Active'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value, configurationA);
        var payableA = await ScalarGuid(connection, "select v.target_account_id from finance_ledger.account_mapping_version v join finance_ledger.account_mapping m on m.tenant_id=v.tenant_id and m.legal_entity_id=v.legal_entity_id and m.id=v.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id where v.tenant_id=$1 and v.legal_entity_id=$2 and v.accounting_configuration_version_id=$3 and r.code='Payable' and v.status='Active'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value, configurationA);
        var receivableB = (await ledger.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"8{Guid.NewGuid():N}"[..13], "عملاء بديل", "Alternate receivables", AccountType.Asset, "ReceivableControl", null, true), default)).Id;
        var payableB = (await ledger.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"7{Guid.NewGuid():N}"[..13], "موردون بديل", "Alternate payables", AccountType.Liability, "PayableControl", null, true), default)).Id;

        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow);
        var incompleteDraft = await lifecycle.CreateDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(effectiveFrom), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var missingMapping = incompleteDraft.Mappings.Single(mapping => mapping.RoleCode == "SalesRevenue");
        await using (var removeDraftMapping = new NpgsqlCommand("delete from finance_ledger.account_mapping_version where tenant_id=$1 and legal_entity_id=$2 and accounting_configuration_version_id=$3 and id=$4 and status='Draft'", connection))
        {
            removeDraftMapping.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); removeDraftMapping.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); removeDraftMapping.Parameters.AddWithValue(incompleteDraft.Id); removeDraftMapping.Parameters.AddWithValue(missingMapping.MappingVersionId);
            Assert.Equal(1, await removeDraftMapping.ExecuteNonQueryAsync());
        }
        var incompleteValidation = await lifecycle.ValidateAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, incompleteDraft.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        Assert.Contains(incompleteValidation.Errors, error => error.Code == "ledger.account_mapping_missing");

        var conflictDraft = await lifecycle.CreateDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(effectiveFrom), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var conflictRoleId = await ScalarGuid(connection, "select id from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2 and code='Receivable'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value);
        var conflictMappingId = Guid.CreateVersion7();
        await using (var addConflictMapping = new NpgsqlCommand("insert into finance_ledger.account_mapping (tenant_id,legal_entity_id,id,role_id,code,status,version) values ($1,$2,$3,$4,$5,'Active',1)", connection))
        {
            addConflictMapping.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); addConflictMapping.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); addConflictMapping.Parameters.AddWithValue(conflictMappingId); addConflictMapping.Parameters.AddWithValue(conflictRoleId); addConflictMapping.Parameters.AddWithValue($"LIFECYCLE-CONFLICT-{Guid.NewGuid():N}"); await addConflictMapping.ExecuteNonQueryAsync();
        }
        await using (var addConflictVersion = new NpgsqlCommand("insert into finance_ledger.account_mapping_version (tenant_id,legal_entity_id,id,mapping_id,accounting_configuration_version_id,version_number,effective_from,specificity_rank,target_account_id,status) values ($1,$2,$3,$4,$5,1,$6,0,$7,'Draft')", connection))
        {
            addConflictVersion.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); addConflictVersion.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); addConflictVersion.Parameters.AddWithValue(Guid.CreateVersion7()); addConflictVersion.Parameters.AddWithValue(conflictMappingId); addConflictVersion.Parameters.AddWithValue(conflictDraft.Id); addConflictVersion.Parameters.AddWithValue(effectiveFrom); addConflictVersion.Parameters.AddWithValue(receivableB); await addConflictVersion.ExecuteNonQueryAsync();
        }
        var conflictValidation = await lifecycle.ValidateAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, conflictDraft.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        Assert.Contains(conflictValidation.Errors, error => error.Code == "ledger.account_mapping_conflict");

        var nonPostable = (await ledger.CreateAccountAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new($"6{Guid.NewGuid():N}"[..13], "حساب غير قابل للترحيل", "Non-postable", AccountType.Asset, "Other", null, false), default)).Id;
        var draft = await lifecycle.CreateDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(effectiveFrom), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var staleDraft = await lifecycle.CreateDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(effectiveFrom), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var receivableMapping = draft.Mappings.Single(mapping => mapping.RoleCode == "Receivable");
        var payableMapping = draft.Mappings.Single(mapping => mapping.RoleCode == "Payable");
        await lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, draft.Id, receivableMapping.MappingVersionId, new(receivableB), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        await lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, draft.Id, payableMapping.MappingVersionId, new(payableB), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var staleReceivable = staleDraft.Mappings.Single(mapping => mapping.RoleCode == "Receivable");
        var stalePayable = staleDraft.Mappings.Single(mapping => mapping.RoleCode == "Payable");
        var invalidTarget = await Assert.ThrowsAsync<LedgerDomainException>(() => lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, staleDraft.Id, staleReceivable.MappingVersionId, new(nonPostable), Guid.CreateVersion7(), Guid.CreateVersion7(), default));
        Assert.Equal("ledger.mapping_target_invalid", invalidTarget.Code);
        await lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, staleDraft.Id, staleReceivable.MappingVersionId, new(receivableB), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        await lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, staleDraft.Id, stalePayable.MappingVersionId, new(payableB), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        var draftValidation = await lifecycle.ValidateAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, draft.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        Assert.True(draftValidation.Valid, System.Text.Json.JsonSerializer.Serialize(draftValidation.Errors));
        async Task<(Guid? Activated, string? Error)> TryActivateAsync(Guid configurationId)
        {
            try { await lifecycle.ActivateAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, configurationId, Guid.CreateVersion7(), Guid.CreateVersion7(), default); return (configurationId, null); }
            catch (LedgerDomainException error) { return (null, error.Code); }
        }
        var concurrent = await Task.WhenAll(TryActivateAsync(draft.Id), TryActivateAsync(staleDraft.Id));
        var winningActivation = Assert.Single(concurrent, result => result.Activated.HasValue);
        var losingActivation = Assert.Single(concurrent, result => result.Error is not null);
        var configurationB = winningActivation.Activated!.Value;
        Assert.Equal("ledger.mapping_configuration_stale", losingActivation.Error);

        var next = await ledger.PostCustomerInvoiceAsync(LedgerTestSupport.Invoice($"CONFIG-B-{Guid.NewGuid():N}"));
        var nextConfig = await ScalarGuid(connection, "select accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and id=$2", LedgerTestSupport.AtlasTenant.Value, next.JournalEntryId);
        var oldArLine = await ScalarGuid(connection, "select account_id from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 and sequence_no=1", LedgerTestSupport.AtlasTenant.Value, initial.JournalEntryId);
        var newArLine = await ScalarGuid(connection, "select account_id from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 and sequence_no=1", LedgerTestSupport.AtlasTenant.Value, next.JournalEntryId);
        Assert.Equal(configurationB, nextConfig);
        Assert.Equal(receivableA, oldArLine);
        Assert.Equal(receivableB, newArLine);

        var writeOffId = Guid.CreateVersion7();
        var writeOff = new SettlementWriteOffPayload(writeOffId, Guid.CreateVersion7(), Guid.CreateVersion7(), "Payable", "1", "EGP", 1m, "EGP", "acceptance", "Historical payable control proof", configurationA);
        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(writeOffId, LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, AccountingFactTypes.SettlementWriteOff, 1, "subledger", $"historical-writeoff:{writeOffId:N}", $"historical-writeoff:{writeOffId:N}", effectiveFrom, effectiveFrom, writeOff, Guid.CreateVersion7(), Guid.CreateVersion7());
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var historicalEffect = await coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, (context, ct) => ledger.PostSettlementWriteOffInTransactionAsync(context, fact, writeOff, ct), default);
        var effectConfig = await ScalarGuid(connection, "select accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and id=$2", LedgerTestSupport.AtlasTenant.Value, historicalEffect.JournalEntryId);
        var historicalPayableLine = await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 and account_id=$3", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("entry", historicalEffect.JournalEntryId), ("account", payableA));
        Assert.Equal(configurationA, effectConfig);
        Assert.Equal(1, historicalPayableLine);
        var currentWriteOffId = Guid.CreateVersion7();
        var currentWriteOff = writeOff with { WriteOffId = currentWriteOffId, OpenItemId = Guid.CreateVersion7(), HistoricalConfigurationVersionId = null };
        var currentFact = fact with { FactId = currentWriteOffId, SourceReference = $"current-writeoff:{currentWriteOffId:N}", IdempotencyKey = $"current-writeoff:{currentWriteOffId:N}", Payload = currentWriteOff };
        var currentEffect = await coordinator.ExecuteAsync(LedgerTestSupport.AtlasTenant, (context, ct) => ledger.PostSettlementWriteOffInTransactionAsync(context, currentFact, currentWriteOff, ct), default);
        var currentEffectConfig = await ScalarGuid(connection, "select accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and id=$2", LedgerTestSupport.AtlasTenant.Value, currentEffect.JournalEntryId);
        var currentPayableLine = await LedgerTestSupport.ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2 and account_id=$3", ("tenant", LedgerTestSupport.AtlasTenant.Value), ("entry", currentEffect.JournalEntryId), ("account", payableB));
        Assert.Equal(configurationB, currentEffectConfig);
        Assert.Equal(1, currentPayableLine);
        Assert.Equal(receivableA, await ScalarGuid(connection, "select v.target_account_id from finance_ledger.account_mapping_version v join finance_ledger.account_mapping m on m.tenant_id=v.tenant_id and m.legal_entity_id=v.legal_entity_id and m.id=v.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id where v.tenant_id=$1 and v.legal_entity_id=$2 and v.accounting_configuration_version_id=$3 and r.code='Receivable' and v.status='Active'", LedgerTestSupport.AtlasTenant.Value, LedgerTestSupport.AtlasEntity.Value, configurationA));

        var restoreDraft = await lifecycle.CreateDraftAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, new(effectiveFrom), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        await lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, restoreDraft.Id, restoreDraft.Mappings.Single(mapping => mapping.RoleCode == "Receivable").MappingVersionId, new(receivableA), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        await lifecycle.UpdateDraftMappingAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, restoreDraft.Id, restoreDraft.Mappings.Single(mapping => mapping.RoleCode == "Payable").MappingVersionId, new(payableA), Guid.CreateVersion7(), Guid.CreateVersion7(), default);
        Assert.True((await lifecycle.ValidateAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, restoreDraft.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default)).Valid);
        await lifecycle.ActivateAsync(LedgerTestSupport.AtlasTenant, LedgerTestSupport.AtlasEntity, restoreDraft.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), default);
    }

    private static async Task<Guid> ScalarGuid(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<(Guid MappingId, Guid VersionId)> AddCandidateAsync(NpgsqlConnection connection, Guid roleId, Guid accountId, int rank, string code, string versionKey)
    {
        var mappingId = Guid.CreateVersion7(); var versionId = Guid.CreateVersion7();
        await using (var mapping = new NpgsqlCommand("insert into finance_ledger.account_mapping (tenant_id,legal_entity_id,id,role_id,code,status,version) values ($1,$2,$3,$4,$5,'Active',1)", connection))
        {
            mapping.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); mapping.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); mapping.Parameters.AddWithValue(mappingId); mapping.Parameters.AddWithValue(roleId); mapping.Parameters.AddWithValue(code); await mapping.ExecuteNonQueryAsync();
        }
        await using (var version = new NpgsqlCommand("insert into finance_ledger.account_mapping_version (tenant_id,legal_entity_id,id,mapping_id,accounting_configuration_version_id,version_number,effective_from,product_category_code,specificity_rank,target_account_id,status) values ($1,$2,$3,$4,(select active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2),1,'2026-01-01',$5,$6,$7,'Active')", connection))
        {
            version.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); version.Parameters.AddWithValue(LedgerTestSupport.AtlasEntity.Value); version.Parameters.AddWithValue(versionId); version.Parameters.AddWithValue(mappingId); version.Parameters.AddWithValue("GENERAL"); version.Parameters.AddWithValue(rank); version.Parameters.AddWithValue(accountId); await version.ExecuteNonQueryAsync();
        }
        return (mappingId, versionId);
    }
}
