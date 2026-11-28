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

    [Fact(DisplayName = "BC2-T08 current budget uses only effective approved changes and nets transfers")]
    public async Task CurrentBudgetUsesEffectiveApprovedChangesAndNetsTransfers()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var sourceVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource));
        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actorId, sourceVersionId, $"BC02 current budget {Guid.NewGuid():N}", default);
        var baselineId = await repository.CreateBaselineAsync(AtlasTenant, actorId, new(draft.Id, "BC02 current budget", 100m, "EGP"), default);
        var coordinates = new List<Guid>();
        await using (var coordinateCommand = new NpgsqlCommand("select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 2", admin))
        {
            coordinateCommand.Parameters.AddWithValue(AtlasTenant.Value); coordinateCommand.Parameters.AddWithValue(draft.Id);
            await using var reader = await coordinateCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync()) coordinates.Add(reader.GetGuid(0));
        }
        Assert.Equal(2, coordinates.Count);
        var past = new DateOnly(2026, 1, 1);
        var future = new DateOnly(2027, 1, 1);
        await ExecuteAsync(admin, "insert into business_planning.budget_revision(tenant_id,id,baseline_id,delta_amount,reason,state,effective_from,created_by) values($1,$2,$3,20,'approved increase','Approved',$4,$5),($1,$6,$3,-5,'approved reduction','Approved',$4,$5),($1,$7,$3,999,'pending','Draft',$4,$5),($1,$8,$3,999,'rejected','Rejected',$4,$5),($1,$9,$3,500,'future','Approved',$10,$5)", AtlasTenant.Value, Guid.CreateVersion7(), baselineId, past, actorId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), future);
        await ExecuteAsync(admin, "insert into business_planning.budget_transfer(tenant_id,id,baseline_id,from_coordinate_id,to_coordinate_id,amount,state,reason,effective_from,created_by) values($1,$2,$3,$4,$5,20,'Approved','approved transfer',$6,$7),($1,$8,$3,$4,$5,70,'Draft','pending transfer',$6,$7),($1,$9,$3,$4,$5,80,'Approved','future transfer',$10,$7)", AtlasTenant.Value, Guid.CreateVersion7(), baselineId, coordinates[0], coordinates[1], past, actorId, Guid.CreateVersion7(), Guid.CreateVersion7(), future);

        Assert.Equal(115m, await repository.CurrentBudgetAsync(AtlasTenant, baselineId, new DateOnly(2026, 12, 31), default));
        Assert.Equal(100m, await repository.CurrentBudgetAsync(AtlasTenant, baselineId, new DateOnly(2025, 12, 31), default));
    }

    [Fact(DisplayName = "BC2-T03 published Actual basis remains pinned across a later restatement")]
    public async Task PublishedActualBasisRemainsPinnedAcrossRestatement()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var sourceVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var source = new SwitchingActualSource(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.CreateVersion7().ToString("N"));
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [source]);
        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actorId, sourceVersionId, $"BC02 historical basis {Guid.NewGuid():N}", default);
        var firstRun = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", $"basis-a-{Guid.NewGuid():N}", null, draft.Id), default);
        var firstSnapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, firstRun))!;
        await repository.AdvanceVersionAsync(AtlasTenant, actorId, draft.Id, "submit", default);
        await repository.AdvanceVersionAsync(AtlasTenant, actorId, draft.Id, "approve", default);
        await repository.AdvanceVersionAsync(AtlasTenant, actorId, draft.Id, "publish", default);
        var firstBasis = await repository.GetActualBasisAsync(AtlasTenant, draft.Id, default);
        Assert.Equal(firstSnapshot, firstBasis.SnapshotId);
        Assert.True(firstBasis.Pinned);

        source.Amount = 140m;
        var secondRun = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", $"basis-b-{Guid.NewGuid():N}", null, draft.Id), default);
        var secondSnapshot = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, secondRun))!;
        var historicalBasis = await repository.GetActualBasisAsync(AtlasTenant, draft.Id, default);
        Assert.Equal(firstSnapshot, historicalBasis.SnapshotId);
        Assert.NotEqual(firstSnapshot, secondSnapshot);
        Assert.Equal("Published", await ScalarAsync(admin, "select state from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, firstSnapshot));
        Assert.Equal("Published", await ScalarAsync(admin, "select state from business_planning.planning_actual_snapshot where tenant_id=$1 and id=$2", AtlasTenant.Value, secondSnapshot));
    }

    [Fact(DisplayName = "BC2-T12 governed target and proposal lifecycle publishes without losing proposal identity")]
    public async Task GovernedTargetAndProposalLifecyclePublishesWithoutLosingIdentity()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var sourceVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var legalEntityId = (Guid)(await ScalarAsync(admin, "select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id limit 1", AtlasTenant.Value))!;
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource));
        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actorId, sourceVersionId, $"BC02 target {Guid.NewGuid():N}", default);
        var target = await repository.CreatePlanningTargetAsync(AtlasTenant, actorId, new(draft.Id, legalEntityId, new(2027, 1, 1), "OPEX", 500m, "EGP", "governed target"), default);
        var proposal = await repository.CreatePlanningProposalAsync(AtlasTenant, actorId, new(draft.Id, legalEntityId, null, new(2027, 1, 1), "OPEX", 450m, "EGP", "department proposal"), default);
        Assert.NotNull(proposal.ProposalId);
        await repository.AdvancePlanningProposalAsync(AtlasTenant, actorId, proposal.ProposalId!.Value, "submit", default);
        await repository.AdjustPlanningProposalAsync(AtlasTenant, actorId, proposal.ProposalId.Value, new(475m, "manager adjustment"), default);
        await repository.AdvancePlanningProposalAsync(AtlasTenant, actorId, proposal.ProposalId.Value, "approve", default);
        await repository.AdvancePlanningTargetAsync(AtlasTenant, actorId, target.Id, "approve", default);
        var published = await repository.AdvancePlanningTargetAsync(AtlasTenant, actorId, target.Id, "publish", default);
        Assert.Equal("Published", published.State);
        Assert.Equal(proposal.ProposalId, published.ProposalId);
        Assert.Equal("Approved", await ScalarAsync(admin, "select state from business_planning.planning_proposal where tenant_id=$1 and id=$2", AtlasTenant.Value, proposal.ProposalId.Value));
    }

    [Fact(DisplayName = "BC2-T04 published inputs and Actual snapshots are protected at repository and PostgreSQL boundaries")]
    public async Task PublishedInputsAndActualSnapshotsAreProtectedAtBothBoundaries()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var publishedVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", AtlasTenant.Value))!;
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var legalEntityId = (Guid)(await ScalarAsync(admin, "select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id limit 1", AtlasTenant.Value))!;
        var source = new SwitchingActualSource(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.CreateVersion7().ToString("N"));
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [source]);
        var rejected = await Assert.ThrowsAsync<PlanningException>(() => repository.WriteGridAsync(AtlasTenant, actorId, new(publishedVersionId, [new(Guid.Empty, new(2027, 1, 1), "OPEX", System.Text.Json.JsonDocument.Parse("{}").RootElement, 1m, "EGP", false, "blocked")], null), default));
        Assert.Equal("planning.version_immutable", rejected.Code);

        var draft = await repository.ClonePlanVersionAsync(AtlasTenant, actorId, publishedVersionId, $"BC02 immutable snapshot {Guid.NewGuid():N}", default);
        var run = await repository.RefreshActualsAsync(AtlasTenant, actorId, new("seed-v1", $"immutable-{Guid.NewGuid():N}", null, draft.Id), default);
        var snapshotId = (Guid)(await ScalarAsync(admin, "select snapshot_id from business_planning.actual_refresh_run where tenant_id=$1 and id=$2", AtlasTenant.Value, run))!;
        await using var runtime = new NpgsqlConnection(runtimeConnection);
        await runtime.OpenAsync();
        await using var transaction = await runtime.BeginTransactionAsync();
        await ExecuteAsync(runtime, "select set_config('app.tenant_id',$1::text,true)", transaction, AtlasTenant.Value);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(runtime, "update business_planning.planning_actual_snapshot set total_amount=total_amount+1 where tenant_id=$1 and id=$2", transaction, AtlasTenant.Value, snapshotId));
        await transaction.RollbackAsync();

        await using var targetConnection = new NpgsqlConnection(runtimeConnection);
        await targetConnection.OpenAsync();
        await using var targetTransaction = await targetConnection.BeginTransactionAsync();
        await ExecuteAsync(targetConnection, "select set_config('app.tenant_id',$1::text,true)", targetTransaction, AtlasTenant.Value);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(targetConnection, "insert into business_planning.planning_target(tenant_id,id,plan_version_id,legal_entity_id,period_start,metric_code,target_amount,currency_code,created_by) values($1,$2,$3,$4,$5,'OPEX',1,'EGP',$6)", targetTransaction, AtlasTenant.Value, Guid.CreateVersion7(), publishedVersionId, legalEntityId, new DateOnly(2027, 1, 1), actorId));
        await targetTransaction.RollbackAsync();
    }

    [Fact(DisplayName = "BC2-T05 metric mapping publish retire and replacement preserve history")]
    public async Task MetricMappingPublishRetireAndReplacementPreserveHistory()
    {
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnection);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var actorId = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", AtlasTenant.Value))!;
        var source = new SwitchingActualSource(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.CreateVersion7().ToString("N"));
        var repository = new PlanningRepository(new Core.Transactions.NpgsqlTransactionCoordinator(dataSource), ownerSources: [source]);
        var version = Guid.NewGuid().ToString("N");
        var first = await repository.CreateMetricMappingAsync(AtlasTenant, actorId, new(version, "ledger", "FinancialRole", "OPEX", "OPEX", null, new(2026, 1, 1), new(2026, 12, 31)), default);
        Assert.Equal("Draft", first.State);
        Assert.Equal("Published", (await repository.AdvanceMetricMappingAsync(AtlasTenant, actorId, first.Id, "publish", default)).State);
        Assert.Equal("Retired", (await repository.AdvanceMetricMappingAsync(AtlasTenant, actorId, first.Id, "retire", default)).State);
        var second = await repository.CreateMetricMappingAsync(AtlasTenant, actorId, new(version + "-replacement", "ledger", "FinancialRole", "OPEX", "OPEX", null, new(2027, 1, 1), null), default);
        Assert.Equal("Published", (await repository.AdvanceMetricMappingAsync(AtlasTenant, actorId, second.Id, "publish", default)).State);
        Assert.Equal("Retired", await ScalarAsync(admin, "select state from business_planning.planning_metric_mapping where tenant_id=$1 and id=$2", AtlasTenant.Value, first.Id));
        Assert.Equal("Published", await ScalarAsync(admin, "select state from business_planning.planning_metric_mapping where tenant_id=$1 and id=$2", AtlasTenant.Value, second.Id));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is required.");

    private static async Task EnsureCanonicalExpenseFactAsync(NpgsqlConnection connection, Guid actorId)
    {
        // The fixture writes through the Ledger trigger that projects journal lines into
        // another FORCE RLS table. Establish the same tenant context as the runtime path
        // so the fixture reaches the intended accounting-source contract instead of
        // failing while PostgreSQL casts an empty tenant setting to uuid.
        await ExecuteAsync(connection, "select set_config('app.tenant_id',$1::text,false)", AtlasTenant.Value.ToString());
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

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction transaction, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
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
