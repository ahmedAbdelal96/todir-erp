using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Banking;

public enum TreasuryAccountType { Bank, Cash, PettyCash, DigitalWallet, ProviderSettlement }
public enum TreasuryAccountStatus { Draft, Active, Suspended, Closing, Closed }
public enum TreasuryCurrencyPolicy { SingleCurrency, MultiCurrency }

public sealed record TreasuryAccountReference(Guid Id, TenantId TenantId, Guid LegalEntityId, Guid? BranchId,
    string Code, string Name, TreasuryAccountType Type, TreasuryAccountStatus Status, string CurrencyPolicy, string? CurrencyCode);

public interface IBankingTreasuryAccountCapability
{
    Task<TreasuryAccountReference> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, TreasuryAccountCreateRequest request, Guid correlationId, CancellationToken cancellationToken = default);
    Task<TreasuryAccountReference> ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, TreasuryAccountStatus status, long? expectedVersion = null, CancellationToken cancellationToken = default);
    Task<TreasuryAccountReference?> GetAsync(TenantId tenantId, Guid legalEntityId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleForPaymentAsync(TenantId tenantId, Guid legalEntityId, Guid id, string currencyCode, CancellationToken cancellationToken = default);
}

public sealed record TreasuryAccountCreateRequest(Guid LegalEntityId, Guid? BranchId, string Code, string Name,
    TreasuryAccountType Type, TreasuryCurrencyPolicy CurrencyPolicy, string? CurrencyCode, string Purpose,
    string LiquidityClass, IReadOnlyList<string> AllowedUses, string? MaskedIdentifier, string? SecretRef);

public sealed record InternalTransferCapabilityRequest(Guid LegalEntityId, Guid SourceTreasuryAccountId, Guid DestinationTreasuryAccountId,
    decimal Amount, string CurrencyCode, DateOnly PaymentDate, DateOnly PostingDate, DateOnly? ValueDate, string? Reference, string IdempotencyKey);
public sealed record InternalTransferCapabilityResult(Guid TransferId, string State, decimal SourceAmount, string SourceCurrencyCode,
    decimal DestinationAmount, string DestinationCurrencyCode, DateTimeOffset CompletedAt, string BaseCurrencyCode, string? FxSnapshot);
public interface IInternalTransferCapability
{
    Task<InternalTransferCapabilityResult> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid correlationId,
        InternalTransferCapabilityRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Canonical event emitted by a bank connector after it has received a provider feed transaction.</summary>
public sealed record ExternalFeedTransactionReceivedV1(
    Guid LegalEntityId,
    Guid TreasuryAccountId,
    string SourceType,
    string ExternalTransactionId,
    string ContentHash,
    DateOnly BookingDate,
    string Direction,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string RawPayload);

/// <summary>Provider settlement evidence; it is not a customer payment.</summary>
public sealed record ProviderSettlementImportedV1(
    Guid LegalEntityId,
    Guid SettlementAccountId,
    string ProviderCode,
    string ProviderReference,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string CurrencyCode,
    decimal GrossAmount,
    decimal FeesAmount,
    decimal AdjustmentsAmount,
    decimal NetPayoutAmount,
    string ExternalReference);

public sealed record BankOriginatedFinancialEventApprovedV1(
    Guid LegalEntityId,
    Guid CandidateId,
    Guid StatementLineId,
    Guid TreasuryAccountId,
    Guid ActorId,
    string EventType,
    string Direction,
    decimal Amount,
    string CurrencyCode,
    DateOnly BookingDate,
    string EvidenceJson,
    Guid? MembershipId = null);
