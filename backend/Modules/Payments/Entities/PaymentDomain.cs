namespace Modules.Payments.Entities;

public enum PaymentDirection { Incoming, Outgoing, InternalTransfer }
public enum PaymentBusinessState { Draft, Pending, Completed, Failed, Cancelled, Reversed }
public enum PaymentMethod { Cash, BankTransfer, Cheque, Card, PaymentGateway, Wallet, Other }
public enum PaymentAccountType { Bank, Cashbox, ProviderClearing, Wallet, InternalClearing }
public enum PaymentAccountStatus { Active, Suspended, Archived }
public enum PaymentType { CustomerReceipt, SupplierPayment, CustomerAdvance, SupplierAdvance, CustomerRefund, SupplierRefund, ExpensePayment, Standalone, TransferLeg, ProviderFee }
public enum PaymentFeeType { BankFee, ProviderFee, CardProcessingFee, FXFee, TransferFee, OtherApprovedFee }
public enum PaymentFeeDisposition { Retain, Reverse }
public enum PaymentLinkAmountPolicy { Fixed, PartialAllowed, CustomerEnteredWithinLimits }

public sealed record PaymentAccount(Guid Id, Guid LegalEntityId, Guid? BranchId, string Code, string Name, PaymentAccountType Type,
    string CurrencyPolicy, string? CurrencyCode, PaymentAccountStatus Status, string? BankName, string? AccountHolder, string? MaskedIdentifier,
    string? SecretRef, long Version);

public sealed record PaymentRecord(Guid Id, Guid LegalEntityId, string Number, Guid? BranchId, PaymentDirection Direction, PaymentType Type,
    Guid? PartyId, decimal Amount, string CurrencyCode, PaymentMethod Method, Guid PaymentAccountId, DateOnly PaymentDate,
    DateOnly PostingDate, DateOnly? ValueDate, string? BankReference, string? CustomerReference, string? SupplierReference,
    string? SourceType, string? SourceReference, PaymentBusinessState State, DateTimeOffset? CompletedAt, long Version);

public sealed record CreatePaymentCommand(Guid LegalEntityId, Guid? BranchId, PaymentDirection Direction, PaymentType Type, Guid? PartyId,
    string Amount, string CurrencyCode, PaymentMethod Method, Guid PaymentAccountId, DateOnly PaymentDate, DateOnly PostingDate,
    DateOnly? ValueDate, string? BankReference, string? CustomerReference, string? SupplierReference, string? SourceType,
    string? SourceReference, string IdempotencyKey, string? ProviderCode = null, string? ProviderTransactionId = null);

public sealed record CreatePaymentAccountCommand(Guid LegalEntityId, Guid? BranchId, string Code, string Name, PaymentAccountType Type,
    string CurrencyPolicy, string? CurrencyCode, string? BankName, string? AccountHolder, string? MaskedIdentifier, string? SecretRef);

public sealed record CreateRefundCommand(Guid LegalEntityId, Guid OriginalPaymentId, Guid PaymentAccountId, PaymentMethod Method,
    string Amount, DateOnly PaymentDate, DateOnly PostingDate, DateOnly? ValueDate, string ReasonCode, string? BankReference, string IdempotencyKey);

public sealed record ReversePaymentCommand(Guid LegalEntityId, Guid PaymentId, DateOnly PaymentDate, DateOnly PostingDate,
    string ReasonCode, string IdempotencyKey, IReadOnlyList<PaymentFeeDispositionSelection>? FeeDispositions = null);
public sealed record PaymentFeeDispositionSelection(Guid FeeId, PaymentFeeDisposition Disposition, string? EvidenceReference);

public sealed record InternalTransferResult(Guid Id, PaymentRecord SourcePayment, PaymentRecord DestinationPayment,
    decimal SourceAmount, string SourceCurrencyCode, decimal DestinationAmount, string DestinationCurrencyCode,
    string BaseCurrencyCode, string? FxSnapshot, DateTimeOffset CompletedAt);

public sealed record PaymentListQuery(Guid LegalEntityId, int Page, int PageSize, string? Search, PaymentDirection? Direction,
    PaymentBusinessState? State, DateOnly? FromDate, DateOnly? ToDate, Guid? BranchId = null);

public sealed record PaymentPage(IReadOnlyList<PaymentRecord> Items, int Page, int PageSize, bool HasMore, int TotalCount = 0);

public sealed class PaymentException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public static class PaymentRules
{
    public static string NormalizeCurrency(string? value)
    {
        var currency = value?.Trim().ToUpperInvariant();
        if (currency is null || currency.Length != 3 || currency.Any(character => !char.IsAsciiLetter(character)))
            throw new PaymentException("payments.currency_invalid", "Select a valid currency.");
        return currency;
    }

    public static decimal ParseAmount(string value)
    {
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount)
            || amount <= 0m || amount > 99999999999999.999999m || decimal.Round(amount, 6) != amount)
            throw new PaymentException("payments.amount_invalid", "Enter an amount greater than zero with no more than six decimal places.");
        return amount;
    }

    public static void Validate(CreatePaymentCommand command)
    {
        if (command.LegalEntityId == Guid.Empty || command.PaymentAccountId == Guid.Empty || string.IsNullOrWhiteSpace(command.IdempotencyKey)
            || command.IdempotencyKey.Trim().Length > 200)
            throw new PaymentException("payments.request_invalid", "Payment details are incomplete.");
        if (command.Direction == PaymentDirection.InternalTransfer)
            throw new PaymentException("payments.transfer_invalid", "Use the internal transfer command for account transfers.");
        var anonymousPosCash = string.Equals(command.SourceType, "pos", StringComparison.OrdinalIgnoreCase)
            && command.Type == PaymentType.CustomerReceipt && command.PartyId is null;
        var partyRequired = command.Type is PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierPayment
            or PaymentType.SupplierAdvance or PaymentType.CustomerRefund or PaymentType.SupplierRefund;
        if (partyRequired && !anonymousPosCash && (command.PartyId is null || command.PartyId == Guid.Empty))
            throw new PaymentException("payments.party_required", "Select a customer or supplier for this payment.");
        var incomingType = command.Type is PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierRefund;
        var outgoingType = command.Type is PaymentType.SupplierPayment or PaymentType.SupplierAdvance or PaymentType.CustomerRefund or PaymentType.ExpensePayment or PaymentType.Standalone;
        if ((!incomingType && !outgoingType) || (incomingType && command.Direction != PaymentDirection.Incoming)
            || (outgoingType && command.Direction != PaymentDirection.Outgoing))
            throw new PaymentException("payments.payment_type_direction_invalid", "The payment type does not match its money direction.");
        _ = ParseAmount(command.Amount);
        _ = NormalizeCurrency(command.CurrencyCode);
        EnsureLength(command.BankReference, 240, "payments.reference_invalid");
        EnsureLength(command.CustomerReference, 240, "payments.reference_invalid");
        EnsureLength(command.SupplierReference, 240, "payments.reference_invalid");
        EnsureLength(command.SourceType, 80, "payments.source_invalid");
        EnsureLength(command.SourceReference, 240, "payments.source_invalid");
        EnsureLength(command.ProviderCode, 80, "payments.provider_invalid");
        EnsureLength(command.ProviderTransactionId, 240, "payments.provider_reference_invalid");
    }

    private static void EnsureLength(string? value, int maximum, string code)
    {
        if (value?.Trim().Length > maximum) throw new PaymentException(code, "One of the payment references is longer than the supported limit.");
    }
}
