namespace Modules.Accounting.DTOs;

public sealed record AccountingHealthItem(string Code, string Severity, string Status, string Message, string? Source = null);
public sealed record AccountingHealthView(Guid LegalEntityId, DateOnly AsOfDate, string Status, IReadOnlyList<AccountingHealthItem> Items);
public sealed record ReconciliationView(Guid Id, string Type, DateOnly AsOfDate, decimal? SourceBalance, decimal? LedgerBalance, decimal? Difference, string? CurrencyCode, string Status, IReadOnlyList<string> Diagnostics, DateTimeOffset CreatedAt);
public sealed record ReconciliationIssueView(Guid Id, Guid ReconciliationRunId, string Code, string Severity, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);
public sealed record ReconciliationPageDto(IReadOnlyList<ReconciliationView> Items, string? NextCursor, int TotalCount = 0, int Page = 1, int PageSize = 0);
public sealed record ReconciliationIssuePageDto(IReadOnlyList<ReconciliationIssueView> Items, string? NextCursor, int TotalCount = 0, int Page = 1, int PageSize = 0);
public sealed record ClosePreviewRequest(Guid PeriodId, string IdempotencyKey, string? Reason = null);
public sealed record CloseCheckView(string Code, string Severity, string Status, string Message);
public sealed record CloseRunView(Guid Id, Guid PeriodId, string Status, IReadOnlyList<CloseCheckView> Checks, DateTimeOffset CreatedAt);
public sealed record ApproveGoLiveRequest(Guid OpeningBatchId, Guid? SubledgerOpeningBatchId, DateOnly? FinancialGoLiveDate, string Reason, string IdempotencyKey = "");
public sealed record GoLiveApprovalView(Guid LegalEntityId, Guid OpeningBatchId, Guid? SubledgerOpeningBatchId, DateOnly FinancialGoLiveDate, string Status, IReadOnlyList<CloseCheckView> Checks, DateTimeOffset ApprovedAt);
public sealed record FxRevaluationRequest(DateOnly AsOfDate, string IdempotencyKey, DateOnly? AutoReversalDate = null);
public sealed record FxRevaluationLineView(Guid Id, Guid OpenItemId, string Direction, string TransactionCurrency, decimal OpenTransactionAmount, string BaseCurrency, decimal CarryingBaseAmount, decimal RevaluedBaseAmount, decimal DifferenceAmount, string DifferenceRole, decimal ExchangeRate, Guid? JournalEntryId, Guid? ReversalEntryId, string ExchangeRateSnapshot);
public sealed record FxRevaluationRunView(Guid Id, DateOnly AsOfDate, string Status, DateOnly? AutoReversalDate, IReadOnlyList<FxRevaluationLineView> Lines, DateTimeOffset CreatedAt);
public sealed record FxRevaluationReversalRequest(DateOnly ReversalDate, string Reason);
public sealed record StatementLineView(string Code, string Name, string AccountType, decimal Amount, IReadOnlyList<Guid> AccountIds, string LineKind = "MappedAccounts", string? ParentLineCode = null, string? NameAr = null, string? NameEn = null);
public sealed record FinancialStatementView(string Kind, DateOnly FromDate, DateOnly ToDate, string CurrencyCode, decimal Total, IReadOnlyList<StatementLineView> Lines, bool IsBalanced, string? Diagnostic);
/// <summary>Constrained presentation formula. It deliberately cannot contain SQL or arbitrary expressions.</summary>
public sealed record FinancialStatementDefinitionLineRequest(
    string Code,
    string NameAr,
    string NameEn,
    int Sequence,
    string? AccountType,
    IReadOnlyList<Guid>? AccountIds = null,
    string LineKind = "MappedAccounts",
    string? ParentLineCode = null,
    string? ArithmeticOperator = null,
    IReadOnlyList<string>? FormulaLineCodes = null);
public sealed record FinancialStatementDefinitionRequest(string Code, string Kind, IReadOnlyList<FinancialStatementDefinitionLineRequest> Lines, DateOnly? EffectiveFrom = null);
public sealed record FinancialStatementDefinitionView(Guid Id, string Code, string Kind, string Status, int Version, IReadOnlyList<FinancialStatementDefinitionLineRequest> Lines);
public sealed record OwnerFinancialSummary(Guid LegalEntityId, DateOnly AsOfDate, string CurrencyCode, decimal CashBookValue, decimal Receivables, decimal Payables, decimal? InventoryValue, decimal Profit, string Health);
