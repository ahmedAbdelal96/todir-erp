namespace Shared.Contracts.Payments;

public sealed record PosCashPaymentRequest(Guid LegalEntityId, Guid? BranchId, Guid? PartyId, decimal Amount, string CurrencyCode,
    Guid PaymentAccountId, DateOnly PaymentDate, DateOnly PostingDate, string SourceReference, string IdempotencyKey);

public sealed record PosPaymentResult(Guid PaymentId, string State, decimal Amount, string CurrencyCode, string Method);

public interface IPosPaymentCapabilities
{
    Task<PosPaymentResult> CaptureCashAsync(Guid tenantId, Guid actorId, Guid membershipId,
        PosCashPaymentRequest request, Guid correlationId, CancellationToken cancellationToken = default);
}
