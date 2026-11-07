namespace Shared.Contracts.Payments;

public sealed record PosCashPaymentRequest(Guid LegalEntityId, Guid? BranchId, Guid? PartyId, decimal Amount, string CurrencyCode,
    Guid PaymentAccountId, DateOnly PaymentDate, DateOnly PostingDate, string SourceReference, string IdempotencyKey);

public sealed record PosCardPaymentRequest(Guid LegalEntityId, Guid? BranchId, Guid PartyId, decimal Amount, string CurrencyCode,
    Guid ProviderConfigId, string SourceReference, string IdempotencyKey);

public sealed record PosPaymentResult(Guid PaymentId, string State, decimal Amount, string CurrencyCode, string Method);
public sealed record PosProviderPaymentResult(Guid PaymentRequestId, string State, decimal Amount, string CurrencyCode, string MerchantReference);
public sealed record PosCashRefundRequest(Guid LegalEntityId, Guid OriginalPaymentId, Guid PaymentAccountId, decimal Amount, string CurrencyCode, DateOnly PaymentDate, string ReasonCode, string IdempotencyKey);
public sealed record PosRefundResult(Guid RefundPaymentId, string State, decimal Amount, string CurrencyCode);

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
}
