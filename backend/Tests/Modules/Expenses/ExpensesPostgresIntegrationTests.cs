using Modules.Expenses.Repositories;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Core.Transactions;
using Core.Services;
using Modules.Expenses.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Payments;
using Shared.Contracts.Platform;
using Xunit;
using System.Text.Json;

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
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(ledgerRepository), approvals: new TestApprovalCapability());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var created = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "اختبار مالي", new(2026, 10, 12), 125m, "EGP", "EmployeePaid", categoryVersion, "رحلة عميل", "اختبار الترحيل"), default);
        await repository.AddEvidenceAsync(new(Atlas), entity, created.Id, new("Receipt", "file://expenses/test-receipt"), default);
        Assert.Equal(1, await ScalarLongAsync(connection, "select count(*) from business_expenses.expense_history where tenant_id=$1 and expense_id=$2 and action='evidence.attached'", Atlas, created.Id));
        var submitted = await repository.TransitionAsync(new(Atlas), entity, created.Id, new(created.Version), "submit", default);
        await repository.EvaluatePolicyAsync(new(Atlas), entity, created.Id, default);
        var approvedVersion = await ApproveAsync(repository, new(Atlas), entity, created.Id, submitted.Version);
        var finalized = await repository.TransitionAsync(new(Atlas), entity, created.Id, new(approvedVersion), "finalize", default);
        Assert.Equal("Posted", finalized.AccountingState);
        Assert.Equal("ReimbursementPending", finalized.SettlementState);
        var report = await repository.CreateReportAsync(new(Atlas), new(entity, null, "October finance report", "EGP"), default);
        var reportItem = await repository.AddReportItemAsync(new(Atlas), entity, report.Id, new(finalized.Id, 125m), default);
        Assert.Equal(finalized.Id, reportItem.ExpenseId);
        var reportItems = await repository.ListReportItemsAsync(new(Atlas), entity, report.Id, 1, 10, default);
        Assert.Single(reportItems.Items);
        Assert.Contains((await repository.ListReportsAsync(new(Atlas), entity, "Draft", "October finance", 1, 10, default)).Items, item => item.TotalAmount == 125m);
        var currentReport = (await repository.ListReportsAsync(new(Atlas), entity, "Draft", "October finance", 1, 10, default)).Items.Single(item => item.Id == report.Id);
        var submittedReport = await repository.TransitionReportAsync(new(Atlas), entity, report.Id, new(currentReport.Version), "submit", default);
        var approvedReport = await repository.TransitionReportAsync(new(Atlas), entity, report.Id, new(submittedReport.Version), "approve", default);
        var finalizedReport = await repository.TransitionReportAsync(new(Atlas), entity, report.Id, new(approvedReport.Version), "finalize", default);
        Assert.Equal("Finalized", finalizedReport.State);
        Assert.Equal(3, (await repository.ListReportHistoryAsync(new(Atlas), entity, report.Id, default)).Count);
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
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), approvals: new TestApprovalCapability());
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

        var repositoryWithAdvancePayments = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), advancePayments: new TestAdvancePaymentCapability(), approvals: new TestApprovalCapability());
        var advance = await repositoryWithAdvancePayments.CreateAdvanceAsync(new(Atlas), new(entity, employee, "سلفة رحلة", 500m), default);
        advance = await repositoryWithAdvancePayments.TransitionAdvanceAsync(new(Atlas), entity, advance.Id, advance.Version, "approve", default);
        advance = await repositoryWithAdvancePayments.IssueAdvanceAsync(new(Atlas), entity, advance.Id, Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), "BankTransfer", 500m, new(2026, 10, 14), new(2026, 10, 14), advance.Version), default);
        var advanceExpense = await repositoryWithAdvancePayments.CreateAsync(new(Atlas), new(entity, employee, "Advance expense", new(2026, 10, 14), 300m, "EGP", "EmployeePaid", categoryVersion), default);
        await repositoryWithAdvancePayments.AddEvidenceAsync(new(Atlas), entity, advanceExpense.Id, new("Receipt", "file://expenses/advance"), default);
        var submitted = await repositoryWithAdvancePayments.TransitionAsync(new(Atlas), entity, advanceExpense.Id, new(advanceExpense.Version), "submit", default);
        await ApproveAsync(repositoryWithAdvancePayments, new(Atlas), entity, advanceExpense.Id, submitted.Version);
        advance = await repositoryWithAdvancePayments.ApplyAdvanceAsync(new(Atlas), entity, advance.Id, new(advanceExpense.Id, 300m), default);
        Assert.Equal(300m, advance.AppliedAmount);
        advance = await repositoryWithAdvancePayments.ReturnAdvanceAsync(new(Atlas), entity, advance.Id, new(200m, advance.Version), default);
        Assert.Equal("Settled", advance.State);
        var advancePage = await repository.ListAdvancesAsync(new(Atlas), entity, "Settled", employee, false, 1, 10, default);
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
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), approvals: new TestApprovalCapability());
        var advance = await repository.CreateAdvanceAsync(new(Atlas), new(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"), Guid.NewGuid(), "Guarded settlement", 90m), default);
        advance = await repository.TransitionAdvanceAsync(new(Atlas), advance.LegalEntityId, advance.Id, advance.Version, "approve", default);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.TransitionAdvanceAsync(new(Atlas), advance.LegalEntityId, advance.Id, advance.Version, "settle", default));
    }

    [ExpensesPostgresFact]
    public async Task IssuedAdvanceWithPastDueDateIsReturnedByOverdueFilter()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), advancePayments: new TestAdvancePaymentCapability(), approvals: new TestApprovalCapability());
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var advance = await repository.CreateAdvanceAsync(new(Atlas), new(entity, Guid.NewGuid(), "Overdue field visit", 180m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-2))), default);
        advance = await repository.TransitionAdvanceAsync(new(Atlas), entity, advance.Id, advance.Version, "approve", default);
        advance = await repository.IssueAdvanceAsync(new(Atlas), entity, advance.Id, Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), "BankTransfer", 180m, DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date), advance.Version), default);
        Assert.True(advance.IsOverdue);
        var overdue = await repository.ListAdvancesAsync(new(Atlas), entity, null, advance.EmployeeId, true, 1, 20, default);
        Assert.Contains(overdue.Items, item => item.Id == advance.Id && item.IsOverdue && item.SettlementDueDate == DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-2)));
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

    [ExpensesPostgresFact]
    public async Task CorporateCardPendingClearAndRefundAdjustmentsAreIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var expense = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "Card refund", new(2026, 10, 20), 80m, "EGP", "CorporateCard", categoryVersion), default);
        var card = await repository.ImportCardTransactionAsync(new(Atlas), entity, new($"pending-{Guid.CreateVersion7():N}", "Card provider", new(2026, 10, 20), 80m, "EGP", "Pending"), default);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.MatchCardTransactionAsync(new(Atlas), entity, card.Id, new(expense.Id), default));
        card = await repository.ClearCardTransactionAsync(new(Atlas), entity, card.Id, default);
        Assert.Equal("Cleared", card.State);
        card = await repository.MatchCardTransactionAsync(new(Atlas), entity, card.Id, new(expense.Id), default);
        Assert.Equal("Matched", card.State);
        var reference = $"refund-{Guid.CreateVersion7():N}";
        var first = await repository.RecordCardAdjustmentAsync(new(Atlas), entity, card.Id, new("Refund", reference, 30m, "EGP", "Merchant partial refund"), default);
        var retry = await repository.RecordCardAdjustmentAsync(new(Atlas), entity, card.Id, new("Refund", reference, 30m, "EGP", "Merchant partial refund"), default);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal("Matched", (await repository.ListCardTransactionsAsync(new(Atlas), entity, null, card.ProviderReference, 1, 10, default)).Items.Single().State);
        var completed = await repository.RecordCardAdjustmentAsync(new(Atlas), entity, card.Id, new("Refund", $"refund-{Guid.CreateVersion7():N}", 50m, "EGP", "Merchant final refund"), default);
        Assert.Equal(80m, (decimal)(await ScalarAsync(connection, "select coalesce(sum(amount),0) from business_expenses.expense_card_adjustment where tenant_id=$1 and card_transaction_id=$2 and state='Applied'", Atlas, card.Id))!);
        Assert.Equal("Applied", completed.State);
        Assert.Equal("Refunded", (await repository.ListCardTransactionsAsync(new(Atlas), entity, "Refunded", card.ProviderReference, 1, 10, default)).Items.Single().State);
    }

    [ExpensesPostgresFact]
    public async Task EffectiveRatePoliciesDriveReproducibleMileageAndPerDiemAmounts()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var mileagePolicy = await repository.CreateRatePolicyAsync(new(Atlas), new(entity, "Mileage", new(2026, 1, 1), null, "EGP", 7.5m, "Car"), default);
        mileagePolicy = await repository.PublishRatePolicyAsync(new(Atlas), entity, mileagePolicy.Id, new(), default);
        var mileageExpense = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "Mileage policy", new(2026, 10, 20), 1m, "EGP", "EmployeePaid", categoryVersion), default);
        var mileage = await repository.SetMileageDetailAsync(new(Atlas), entity, mileageExpense.Id, new(100m, 0m, "KM", "Car", "Cairo-Alex", mileagePolicy.Id), default);
        Assert.Equal(7.5m, mileage.Rate);
        Assert.Equal(mileagePolicy.Id, mileage.RatePolicyVersionId);
        Assert.Equal(750m, (decimal)(await ScalarAsync(connection, "select amount from business_expenses.expense where tenant_id=$1 and id=$2", Atlas, mileageExpense.Id))!);
        var perDiemPolicy = await repository.CreateRatePolicyAsync(new(Atlas), new(entity, "PerDiem", new(2026, 1, 1), null, "EGP", 100m, null, "CAI"), default);
        perDiemPolicy = await repository.PublishRatePolicyAsync(new(Atlas), entity, perDiemPolicy.Id, new(), default);
        var perDiemExpense = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "Per diem policy", new(2026, 10, 20), 1m, "EGP", "EmployeePaid", categoryVersion), default);
        var perDiem = await repository.SetPerDiemDetailAsync(new(Atlas), entity, perDiemExpense.Id, new(2m, 0m, 1, "CAI", perDiemPolicy.Id), default);
        Assert.Equal(100m, perDiem.DailyRate);
        Assert.Equal(perDiemPolicy.Id, perDiem.RatePolicyVersionId);
        Assert.Equal(166.6667m, perDiem.CalculatedAmount);
    }

    [ExpensesPostgresFact]
    public async Task EvidenceReplacementPreservesPriorReceiptAndAuditHistory()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var expense = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "Receipt replacement", new(2026, 10, 21), 50m, "EGP", "EmployeePaid", categoryVersion), default);
        var first = await repository.AddEvidenceAsync(new(Atlas), entity, expense.Id, new("Receipt", "file://expenses/original"), default);
        var replacement = await repository.AddEvidenceAsync(new(Atlas), entity, expense.Id, new("Receipt", "file://expenses/replacement", first.Id), default);
        Assert.Equal(first.Id, replacement.ReplacesEvidenceId);
        var evidence = await repository.ListEvidenceAsync(new(Atlas), entity, expense.Id, default);
        Assert.Equal(2, evidence.Count);
        Assert.Equal("Replaced", evidence.Single(item => item.Id == first.Id).Status);
        Assert.Equal("Ready", evidence.Single(item => item.Id == replacement.Id).Status);
        Assert.Equal(2, await ScalarLongAsync(connection, "select count(*) from business_expenses.expense_history where tenant_id=$1 and expense_id=$2 and action='evidence.attached'", Atlas, expense.Id));
    }

    [ExpensesPostgresFact]
    public async Task PublishedExpensePolicyVersionIsCapturedInEvaluationSnapshot()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var policy = await repository.CreatePolicyAsync(new(Delta), new(entity, $"TRAVEL-{Guid.CreateVersion7():N}", "Travel policy"), default);
        using var rules = JsonDocument.Parse("{\"maxAmount\":1000,\"receiptRequired\":true}");
        var maximumEffective = await ScalarAsync(connection, "select max(effective_from) from business_expenses.expense_policy_version where tenant_id=$1 and legal_entity_id=$2", Delta, entity);
        var policyDate = maximumEffective switch { DateOnly date => date.AddYears(1), DateTime dateTime => DateOnly.FromDateTime(dateTime).AddYears(1), _ => new DateOnly(2090, 1, 1) };
        var version = await repository.CreatePolicyVersionAsync(new(Delta), entity, policy.Id, new(rules.RootElement.Clone(), policyDate), default);
        version = await repository.PublishPolicyVersionAsync(new(Delta), entity, version.Id, new(), default);
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Delta, entity))!;
        var expenseDate = policyDate.AddMonths(1);
        var expense = await repository.CreateAsync(new(Delta), new(entity, Guid.NewGuid(), "Policy test", expenseDate, 125m, "EGP", "EmployeePaid", categoryVersion), default);
        await repository.AddEvidenceAsync(new(Delta), entity, expense.Id, new("Receipt", "file://expenses/policy-test"), default);
        var evaluation = await repository.EvaluatePolicyAsync(new(Delta), entity, expense.Id, default);
        Assert.Equal("Passed", evaluation.State);
        Assert.Equal(version.Id.ToString("D"), evaluation.Snapshot.GetProperty("policyVersionId").GetString());
        Assert.Equal(1000, evaluation.Snapshot.GetProperty("rules").GetProperty("maxAmount").GetInt32());
        Assert.Equal(1, await ScalarLongAsync(connection, "select count(*) from business_expenses.expense_policy_evaluation where tenant_id=$1 and expense_id=$2 and policy_version_id=$3", Delta, expense.Id, version.Id));
        var overLimit = await repository.CreateAsync(new(Delta), new(entity, Guid.NewGuid(), "Over limit", expenseDate, 1500m, "EGP", "EmployeePaid", categoryVersion), default);
        var blocked = await repository.EvaluatePolicyAsync(new(Delta), entity, overLimit.Id, default);
        Assert.Equal("Blocked", blocked.State);
    }

    [ExpensesPostgresFact]
    public async Task PolicyApplicabilityScopesByEmployeeCategoryAndPaymentSource()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        var employee = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Delta, entity))!;
        var policy = await repository.CreatePolicyAsync(new(Delta), new(entity, $"SCOPED-{Guid.CreateVersion7():N}", "Scoped policy"), default);
        using var rules = JsonDocument.Parse($"{{\"paymentSources\":[\"EmployeePaid\"],\"employeeIds\":[\"{employee:D}\"],\"categoryVersionIds\":[\"{categoryVersion:D}\"],\"maxAmount\":5000}}");
        var policyVersion = await repository.CreatePolicyVersionAsync(new(Delta), entity, policy.Id, new(rules.RootElement.Clone(), new(2095, 1, 1)), default);
        await repository.PublishPolicyVersionAsync(new(Delta), entity, policyVersion.Id, new(), default);
        var matching = await repository.CreateAsync(new(Delta), new(entity, employee, "Scoped match", new(2095, 2, 1), 125m, "EGP", "EmployeePaid", categoryVersion), default);
        var matchingEvaluation = await repository.EvaluatePolicyAsync(new(Delta), entity, matching.Id, default);
        Assert.Equal(policyVersion.Id.ToString("D"), matchingEvaluation.Snapshot.GetProperty("policyVersionId").GetString());
        var otherEmployee = await repository.CreateAsync(new(Delta), new(entity, Guid.CreateVersion7(), "Different employee", new(2095, 2, 1), 125m, "EGP", "EmployeePaid", categoryVersion), default);
        var otherEmployeeEvaluation = await repository.EvaluatePolicyAsync(new(Delta), entity, otherEmployee.Id, default);
        Assert.NotEqual(policyVersion.Id.ToString("D"), otherEmployeeEvaluation.Snapshot.GetProperty("policyVersionId").GetString());
        var otherPaymentSource = await repository.CreateAsync(new(Delta), new(entity, employee, "Different source", new(2095, 2, 1), 125m, "EGP", "CorporateCard", categoryVersion), default);
        var otherPaymentEvaluation = await repository.EvaluatePolicyAsync(new(Delta), entity, otherPaymentSource.Id, default);
        Assert.NotEqual(policyVersion.Id.ToString("D"), otherPaymentEvaluation.Snapshot.GetProperty("policyVersionId").GetString());
    }

    [ExpensesPostgresFact]
    public async Task FinalizationRejectsMissingOrStalePolicyEvaluation()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), approvals: new TestApprovalCapability());
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var policy = await repository.CreatePolicyAsync(new(Delta), new(entity, $"STALE-{Guid.CreateVersion7():N}", "Stale policy"), default);
        using var rules = JsonDocument.Parse("{\"maxAmount\":1000,\"receiptRequired\":true}");
        var policyVersion = await repository.CreatePolicyVersionAsync(new(Delta), entity, policy.Id, new(rules.RootElement.Clone(), new(2031, 1, 1)), default);
        await repository.PublishPolicyVersionAsync(new(Delta), entity, policyVersion.Id, new(), default);
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Delta, entity))!;
        var expense = await repository.CreateAsync(new(Delta), new(entity, Guid.NewGuid(), "Stale policy", new(2031, 10, 16), 125m, "EGP", "EmployeePaid", categoryVersion), default);
        await repository.AddEvidenceAsync(new(Delta), entity, expense.Id, new("Receipt", "file://expenses/stale"), default);
        await repository.EvaluatePolicyAsync(new(Delta), entity, expense.Id, default);
        var submitted = await repository.TransitionAsync(new(Delta), entity, expense.Id, new(expense.Version), "submit", default);
        var approvedVersion = await ApproveAsync(repository, new(Delta), entity, expense.Id, submitted.Version);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.TransitionAsync(new(Delta), entity, expense.Id, new(approvedVersion), "finalize", default));
    }

    [ExpensesPostgresFact]
    public async Task PlatformApprovalLifecycleRequiresAuthorizedDifferentActorAndFinalizes()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledger = new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var approvals = new NpgsqlPlatformApprovalCapability(dataSource, TimeProvider.System);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(ledger), approvals: approvals);
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var requester = await ReadUserAsync(connection, "admin");
        var decider = await ReadUserAsync(connection, "office");
        await using (var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,a.role_id,p.permission_code from platform.membership_role_assignment a cross join unnest(array['expenses.approve','expenses.submit']::text[]) p(permission_code) where a.tenant_id=$1 and a.membership_id=any($2::uuid[]) on conflict do nothing", connection))
        { grant.Parameters.AddWithValue(Delta); grant.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid, new[] { requester.MembershipId, decider.MembershipId }); await grant.ExecuteNonQueryAsync(); }
        var policy = await repository.CreatePolicyAsync(new(Delta), new(entity, $"PLATFORM-{Guid.CreateVersion7():N}", "Platform approval policy"), default);
        using var rules = JsonDocument.Parse("{\"maxAmount\":1000,\"receiptRequired\":true}");
        var policyVersion = await repository.CreatePolicyVersionAsync(new(Delta), entity, policy.Id, new(rules.RootElement.Clone(), new(2026, 1, 1)), default);
        await repository.PublishPolicyVersionAsync(new(Delta), entity, policyVersion.Id, new(), default);
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Delta, entity))!;
        var expense = await repository.CreateAsync(new(Delta), new(entity, Guid.NewGuid(), "Platform approval", new(2026, 10, 20), 125m, "EGP", "EmployeePaid", categoryVersion), default);
        await repository.AddEvidenceAsync(new(Delta), entity, expense.Id, new("Receipt", "file://expenses/platform-approval"), default);
        var submitted = await repository.TransitionAsync(new(Delta), entity, expense.Id, new(expense.Version), "submit", default);
        var evaluation = await repository.EvaluatePolicyAsync(new(Delta), entity, expense.Id, default);
        Assert.Equal("Passed", evaluation.State);
        var pending = await repository.RequestApprovalAsync(new(Delta), requester.IdentityId, requester.MembershipId, entity, expense.Id, "Manager review", default);
        Assert.Equal("Pending", pending.State);
        var pendingRetry = await repository.RequestApprovalAsync(new(Delta), requester.IdentityId, requester.MembershipId, entity, expense.Id, "Manager review retry", default);
        Assert.Equal(pending.RequestId, pendingRetry.RequestId);
        Assert.Equal("Pending", pendingRetry.State);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.DecideApprovalAsync(new(Delta), decider.IdentityId, decider.MembershipId, entity, Guid.CreateVersion7(), new(pending.RequestId, "Approved", "Wrong expense"), default));
        Assert.Equal("Pending", (await approvals.GetAsync(new(Delta), pending.RequestId, default))!.State);
        var approved = await repository.DecideApprovalAsync(new(Delta), decider.IdentityId, decider.MembershipId, entity, expense.Id, new(pending.RequestId, "Approved", "Reviewed by Finance"), default);
        Assert.Equal("Approved", approved.State);
        var finalized = await repository.TransitionAsync(new(Delta), entity, expense.Id, new(submitted.Version + 1), "finalize", default);
        Assert.Equal("Posted", finalized.AccountingState);
        Assert.Equal(1, await ScalarLongAsync(connection, "select count(*) from platform.approval_request where tenant_id=$1 and id=$2 and state='Approved' and decided_by=$3", Delta, pending.RequestId, decider.IdentityId));
    }

    [ExpensesPostgresFact]
    public async Task LegacyApproveTransitionCannotBypassPlatformApprovalWorkflow()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var approvals = new NpgsqlPlatformApprovalCapability(dataSource, TimeProvider.System);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), approvals: approvals);
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Delta, entity))!;
        var expense = await repository.CreateAsync(new(Delta), new(entity, null, "Legacy approval guard", new(2026, 11, 1), 25m, "EGP", "CompanyPaid", categoryVersion), default);
        await repository.AddEvidenceAsync(new(Delta), entity, expense.Id, new("Receipt", "file://expenses/legacy-approval-guard"), default);
        var submitted = await repository.TransitionAsync(new(Delta), entity, expense.Id, new(expense.Version), "submit", default);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.TransitionAsync(new(Delta), entity, expense.Id, new(submitted.Version), "approve", default));
        Assert.Equal("Pending", (await repository.GetAsync(new(Delta), entity, expense.Id, default))!.ApprovalState);
    }

    [ExpensesPostgresFact]
    public async Task ApprovalFailsClosedWhenPlatformCapabilityIsUnavailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)));
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Delta, entity))!;
        var expense = await repository.CreateAsync(new(Delta), new(entity, null, "No approval capability", new(2026, 11, 2), 20m, "EGP", "CompanyPaid", categoryVersion), default);
        await repository.AddEvidenceAsync(new(Delta), entity, expense.Id, new("Receipt", "file://expenses/no-approval-capability"), default);
        var submitted = await repository.TransitionAsync(new(Delta), entity, expense.Id, new(expense.Version), "submit", default);
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.TransitionAsync(new(Delta), entity, expense.Id, new(submitted.Version), "approve", default));
        await Assert.ThrowsAsync<ExpenseRepositoryException>(() => repository.TransitionAsync(new(Delta), entity, expense.Id, new(submitted.Version), "reject", default));
        Assert.Equal("Pending", (await repository.GetAsync(new(Delta), entity, expense.Id, default))!.ApprovalState);
    }

    [ExpensesPostgresFact]
    public async Task ApprovalRequesterNeedsSubmitPermissionButCannotDecide()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Delta.ToString("D")); await context.ExecuteNonQueryAsync();
        var office = await ReadUserAsync(connection, "office");
        var entity = Guid.Parse("bbdcc188-1acc-805e-8721-f43308540a4d");
        await using var permission = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,a.role_id,'expenses.submit' from platform.membership_role_assignment a where a.tenant_id=$1 and a.membership_id=$2 on conflict do nothing", connection);
        permission.Parameters.AddWithValue(Delta); permission.Parameters.AddWithValue(office.MembershipId); await permission.ExecuteNonQueryAsync();
        var approvals = new NpgsqlPlatformApprovalCapability(dataSource, TimeProvider.System);
        var request = await approvals.RequestAsync(new(Delta), office.IdentityId, office.MembershipId, entity, "expenses", "expense", Guid.CreateVersion7(), 1, null, "Employee submission", "expenses.submit", default);
        Assert.Equal("Pending", request.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.DecideAsync(new(Delta), office.IdentityId, office.MembershipId, request.Id, "Approved", "Self approval", default));
        Assert.Equal("Pending", (await approvals.GetAsync(new(Delta), request.Id, default))!.State);
    }

    [ExpensesPostgresFact]
    public async Task OcrProviderFailureIsDurableRetryableAndSupportsManualFallback()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_EXPENSES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var repository = new ExpenseRepository(dataSource, coordinator, new LedgerTransactionCapability(new LedgerRepository(dataSource, coordinator, new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System)), approvals: new TestApprovalCapability(), ocrProvider: new FailingOcrProvider());
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(Atlas.ToString("D")); await context.ExecuteNonQueryAsync();
        var categoryVersion = (Guid)(await ScalarAsync(connection, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", Atlas, entity))!;
        var expense = await repository.CreateAsync(new(Atlas), new(entity, Guid.NewGuid(), "OCR receipt", new(2026, 10, 22), 55m, "EGP", "EmployeePaid", categoryVersion), default);
        var evidenceId = Guid.CreateVersion7(); var fileRef = Guid.CreateVersion7();
        await using (var insert = new NpgsqlCommand("insert into business_expenses.expense_evidence(tenant_id,id,legal_entity_id,expense_id,evidence_type,file_reference,status) values($1,$2,$3,$4,'Receipt',$5,'Ready')", connection))
        { insert.Parameters.AddWithValue(Atlas); insert.Parameters.AddWithValue(evidenceId); insert.Parameters.AddWithValue(entity); insert.Parameters.AddWithValue(expense.Id); insert.Parameters.AddWithValue(fileRef.ToString("D")); await insert.ExecuteNonQueryAsync(); }
        var job = await repository.RequestOcrAsync(new(Atlas), entity, expense.Id, evidenceId, default);
        Assert.Equal("Requested", job.State);
        await repository.ProcessOcrJobAsync(new(Atlas), entity, expense.Id, job.Id, evidenceId, default);
        var failed = (await repository.GetOcrAsync(new(Atlas), entity, expense.Id, job.Id, default))!;
        Assert.Equal("Failed", failed.State);
        Assert.Equal("expenses.ocr_provider_unavailable", failed.FailureCode);
        var retried = await repository.RetryOcrAsync(new(Atlas), entity, expense.Id, job.Id, default);
        Assert.Equal("Requested", retried.State);
        using var suggestions = JsonDocument.Parse("{\"merchantName\":\"Manual merchant\",\"amount\":55}");
        var manual = await repository.CompleteOcrManuallyAsync(new(Atlas), entity, expense.Id, job.Id, new(suggestions.RootElement.Clone(), "Provider unavailable; reviewed by Finance"), default);
        Assert.Equal("Manual", manual.State);
        Assert.Equal("Manual merchant", manual.Suggestions!.Value.GetProperty("merchantName").GetString());
        Assert.Equal(1, await ScalarLongAsync(connection, "select count(*) from business_expenses.expense_history where tenant_id=$1 and expense_id=$2 and action='ocr.manual'", Atlas, expense.Id));
    }

    private static async Task<(Guid IdentityId, Guid MembershipId)> ReadUserAsync(NpgsqlConnection connection, string prefix)
    {
        await using var command = new NpgsqlCommand("select i.id,m.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id where i.tenant_id=$1 and i.login_prefix=$2 and m.status='Active'", connection);
        command.Parameters.AddWithValue(Delta); command.Parameters.AddWithValue(prefix);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    { await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); return await command.ExecuteScalarAsync(); }
    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, params object[] values)
    { return Convert.ToInt64(await ScalarAsync(connection, sql, values), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<(long EntryCount, decimal Debit, decimal Credit)> ReadPostingAsync(NpgsqlConnection connection, Guid expenseId)
    {
        await using var command = new NpgsqlCommand("select count(distinct e.id),coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from business_expenses.expense x join finance_ledger.journal_entry e on e.tenant_id=x.tenant_id and e.id=x.ledger_entry_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.journal_entry_id=e.id where x.tenant_id=$1 and x.id=$2", connection); command.Parameters.AddWithValue(Atlas); command.Parameters.AddWithValue(expenseId); await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); return (reader.GetInt64(0), reader.GetDecimal(1), reader.GetDecimal(2));
    }

    private static async Task<long> ApproveAsync(ExpenseRepository repository, TenantId tenant, Guid entity, Guid expenseId, long submittedVersion)
    {
        var requester = Guid.CreateVersion7();
        var requesterMembership = Guid.CreateVersion7();
        var decider = Guid.CreateVersion7();
        var deciderMembership = Guid.CreateVersion7();
        var pending = await repository.RequestApprovalAsync(tenant, requester, requesterMembership, entity, expenseId, "Test request", default);
        var decision = await repository.DecideApprovalAsync(tenant, decider, deciderMembership, entity, expenseId, new(pending.RequestId, "Approved", "Test decision"), default);
        Assert.Equal(submittedVersion, decision.ResourceVersion);
        return decision.ResourceVersion + 1;
    }

    private sealed class TestApprovalCapability : IPlatformApprovalCapability
    {
        private readonly Dictionary<Guid, PlatformApprovalRequestResult> requests = [];

        public Task<PlatformApprovalRequestResult> RequestAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid legalEntityId, string ownerModule, string ownerEntityType, Guid ownerEntityId, long resourceVersion, Guid? policyVersionId, string? reason, string requesterPermission = "platform.approval.request", CancellationToken cancellationToken = default)
        {
            var result = new PlatformApprovalRequestResult(Guid.CreateVersion7(), legalEntityId, ownerModule, ownerEntityType, ownerEntityId, resourceVersion, policyVersionId, "Pending", null, reason, DateTimeOffset.UtcNow, null);
            requests[result.Id] = result;
            return Task.FromResult(result);
        }

        public Task<PlatformApprovalRequestResult> DecideAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid requestId, string decision, string? reason, CancellationToken cancellationToken = default)
        {
            var current = requests[requestId];
            var result = current with { State = decision, DecidedBy = actorId, DecisionReason = reason, DecidedAt = DateTimeOffset.UtcNow };
            requests[requestId] = result;
            return Task.FromResult(result);
        }

        public Task<PlatformApprovalRequestResult?> GetAsync(TenantId tenantId, Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult(requests.GetValueOrDefault(requestId));
    }

    private sealed class FailingOcrProvider : IExpenseOcrProvider
    {
        public Task<ExpenseOcrResult> ExtractAsync(TenantId tenant, Guid fileReference, CancellationToken cancellationToken = default) =>
            throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", "test provider unavailable");
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
