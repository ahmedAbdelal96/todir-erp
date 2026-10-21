using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Pricing;

public enum PricingDirection { Selling, Buying }
public enum PricingPartyScope { SpecificParty, PriceGroup, AllParties }
public enum PricingProductScope { Variant, Template, Category, Brand, AllProducts }
public enum PricingResultKind { FixedUnitPrice, PercentageDiscount, FixedDiscountPerUnit }
public enum PricingCurrencyPolicy { ExactCurrency, AllowFinBaseConversion }

public sealed record PricingContext(
    TenantId TenantId,
    LegalEntityId LegalEntityId,
    PricingDirection Direction,
    DateOnly PricingDate,
    string TransactionCurrencyCode,
    Guid? PriceListId,
    Guid? PartyId,
    Guid? PriceGroupId,
    Guid ProductVariantId,
    Guid ProductTemplateId,
    IReadOnlyList<Guid> CategoryPath,
    Guid? BrandId,
    Guid UnitOfMeasureId,
    decimal Quantity);

public sealed record PriceSnapshot(
    Guid PriceListId,
    Guid PriceListVersionId,
    Guid? WinningRuleId,
    string Direction,
    Guid ProductVariantId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    DateOnly PricingDate,
    string PriceListCurrencyCode,
    string TransactionCurrencyCode,
    string FinalUnitPrice,
    string? BaseUnitPrice,
    string? AutomaticDiscount,
    string ResolverVersion,
    string? ExchangeRateSnapshotJson,
    DateTimeOffset CapturedAt);

public sealed record PriceResolution(
    string Status,
    PriceSnapshot? Snapshot,
    string? ReasonCode,
    string Explanation,
    IReadOnlyList<Guid> EquivalentWinningRuleIds);

public sealed record PriceOverrideDeviation(string ResolvedUnitPrice, string FinalUnitPrice, string Deviation, string CurrencyCode, string Reason);

/// <summary>In-process Pricing contract for transaction modules; it carries no persistence concepts.</summary>
public interface IPricingCapabilities
{
    Task<PriceResolution> ResolveAsync(PricingContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceResolution>> ResolveBatchAsync(IReadOnlyList<PricingContext> contexts, CancellationToken cancellationToken = default);
}
