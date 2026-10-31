using Modules.Accounting.DTOs;
using Modules.Accounting.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using System.Globalization;
using Xunit;

namespace Modules.Accounting.Tests;

public sealed class AccountingPostgresIntegrationTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact]
    public async Task StatementDefinitionPersistsConstrainedFormulaAndRejectsCycles()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var request = new FinancialStatementDefinitionRequest("profit-and-loss", "ProfitAndLoss",
        [
            new("revenue", "الإيرادات", "Revenue", 10, "Revenue"),
            new("expense", "المصروفات", "Expenses", 20, "Expense"),
            new("net-profit", "صافي الربح", "Net profit", 30, null, null, "Arithmetic", null, "Subtract", ["revenue", "expense"])
        ], new DateOnly(2026, 1, 1));

        var saved = await repository.SaveStatementDefinitionAsync(AtlasTenant, AtlasEntity, request, Guid.CreateVersion7(), default);
        var loaded = await repository.GetStatementDefinitionAsync(AtlasTenant, AtlasEntity, "ProfitAndLoss", new DateOnly(2026, 1, 15), default);

        Assert.NotNull(loaded);
        Assert.Equal(saved.Version, loaded!.Version);
        var formula = Assert.Single(loaded.Lines, line => line.Code == "net-profit");
        Assert.Equal("Arithmetic", formula.LineKind);
        Assert.Equal("Subtract", formula.ArithmeticOperator);
        Assert.Equal(["revenue", "expense"], formula.FormulaLineCodes);

        var cycle = request with { Lines = [
            new("a", "أ", "A", 10, null, null, "Arithmetic", null, "Add", ["b", "c"]),
            new("b", "ب", "B", 20, null, null, "Arithmetic", null, "Add", ["a", "c"]),
            new("c", "ج", "C", 30, "Revenue") ] };
        var exception = await Assert.ThrowsAsync<AccountingRepositoryException>(() => repository.SaveStatementDefinitionAsync(AtlasTenant, AtlasEntity, cycle, Guid.CreateVersion7(), default));
        Assert.Equal("accounting.statement_formula_cycle", exception.Code);
    }

    [Fact]
    public async Task CloseEvidenceIsIdempotentAndPooledConnectionDoesNotLeakTenantContext()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var periodId = Guid.CreateVersion7();
        var key = "accounting-close-" + Guid.CreateVersion7().ToString("N");
        var hash = new string('a', 64);
        var checks = new[] { new CloseCheckView("ledger.period", "Info", "Pass", "Ready") };

        var first = await repository.SaveCloseRunAsync(AtlasTenant, AtlasEntity, periodId, key, hash, "Closed", "test", Guid.CreateVersion7(), checks, default);
        var retry = await repository.SaveCloseRunAsync(AtlasTenant, AtlasEntity, periodId, key, hash, "Closed", "test", Guid.CreateVersion7(), checks, default);
        var regression = await repository.SaveCloseRunAsync(AtlasTenant, AtlasEntity, periodId, key, hash, "Previewed", "test", Guid.CreateVersion7(), [new CloseCheckView("ledger.period", "Blocking", "Fail", "stale")], default);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal("Closed", regression.Status);
        Assert.Equal("Pass", Assert.Single(regression.Checks).Status);
        var history = await repository.GetCloseRunAsync(AtlasTenant, AtlasEntity, periodId, default);
        Assert.NotNull(history);
        Assert.Equal("Closed", history!.Status);
        Assert.Empty(await repository.ListCloseRunIssuesAsync(AtlasTenant, AtlasEntity, periodId, default));
        await using var tenantConnection = await source.OpenConnectionAsync();
        await SetTenantAsync(tenantConnection, AtlasTenant);
        Assert.Equal(1L, await ScalarAsync(tenantConnection, "select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2 and message_type='AccountingCloseCompletedV1'", AtlasTenant.Value, first.Id));

        await tenantConnection.CloseAsync();
        await using var noContextConnection = await source.OpenConnectionAsync();
        Assert.Equal(0L, await ScalarAsync(noContextConnection, "select count(*) from business_accounting.close_run"));
    }

    [Fact]
    public async Task ReconciliationRunPersistsItsOwnOutboxEvidence()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var correlation = Guid.CreateVersion7();
        var saved = await repository.SaveReconciliationAsync(AtlasTenant, AtlasEntity, "AR", new DateOnly(2026, 1, 31), 100m, 100m, 0m, "EGP", "Reconciled", [], Guid.CreateVersion7(), correlation, default);

        await using var connection = await source.OpenConnectionAsync();
        await SetTenantAsync(connection, AtlasTenant);
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2 and message_type='AccountingReconciliationCompletedV1'", AtlasTenant.Value, saved.Id));
    }

    [Fact]
    public async Task ReconciliationDifferencePersistsQueryableIssueHistory()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var saved = await repository.SaveReconciliationAsync(AtlasTenant, AtlasEntity, "AP", new DateOnly(2026, 2, 1), 125m, 100m, 25m, "EGP", "Difference", ["control_balance_difference"], Guid.CreateVersion7(), Guid.CreateVersion7(), default);

        var history = await repository.ListReconciliationsAsync(AtlasTenant, AtlasEntity, "AP", new DateOnly(2026, 2, 1), 10, default);
        var issues = await repository.ListReconciliationIssuesAsync(AtlasTenant, AtlasEntity, "Open", 10, default);
        Assert.Contains(history, item => item.Id == saved.Id && item.Status == "Difference");
        Assert.Contains(issues, item => item.ReconciliationRunId == saved.Id && item.Code == "control_balance_difference" && item.Severity == "Blocking");
    }

    [Fact]
    public async Task FxRevaluationPreviewIsIdempotentAndPreservesItsImmutableMeasurement()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var request = new FxRevaluationRequest(new DateOnly(2026, 10, 30), "fx-preview-" + Guid.CreateVersion7().ToString("N"), new DateOnly(2026, 11, 1));
        var line = new FxRevaluationLineDraft(Guid.CreateVersion7(), Guid.CreateVersion7(), 7, "Receivable", "USD", 100m, "EGP", 5000m, 5100m, 100m, "FXGain", 51m,
            "{\"rate\":51,\"fromCurrency\":\"USD\",\"toCurrency\":\"EGP\"}");
        var hash = new string('b', 64);

        var first = await repository.SaveFxRevaluationPreviewAsync(AtlasTenant, AtlasEntity, request, hash, [line], Guid.CreateVersion7(), default);
        var retry = await repository.SaveFxRevaluationPreviewAsync(AtlasTenant, AtlasEntity, request, hash, [line], Guid.CreateVersion7(), default);

        Assert.Equal(first.Id, retry.Id);
        var stored = Assert.Single(retry.Lines);
        Assert.Equal(100m, stored.DifferenceAmount);
        Assert.Equal("FXGain", stored.DifferenceRole);
        Assert.Contains("USD", stored.ExchangeRateSnapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoLiveApprovalPersistsApprovalAndOutboxEvidence()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var openingBatchId = Guid.CreateVersion7();
        var correlation = Guid.CreateVersion7();
        var checks = new[] { new CloseCheckView("opening.ledger", "Info", "Pass", "Posted and ready to lock.") };

        var approved = await repository.ApproveGoLiveAsync(AtlasTenant, AtlasEntity, openingBatchId, null, new DateOnly(2026, 1, 1), "go-live-test-" + Guid.CreateVersion7().ToString("N"), new string('c', 64), "migration accepted", Guid.CreateVersion7(), correlation, checks, default);

        Assert.Equal("Approved", approved.Status);
        await using var connection = await source.OpenConnectionAsync();
        await SetTenantAsync(connection, AtlasTenant);
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and correlation_id=$2 and message_type='AccountingGoLiveApprovedV1'", AtlasTenant.Value, correlation));
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from business_accounting.accounting_setup where tenant_id=$1 and legal_entity_id=$2 and go_live_status='Approved'", AtlasTenant.Value, AtlasEntity));
    }

    [Fact]
    public async Task GoLiveApprovalRetryIsIdempotentAndDoesNotDuplicateOutbox()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var openingBatchId = Guid.CreateVersion7();
        var key = "go-live-retry-" + Guid.CreateVersion7().ToString("N");
        var correlation = Guid.CreateVersion7();
        var checks = new[] { new CloseCheckView("opening.ledger", "Info", "Pass", "Ready") };

        var first = await repository.ApproveGoLiveAsync(AtlasTenant, AtlasEntity, openingBatchId, null, new DateOnly(2026, 1, 1), key, new string('d', 64), "migration accepted", Guid.CreateVersion7(), correlation, checks, default);
        var retry = await repository.ApproveGoLiveAsync(AtlasTenant, AtlasEntity, openingBatchId, null, new DateOnly(2026, 1, 1), key, new string('d', 64), "migration accepted", Guid.CreateVersion7(), Guid.CreateVersion7(), checks, default);

        Assert.Equal(first.OpeningBatchId, retry.OpeningBatchId);
        await using var connection = await source.OpenConnectionAsync();
        await SetTenantAsync(connection, AtlasTenant);
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from business_accounting.go_live_approval where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", AtlasTenant.Value, AtlasEntity, key));
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='AccountingGoLiveApprovedV1' and payload->>'openingBatchId'=$2", AtlasTenant.Value, openingBatchId.ToString()));
    }

    [Fact]
    public async Task ConcurrentGoLiveApprovalRequestsConvergeToOneApprovalAndEvent()
    {
        await using var source = NpgsqlDataSource.Create(RequireConnection());
        var repository = new AccountingRepository(source, TimeProvider.System);
        var openingBatchId = Guid.CreateVersion7();
        var key = "go-live-concurrent-" + Guid.CreateVersion7().ToString("N");
        var checks = new[] { new CloseCheckView("opening.ledger", "Info", "Pass", "Ready") };
        var calls = Enumerable.Range(0, 2).Select(index => repository.ApproveGoLiveAsync(
            AtlasTenant, AtlasEntity, openingBatchId, null, new DateOnly(2026, 1, 1), key, new string('e', 64),
            "migration accepted", Guid.CreateVersion7(), Guid.CreateVersion7(), checks, default));

        var approvals = await Task.WhenAll(calls);

        Assert.All(approvals, approval => Assert.Equal(openingBatchId, approval.OpeningBatchId));
        await using var connection = await source.OpenConnectionAsync();
        await SetTenantAsync(connection, AtlasTenant);
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from business_accounting.go_live_approval where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", AtlasTenant.Value, AtlasEntity, key));
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='AccountingGoLiveApprovedV1' and payload->>'idempotencyKey'=$2", AtlasTenant.Value, key));
    }

    private static string RequireConnection() => Environment.GetEnvironmentVariable("TODIR_ACCOUNTING_TEST_CONNECTION") is { Length: > 0 } value
        ? value : throw new InvalidOperationException("Set TODIR_ACCOUNTING_TEST_CONNECTION for Accounting PostgreSQL integration tests.");
    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
}
