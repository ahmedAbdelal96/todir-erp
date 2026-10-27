using Modules.Payments.Entities;

namespace Modules.Payments.DTOs;

public sealed record CreatePaymentAccountRequest(Guid LegalEntityId, Guid? BranchId, string Code, string Name, PaymentAccountType Type,
    string CurrencyPolicy, string? CurrencyCode, string? BankName, string? AccountHolder, string? MaskedIdentifier, string? SecretRef);

public sealed record UpdatePaymentAccountStatusRequest(Guid? BranchId, PaymentAccountStatus Status);

public sealed record CreateRefundRequest(Guid PaymentAccountId, PaymentMethod Method, string Amount, DateOnly PaymentDate,
    DateOnly PostingDate, DateOnly? ValueDate, string ReasonCode, string? BankReference);

public sealed record ReversePaymentRequest(DateOnly PaymentDate, DateOnly PostingDate, string ReasonCode,
    IReadOnlyList<PaymentFeeDispositionRequest>? FeeDispositions = null);
public sealed record PaymentFeeDispositionRequest(Guid FeeId, PaymentFeeDisposition Disposition, string? EvidenceReference = null);

public sealed record CreatePaymentIntentRequest(Guid LegalEntityId, Guid? BranchId, Guid ProviderConfigId, Guid? PartyId,
    string Amount, string CurrencyCode, string? SourceType, string? SourceReference, DateTimeOffset? ExpiresAt);

public sealed record RetryPaymentIntentRequest(Guid LegalEntityId);

public sealed record PaymentIntentResult(Guid Id, Guid LegalEntityId, string MerchantReference, decimal Amount, string CurrencyCode,
    string State, Guid AttemptId, int AttemptNumber);
public sealed record PaymentAttemptReadModel(Guid Id, int AttemptNumber, string State, string? ProviderState,
    string? FailureCode, string? SafeFailureMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PaymentRequestDetails(PaymentIntentResult Request, IReadOnlyList<PaymentAttemptReadModel> Attempts,
    DateTimeOffset? ExpiresAt);
public sealed record PaymentRequestListQuery(Guid LegalEntityId, int Page, int PageSize, string? Search, string? State);
public sealed record PaymentRequestPage(IReadOnlyList<PaymentIntentResult> Items, int Page, int PageSize, bool HasMore);

public sealed record ProviderPaymentOperation(Guid OperationId, Guid RequestId, Guid AttemptId, Guid LegalEntityId, Guid? BranchId,
    Guid? PartyId, Guid PaymentAccountId, Guid CreatedBy, Guid? CreatedByMembershipId, decimal Amount, string CurrencyCode,
    string MerchantReference, string OperationState, string? ProviderTransactionId, string IdempotencyKey,
    ProviderOperationCapabilities Capabilities);

public sealed record ReferenceProviderResult(string Status, string TransactionId, DateTimeOffset ConfirmedAt);

public sealed record ReferenceProviderCancelResult(string Status, DateTimeOffset ConfirmedAt);

public sealed record ReferenceWebhookEnvelope(string EventId, string MerchantReference, string TransactionId, string EventType, DateTimeOffset OccurredAt);

public sealed record ProviderWebhookReceipt(Guid InboxId, Guid? OperationId, bool Duplicate, bool ShouldComplete);

public sealed record ProviderRefundOperation(Guid OperationId, Guid RefundId, Guid RefundPaymentId, Guid OriginalPaymentId,
    Guid ProviderConfigId, string ProviderTransactionId, string IdempotencyKey, string State, decimal Amount, string CurrencyCode);

public sealed record ProviderRefundResult(PaymentRecord Refund, string ProviderState, Guid OperationId);

public sealed record ReferenceProviderRefundResult(string Status, string RefundId, DateTimeOffset ConfirmedAt);

public sealed record CreatePaymentLinkRequest(Guid PaymentRequestId, DateTimeOffset ExpiresAt,
    PaymentLinkAmountPolicy AmountPolicy = PaymentLinkAmountPolicy.Fixed, string? MinimumAmount = null, string? MaximumAmount = null);

public sealed record PaymentLinkResult(Guid Id, Guid PaymentRequestId, string Token, string Status, DateTimeOffset ExpiresAt);
public sealed record PublicPaymentLinkDetails(decimal Amount, string CurrencyCode, string AmountPolicy,
    decimal? MinimumAmount, decimal? MaximumAmount, string Status, DateTimeOffset ExpiresAt);

public sealed record ProviderOperationCapabilities(bool CreatePaymentRequest, bool GetPaymentStatus, bool Capture, bool Cancel, bool Refund, bool Webhooks);

public sealed record CreatePaymentProviderRequest(Guid LegalEntityId, string ProviderCode, string Environment, string MerchantReference,
    string ApiVersion, ProviderOperationCapabilities Capabilities, IReadOnlyList<string> SupportedCurrencies,
    IReadOnlyList<string> SupportedMethods, Guid PaymentAccountId, string? SecretRef);

public sealed record PaymentProviderResult(Guid Id, Guid LegalEntityId, string ProviderCode, string Environment, string MerchantReference,
    string ApiVersion, ProviderOperationCapabilities Capabilities, IReadOnlyList<string> SupportedCurrencies,
    IReadOnlyList<string> SupportedMethods, Guid PaymentAccountId, bool HasSecretReference, string Status, long Version);

public sealed record CreatePaymentFeeRequest(PaymentFeeType Type, string Amount, string CurrencyCode, string? SourceReference);

public sealed record PaymentFeeResult(Guid Id, PaymentFeeType Type, decimal Amount, string CurrencyCode, string RecognitionState);

public sealed record CreateInternalTransferRequest(Guid LegalEntityId, Guid SourcePaymentAccountId, Guid DestinationPaymentAccountId,
    string Amount, string CurrencyCode, DateOnly PaymentDate, DateOnly PostingDate, DateOnly? ValueDate, string? Reference,
    string? DestinationCurrencyCode = null);

public sealed record ApplyPaymentLineRequest(Guid TargetOpenItemId, string Amount);

public sealed record ApplyPaymentRequest(DateOnly ApplicationDate, DateOnly SettlementDate, IReadOnlyList<ApplyPaymentLineRequest> Lines);

public sealed record CreatePaymentRequest(Guid LegalEntityId, Guid? BranchId, PaymentType Type, Guid? PartyId, string Amount,
    string CurrencyCode, PaymentMethod Method, Guid PaymentAccountId, DateOnly PaymentDate, DateOnly PostingDate, DateOnly? ValueDate,
    string? BankReference, string? CustomerReference, string? SupplierReference, string? SourceType, string? SourceReference);
