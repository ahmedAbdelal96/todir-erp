using Core.Transactions;
using Core.Services;
using Modules.Expenses.DTOs;
using Modules.Expenses.Repositories;
using Modules.FinanceBase.Repositories;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Modules.Planning;
using Npgsql;
using Shared.Primitives.Identifiers;
using System.Text.Json;
using Xunit;

namespace Modules.Expenses.Tests;

public sealed class PlanningBudgetControlIntegrationTests
{
    private static readonly Guid Atlas = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [ExpensesPostgresFact]
    public async Task DraftExpenseReservationIsUpdatedAndReleasedInsideOwnerLifecycle()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION")!;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var financialRead = new NpgsqlLegalEntityFinancialReadStore(dataSource);
        var repository = new ExpenseRepository(
            dataSource,
            coordinator,
            new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, financialRead, TimeProvider.System)),
            budgetControl: new PlanningBudgetControlCapability());

        var categoryVersion = (Guid)(await ScalarAsync(dataSource,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, AtlasEntity))!;
        var bucketId = await EnsureOpexBucketAsync(dataSource, categoryVersion);

        var created = await repository.CreateAsync(new(Atlas), new(AtlasEntity, null, "Planning control integration", new(2027, 1, 10), 25m, "EGP", "CompanyPaid", categoryVersion, BudgetBucketId: bucketId), default);
        var deleted = false;
        try
        {
            Assert.Equal(25m, await ScalarAsync(dataSource, "select amount from business_planning.budget_reservation where tenant_id=$1 and source_module='expenses' and source_document_id=$2 and source_line_id='expense' and state='Reserved'", Atlas, created.Id.ToString("D")));
            Assert.Equal("Precommitment", (string)(await ScalarAsync(dataSource, "select reservation_kind from business_planning.budget_reservation where tenant_id=$1 and source_module='expenses' and source_document_id=$2 and source_line_id='expense'", Atlas, created.Id.ToString("D")))!);

            var amended = await repository.UpdateAsync(new(Atlas), AtlasEntity, created.Id,
                new("Planning control integration amended", new(2027, 1, 10), 30m, "EGP", categoryVersion, null, null, created.Version, null, null, null, false, bucketId), default);
            Assert.Equal(30m, await ScalarAsync(dataSource, "select amount from business_planning.budget_reservation where tenant_id=$1 and source_module='expenses' and source_document_id=$2 and source_line_id='expense' and state='Reserved'", Atlas, created.Id.ToString("D")));
            Assert.Equal(2L, amended.Version);

            await repository.DeleteDraftExpenseAsync(new(Atlas), AtlasEntity, created.Id, Guid.NewGuid(), Guid.NewGuid(), default);
            deleted = true;
            Assert.Equal("Released", (string)(await ScalarAsync(dataSource, "select state from business_planning.budget_reservation where tenant_id=$1 and source_module='expenses' and source_document_id=$2 and source_line_id='expense' order by created_at desc limit 1", Atlas, created.Id.ToString("D")))!);
        }
        catch
        {
            if (!deleted) await repository.DeleteDraftExpenseAsync(new(Atlas), AtlasEntity, created.Id, Guid.NewGuid(), Guid.NewGuid(), default);
            throw;
        }
    }

    [ExpensesPostgresFact]
    public async Task OmittedBucketCannotBypassActiveBlockPolicy()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION")!;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var financialRead = new NpgsqlLegalEntityFinancialReadStore(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator,
            new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, financialRead, TimeProvider.System)),
            budgetControl: new PlanningBudgetControlCapability());
        var categoryVersion = (Guid)(await ScalarAsync(dataSource,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, AtlasEntity))!;
        await ExecuteAsync(dataSource, "update business_planning.budget_control_policy set mode='Block',active=true where tenant_id=$1 and legal_entity_id=$2", Atlas, AtlasEntity);
        try
        {
            var exception = await Assert.ThrowsAsync<PlanningBudgetControlException>(() => repository.CreateAsync(new(Atlas),
                new(AtlasEntity, null, "Budget bypass attempt", new(2027, 1, 10), 100001m, "EGP", "CompanyPaid", categoryVersion), default));
            Assert.Equal("planning.budget_insufficient", exception.Code);
        }
        finally
        {
            await ExecuteAsync(dataSource, "update business_planning.budget_control_policy set mode='Warn',active=true where tenant_id=$1 and legal_entity_id=$2", Atlas, AtlasEntity);
        }
    }

    [ExpensesPostgresFact]
    public async Task InvalidBucketReferenceIsRejectedByOwnerCommand()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION")!;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var financialRead = new NpgsqlLegalEntityFinancialReadStore(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator,
            new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, financialRead, TimeProvider.System)),
            budgetControl: new PlanningBudgetControlCapability());
        var categoryVersion = (Guid)(await ScalarAsync(dataSource,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, AtlasEntity))!;
        var exception = await Assert.ThrowsAsync<PlanningBudgetControlException>(() => repository.CreateAsync(new(Atlas),
            new(AtlasEntity, null, "Invalid bucket", new(2027, 1, 10), 25m, "EGP", "CompanyPaid", categoryVersion, BudgetBucketId: Guid.NewGuid()), default));
        Assert.Equal("planning.bucket_not_found", exception.Code);
    }

    [ExpensesPostgresFact]
    public async Task PartialConsumptionIsConservedAndIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION")!;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var bucket = await EnsureOpexBucketAsync(dataSource, (Guid)(await ScalarAsync(dataSource,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, AtlasEntity))!);
        var baselineActual = Convert.ToDecimal(await ScalarAsync(dataSource, "select actual_total from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", Atlas, bucket), System.Globalization.CultureInfo.InvariantCulture);
        var sourceId = Guid.NewGuid().ToString("D");
        var capability = new PlanningBudgetControlCapability();
        await coordinator.ExecuteAsync(new(Atlas), async (context, token) =>
        {
            await capability.UpsertReservationAsync(context, new(Atlas), Guid.NewGuid(), new(AtlasEntity, bucket, "test", sourceId, "line", "v1", 100m, "EGP", PlanningBudgetReservationKind.Commitment), token);
            var first = await capability.ConsumeReservationAsync(context, new(Atlas), Guid.NewGuid(), new(AtlasEntity, "test", sourceId, "line", "actual-1", 40m, "EGP"), token);
            Assert.Equal("PartiallyConsumed", first.Decision);
            var retry = await capability.ConsumeReservationAsync(context, new(Atlas), Guid.NewGuid(), new(AtlasEntity, "test", sourceId, "line", "actual-1", 40m, "EGP"), token);
            Assert.Equal("PartiallyConsumed", retry.Decision);
            var final = await capability.ConsumeReservationAsync(context, new(Atlas), Guid.NewGuid(), new(AtlasEntity, "test", sourceId, "line", "actual-2", 60m, "EGP"), token);
            Assert.Equal("Consumed", final.Decision);
            return 0;
        });
        Assert.Equal(baselineActual + 100m, await ScalarAsync(dataSource, "select actual_total from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", Atlas, bucket));
        Assert.Equal("Consumed", (string)(await ScalarAsync(dataSource, "select state from business_planning.budget_reservation where tenant_id=$1 and source_module='test' and source_document_id=$2 and source_line_id='line' order by created_at desc limit 1", Atlas, sourceId))!);
    }

    private static async Task<Guid> EnsureOpexBucketAsync(NpgsqlDataSource dataSource, Guid categoryVersion)
    {
        var dimension = JsonSerializer.Serialize(new Dictionary<string, string> { ["categoryVersionId"] = categoryVersion.ToString("D") });
        var existing = await ScalarAsync(dataSource,
            "select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2027-01-01' and metric_code='OPEX' and dimension_key=$3::jsonb limit 1", Atlas, AtlasEntity, dimension);
        if (existing is Guid id) return id;
        var policy = (Guid)(await ScalarAsync(dataSource, "select id from business_planning.budget_control_policy where tenant_id=$1 and legal_entity_id=$2 order by id limit 1", Atlas, AtlasEntity))!;
        var planVersion = (Guid)(await ScalarAsync(dataSource, "select id from business_planning.plan_version where tenant_id=$1 order by id limit 1", Atlas))!;
        var bucket = Guid.NewGuid();
        await ExecuteAsync(dataSource,
            "insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,currency_code) values($1,$2,$3,$4,$5,'2027-01-01','OPEX',$6::jsonb,100000,'EGP')",
            Atlas, bucket, policy, planVersion, AtlasEntity, dimension);
        return bucket;
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql, params object[] values)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1::text,false)", connection);
        tenant.Parameters.AddWithValue(Atlas);
        await tenant.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource dataSource, string sql, params object[] values)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1::text,false)", connection))
        {
            tenant.Parameters.AddWithValue(Atlas);
            await tenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.GetValue(0) : null;
    }

    private sealed class ExpensesPostgresFactAttribute : FactAttribute
    {
        public ExpensesPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION")))
                Skip = "Set TODIR_EXPENSES_TEST_CONNECTION for PostgreSQL acceptance.";
        }
    }
}
