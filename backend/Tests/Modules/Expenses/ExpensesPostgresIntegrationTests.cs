using Modules.Expenses.Repositories;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Core.Transactions;
using Core.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Payments;
using Xunit;

namespace Modules.Expenses.Tests;

public sealed class ExpensesPostgresIntegrationTests
{
    private static readonly Guid Atlas = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid Delta = Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb");

    [ExpensesPostgresFact]
    public async Task SeededEmployeeExpenseHasSingleReimbursementObligation()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var setContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection);
        setContext.Transaction = transaction;
        setContext.Parameters.AddWithValue(Atlas.ToString("D"));
        await setContext.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("select count(*), coalesce(sum(amount),0) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and status='Pending' and amount=320", connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue(Atlas);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(320m, reader.GetDecimal(1));
    }

    [ExpensesPostgresFact]
    public async Task RuntimeTenantContextCannotReadOtherTenantExpenses()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var setContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection);
        setContext.Transaction = transaction;
        setContext.Parameters.AddWithValue(Delta.ToString("D"));
        await setContext.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("select count(*) from business_expenses.expense where tenant_id=$1", connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue(Atlas);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt64(0));
    }

    [ExpensesFinancialPostgresFact]
    public async Task FinalizationPostsExpenseAndReversalToLedgerExactlyOnce()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_FINANCIAL_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledgerRepository = new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(ledgerRepository));
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var created = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "اختبار مالي", new(2026, 10, 12), 125m, "EGP", "EmployeePaid", categoryVersion, "رحلة عميل", "اختبار الترحيل"), default);
        await repository.AddEvidenceAsync(new(Atlas), entity, created.Id, new("Receipt", "file://expenses/test-receipt"), default);
        var submitted = await repository.TransitionAsync(new(Atlas), entity, created.Id, new(created.Version), "submit", default);
        var approved = await repository.TransitionAsync(new(Atlas), entity, created.Id, new(submitted.Version), "approve", default);
        var finalized = await repository.TransitionAsync(new(Atlas), entity, created.Id, new(approved.Version), "finalize", default);
        Assert.Equal("Posted", finalized.AccountingState);
        Assert.Equal("ReimbursementPending", finalized.SettlementState);
        var posted = await ReadPostingAsync(connection, finalized.Id);
        Assert.Equal(1, posted.EntryCount);
        Assert.Equal(125m, posted.Debit);
        Assert.Equal(125m, posted.Credit);
        var reversed = await repository.TransitionAsync(new(Atlas), entity, created.Id, new(finalized.Version, "Duplicate claim correction"), "reverse", default);
        Assert.Equal("Reversed", reversed.ExpenseState);
        Assert.Equal("Reversed", reversed.AccountingState);
        Assert.Equal(1, await ScalarLongAsync(connection, "select count(*) from finance_ledger.journal_entry_reversal r join business_expenses.expense e on e.tenant_id=$1 and e.id=$2 and r.original_entry_id=e.ledger_entry_id", Atlas, created.Id));
    }

    [ExpensesPostgresFact]
    public async Task TypedMileageAndPersonalCardRepaymentRemainReproducibleAndIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var employee = Guid.NewGuid();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var mileageExpense = await repository.CreateAsync(new(Atlas), new(entity, employee, "Mileage", new(2026, 10, 13), 1m, "EGP", "EmployeePaid", categoryVersion), default);
        var mileage = await repository.SetMileageDetailAsync(new(Atlas), entity, mileageExpense.Id, new(100m, 7.5m), default);
        Assert.Equal(750m, mileage.CalculatedAmount);
        Assert.Equal(750m, (decimal)(await ScalarAsync(connection, "select amount from business_expenses.expense where tenant_id=$1 and id=$2", Atlas, mileageExpense.Id))!);
        var dimension = await repository.SetDimensionAsync(new(Atlas), entity, mileageExpense.Id, new("COST_CENTER", "CC-001"), default);
        Assert.Equal("CC-001", dimension.ValueCode);
        Assert.Single(await repository.ListDimensionsAsync(new(Atlas), entity, mileageExpense.Id, default));

        var providerReference = $"provider-acceptance-{Guid.CreateVersion7():N}";
        var card = await repository.ImportCardTransactionAsync(new(Atlas), entity, new(providerReference, "Hotel", new(2026, 10, 13), 210m), default);
        var personal = await repository.MarkCardTransactionPersonalAsync(new(Atlas), entity, card.Id, new(employee), default);
        Assert.Equal("Personal", personal.State);
        await repository.MarkCardTransactionPersonalAsync(new(Atlas), entity, card.Id, new(employee), default);
        Assert.Equal(1, await ScalarLongAsync(connection, "select count(*) from business_expenses.expense_employee_repayment where tenant_id=$1 and source_reference=$2", Atlas, $"corporate-card:{card.Id:D}"));
        Assert.Equal(210m, (decimal)(await ScalarAsync(connection, "select amount from business_expenses.expense_employee_repayment where tenant_id=$1 and source_reference=$2", Atlas, $"corporate-card:{card.Id:D}"))!);
        var cardPage = await repository.ListCardTransactionsAsync(new(Atlas), entity, "Personal", providerReference, 1, 1, default);
        Assert.Equal(1, cardPage.TotalCount);
        Assert.Single(cardPage.Items);
        var repaymentPage = await repository.ListEmployeeRepaymentsAsync(new(Atlas), entity, employee, "Requested", 1, 10, default);
        Assert.True(repaymentPage.TotalCount >= 1);

        var repositoryWithAdvancePayments = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), advancePayments: new TestAdvancePaymentCapability());
        var advance = await repositoryWithAdvancePayments.CreateAdvanceAsync(new(Atlas), new(entity, employee, "سلفة رحلة", 500m), default);
        advance = await repositoryWithAdvancePayments.TransitionAdvanceAsync(new(Atlas), entity, advance.Id, advance.Version, "approve", default);
        advance = await repositoryWithAdvancePayments.IssueAdvanceAsync(new(Atlas), entity, advance.Id, Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), "BankTransfer", 500m, new(2026, 10, 14), new(2026, 10, 14), advance.Version), default);
        var advanceExpense = await repositoryWithAdvancePayments.CreateAsync(new(Atlas), new(entity, employee, "Advance expense", new(2026, 10, 14), 300m, "EGP", "EmployeePaid", categoryVersion), default);
        await repositoryWithAdvancePayments.AddEvidenceAsync(new(Atlas), entity, advanceExpense.Id, new("Receipt", "file://expenses/advance"), default);
        var submitted = await repositoryWithAdvancePayments.TransitionAsync(new(Atlas), entity, advanceExpense.Id, new(advanceExpense.Version), "submit", default);
        var approved = await repositoryWithAdvancePayments.TransitionAsync(new(Atlas), entity, advanceExpense.Id, new(submitted.Version), "approve", default);
        advance = await repositoryWithAdvancePayments.ApplyAdvanceAsync(new(Atlas), entity, advance.Id, new(advanceExpense.Id, 300m), default);
        Assert.Equal(300m, advance.AppliedAmount);
        advance = await repositoryWithAdvancePayments.ReturnAdvanceAsync(new(Atlas), entity, advance.Id, new(200m, advance.Version), default);
        Assert.Equal("Settled", advance.State);
        var advancePage = await repository.ListAdvancesAsync(new(Atlas), entity, "Settled", employee, 1, 10, default);
        Assert.True(advancePage.TotalCount >= 1);
        var summary = await repository.GetExceptionSummaryAsync(new(Atlas), entity, default);
        Assert.True(summary.PersonalRepaymentDue >= 1);
        Assert.True(summary.OutstandingAdvances >= 0);
    }

    [ExpensesPostgresFact]
    public async Task AdvanceCannotBeSettledBeforeIssuedFundsAreAppliedOrReturned()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var advance = await repository.CreateAdvanceAsync(new(Atlas), new(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.NewGuid(), "Guarded settlement", 90m), default);
        advance = await repository.TransitionAdvanceAsync(new(Atlas), advance.LegalEntityId, advance.Id, advance.Version, "approve", default);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.TransitionAdvanceAsync(new(Atlas), advance.LegalEntityId, advance.Id, advance.Version, "settle", default));
    }

    [ExpensesPostgresFact]
    public async Task CorporateCardAllocationSupportsSplitExpensesAndIsIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var first = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "Split hotel", new(2026, 10, 15), 100m, "EGP", "CorporateCard", categoryVersion), default);
        var second = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "Split meal", new(2026, 10, 15), 110m, "EGP", "CorporateCard", categoryVersion), default);
        var providerReference = $"allocation-{Guid.CreateVersion7():N}";
        var card = await repository.ImportCardTransactionAsync(new(Atlas), entity, new(providerReference, "Hotel & Meal", new(2026, 10, 15), 210m), default);
        var partial = await repository.AllocateCardTransactionAsync(new(Atlas), entity, card.Id, new(first.Id, 100m), default);
        Assert.Equal("Unmatched", partial.State);
        var allocated = await repository.AllocateCardTransactionAsync(new(Atlas), entity, card.Id, new(second.Id, 110m), default);
        Assert.Equal("Allocated", allocated.State);
        await repository.AllocateCardTransactionAsync(new(Atlas), entity, card.Id, new(second.Id, 110m), default);
        Assert.Equal(2, await ScalarLongAsync(connection, "select count(*) from business_expenses.expense_card_allocation where tenant_id=$1 and card_transaction_id=$2", Atlas, card.Id));
        Assert.Equal(210m, (decimal)(await ScalarAsync(connection, "select sum(amount) from business_expenses.expense_card_allocation where tenant_id=$1 and card_transaction_id=$2", Atlas, card.Id))!);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    { await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); return await command.ExecuteScalarAsync(); }
    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, params object[] values)
    { return Convert.ToInt64(await ScalarAsync(connection, sql, values), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<(long EntryCount, decimal Debit, decimal Credit)> ReadPostingAsync(NpgsqlConnection connection, Guid expenseId)
    {
        await using var command = new NpgsqlCommand("select count(distinct e.id),coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from business_expenses.expense x join finance_ledger.journal_entry e on e.tenant_id=x.tenant_id and e.id=x.ledger_entry_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.journal_entry_id=e.id where x.tenant_id=$1 and x.id=$2", connection); command.Parameters.AddWithValue(Atlas); command.Parameters.AddWithValue(expenseId); await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); return (reader.GetInt64(0), reader.GetDecimal(1), reader.GetDecimal(2));
    }

    private sealed class TestAdvancePaymentCapability : IBusinessAdvancePaymentCapability
    {
        public Task<BusinessAdvancePaymentResult> IssueBusinessAdvanceAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid advanceId, Guid paymentAccountId, string method, decimal amount, string currencyCode, DateOnly paymentDate, DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(new BusinessAdvancePaymentResult(Guid.CreateVersion7(), amount, currencyCode, "Completed"));
    }

    private sealed class ExpensesPostgresFactAttribute : FactAttribute
    {
        public ExpensesPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION"))) Skip = "Set TODIR_EXPENSES_TEST_CONNECTION for PostgreSQL acceptance."; }
    }

    private sealed class ExpensesFinancialPostgresFactAttribute : FactAttribute
    {
        public ExpensesFinancialPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_EXPENSES_FINANCIAL_TEST_CONNECTION"))) Skip = "Set TODIR_EXPENSES_FINANCIAL_TEST_CONNECTION for isolated financial acceptance."; }
    }
}
