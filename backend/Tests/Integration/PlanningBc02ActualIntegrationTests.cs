using Modules.Ledger.Repositories;
using Modules.Planning;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningBc02ActualIntegrationTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));

    [Fact(DisplayName = "BC2-T01 authoritative Ledger actual refresh is immutable and idempotent")]
    public async Task AuthoritativeLedgerActualRefreshIsImmutableAndIdempotent()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        var ledgerSource = new LedgerOwnerReconciliationSource(dataSource);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var planVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        await EnsureCanonicalExpenseFactAsync(admin, actorId);
        var sourceFacts = await ledgerSource.ListFinancialFactsAsync(AtlasTenant, null);
        Assert.NotEmpty(sourceFacts);

        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [ledgerSource]);
        var first = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", "caller-watermark-is-not-authoritative", null, planVersionId), default);
        var replay = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", "a-different-caller-watermark", null, planVersionId), default);

        Assert.Equal(first, replay);
        var run = await repository.GetActualRefreshRunAsync(AtlasTenant, first, default);
        Assert.Equal("Succeeded", run.State);

        var snapshotId = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, run.Id))!;
        Assert.Equal(sourceFacts.Count, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2", AtlasTenant.Value, snapshotId), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(sourceFacts.Sum(f => f.Amount), await ScalarAsync(admin, "select total_amount from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, snapshotId));
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.actual_refresh_run where tenant_id=$1 and id=$2 and state='Succeeded'", AtlasTenant.Value, first), System.Globalization.CultureInfo.InvariantCulture));

        var suppliedFact = new ActualFactRequest(planVersionId, sourceFacts[0].LegalEntityId, sourceFacts[0].PeriodStart, sourceFacts[0].MetricCode, System.Text.Json.JsonDocument.Parse("{}").RootElement, 1m, sourceFacts[0].CurrencyCode, "caller", "caller-fact");
        var rejected = await Assert.ThrowsAsync<PlanningException>(() => repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", "caller", [suppliedFact], planVersionId), default));
        Assert.Equal("planning.actual_refresh_source_contract_required", rejected.Code);
    }

    [Fact(DisplayName = "BC2-T03 failed refresh preserves the published snapshot and correction restates once")]
    public async Task FailedRefreshPreservesPublishedSnapshotAndCorrectionRestatesOnce()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var planVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var source = new SwitchingActualSource(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.CreateVersion7().ToString("N"));
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [source]);

        var firstRun = await repository.RefreshActualsAsync(new(AtlasTenant.Value), actorId, new("seed-v1", "restatement-initial", null, planVersionId), default);
        var firstSnapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, firstRun))!;
        var firstTotal = await ScalarAsync(admin, "select total_amount from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, firstSnapshot);

        source.Fail = true;
        await Assert.ThrowsAsync<PlanningException>(() => repository.RefreshActualsAsync(new(AtlasTenant.Value), actorId, new("seed-v1", "restatement-failure", null, planVersionId), default));
        Assert.Equal("Published", await ScalarAsync(admin, "select state from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, firstSnapshot));
        Assert.True(Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.actual_refresh_run where tenant_id=$1 and plan_version_id=$2 and state='Failed' and source_watermark is null", AtlasTenant.Value, planVersionId), System.Globalization.CultureInfo.InvariantCulture) > 0);

        source.Fail = false;
        source.Amount = 140m;
        var correctedRun = await repository.RefreshActualsAsync(new(AtlasTenant.Value), actorId, new("seed-v1", "restatement-correction", null, planVersionId), default);
        var correctedSnapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, correctedRun))!;
        Assert.Equal("Superseded", await ScalarAsync(admin, "select state from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, firstSnapshot));
        Assert.Equal(140m, await ScalarAsync(admin, "select total_amount from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, correctedSnapshot));
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2", AtlasTenant.Value, correctedSnapshot), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(100m, Assert.IsType<decimal>(firstTotal));
    }

    [Fact(DisplayName = "BC2-T02 duplicate operational and Ledger identities are recognized once")]
    public async Task DuplicateOperationalAndLedgerIdentityIsRecognizedOnce()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var planVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var source = new DuplicateActualSource(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.CreateVersion7().ToString("N"));
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [source]);

        var run = await repository.RefreshActualsAsync(new(AtlasTenant.Value), actorId, new("seed-v1", "duplicate-recognition", null, planVersionId), default);
        var snapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, run))!;
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from business_planning.planning_actual_snapshot_fact where tenant_id=$1 and snapshot_id=$2", AtlasTenant.Value, snapshot), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(100m, await ScalarAsync(admin, "select total_amount from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, snapshot));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is required.");

    private static async Task EnsureCanonicalExpenseFactAsync(NpgsqlConnection connection, Guid actorId)
    {
        var suffix = Guid.NewGuid().ToString("N");
        Guid journalId, periodId, ruleId, configurationId, debitAccount, creditAccount;
        string currency, baseCurrency;
        await using (var basis = new NpgsqlCommand("select e.journal_id,e.accounting_period_id,e.currency_code,e.base_currency_code,e.posting_rule_version_id,e.accounting_configuration_version_id,(select l.account_id from finance_ledger.journal_entry_line l where l.tenant_id=e.tenant_id and l.journal_entry_id=e.id order by l.sequence_no limit 1),(select l.account_id from finance_ledger.journal_entry_line l where l.tenant_id=e.tenant_id and l.journal_entry_id=e.id order by l.sequence_no desc limit 1) from finance_ledger.journal_entry e where e.tenant_id=$1 and e.legal_entity_id=$2 order by e.posting_date,e.id limit 1", connection))
        {
            basis.Parameters.AddWithValue(AtlasTenant.Value); basis.Parameters.AddWithValue(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"));
            await using var reader = await basis.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
            journalId = reader.GetGuid(0); periodId = reader.GetGuid(1); currency = reader.GetString(2); baseCurrency = reader.GetString(3); ruleId = reader.GetGuid(4); configurationId = reader.GetGuid(5); debitAccount = reader.GetGuid(6); creditAccount = reader.GetGuid(7);
        }
        var factId = Guid.CreateVersion7();
        var entryId = Guid.CreateVersion7();
        await using (var fact = new NpgsqlCommand("insert into finance_ledger.accounting_fact(tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at,posted_at) values($1,$2,$3,'ExpenseRecognized',1,'expenses',$4,$5,'2026-09-01','2026-09-01','{}'::jsonb,$6,'Posted',$7,$8,now(),now())", connection))
        {
            fact.Parameters.AddWithValue(AtlasTenant.Value); fact.Parameters.AddWithValue(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528")); fact.Parameters.AddWithValue(factId); fact.Parameters.AddWithValue($"bc02-expense-{suffix}"); fact.Parameters.AddWithValue($"bc02-idem-{suffix}"); fact.Parameters.AddWithValue("bc02-fixture"); fact.Parameters.AddWithValue(Guid.CreateVersion7()); fact.Parameters.AddWithValue(actorId); await fact.ExecuteNonQueryAsync();
        }
        await using (var entry = new NpgsqlCommand("insert into finance_ledger.journal_entry(tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values($1,$2,$3,$4,$5,'expenses',$6,$7,'2026-09-01',$8,'ExpenseRecognized',1,$9,$10,$11,$12,'BC02 canonical expense',$13,$14,now())", connection))
        {
            entry.Parameters.AddWithValue(AtlasTenant.Value); entry.Parameters.AddWithValue(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528")); entry.Parameters.AddWithValue(entryId); entry.Parameters.AddWithValue(journalId); entry.Parameters.AddWithValue(factId); entry.Parameters.AddWithValue($"bc02-expense-{suffix}"); entry.Parameters.AddWithValue($"BC02-{suffix}"); entry.Parameters.AddWithValue(periodId); entry.Parameters.AddWithValue(currency); entry.Parameters.AddWithValue(baseCurrency); entry.Parameters.AddWithValue(ruleId); entry.Parameters.AddWithValue(configurationId); entry.Parameters.AddWithValue(Guid.CreateVersion7()); entry.Parameters.AddWithValue(actorId); await entry.ExecuteNonQueryAsync();
        }
        await using (var link = new NpgsqlCommand("update finance_ledger.accounting_fact set posted_entry_id=$3 where tenant_id=$1 and id=$2", connection)) { link.Parameters.AddWithValue(AtlasTenant.Value); link.Parameters.AddWithValue(factId); link.Parameters.AddWithValue(entryId); await link.ExecuteNonQueryAsync(); }
        await using var lines = new NpgsqlCommand("insert into finance_ledger.journal_entry_line(tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,account_role,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate,exchange_rate_snapshot) values($1,$2,$3,$4,1,$5,375,0,$6,375,'ExpenseRecognized','PurchaseExpense',375,0,375,0,1,'{}'::jsonb),($1,$2,$7,$4,2,$8,0,375,$6,375,'ExpenseRecognized','EmployeeReimbursementPayable',0,375,0,375,1,'{}'::jsonb)", connection);
        lines.Parameters.AddWithValue(AtlasTenant.Value); lines.Parameters.AddWithValue(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528")); lines.Parameters.AddWithValue(Guid.CreateVersion7()); lines.Parameters.AddWithValue(entryId); lines.Parameters.AddWithValue(debitAccount); lines.Parameters.AddWithValue(currency); lines.Parameters.AddWithValue(Guid.CreateVersion7()); lines.Parameters.AddWithValue(creditAccount); await lines.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return await command.ExecuteScalarAsync();
    }

    private sealed class SwitchingActualSource(Guid legalEntityId, string identity) : IPlanningOwnerReconciliationSource
    {
        private readonly Guid ownerLegalEntityId = legalEntityId;
        private readonly string sourceIdentity = identity;
        public string SourceModule => "ledger";
        public bool Fail { get; set; }
        public decimal Amount { get; set; } = 100m;

        public Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenant, Guid? legalEntityId, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("controlled owner-source outage");
            return Task.FromResult<IReadOnlyList<PlanningOwnerFinancialFact>>([new(
                SourceModule,
                $"bc02-restatement-{sourceIdentity}",
                "line-1",
                "1",
                legalEntityId ?? ownerLegalEntityId,
                new(2026, 10, 1),
                "OPEX",
                "{}",
                "Actual",
                Amount,
                "EGP",
                $"bc02-restatement-{sourceIdentity}|line-1",
                "PostedToDate")]);
        }
    }

    private sealed class DuplicateActualSource(Guid legalEntityId, string identity) : IPlanningOwnerReconciliationSource
    {
        private readonly Guid ownerLegalEntityId = legalEntityId;
        private readonly string sourceIdentity = identity;
        public string SourceModule => "operational";

        public Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenant, Guid? legalEntityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanningOwnerFinancialFact>>([
                new("ledger", $"bc02-duplicate-{sourceIdentity}", "line-1", "1", legalEntityId ?? ownerLegalEntityId, new(2026, 10, 1), "OPEX", "{}", "Actual", 100m, "EGP", $"shared-economic-{sourceIdentity}", "PostedToDate"),
                new("purchasing", $"bc02-duplicate-{sourceIdentity}", "line-1", "1", legalEntityId ?? ownerLegalEntityId, new(2026, 10, 1), "OPEX", "{}", "Actual", 100m, "EGP", $"shared-economic-{sourceIdentity}", "PostedToDate")]);
    }
}
