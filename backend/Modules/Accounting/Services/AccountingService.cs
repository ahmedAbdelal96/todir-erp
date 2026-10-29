using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modules.Accounting.DTOs;
using Modules.Accounting.Repositories;
using Shared.Contracts.Accounting;
using Shared.Contracts.Ledger;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Modules.Accounting.Services;

public sealed class AccountingService(
    AccountingRepository repository,
    ILedgerAccountingWorkspaceCapabilities ledgerWorkspace,
    ILedgerPostingEngine ledgerPosting,
    ILedgerControlBalanceRead ledgerControlBalances,
    ISubledgerCapabilities subledger,
    IInventoryAccountingRead? inventory)
{
    public async Task<AccountingHealthView> GetHealthAsync(TenantId tenant, Guid membershipId, Guid entity, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct)
    {
        var ledger = await ledgerWorkspace.GetReadinessAsync(tenant, new(entity), ct);
        var items = new List<AccountingHealthItem> { new("ledger.readiness", ledger.State == "Ready" ? "Info" : "Blocking", ledger.State, ledger.BlockingReasons.Count == 0 ? "Ledger configuration is ready." : string.Join(", ", ledger.BlockingReasons), "ledger") };
        foreach (var reconciliation in await ReconcileAllAsync(tenant, membershipId, entity, asOf, actor, correlation, ct))
            items.Add(new($"reconciliation.{reconciliation.Type.ToLowerInvariant()}", reconciliation.Status == "Reconciled" ? "Info" : reconciliation.Status is "NotReady" or "NotApplicable" ? "Attention" : "Blocking", reconciliation.Status, string.Join(", ", reconciliation.Diagnostics.DefaultIfEmpty("No differences found.")), reconciliation.Type));
        var status = items.Any(item => item.Severity == "Blocking") ? "Blocking" : items.Any(item => item.Severity == "Attention") ? "Attention" : "Healthy";
        return new(entity, asOf, status, items);
    }

    public Task<LedgerTrialBalanceSnapshot> GetTrialBalanceAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, CancellationToken ct) => ledgerWorkspace.GetTrialBalanceAsync(tenant, new(entity), from, to, ct);
    public Task<LedgerGeneralLedgerPage> GetGeneralLedgerAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, Guid? accountId, string? cursor, int limit, CancellationToken ct) => ledgerWorkspace.GetGeneralLedgerAsync(tenant, new(entity), from, to, accountId, cursor, Math.Clamp(limit, 1, 200), ct);

    public async Task<ReconciliationView> ReconcileAsync(TenantId tenant, Guid membershipId, Guid entity, string type, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct)
    {
        if (type is "AR" or "AP")
        {
            var direction = type == "AR" ? SubledgerDirectionContract.Receivable : SubledgerDirectionContract.Payable;
            var result = await subledger.ReconcileControlBalancesAsync(tenant, membershipId, new(new(entity), asOf), actor, correlation, ct);
            var details = result.Details.Where(detail => detail.Direction == direction).ToArray();
            var sourceBalance = details.Sum(detail => decimal.Parse(detail.SubledgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
            var ledgerBalance = details.Sum(detail => decimal.Parse(detail.LedgerBaseBalance, System.Globalization.CultureInfo.InvariantCulture));
            var difference = sourceBalance - ledgerBalance;
            var diagnostics = details.Where(detail => !detail.ContextResolved || decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture) != 0m)
                .Select(detail => !detail.ContextResolved ? "control_context_missing" : "control_balance_difference").Distinct().ToArray();
            return await repository.SaveReconciliationAsync(tenant, entity, type, asOf, sourceBalance, ledgerBalance, difference, result.BaseCurrencyCode, difference == 0m && details.All(detail => detail.ContextResolved) ? "Reconciled" : "Difference", diagnostics, actor, correlation, ct);
        }
        if (type == "Inventory")
        {
            if (inventory is null) return await repository.SaveReconciliationAsync(tenant, entity, type, asOf, null, null, null, null, "NotApplicable", ["inventory_capability_unavailable"], actor, correlation, ct);
            var result = await inventory.ReconcileInventoryAsync(tenant, new(entity), asOf, ct);
            return await repository.SaveReconciliationAsync(tenant, entity, type, asOf, result.InventoryValue, result.LedgerValue, result.Difference, result.CurrencyCode, result.Available ? result.Status : "NotReady", result.Diagnostics, actor, correlation, ct);
        }
        throw new AccountingException("accounting.reconciliation_type_invalid");
    }

    public async Task<CloseRunView> PreviewCloseAsync(TenantId tenant, Guid membershipId, Guid entity, ClosePreviewRequest request, Guid actor, Guid correlation, CancellationToken ct)
    {
        var period = await ledgerWorkspace.GetPeriodAsync(tenant, new(entity), request.PeriodId, ct) ?? throw new AccountingException("accounting.period_not_found");
        var closeReadiness = await ledgerWorkspace.GetCloseReadinessAsync(tenant, new(entity), request.PeriodId, ct);
        var checks = new List<CloseCheckView> { new("ledger.period", closeReadiness.CanClose ? "Info" : "Blocking", closeReadiness.CanClose ? "Pass" : "Fail", closeReadiness.CanClose ? "Ledger permits period close." : string.Join(", ", closeReadiness.Blockers)) };
        foreach (var reconciliation in await ReconcileAllAsync(tenant, membershipId, entity, period.EndDate, actor, correlation, ct))
            checks.Add(new($"reconciliation.{reconciliation.Type.ToLowerInvariant()}", reconciliation.Status == "Reconciled" || reconciliation.Status == "NotApplicable" ? "Info" : "Blocking", reconciliation.Status == "Reconciled" || reconciliation.Status == "NotApplicable" ? "Pass" : "Fail", string.Join(", ", reconciliation.Diagnostics.DefaultIfEmpty(reconciliation.Status))));
        var status = checks.Any(check => check.Status == "Fail") ? "Blocked" : "Previewed";
        return await repository.SaveCloseRunAsync(tenant, entity, request.PeriodId, request.IdempotencyKey, Hash(new { request.PeriodId, request.Reason }), status, request.Reason, actor, checks, ct);
    }

    public async Task<CloseRunView> CloseAsync(TenantId tenant, Guid membershipId, Guid entity, ClosePreviewRequest request, Guid actor, Guid correlation, CancellationToken ct)
    {
        var preview = await PreviewCloseAsync(tenant, membershipId, entity, request, actor, correlation, ct);
        if (preview.Status == "Blocked") throw new AccountingException("accounting.close_blocked");
        await ledgerWorkspace.SetPeriodStatusAsync(tenant, new(entity), request.PeriodId, "Closed", actor, correlation, ct);
        return await repository.SaveCloseRunAsync(tenant, entity, request.PeriodId, request.IdempotencyKey, Hash(new { request.PeriodId, request.Reason }), "Closed", request.Reason, actor, preview.Checks, ct);
    }

    public async Task<CloseRunView> ReopenAsync(TenantId tenant, Guid entity, Guid periodId, string reason, string idempotencyKey, Guid actor, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AccountingException("accounting.reopen_reason_required");
        await ledgerWorkspace.SetPeriodStatusAsync(tenant, new(entity), periodId, "Open", actor, correlation, ct);
        var checks = new[] { new CloseCheckView("ledger.period", "Warning", "Pass", "Period reopened with explicit reason: " + reason.Trim()) };
        return await repository.SaveCloseRunAsync(tenant, entity, periodId, idempotencyKey, Hash(new { periodId, reason }), "Reopened", reason.Trim(), actor, checks, ct);
    }

    public async Task<FinancialStatementView> GetStatementAsync(TenantId tenant, Guid entity, string kind, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (kind is not ("ProfitAndLoss" or "BalanceSheet")) throw new AccountingException("accounting.statement_kind_invalid");
        var trial = await ledgerWorkspace.GetTrialBalanceAsync(tenant, new(entity), from, to, ct);
        var types = kind == "ProfitAndLoss" ? new[] { "Revenue", "Expense" } : new[] { "Asset", "Liability", "Equity" };
        var lines = trial.Accounts.Where(account => types.Contains(account.AccountType, StringComparer.Ordinal)).GroupBy(account => account.AccountType).Select(group =>
        {
            var amount = group.Key is "Liability" or "Equity" or "Revenue" ? -group.Sum(account => account.SignedBalance) : group.Sum(account => account.SignedBalance);
            return new StatementLineView(group.Key, group.Key, group.Key, amount, group.Select(account => account.AccountId).ToArray());
        }).OrderBy(line => line.Code).ToArray();
        var total = lines.Sum(line => line.Amount);
        var assets = lines.SingleOrDefault(line => line.AccountType == "Asset")?.Amount ?? 0m;
        var liabilities = lines.SingleOrDefault(line => line.AccountType == "Liability")?.Amount ?? 0m;
        var equity = lines.SingleOrDefault(line => line.AccountType == "Equity")?.Amount ?? 0m;
        var balanced = kind != "BalanceSheet" || assets == liabilities + equity;
        return new(kind, from, to, trial.BaseCurrencyCode, total, lines, balanced, balanced ? null : "accounting.balance_sheet_inequality");
    }

    public async Task<OwnerFinancialSummary> GetOwnerSummaryAsync(TenantId tenant, Guid membershipId, Guid entity, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct)
    {
        var ar = await ReconcileAsync(tenant, membershipId, entity, "AR", asOf, actor, correlation, ct);
        var ap = await ReconcileAsync(tenant, membershipId, entity, "AP", asOf, actor, correlation, ct);
        var inventoryResult = await ReconcileAsync(tenant, membershipId, entity, "Inventory", asOf, actor, correlation, ct);
        var configuration = await ledgerPosting.GetActiveConfigurationVersionIdAsync(tenant, new(entity), ct);
        var cash = await ledgerControlBalances.GetControlBalanceAsync(tenant, new(entity), "Cash", configuration, asOf, ct);
        var pnl = await GetStatementAsync(tenant, entity, "ProfitAndLoss", new DateOnly(asOf.Year, 1, 1), asOf, ct);
        var health = new[] { ar.Status, ap.Status, inventoryResult.Status }.Any(status => status == "Difference") ? "Attention" : "Healthy";
        return new(entity, asOf, cash.BaseCurrencyCode, cash.SignedBaseBalance, ar.SourceBalance ?? 0m, ap.SourceBalance ?? 0m, inventoryResult.SourceBalance, pnl.Total, health);
    }

    private async Task<IReadOnlyList<ReconciliationView>> ReconcileAllAsync(TenantId tenant, Guid membership, Guid entity, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct) => [await ReconcileAsync(tenant, membership, entity, "AR", asOf, actor, correlation, ct), await ReconcileAsync(tenant, membership, entity, "AP", asOf, actor, correlation, ct), await ReconcileAsync(tenant, membership, entity, "Inventory", asOf, actor, correlation, ct)];
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
}

public sealed class AccountingException(string code) : Exception(code) { public string Code { get; } = code; }
