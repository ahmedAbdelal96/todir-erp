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
