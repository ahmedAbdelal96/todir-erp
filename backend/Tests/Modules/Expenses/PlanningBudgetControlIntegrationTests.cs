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
        var bucketId = (Guid)(await ScalarAsync(dataSource,
            "select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where b.tenant_id=$1 and b.legal_entity_id=$2 and b.currency_code='EGP' and p.active=true order by b.period_start,b.id limit 1", Atlas, AtlasEntity))!;

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
