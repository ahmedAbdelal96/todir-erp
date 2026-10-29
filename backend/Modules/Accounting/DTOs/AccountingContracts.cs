namespace Modules.Accounting.DTOs;

public sealed record AccountingHealthItem(string Code, string Severity, string Status, string Message, string? Source = null);
public sealed record AccountingHealthView(Guid LegalEntityId, DateOnly AsOfDate, string Status, IReadOnlyList<AccountingHealthItem> Items);
public sealed record ReconciliationView(Guid Id, string Type, DateOnly AsOfDate, decimal? SourceBalance, decimal? LedgerBalance, decimal? Difference, string? CurrencyCode, string Status, IReadOnlyList<string> Diagnostics, DateTimeOffset CreatedAt);
public sealed record ClosePreviewRequest(Guid PeriodId, string IdempotencyKey, string? Reason = null);
public sealed record CloseCheckView(string Code, string Severity, string Status, string Message);
public sealed record CloseRunView(Guid Id, Guid PeriodId, string Status, IReadOnlyList<CloseCheckView> Checks, DateTimeOffset CreatedAt);
public sealed record StatementLineView(string Code, string Name, string AccountType, decimal Amount, IReadOnlyList<Guid> AccountIds);
public sealed record FinancialStatementView(string Kind, DateOnly FromDate, DateOnly ToDate, string CurrencyCode, decimal Total, IReadOnlyList<StatementLineView> Lines, bool IsBalanced, string? Diagnostic);
public sealed record OwnerFinancialSummary(Guid LegalEntityId, DateOnly AsOfDate, string CurrencyCode, decimal CashBookValue, decimal Receivables, decimal Payables, decimal? InventoryValue, decimal Profit, string Health);
