namespace Modules.Expenses.DTOs;

public sealed record ExpensePage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record ExpenseDto(Guid Id, Guid LegalEntityId, Guid? EmployeeId, string MerchantName, DateOnly ExpenseDate, decimal Amount, string CurrencyCode, string PaymentSource, Guid CategoryVersionId, string ExpenseState, string ApprovalState, string AccountingState, string SettlementState, string EvidenceState, string PolicyState, string? BusinessPurpose, string? Description, long Version, DateTimeOffset CreatedAt, DateTimeOffset? FinalizedAt);
public sealed record CreateExpenseRequest(Guid LegalEntityId, Guid? EmployeeId, string MerchantName, DateOnly ExpenseDate, decimal Amount, string CurrencyCode, string PaymentSource, Guid CategoryVersionId, string? BusinessPurpose = null, string? Description = null);
public sealed record UpdateExpenseRequest(string MerchantName, DateOnly ExpenseDate, decimal Amount, string CurrencyCode, Guid CategoryVersionId, string? BusinessPurpose, string? Description, long Version);
public sealed record ExpenseActionRequest(long Version, string? Reason = null);
public sealed record ExpenseCategoryDto(Guid Id, Guid LegalEntityId, string Code, string Name, string Status, Guid? PublishedVersionId);
public sealed record CreateExpenseCategoryRequest(Guid LegalEntityId, string Code, string Name, bool RequiresReceipt = true);
public sealed record ExpenseCategoryVersionDto(Guid Id, Guid CategoryId, int Version, string Status, bool RequiresReceipt, decimal? ReceiptThreshold, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record CreateExpenseCategoryVersionRequest(bool RequiresReceipt, decimal? ReceiptThreshold, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record PublishExpenseCategoryVersionRequest(DateOnly? EffectiveTo = null);
public sealed record ExpenseEvidenceDto(Guid Id, Guid ExpenseId, string EvidenceType, string FileReference, string Status, DateTimeOffset CreatedAt);
public sealed record AddExpenseEvidenceRequest(string EvidenceType, string FileReference);
public sealed record ReimbursementDto(Guid Id, Guid ExpenseId, Guid? EmployeeId, decimal Amount, decimal SettledAmount, string CurrencyCode, string Status, DateTimeOffset CreatedAt);
public sealed record ExpenseItemizationDto(Guid Id, Guid ExpenseId, string Description, decimal Amount, decimal TaxAmount, string CurrencyCode);
public sealed record CreateExpenseItemizationRequest(string Description, decimal Amount, decimal TaxAmount = 0, string CurrencyCode = "EGP");
public sealed record ExpenseHistoryDto(Guid Id, Guid ExpenseId, string Action, System.Text.Json.JsonElement ToState, DateTimeOffset OccurredAt);
