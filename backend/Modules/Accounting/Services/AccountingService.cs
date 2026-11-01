using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Modules.Accounting.DTOs;
using Modules.Accounting.Repositories;
using Shared.Contracts.Accounting;
using Shared.Contracts.FinanceBase;
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
    IFinanceBaseCapabilities financeBase,
    IInventoryAccountingRead? inventory,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<AccountingHealthView> GetHealthAsync(TenantId tenant, Guid membershipId, Guid entity, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct)
    {
        var ledger = await ledgerWorkspace.GetReadinessAsync(tenant, new(entity), ct);
        var items = new List<AccountingHealthItem> { new("ledger.readiness", ledger.State == "Ready" ? "Info" : "Blocking", ledger.State, ledger.BlockingReasons.Count == 0 ? "Ledger configuration is ready." : string.Join(", ", ledger.BlockingReasons), "ledger") };
        var setup = await repository.GetSetupAsync(tenant, entity, ct);
        items.Add(setup is null
            ? new AccountingHealthItem("accounting.setup", "Blocking", "Missing", "Accounting setup is required before financial go-live.", "accounting")
            : new AccountingHealthItem("accounting.go_live", setup.GoLiveStatus is "Ready" or "Approved" ? "Info" : "Blocking", setup.GoLiveStatus, $"Accounting is running in {setup.Mode} mode.", "accounting"));
        var definitions = new Dictionary<string, FinancialStatementDefinitionView>(StringComparer.Ordinal);
        foreach (var kind in new[] { "ProfitAndLoss", "BalanceSheet" })
        {
            var definition = await repository.GetStatementDefinitionAsync(tenant, entity, kind, asOf, ct);
            items.Add(definition is null
                ? new AccountingHealthItem($"statement.{kind}", "Blocking", "Missing", "A published financial statement definition is required.", "accounting")
                : new AccountingHealthItem($"statement.{kind}", "Info", "Ready", $"Statement definition {definition.Code} version {definition.Version} is active.", "accounting"));
            if (definition is not null) definitions.Add(kind, definition);
        }
        if (definitions.Count == 2)
        {
            var trial = await ledgerWorkspace.GetTrialBalanceAsync(tenant, new(entity), null, asOf, ct);
            var unmapped = trial.Accounts.Where(account => HasMovement(account) && !IsMapped(account, definitions.Values)).Select(account => account.AccountCode).ToArray();
            items.Add(unmapped.Length == 0
                ? new AccountingHealthItem("statement.unmapped_accounts", "Info", "Ready", "Every posted account is mapped to its required financial statement.", "accounting")
                : new AccountingHealthItem("statement.unmapped_accounts", "Blocking", "Unmapped", string.Join(", ", unmapped), "accounting"));
        }
        foreach (var reconciliation in await ReconcileAllReadOnlyAsync(tenant, membershipId, entity, asOf, ct))
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
            var diagnostics = details.Where(detail => !detail.ContextResolved || decimal.Parse(detail.Variance, System.Globalization.CultureInfo.InvariantCulture) != 0m)
                .Select(detail => !detail.ContextResolved ? "control_context_missing" : "control_balance_difference").Distinct().ToArray();
            if (details.Length == 0)
            {
                var configuration = await ledgerPosting.GetActiveConfigurationVersionIdAsync(tenant, new(entity), ct);
                var control = await ledgerControlBalances.GetControlBalanceAsync(tenant, new(entity), direction.ToString(), configuration, asOf, ct);
                ledgerBalance = control.SignedBaseBalance;
                if (ledgerBalance != 0m) diagnostics = ["ledger_control_without_source_context"];
            }
            var difference = sourceBalance - ledgerBalance;
            var saved = await repository.SaveReconciliationAsync(tenant, entity, type, asOf, sourceBalance, ledgerBalance, difference, result.BaseCurrencyCode, difference == 0m && details.All(detail => detail.ContextResolved) ? "Reconciled" : "Difference", diagnostics, actor, correlation, ct);
            await WriteAuditAsync(tenant, entity, actor, "reconciliation.completed", "AccountingReconciliation", saved.Id, null, JsonSerializer.Serialize(saved), null, ct);
            return saved;
        }
        if (type == "Inventory")
        {
            if (inventory is null)
            {
                var unavailable = await repository.SaveReconciliationAsync(tenant, entity, type, asOf, null, null, null, null, "NotApplicable", ["inventory_capability_unavailable"], actor, correlation, ct);
                await WriteAuditAsync(tenant, entity, actor, "reconciliation.completed", "AccountingReconciliation", unavailable.Id, null, JsonSerializer.Serialize(unavailable), null, ct);
                return unavailable;
            }
            var result = await inventory.ReconcileInventoryAsync(tenant, new(entity), asOf, ct);
            var saved = await repository.SaveReconciliationAsync(tenant, entity, type, asOf, result.InventoryValue, result.LedgerValue, result.Difference, result.CurrencyCode, result.Available ? result.Status : "NotReady", result.Diagnostics, actor, correlation, ct);
            await WriteAuditAsync(tenant, entity, actor, "reconciliation.completed", "AccountingReconciliation", saved.Id, null, JsonSerializer.Serialize(saved), null, ct);
            return saved;
        }
        throw new AccountingException("accounting.reconciliation_type_invalid");
    }

    public Task<IReadOnlyList<ReconciliationView>> ListReconciliationsAsync(TenantId tenant, Guid entity, string? type, DateOnly? asOf, int limit, CancellationToken ct) => repository.ListReconciliationsAsync(tenant, entity, type, asOf, limit, ct);
    public async Task<ReconciliationView> GetReconciliationAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct) => await repository.GetReconciliationAsync(tenant, entity, runId, ct) ?? throw new AccountingException("accounting.reconciliation_not_found");
    public Task<IReadOnlyList<ReconciliationIssueView>> ListReconciliationIssuesAsync(TenantId tenant, Guid entity, string? status, int limit, CancellationToken ct) => repository.ListReconciliationIssuesAsync(tenant, entity, status, limit, ct);
    public async Task<CloseRunView> GetCloseRunAsync(TenantId tenant, Guid entity, Guid periodId, CancellationToken ct) => await repository.GetCloseRunAsync(tenant, entity, periodId, ct) ?? throw new AccountingException("accounting.close_run_not_found");
    public Task<IReadOnlyList<CloseCheckView>> ListCloseRunIssuesAsync(TenantId tenant, Guid entity, Guid periodId, CancellationToken ct) => repository.ListCloseRunIssuesAsync(tenant, entity, periodId, ct);

    public async Task<FxRevaluationRunView> PreviewFxRevaluationAsync(TenantId tenant, Guid entity, FxRevaluationRequest request, Guid actor, CancellationToken ct)
    {
        if (request.AsOfDate == DateOnly.MinValue || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 200
            || request.AutoReversalDate is { } reversal && reversal <= request.AsOfDate)
            throw new AccountingException("accounting.fx_revaluation_request_invalid");
        var sourceItems = await subledger.ListForeignCurrencyOpenItemsForRevaluationAsync(tenant, new(entity), request.AsOfDate, ct);
        var lines = new List<FxRevaluationLineDraft>();
        foreach (var item in sourceItems)
        {
            var context = await financeBase.ResolveCalculationContextAsync(tenant, new(entity), item.TransactionCurrency, FinancialCalculationPurpose.Accounting, request.AsOfDate, ct)
                ?? throw new AccountingException("accounting.fx_revaluation_context_unavailable");
            if (context.ExchangeRate is null || !decimal.TryParse(context.ExchangeRate.Rate, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var rate) || rate <= 0m)
                throw new AccountingException("accounting.fx_revaluation_rate_missing");
            if (!decimal.TryParse(item.OpenTransactionAmount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var openAmount)
                || !decimal.TryParse(item.OpenBaseCarryingAmount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var carryingAmount))
                throw new AccountingException("accounting.fx_revaluation_source_invalid");
            var revaluedAmount = decimal.Round(openAmount * rate, context.RoundingPolicy.MoneyScale, MidpointRounding.ToEven);
            var difference = revaluedAmount - carryingAmount;
            if (difference == 0m) continue;
            var isIncrease = difference > 0m;
            var role = item.Direction == SubledgerDirectionContract.Receivable
                ? isIncrease ? "FXGain" : "FXLoss"
                : isIncrease ? "FXLoss" : "FXGain";
            lines.Add(new FxRevaluationLineDraft(Guid.CreateVersion7(), item.Id, item.RowVersion, item.Direction.ToString(), item.TransactionCurrency, openAmount,
                item.BaseCurrency, carryingAmount, revaluedAmount, difference, role, rate, JsonSerializer.Serialize(context.ExchangeRate)));
        }
        var payloadHash = Hash(new
        {
            request.AsOfDate,
            key = request.IdempotencyKey.Trim(),
            request.AutoReversalDate,
            lines = lines.OrderBy(line => line.OpenItemId).Select(line => new { line.OpenItemId, line.SourceRowVersion, line.OpenTransactionAmount, line.CarryingBaseAmount, line.RevaluedBaseAmount, line.DifferenceAmount, line.ExchangeRate, line.DifferenceRole })
        });
        var saved = await repository.SaveFxRevaluationPreviewAsync(tenant, entity, request with { IdempotencyKey = request.IdempotencyKey.Trim() }, payloadHash, lines, actor, ct);
        await WriteAuditAsync(tenant, entity, actor, "fx_revaluation.previewed", "FxRevaluationRun", saved.Id, null, JsonSerializer.Serialize(saved), null, ct);
        return saved;
    }

    public async Task<FxRevaluationRunView> PostFxRevaluationAsync(TenantId tenant, Guid entity, Guid runId, Guid actor, Guid correlation, CancellationToken ct)
    {
        var run = await repository.StartFxRevaluationPostAsync(tenant, entity, runId, ct);
        if (run.Status == "Posted") return run;
        try
        {
            var current = (await subledger.ListForeignCurrencyOpenItemsForRevaluationAsync(tenant, new(entity), run.AsOfDate, ct)).ToDictionary(item => item.Id);
            foreach (var line in run.Lines)
            {
                if (!current.TryGetValue(line.OpenItemId, out var source) || source.RowVersion != await SourceRowVersionAsync(tenant, entity, runId, line.Id, ct))
                    throw new AccountingException("accounting.fx_revaluation_source_changed");
                if (line.JournalEntryId is not null) continue;
                Guid? configurationId = source.ControlConfigurationReference.StartsWith("configuration:", StringComparison.Ordinal)
                    && Guid.TryParse(source.ControlConfigurationReference["configuration:".Length..], out var parsedConfiguration) ? parsedConfiguration : null;
                var factId = DeterministicGuid(run.Id, line.Id);
                var payload = new UnrealizedFxRevaluationPayload(run.Id, line.OpenItemId, line.Direction, line.DifferenceAmount, line.BaseCurrency,
                    source.ControlAccountRole, line.DifferenceRole, line.TransactionCurrency, line.OpenTransactionAmount, line.CarryingBaseAmount,
                    line.RevaluedBaseAmount, line.ExchangeRate, line.ExchangeRateSnapshot, configurationId);
                IAccountingFactPayload semantic = payload;
                var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(entity), AccountingFactTypes.UnrealizedFxRevaluation, 1, "accounting",
                    $"fx-revaluation:{run.Id:N}:{line.Id:N}", $"accounting-fx-revaluation:{run.Id:N}:{line.Id:N}", run.AsOfDate, run.AsOfDate, semantic, correlation, actor);
                var posted = await ledgerPosting.PostUnrealizedFxRevaluationAsync(fact, payload, ct);
                await repository.SaveFxRevaluationPostingAsync(tenant, entity, run.Id, line.Id, factId, posted.JournalEntryId, ct);
            }
            await repository.CompleteFxRevaluationAsync(tenant, entity, run.Id, ct);
        }
        catch
        {
            // Each Ledger fact commits independently. If a later line fails, make
            // the run explicitly recoverable so committed lines can be reversed.
            await repository.InvalidateFxRevaluationAsync(tenant, entity, run.Id, CancellationToken.None);
            throw;
        }
        var complete = await repository.GetFxRevaluationRunAsync(tenant, entity, run.Id, ct) ?? throw new AccountingException("accounting.fx_revaluation_not_found");
        await WriteAuditAsync(tenant, entity, actor, "fx_revaluation.posted", "FxRevaluationRun", run.Id, null, JsonSerializer.Serialize(complete), null, ct);
        return complete;
    }

    public async Task<FxRevaluationRunView> GetFxRevaluationAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct) =>
        await repository.GetFxRevaluationRunAsync(tenant, entity, runId, ct) ?? throw new AccountingException("accounting.fx_revaluation_not_found");

    public async Task<FxRevaluationRunView> ReverseFxRevaluationAsync(TenantId tenant, Guid entity, Guid runId, FxRevaluationReversalRequest request, Guid actor, Guid correlation, CancellationToken ct)
    {
        if (request.ReversalDate == DateOnly.MinValue || string.IsNullOrWhiteSpace(request.Reason)) throw new AccountingException("accounting.fx_revaluation_reversal_invalid");
        var run = await repository.GetFxRevaluationRunAsync(tenant, entity, runId, ct) ?? throw new AccountingException("accounting.fx_revaluation_not_found");
        if (run.Status == "Reversed") return run;
        if ((run.Status != "Posted" && run.Status != "Invalidated") || request.ReversalDate <= run.AsOfDate || !run.Lines.Any(line => line.JournalEntryId is not null))
            throw new AccountingException("accounting.fx_revaluation_reversal_invalid");
        foreach (var line in run.Lines)
        {
            if (line.ReversalEntryId is not null) continue;
            if (line.JournalEntryId is null) continue;
            var reversal = await ledgerPosting.ReverseAsync(tenant, new(entity), line.JournalEntryId.Value, request.ReversalDate, null, request.Reason.Trim(), actor, correlation, ct);
            await repository.SaveFxRevaluationReversalAsync(tenant, entity, runId, line.Id, reversal.JournalEntryId, ct);
        }
        await repository.CompleteFxRevaluationReversalAsync(tenant, entity, runId, ct);
        var complete = await repository.GetFxRevaluationRunAsync(tenant, entity, runId, ct) ?? throw new AccountingException("accounting.fx_revaluation_not_found");
        await WriteAuditAsync(tenant, entity, actor, "fx_revaluation.reversed", "FxRevaluationRun", run.Id, null, JsonSerializer.Serialize(complete), request.Reason.Trim(), ct);
        return complete;
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
        if (preview.Status == "Closed") return preview;
        var finalReadiness = await ledgerWorkspace.GetCloseReadinessAsync(tenant, new(entity), request.PeriodId, ct);
        if (!finalReadiness.CanClose) throw new AccountingException("accounting.close_blocked");
        await ledgerWorkspace.SetPeriodStatusAsync(tenant, new(entity), request.PeriodId, "Closed", actor, correlation, ct);
        var result = await repository.SaveCloseRunAsync(tenant, entity, request.PeriodId, request.IdempotencyKey, Hash(new { request.PeriodId, request.Reason }), "Closed", request.Reason, actor, preview.Checks, ct);
        await WriteAuditAsync(tenant, entity, actor, "period.closed", "AccountingPeriod", request.PeriodId, null, JsonSerializer.Serialize(result), request.Reason, ct);
        return result;
    }

    public async Task<CloseRunView> ReopenAsync(TenantId tenant, Guid entity, Guid periodId, string reason, string idempotencyKey, Guid actor, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AccountingException("accounting.reopen_reason_required");
        await ledgerWorkspace.SetPeriodStatusAsync(tenant, new(entity), periodId, "Open", actor, correlation, ct);
        var checks = new[] { new CloseCheckView("ledger.period", "Warning", "Pass", "Period reopened with explicit reason: " + reason.Trim()) };
        var result = await repository.SaveCloseRunAsync(tenant, entity, periodId, idempotencyKey, Hash(new { periodId, reason }), "Reopened", reason.Trim(), actor, checks, ct);
        await WriteAuditAsync(tenant, entity, actor, "period.reopened", "AccountingPeriod", periodId, null, JsonSerializer.Serialize(result), reason.Trim(), ct);
        return result;
    }

    public async Task<GoLiveApprovalView> ApproveGoLiveAsync(TenantId tenant, Guid membershipId, Guid entity, ApproveGoLiveRequest request, Guid actor, Guid correlation, CancellationToken ct)
    {
        if (request.OpeningBatchId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 200) throw new AccountingException("accounting.go_live_request_invalid");
        var opening = await ledgerWorkspace.GetOpeningBatchAsync(tenant, new(entity), request.OpeningBatchId, ct)
            ?? throw new AccountingException("accounting.opening_not_found");
        if (opening.Status is not ("Posted" or "Locked") || opening.PostedEntryId is null || request.FinancialGoLiveDate is { } date && date != opening.GoLiveDate)
            throw new AccountingException("accounting.opening_not_ready");
        var checks = new List<CloseCheckView>
        {
            new("opening.ledger", "Info", "Pass", opening.Status == "Locked" ? "The Ledger opening batch is already locked." : "The posted Ledger opening batch is ready to lock.")
        };
        if (request.SubledgerOpeningBatchId is { } subledgerBatchId)
        {
            var subledgerOpening = await subledger.GetOpeningSubledgerBatchAsync(tenant, new(entity), subledgerBatchId, ct);
            if (subledgerOpening is null || subledgerOpening.Status != "Finalized" || subledgerOpening.GoLiveDate != opening.GoLiveDate)
                throw new AccountingException("accounting.subledger_opening_not_ready");
            checks.Add(new("opening.subledger", "Info", "Pass", "The Subledger opening batch is finalized and agrees on the go-live boundary."));
        }
        // A posted opening proves the legal entity's active period, opening journal,
        // base currency, and the exact accounting configuration used at the boundary.
        // Optional module capabilities (such as Payroll) must not prevent a company
        // from approving a separately reconciled financial migration.
        checks.Add(new("ledger.opening_context", "Info", "Pass", "The posted opening established a valid Ledger financial boundary."));
        var setup = await repository.GetSetupAsync(tenant, entity, ct);
        if (setup is null || setup.GoLiveStatus is not ("Ready" or "Approved")) throw new AccountingException("accounting.go_live_setup_not_ready");
        foreach (var kind in new[] { "ProfitAndLoss", "BalanceSheet" })
            if (await repository.GetStatementDefinitionAsync(tenant, entity, kind, opening.GoLiveDate, ct) is null) throw new AccountingException("accounting.go_live_statement_missing");
        var reconciliations = await ReconcileAllAsync(tenant, membershipId, entity, opening.GoLiveDate, actor, correlation, ct);
        foreach (var reconciliation in reconciliations)
        {
            var accepted = reconciliation.Status is "Reconciled" or "NotApplicable";
            checks.Add(new($"reconciliation.{reconciliation.Type.ToLowerInvariant()}", accepted ? "Info" : "Blocking", accepted ? "Pass" : "Fail", string.Join(", ", reconciliation.Diagnostics.DefaultIfEmpty(reconciliation.Status))));
        }
        checks.Add(new("reconciliation.cash_bank", "Info", "NotApplicable", "Cash and bank opening reconciliation is deferred until the Banking capability is enabled."));
        if (checks.Any(check => check.Status == "Fail")) throw new AccountingException("accounting.go_live_reconciliation_blocked");
        if (opening.Status == "Posted") await ledgerWorkspace.LockOpeningBatchAsync(tenant, new(entity), opening.Id, actor, correlation, ct);
        var idempotencyKey = request.IdempotencyKey.Trim();
        var payloadHash = Hash(new { openingBatchId = opening.Id, request.SubledgerOpeningBatchId, goLiveDate = opening.GoLiveDate, reason = request.Reason.Trim() });
        var approved = await repository.ApproveGoLiveAsync(tenant, entity, opening.Id, request.SubledgerOpeningBatchId, opening.GoLiveDate, idempotencyKey, payloadHash, request.Reason.Trim(), actor, correlation, checks, ct);
        await WriteAuditAsync(tenant, entity, actor, "go_live.approved", "AccountingGoLive", opening.Id, null, JsonSerializer.Serialize(approved), request.Reason.Trim(), ct);
        return approved;
    }

    public async Task<FinancialStatementView> GetStatementAsync(TenantId tenant, Guid entity, string kind, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (kind is not ("ProfitAndLoss" or "BalanceSheet")) throw new AccountingException("accounting.statement_kind_invalid");
        // A balance sheet is an as-of view; Ledger supplies its cumulative balance
        // from inception so a caller-provided comparative range cannot omit equity.
        var trial = await ledgerWorkspace.GetTrialBalanceAsync(tenant, new(entity), kind == "BalanceSheet" ? null : from, to, ct);
        var definition = await repository.GetStatementDefinitionAsync(tenant, entity, kind, to, ct) ?? throw new AccountingException("accounting.statement_definition_missing");
        var linesByCode = definition.Lines.ToDictionary(line => line.Code, StringComparer.OrdinalIgnoreCase);
        var computed = new Dictionary<string, StatementLineView>(StringComparer.OrdinalIgnoreCase);
        StatementLineView Evaluate(string code)
        {
            if (computed.TryGetValue(code, out var existing)) return existing;
            var line = linesByCode[code];
            StatementLineView value;
            if (line.LineKind == "MappedAccounts")
            {
                var accounts = trial.Accounts.Where(account => (line.AccountIds?.Count ?? 0) > 0 ? line.AccountIds!.Contains(account.AccountId) : account.AccountType == line.AccountType).ToArray();
                value = new(line.Code, line.NameEn, line.AccountType ?? "Mapped", accounts.Sum(PresentationAmount), accounts.Select(account => account.AccountId).ToArray(), line.LineKind, line.ParentLineCode, line.NameAr, line.NameEn);
            }
            else
            {
                var references = line.FormulaLineCodes is { Count: > 0 }
                    ? line.FormulaLineCodes
                    : definition.Lines.Where(child => string.Equals(child.ParentLineCode, line.Code, StringComparison.OrdinalIgnoreCase)).Select(child => child.Code).ToArray();
                var values = references.Select(Evaluate).ToArray();
                var amount = line.LineKind == "Arithmetic" && line.ArithmeticOperator == "Subtract"
                    ? values[0].Amount - values.Skip(1).Sum(item => item.Amount)
                    : values.Sum(item => item.Amount);
                value = new(line.Code, line.NameEn, "Formula", amount, values.SelectMany(item => item.AccountIds).Distinct().ToArray(), line.LineKind, line.ParentLineCode, line.NameAr, line.NameEn);
            }
            computed.Add(code, value);
            return value;
        }
        var lines = definition.Lines.OrderBy(line => line.Sequence).Select(line => Evaluate(line.Code)).ToArray();
        var total = kind == "ProfitAndLoss"
            ? linesByCode.ContainsKey("net-profit") ? Evaluate("net-profit").Amount : trial.Accounts.Where(account => account.AccountType == "Revenue").Sum(PresentationAmount) - trial.Accounts.Where(account => account.AccountType == "Expense").Sum(PresentationAmount)
            : lines.Where(line => line.ParentLineCode is null).Sum(line => line.Amount);
        var assets = trial.Accounts.Where(account => account.AccountType == "Asset").Sum(PresentationAmount);
        var liabilities = trial.Accounts.Where(account => account.AccountType == "Liability").Sum(PresentationAmount);
        var equity = trial.Accounts.Where(account => account.AccountType == "Equity").Sum(PresentationAmount);
        var currentEarnings = trial.Accounts.Where(account => account.AccountType == "Revenue").Sum(account => -account.SignedBalance)
            - trial.Accounts.Where(account => account.AccountType == "Expense").Sum(account => account.SignedBalance);
        if (kind == "BalanceSheet")
        {
            lines = [.. lines, new StatementLineView("current-year-earnings", "Current year earnings", "CurrentYearEarnings", currentEarnings, [], "Derived", null, "أرباح العام الحالي", "Current year earnings")];
            total += currentEarnings;
        }
        var balanced = kind != "BalanceSheet" || assets == liabilities + equity + currentEarnings;
        return new(kind, from, to, trial.BaseCurrencyCode, total, lines, balanced, balanced ? null : "accounting.balance_sheet_inequality");
    }

    public async Task<FinancialStatementDefinitionView> SaveStatementDefinitionAsync(TenantId tenant, Guid entity, FinancialStatementDefinitionRequest request, Guid actor, CancellationToken ct)
    {
        var selectedAccounts = request.Lines.SelectMany(line => line.AccountIds ?? []).Distinct().ToArray();
        if (selectedAccounts.Length > 0 && (await ledgerWorkspace.FindAccountsAsync(tenant, new(entity), selectedAccounts, ct)).Count != selectedAccounts.Length)
            throw new AccountingException("accounting.statement_mapping_account_invalid");
        if (selectedAccounts.Length > 0)
        {
            var accountTypes = await ledgerWorkspace.GetAccountTypesAsync(tenant, new(entity), ct);
            var mappedTypes = request.Lines.Where(line => line.LineKind == "MappedAccounts" && line.AccountType is not null).Select(line => line.AccountType!).ToHashSet(StringComparer.Ordinal);
            if (request.Lines.Where(line => line.LineKind == "MappedAccounts").SelectMany(line => line.AccountIds ?? []).Any(accountId => accountTypes.TryGetValue(accountId, out var type) && mappedTypes.Contains(type)))
                throw new AccountingException("accounting.statement_mapping_overlap");
        }
        var saved = await repository.SaveStatementDefinitionAsync(tenant, entity, request, actor, ct);
        await WriteAuditAsync(tenant, entity, actor, "statement_definition.saved", "FinancialStatementDefinition", saved.Id, null, JsonSerializer.Serialize(saved), null, ct);
        return saved;
    }
    public Task<FinancialStatementDefinitionView?> GetStatementDefinitionAsync(TenantId tenant, Guid entity, string kind, CancellationToken ct) => repository.GetActiveStatementDefinitionAsync(tenant, entity, kind, ct);

    public async Task<OwnerFinancialSummary> GetOwnerSummaryAsync(TenantId tenant, Guid membershipId, Guid entity, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct)
    {
        var readOnly = await ReconcileAllReadOnlyAsync(tenant, membershipId, entity, asOf, ct);
        var ar = readOnly.Single(item => item.Type == "AR");
        var ap = readOnly.Single(item => item.Type == "AP");
        var inventoryResult = readOnly.Single(item => item.Type == "Inventory");
        var trial = await ledgerWorkspace.GetTrialBalanceAsync(tenant, new(entity), null, asOf, ct);
        var cashBalance = trial.Accounts.Where(account => account.AccountSubtype is "Cash" or "Bank" || account.AccountCode.StartsWith("1102", StringComparison.Ordinal)).Sum(account => account.SignedBalance);
        var pnl = await GetStatementAsync(tenant, entity, "ProfitAndLoss", new DateOnly(asOf.Year, 1, 1), asOf, ct);
        var health = new[] { ar.Status, ap.Status, inventoryResult.Status }.Any(status => status is "Difference" or "NotReady" or "Error") ? "Attention" : "Healthy";
        return new(entity, asOf, trial.BaseCurrencyCode, cashBalance, ar.SourceBalance ?? 0m, ap.SourceBalance ?? 0m, inventoryResult.SourceBalance, pnl.Total, health);
    }

    private async Task<IReadOnlyList<ReconciliationView>> ReconcileAllReadOnlyAsync(TenantId tenant, Guid membership, Guid entity, DateOnly asOf, CancellationToken ct) => [await EvaluateReconciliationReadOnlyAsync(tenant, membership, entity, "AR", asOf, ct), await EvaluateReconciliationReadOnlyAsync(tenant, membership, entity, "AP", asOf, ct), await EvaluateReconciliationReadOnlyAsync(tenant, membership, entity, "Inventory", asOf, ct)];
    private async Task<IReadOnlyList<ReconciliationView>> ReconcileAllAsync(TenantId tenant, Guid membership, Guid entity, DateOnly asOf, Guid actor, Guid correlation, CancellationToken ct) => [await ReconcileAsync(tenant, membership, entity, "AR", asOf, actor, correlation, ct), await ReconcileAsync(tenant, membership, entity, "AP", asOf, actor, correlation, ct), await ReconcileAsync(tenant, membership, entity, "Inventory", asOf, actor, correlation, ct)];

    private async Task<ReconciliationView> EvaluateReconciliationReadOnlyAsync(TenantId tenant, Guid membership, Guid entity, string type, DateOnly asOf, CancellationToken ct)
    {
        if (type == "Inventory")
        {
            if (inventory is null) return new(Guid.CreateVersion7(), type, asOf, null, null, null, null, "NotApplicable", ["inventory_capability_unavailable"], clock.GetUtcNow());
            var result = await inventory.ReconcileInventoryAsync(tenant, new(entity), asOf, ct);
            return new(Guid.CreateVersion7(), type, asOf, result.InventoryValue, result.LedgerValue, result.Difference, result.CurrencyCode, result.Available ? result.Status : "NotReady", result.Diagnostics, clock.GetUtcNow());
        }
        var direction = type == "AR" ? SubledgerDirectionContract.Receivable : SubledgerDirectionContract.Payable;
        var sources = await subledger.GetReconciliationControlAccountsAsync(tenant, membership, new(new(entity), asOf), ct);
        var selected = sources.Where(source => source.Direction == direction).ToArray();
        var sourceBalance = selected.Sum(source => source.SubledgerBaseBalance);
        var ledgerBalance = 0m;
        var diagnostics = new List<string>();
        if (selected.Length == 0)
        {
            try
            {
                var configuration = await ledgerPosting.GetActiveConfigurationVersionIdAsync(tenant, new(entity), ct);
                var control = await ledgerControlBalances.GetControlBalanceAsync(tenant, new(entity), direction.ToString(), configuration, asOf, ct);
                ledgerBalance = control.SignedBaseBalance;
                if (ledgerBalance != 0m) diagnostics.Add("ledger_control_without_source_context");
            }
            catch (OperationCanceledException) { throw; }
            catch { diagnostics.Add("control_context_missing"); }
        }
        foreach (var source in selected)
        {
            var configurationId = Guid.Empty;
            var resolved = source.ControlRole == source.Direction.ToString() && source.ControlConfigurationReference.StartsWith("configuration:", StringComparison.Ordinal) && Guid.TryParse(source.ControlConfigurationReference["configuration:".Length..], out configurationId);
            if (!resolved) { diagnostics.Add("control_context_missing"); continue; }
            try
            {
                var snapshot = await ledgerControlBalances.GetControlBalanceAsync(tenant, new(entity), source.ControlRole, configurationId, asOf, ct);
                ledgerBalance += snapshot.SignedBaseBalance;
                if (snapshot.BaseCurrencyCode != source.BaseCurrencyCode) diagnostics.Add("control_currency_mismatch");
            }
            catch (OperationCanceledException) { throw; }
            catch { diagnostics.Add("control_context_missing"); }
        }
        var difference = sourceBalance - ledgerBalance;
        if (difference != 0m) diagnostics.Add("control_balance_difference");
        var status = diagnostics.Count == 0 ? "Reconciled" : selected.Length == 0 ? "NotReady" : "Difference";
        return new(Guid.CreateVersion7(), type, asOf, sourceBalance, ledgerBalance, difference, selected.FirstOrDefault()?.BaseCurrencyCode, status, diagnostics.Distinct().ToArray(), clock.GetUtcNow());
    }
    private static decimal PresentationAmount(LedgerAccountBalance account) => account.AccountType is "Liability" or "Equity" or "Revenue" ? -account.SignedBalance : account.SignedBalance;
    private static bool HasMovement(LedgerAccountBalance account) => account.OpeningDebit != 0m || account.OpeningCredit != 0m || account.PeriodDebit != 0m || account.PeriodCredit != 0m;
    private static bool IsMapped(LedgerAccountBalance account, IEnumerable<FinancialStatementDefinitionView> definitions) => definitions
        .SelectMany(definition => definition.Lines)
        .Where(line => line.LineKind == "MappedAccounts")
        .Any(line => (line.AccountIds?.Count ?? 0) > 0 ? line.AccountIds!.Contains(account.AccountId) : line.AccountType == account.AccountType);
    private Task WriteAuditAsync(TenantId tenant, Guid entity, Guid actor, string action, string entityType, Guid entityId, string? before, string? after, string? reason, CancellationToken ct) => audit.WriteAsync(new AuditEntry(Guid.CreateVersion7(), tenant, entity, null, "User", actor, null, "accounting", action, entityType, entityId, before, after, reason, "api", Guid.CreateVersion7(), null, clock.GetUtcNow(), null), ct);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private async Task<long> SourceRowVersionAsync(TenantId tenant, Guid entity, Guid runId, Guid lineId, CancellationToken ct)
    {
        return await repository.GetFxSourceRowVersionAsync(tenant, entity, runId, lineId, ct);
    }
    private static Guid DeterministicGuid(Guid first, Guid second)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{first:D}:{second:D}"));
        return new Guid(bytes[..16]);
    }
}

public sealed class AccountingException(string code) : Exception(code) { public string Code { get; } = code; }
