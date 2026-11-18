using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Payments;

public sealed record ChequeInTransitReference(Guid PaymentId, Guid LegalEntityId, string PaymentNumber,
    string Direction, decimal Amount, string CurrencyCode, Guid PaymentAccountId, DateOnly PaymentDate,
    string State, string? BankReference, string? SourceReference);
public sealed record ChequeInTransitPage(IReadOnlyList<ChequeInTransitReference> Items, int Page, int PageSize, bool HasMore, int TotalCount);
public interface IPaymentChequeReadCapability
{
    Task<ChequeInTransitPage> ListChequeInTransitAsync(TenantId tenantId, Guid legalEntityId, int page, int pageSize,
        CancellationToken cancellationToken = default);
}

public sealed record BankOriginatedReceiptRequest(Guid LegalEntityId, Guid PaymentAccountId, decimal Amount,
    string CurrencyCode, DateOnly PaymentDate, DateOnly PostingDate, string? BankReference, string SourceReference,
    string IdempotencyKey);
public sealed record BankOriginatedReceiptResult(Guid PaymentId, string PaymentNumber, string State, decimal Amount,
    string CurrencyCode);
public interface IBankOriginatedPaymentCapability
{
    Task<BankOriginatedReceiptResult> CreateUnallocatedReceiptAsync(TenantId tenantId, Guid actorId, Guid membershipId,
        Guid correlationId, BankOriginatedReceiptRequest request, CancellationToken cancellationToken = default);
}

public sealed record PosCashPaymentRequest(Guid LegalEntityId, Guid? BranchId, Guid? PartyId, decimal Amount, string CurrencyCode,
    Guid PaymentAccountId, DateOnly PaymentDate, DateOnly PostingDate, string SourceReference, string IdempotencyKey);

public sealed record PosCardPaymentRequest(Guid LegalEntityId, Guid? BranchId, Guid PartyId, decimal Amount, string CurrencyCode,
    Guid ProviderConfigId, string SourceReference, string IdempotencyKey);

public sealed record PosPaymentResult(Guid PaymentId, string State, decimal Amount, string CurrencyCode, string Method);
public sealed record PosProviderPaymentResult(Guid PaymentRequestId, string State, decimal Amount, string CurrencyCode, string MerchantReference);
public sealed record PosCashRefundRequest(Guid LegalEntityId, Guid OriginalPaymentId, Guid PaymentAccountId, decimal Amount, string CurrencyCode, DateOnly PaymentDate, string ReasonCode, string IdempotencyKey);
public sealed record PosRefundResult(Guid RefundPaymentId, string State, decimal Amount, string CurrencyCode);
public sealed record PosCapturedPaymentReference(Guid PaymentId, Guid PaymentAccountId, decimal Amount, string CurrencyCode, string Method, string State);

public interface IPosPaymentCapabilities
{
    Task<PosPaymentResult> CaptureCashAsync(Guid tenantId, Guid actorId, Guid membershipId,
        PosCashPaymentRequest request, Guid correlationId, CancellationToken cancellationToken = default);

    Task<PosProviderPaymentResult> BeginCardAsync(Guid tenantId, Guid actorId, Guid membershipId,
        PosCardPaymentRequest request, Guid correlationId, CancellationToken cancellationToken = default);

    Task<PosProviderPaymentResult> SyncCardAsync(Guid tenantId, Guid legalEntityId, Guid paymentRequestId,
        CancellationToken cancellationToken = default);

    Task<PosRefundResult> RefundCashAsync(Guid tenantId, Guid actorId, Guid membershipId, PosCashRefundRequest request, Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<PosCapturedPaymentReference?> FindCapturedPaymentAsync(Guid tenantId, Guid legalEntityId, Guid paymentRequestId,
        CancellationToken cancellationToken = default);

    Task<PosRefundResult> RefundCardAsync(Guid tenantId, Guid actorId, Guid membershipId, Guid legalEntityId,
        Guid originalPaymentId, Guid paymentAccountId, decimal amount, string currencyCode, DateOnly paymentDate, string reasonCode,
        string idempotencyKey, Guid correlationId, CancellationToken cancellationToken = default);
}
