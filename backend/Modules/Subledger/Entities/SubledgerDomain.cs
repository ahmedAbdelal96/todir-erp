namespace Modules.Subledger.Entities;

public enum SubledgerDirection { Receivable, Payable }
public enum OpenItemPolarity { Debit, Credit }
public enum OpenItemStatus { Open, PartiallySettled, Settled }
public enum ApplicationOperation { Apply, Reverse }

public sealed class SubledgerException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed record PartyFinancialProfileSnapshot(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PartyId,
    Guid? DefaultReceivablePaymentTermId,
    Guid? DefaultPayablePaymentTermId,
    decimal? CreditLimit,
    string? CreditLimitCurrency,
    string? ReceivablePolicyCode,
    string? PayablePolicyCode,
    string Status,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PartyOpenItemSnapshot(
    Guid Id,
    Guid TenantId,
    Guid LegalEntityId,
    Guid PartyId,
    SubledgerDirection Direction,
    OpenItemPolarity Polarity,
    string SourceType,
    string SourceReference,
    string? SourceScheduleReference,
    string? SourceDisplayNumber,
    DateOnly DocumentDate,
    DateOnly PostingDate,
    DateOnly DueDate,
    string TransactionCurrency,
    decimal OriginalTransactionAmount,
    decimal SettledTransactionAmount,
    decimal OpenTransactionAmount,
    string BaseCurrency,
    decimal OriginalBaseAmount,
    decimal SettledBaseCarryingAmount,
    decimal OpenBaseCarryingAmount,
    string ControlAccountRole,
    string ControlAccountReference,
    string ControlConfigurationReference,
    string? HistoricFxSnapshot,
    OpenItemStatus Status,
    long SourceVersion,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);

public sealed record OpenItemApplicationSnapshot(
    Guid Id,
    Guid BatchId,
    Guid DebitItemId,
    Guid CreditItemId,
    decimal DebitAppliedAmount,
    decimal CreditAppliedAmount,
    decimal DebitBaseCarryingAmountClosed,
    decimal CreditBaseCarryingAmountClosed,
    DateOnly ApplicationDate,
    DateOnly SettlementDate,
    long Sequence,
    DateTimeOffset CreatedAt);

public sealed record ApplicationBatchSnapshot(
    Guid Id,
    Guid TenantId,
    Guid LegalEntityId,
    Guid PartyId,
    SubledgerDirection Direction,
    ApplicationOperation Operation,
    string IdempotencyKey,
    string PayloadHash,
    DateOnly ApplicationDate,
    DateOnly SettlementDate,
    Guid ActorId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OpenItemApplicationSnapshot> Applications);

public static class SubledgerRules
{
    public static OpenItemPolarity SourcePolarity(SubledgerDirection direction) => direction switch
    {
        SubledgerDirection.Receivable => OpenItemPolarity.Debit,
        SubledgerDirection.Payable => OpenItemPolarity.Credit,
        _ => throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.")
    };

    public static OpenItemPolarity PaymentPolarity(SubledgerDirection direction) => direction switch
    {
        SubledgerDirection.Receivable => OpenItemPolarity.Credit,
        SubledgerDirection.Payable => OpenItemPolarity.Debit,
        _ => throw new SubledgerException("subledger.direction_invalid", "The financial direction is invalid.")
    };

    public static OpenItemStatus StatusFor(decimal settled, decimal open) => open == 0m
        ? OpenItemStatus.Settled
        : settled == 0m ? OpenItemStatus.Open : OpenItemStatus.PartiallySettled;

    public static void ValidatePositiveAmount(decimal amount, string code = "subledger.amount_invalid")
    {
        if (amount <= 0m) throw new SubledgerException(code, "The amount must be greater than zero.");
    }
}
