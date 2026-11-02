export type PricingDirection = "Selling" | "Buying";

export type PricingPartyScope = "SpecificParty" | "PriceGroup" | "AllParties";

export type PricingProductScope = "Variant" | "Template" | "Category" | "Brand" | "AllProducts";

export type PricingResultKind = "FixedUnitPrice" | "PercentageDiscount" | "FixedDiscountPerUnit";

export type PricingCurrencyPolicy = "ExactCurrency" | "AllowFinBaseConversion";

export type PriceListStatus = "Active" | "Archived";

export type PriceVersionStatus = "Draft" | "Scheduled" | "Active" | "Expired" | "Archived";

export interface PriceListDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly direction: PricingDirection | string;
  readonly currencyCode: string;
  readonly currencyPolicy: PricingCurrencyPolicy | string;
  readonly status: PriceListStatus | string;
  readonly version: number;
  readonly activeVersionId?: string | null;
}

export interface PriceVersionDto {
  readonly id: string;
  readonly priceListId: string;
  readonly versionNumber: number;
  readonly status: PriceVersionStatus | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly version: number;
}

export interface PriceVersionContentItemDto {
  readonly id: string;
  readonly productScope: string;
  readonly variantId?: string | null;
  readonly templateId?: string | null;
  readonly categoryId?: string | null;
  readonly brandId?: string | null;
  readonly uomId?: string | null;
  readonly partyScope: string;
  readonly partyId?: string | null;
  readonly priceGroupId?: string | null;
  readonly minQuantity: number;
  readonly maxQuantity?: number | null;
  readonly resultKind: string;
  readonly resultValue: string;
  readonly validFrom?: string | null;
  readonly validTo?: string | null;
  readonly priority: number;
  readonly status: string;
  readonly isDirectEntry: boolean;
  readonly variantSku?: string | null;
  readonly productDisplayName?: string | null;
}

export interface PriceVersionContentPageDto {
  readonly items: readonly PriceVersionContentItemDto[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface PriceCoverageDto {
  readonly productCount: number;
  readonly ruleCount: number;
  readonly entryCount: number;
  readonly explicitZeroCount: number;
  readonly missingCandidateCount: number;
}

export interface PricingPolicyDto {
  readonly legalEntityId: string;
  readonly currencyPolicy: PricingCurrencyPolicy | string;
  readonly allowUomDerivedPrice: boolean;
  readonly version: number;
}

export interface PricingPolicyRequest {
  readonly currencyPolicy: PricingCurrencyPolicy;
  readonly allowUomDerivedPrice: boolean;
  readonly expectedVersion?: number;
}

export interface CreatePriceListRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly direction: PricingDirection;
  readonly currencyCode: string;
  readonly currencyPolicy: PricingCurrencyPolicy;
  readonly isDefault: boolean;
  readonly effectiveFrom: string;
}

export interface CreatePriceVersionRequest {
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly expectedListVersion: number;
}

export interface PriceListLifecycleRequest {
  readonly expectedVersion: number;
}

export interface CreatePriceEntryRequest {
  readonly variantId: string;
  readonly uomId: string;
  readonly partyId?: string | null;
  readonly priceGroupId?: string | null;
  readonly minQuantity: number;
  readonly maxQuantity?: number | null;
  readonly unitPrice: string;
  readonly validFrom?: string | null;
  readonly validTo?: string | null;
  readonly priority: number;
}

export interface CreatePriceRuleRequest {
  readonly productScope: PricingProductScope;
  readonly variantId?: string | null;
  readonly templateId?: string | null;
  readonly categoryId?: string | null;
  readonly categoryDepth: number;
  readonly brandId?: string | null;
  readonly partyScope: PricingPartyScope;
  readonly partyId?: string | null;
  readonly priceGroupId?: string | null;
  readonly uomId?: string | null;
  readonly minQuantity: number;
  readonly maxQuantity?: number | null;
  readonly validFrom?: string | null;
  readonly validTo?: string | null;
  readonly priority: number;
  readonly resultKind: PricingResultKind;
  readonly resultValue: string;
}

export interface CreatePriceGroupRequest {
  readonly code: string;
  readonly name: string;
}

export interface AddPriceGroupMemberRequest {
  readonly partyId: string;
  readonly validFrom: string;
  readonly validTo?: string | null;
}

export interface CreatePriceListAssignmentRequest {
  readonly legalEntityId: string;
  readonly direction: PricingDirection;
  readonly priceListId: string;
  readonly partyId?: string | null;
  readonly priceGroupId?: string | null;
  readonly isDefault: boolean;
  readonly validFrom: string;
  readonly validTo?: string | null;
}

export interface ResolvePriceRequest {
  readonly legalEntityId: string;
  readonly direction: PricingDirection;
  readonly pricingDate: string;
  readonly transactionCurrencyCode: string;
  readonly priceListId?: string | null;
  readonly partyId?: string | null;
  readonly productVariantId: string;
  readonly unitOfMeasureId: string;
  readonly quantity: number;
}

export interface ResolvePriceBatchRequest {
  readonly items: readonly ResolvePriceRequest[];
}

export interface PriceSnapshot {
  readonly priceListId: string;
  readonly priceListVersionId: string;
  readonly winningRuleId?: string | null;
  readonly direction: string;
  readonly productVariantId: string;
  readonly unitOfMeasureId: string;
  readonly quantity: number;
  readonly pricingDate: string;
  readonly priceListCurrencyCode: string;
  readonly transactionCurrencyCode: string;
  readonly finalUnitPrice: string;
  readonly baseUnitPrice?: string | null;
  readonly automaticDiscount?: string | null;
  readonly resolverVersion: string;
  readonly exchangeRateSnapshotJson?: string | null;
  readonly capturedAt: string;
}

export interface PriceResolution {
  readonly status: "Resolved" | "PriceMissing" | "Conflict" | string;
  readonly snapshot?: PriceSnapshot | null;
  readonly reasonCode?: string | null;
  readonly explanation: string;
  readonly equivalentWinningRuleIds: readonly string[];
}

export interface PricingImportRequest {
  readonly legalEntityId: string;
  readonly priceListId: string;
  readonly targetVersionId: string;
  readonly csv: string;
}

export interface PricingImportRowDto {
  readonly rowNumber: number;
  readonly sku: string;
  readonly variantId?: string | null;
  readonly uomId?: string | null;
  readonly unitPrice?: string | null;
  readonly currencyCode?: string | null;
  readonly errorCode?: string | null;
  readonly errorMessage?: string | null;
  readonly partyId?: string | null;
  readonly priceGroupId?: string | null;
  readonly minQuantity: number;
  readonly maxQuantity?: number | null;
  readonly validFrom?: string | null;
  readonly validTo?: string | null;
  readonly priority: number;
  readonly ruleType?: string | null;
}

export interface PricingImportPreviewDto {
  readonly batchId: string;
  readonly rowCount: number;
  readonly errorCount: number;
  readonly rows: readonly PricingImportRowDto[];
}

export interface PricingImportCommitDto {
  readonly batchId: string;
  readonly createdEntries: number;
  readonly status: string;
}
