using Shared.Contracts.Pricing;

namespace Modules.Pricing.DTOs;

public sealed record CreatePriceListRequest(Guid LegalEntityId, string Code, string Name, PricingDirection Direction, string CurrencyCode, PricingCurrencyPolicy CurrencyPolicy, bool IsDefault, DateOnly EffectiveFrom);
public sealed record CreatePriceVersionRequest(DateOnly EffectiveFrom, DateOnly? EffectiveTo, long ExpectedListVersion);
public sealed record PriceListLifecycleRequest(long ExpectedVersion);
public sealed record CreatePriceGroupRequest(string Code, string Name);
public sealed record AddPriceGroupMemberRequest(Guid PartyId, DateOnly ValidFrom, DateOnly? ValidTo);
public sealed record CreatePriceListAssignmentRequest(Guid LegalEntityId, PricingDirection Direction, Guid PriceListId, Guid? PartyId, Guid? PriceGroupId, bool IsDefault, DateOnly ValidFrom, DateOnly? ValidTo);
public sealed record CreatePriceEntryRequest(Guid VariantId, Guid UomId, Guid? PartyId, Guid? PriceGroupId, decimal MinQuantity, decimal? MaxQuantity, string UnitPrice, DateOnly? ValidFrom, DateOnly? ValidTo, int Priority);
public sealed record CreatePriceRuleRequest(PricingProductScope ProductScope, Guid? VariantId, Guid? TemplateId, Guid? CategoryId, int CategoryDepth, Guid? BrandId, PricingPartyScope PartyScope, Guid? PartyId, Guid? PriceGroupId, Guid? UomId, decimal MinQuantity, decimal? MaxQuantity, DateOnly? ValidFrom, DateOnly? ValidTo, int Priority, PricingResultKind ResultKind, string ResultValue);
public sealed record ResolvePriceRequest(Guid LegalEntityId, PricingDirection Direction, DateOnly PricingDate, string TransactionCurrencyCode, Guid? PriceListId, Guid? PartyId, Guid ProductVariantId, Guid UnitOfMeasureId, decimal Quantity);
public sealed record ResolvePriceBatchRequest(IReadOnlyList<ResolvePriceRequest> Items);
public sealed record PriceListDto(Guid Id, Guid LegalEntityId, string Code, string Name, string Direction, string CurrencyCode, string CurrencyPolicy, string Status, long Version, Guid? ActiveVersionId);
public sealed record PriceVersionDto(Guid Id, Guid PriceListId, int VersionNumber, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, long Version);
public sealed record PriceCoverageDto(int ProductCount, int RuleCount, int EntryCount, int ExplicitZeroCount, int MissingCandidateCount);
public sealed record PricingPolicyRequest(PricingCurrencyPolicy CurrencyPolicy, bool AllowUomDerivedPrice, long ExpectedVersion = 0);
public sealed record PricingPolicyDto(Guid LegalEntityId, string CurrencyPolicy, bool AllowUomDerivedPrice, long Version);
public sealed record PricingImportRequest(Guid LegalEntityId, Guid PriceListId, Guid TargetVersionId, string Csv);
public sealed record PricingImportPreviewDto(Guid BatchId, int RowCount, int ErrorCount, IReadOnlyList<PricingImportRowDto> Rows);
public sealed record PricingImportRowDto(int RowNumber, string Sku, Guid? VariantId, Guid? UomId, string? UnitPrice, string? CurrencyCode, string? ErrorCode, string? ErrorMessage, Guid? PartyId = null, Guid? PriceGroupId = null, decimal MinQuantity = 0m, decimal? MaxQuantity = null, DateOnly? ValidFrom = null, DateOnly? ValidTo = null, int Priority = 0, string? RuleType = null);
public sealed record PricingImportCommitDto(Guid BatchId, int CreatedEntries, string Status);

public sealed record PriceOverrideCalculationRequest(string ResolvedUnitPrice, string OverrideUnitPrice, string CurrencyCode, string Reason);
