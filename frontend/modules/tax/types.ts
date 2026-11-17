export type TaxCapability = "Disabled" | "Enabled";

export interface SetTaxCapabilityRequest {
  readonly capability: TaxCapability;
  readonly effectiveFrom: string;
}

export interface TaxCapabilityDto {
  readonly legalEntityId: string;
  readonly capability: TaxCapability;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly version: number;
}

export interface TaxCoverageIssue {
  readonly code: string;
  readonly message: string;
  readonly businessReference?: string | null;
}

export interface TaxCoverageResult {
  readonly legalEntityId: string;
  readonly at: string;
  readonly health: "Healthy" | "Warning" | "Blocked" | string;
  readonly capability: TaxCapability | string;
  readonly countryPackStatus: string;
  readonly registrationStatus: string;
  readonly registrationEffective: boolean;
  readonly taxPolicyStatus: string;
  readonly roundingPolicyStatus: string;
  readonly productClassificationCoverage: string;
  readonly partyProfileReadiness: string;
  readonly activeRuleSetStatus: string;
  readonly issues: readonly TaxCoverageIssue[];
}

export interface TaxRegistrationDto {
  readonly id: string;
  readonly registrationSeriesId: string;
  readonly supersedesId?: string | null;
  readonly countryCode: string;
  readonly schemeCode: string;
  readonly registrationNumber?: string | null;
  readonly status: "Registered" | "PendingRegistration" | "RegistrationException" | "Deregistered" | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly authority?: string | null;
  readonly evidenceReference?: string | null;
  readonly version: number;
}

export interface TaxRegistrationRequest {
  readonly countryCode: string;
  readonly schemeCode: string;
  readonly registrationNumber?: string | null;
  readonly status: string;
  readonly effectiveFrom: string;
  readonly authority?: string | null;
  readonly evidenceReference?: string | null;
  readonly legalEntityId: string;
}

export interface TaxRegistrationChangeRequest {
  readonly status: string;
  readonly effectiveFrom: string;
  readonly registrationNumber?: string | null | undefined;
  readonly authority?: string | null | undefined;
  readonly evidenceReference?: string | null | undefined;
}

export interface TaxRegistrationMemberDto {
  readonly id: string;
  readonly registrationId: string;
  readonly legalEntityId: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly status: string;
}

export interface TaxRegistrationMemberRequest {
  readonly legalEntityId: string;
  readonly effectiveFrom: string;
}

export interface TaxProductProfileRequest {
  readonly productTemplateId: string;
  readonly productVariantId?: string | null;
  readonly legalEntityId?: string | null;
  readonly countryCode?: string | null;
  readonly schemeCode?: string | null;
  readonly categoryId: string;
  readonly effectiveFrom: string;
}

export interface TaxPartyProfileRequest {
  readonly partyId: string;
  readonly legalEntityId?: string | null;
  readonly registrationId?: string | null;
  readonly countryCode: string;
  readonly schemeCode?: string | null;
  readonly categoryId?: string | null;
  readonly treatment?: string | null;
  readonly reverseChargeEligible: boolean;
  readonly registrationNumber?: string | null;
  readonly registrationCountryCode?: string | null;
  readonly registrationStatus?: string | null;
  readonly evidenceReference?: string | null;
  readonly effectiveFrom: string;
}

export interface TaxProfileDto {
  readonly id: string;
  readonly partyId?: string | null;
  readonly productTemplateId?: string | null;
  readonly productVariantId?: string | null;
  readonly legalEntityId?: string | null;
  readonly registrationId?: string | null;
  readonly countryCode: string;
  readonly schemeCode?: string | null;
  readonly categoryId?: string | null;
  readonly treatment?: string | null;
  readonly reverseChargeEligible?: boolean | null;
  readonly registrationNumber?: string | null;
  readonly registrationCountryCode?: string | null;
  readonly registrationStatus?: string | null;
  readonly evidenceReference?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly version: number;
}

export interface TaxRuleSetDto {
  readonly id: string;
  readonly legalEntityId?: string | null;
  readonly countryCode: string;
  readonly schemeCode?: string | null;
  readonly version: number;
  readonly status: "Draft" | "Approved" | "Scheduled" | "Active" | "Superseded" | "Retired" | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly sourceKind: string;
  readonly mutable: boolean;
}

export interface TaxRuleSetRequest {
  readonly legalEntityId?: string | null;
  readonly countryCode: string;
  readonly version: number;
  readonly sourceKind: string;
  readonly mutable: boolean;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly schemeCode?: string | null;
}

export interface TaxRuleDto {
  readonly id: string;
  readonly ruleSetId: string;
  readonly stackId?: string | null;
  readonly priority: number;
  readonly treatment: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly sourceKind: string;
  readonly lockedByCountryPack: boolean;
}

export interface TaxRuleRequest {
  readonly stackId?: string | null;
  readonly priority: number;
  readonly registrationId?: string | null;
  readonly jurisdictionCode?: string | null;
  readonly direction?: string | null;
  readonly partyCategoryId?: string | null;
  readonly productCategoryId?: string | null;
  readonly documentKind?: string | null;
  readonly transactionType?: string | null;
  readonly placeOfSupplyCountryCode?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly treatment: string;
  readonly sourceReference: string;
  readonly legalReasonCode?: string | null;
  readonly lockedByCountryPack?: boolean;
}

export interface TaxSchemeItem {
  readonly code: string;
  readonly countryCode?: string;
  readonly name?: string;
  readonly status?: string;
  readonly sourceKind?: string;
  readonly mutable?: boolean;
}

export interface TaxTypeItem {
  readonly code: string;
  readonly schemeCode?: string;
  readonly name?: string;
  readonly status?: string;
  readonly sourceKind?: string;
  readonly mutable?: boolean;
}

export type TaxTreatment =
  | "StandardRated"
  | "ZeroRated"
  | "Exempt"
  | "OutOfScope"
  | "ReverseCharge"
  | string;

export interface TaxTreatmentItem {
  readonly code: string;
  readonly sourceKind?: string;
  readonly mutable?: boolean;
}

export interface TaxCategoryItem {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly scope: "Product" | "Party" | string;
  readonly status?: string;
  readonly sourceKind?: string;
  readonly mutable?: boolean;
}

export interface TaxJurisdictionItem {
  readonly code: string;
  readonly countryCode?: string;
  readonly parentCode?: string;
  readonly subdivisionType?: string;
  readonly name?: string;
  readonly status?: string;
  readonly sourceKind?: string;
  readonly mutable?: boolean;
}

export interface TaxPolicyItem {
  readonly legalEntityId?: string;
  readonly policyCode?: string;
  readonly roundingStrategy?: string;
  readonly moneyScale?: number;
  readonly taxDateBasis?: string;
  readonly timezoneId?: string;
  readonly version?: number;
  readonly sourceKind?: string;
  readonly mutable?: boolean;
}

export interface TaxReferenceDataDto {
  readonly schemes: readonly TaxSchemeItem[];
  readonly types: readonly TaxTypeItem[];
  readonly treatments: readonly TaxTreatmentItem[];
  readonly categories: readonly TaxCategoryItem[];
  readonly jurisdictions: readonly TaxJurisdictionItem[];
  readonly policies: readonly TaxPolicyItem[];
}

export interface TaxCountryPackCapabilitiesDto {
  readonly vat: boolean;
  readonly excise: boolean;
  readonly withholding: boolean;
  readonly taxGrouping: boolean;
  readonly eInvoice: boolean;
}

export interface TaxCountryPackValidationIssueDto {
  readonly code: string;
  readonly message: string;
  readonly itemReference?: string | null;
}

export interface TaxCountryPackValidationDto {
  readonly packId: string;
  readonly valid: boolean;
  readonly issues: readonly TaxCountryPackValidationIssueDto[];
}

export interface TaxCountryPackDto {
  readonly id: string;
  readonly packId: string;
  readonly countryCode: string;
  readonly packCode: string;
  readonly packVersion: string;
  readonly engineContractVersion: string;
  readonly status: "Installed" | "Active" | "Scheduled" | "Superseded" | "Retired" | string;
  readonly reviewStatus: "Unreviewed" | "Reviewed" | "Pending" | "Approved" | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly sourceKind: "TestOnly" | "Statutory" | string;
  readonly sourceAuthority: string;
  readonly sourceTitle: string;
  readonly sourceReference: string;
  readonly capabilities: TaxCountryPackCapabilitiesDto;
  readonly ruleSetReferences: readonly string[];
  readonly goldenVectorCount: number;
  readonly goldenVectorStatus: string;
  readonly validationStatus: string;
  readonly validationIssues: readonly TaxCountryPackValidationIssueDto[];
  readonly installedAt: string;
  readonly installedBy?: string | null;
  readonly validatedAt?: string | null;
  readonly validatedBy?: string | null;
  readonly scheduledAt?: string | null;
  readonly scheduledBy?: string | null;
  readonly scheduledEffectiveFrom?: string | null;
  readonly activatedAt?: string | null;
  readonly activatedBy?: string | null;
  readonly supersededAt?: string | null;
  readonly supersededBy?: string | null;
  readonly retiredAt?: string | null;
  readonly retiredBy?: string | null;
  readonly retirementReason?: string | null;
}

export interface InstallTaxCountryPackRequest {
  readonly countryCode: string;
  readonly packCode: string;
  readonly packVersion: string;
  readonly engineContractVersion: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly sourceKind: string;
  readonly sourceAuthority: string;
  readonly sourceTitle: string;
  readonly sourceReference: string;
  readonly ruleSetReferences: readonly string[];
  readonly capabilities: TaxCountryPackCapabilitiesDto;
  readonly goldenVectorCount: number;
  readonly goldenVectorStatus: string;
}

export interface TaxCountryPackActionRequest {
  readonly effectiveFrom?: string | null | undefined;
  readonly reason?: string | null | undefined;
}

export interface TaxDraftImpactRequest {
  readonly legalEntityId: string;
  readonly taxDate: string;
  readonly countryCode: string;
  readonly schemeCode?: string | null;
  readonly previousPackVersion?: string | null;
  readonly previousRuleSetVersion?: number | null;
  readonly previousTaxFingerprint?: string | null;
}

export interface TaxDraftImpactResult {
  readonly taxRecalculationRequired: boolean;
  readonly decision: string;
  readonly reason?: string | null;
  readonly currentPackVersion?: string | null;
  readonly currentRuleSetVersion?: number | null;
  readonly currentTaxFingerprint?: string | null;
}

export interface TaxCalculationRequest {
  readonly legalEntityId: string;
  readonly direction: "Selling" | "Buying" | string;
  readonly documentKind: string;
  readonly transactionType: string;
  readonly documentDate: string;
  readonly taxDate?: string | null;
  readonly supplyDate?: string | null;
  readonly postingDate?: string | null;
  readonly partyId?: string | null;
  readonly partyCountryCode?: string | null;
  readonly placeOfSupplyCountryCode?: string | null;
  readonly jurisdictionCode?: string | null;
  readonly productTemplateId?: string | null;
  readonly productVariantId?: string | null;
  readonly quantity: number | string;
  readonly unitOfMeasureId?: string | null;
  readonly priceMode: "Exclusive" | "Inclusive" | string;
  readonly commercialAmount: string;
  readonly discountAmount: string;
  readonly taxableCharges: string;
  readonly transactionCurrency: string;
  readonly baseCurrency: string;
  readonly registrationId?: string | null;
  readonly taxSchemeCode?: string | null;
  readonly documentSemantics?: string | null;
  readonly dimensions?: Record<string, string> | null;
}

export interface TaxRecoverabilityContract {
  readonly kind: string;
  readonly recoverablePercentage: string;
  readonly recoverableAmount: string;
  readonly nonRecoverableAmount: string;
}

export interface TaxLineContract {
  readonly taxTypeCode: string;
  readonly taxCode: string;
  readonly treatment: string;
  readonly rate?: string | null;
  readonly fixedAmountPerUnit?: string | null;
  readonly taxableBase: string;
  readonly taxAmount: string;
  readonly currencyCode: string;
  readonly jurisdictionCode: string;
  readonly legalReasonCode?: string | null;
  readonly legalReasonText?: string | null;
  readonly reportingCode?: string | null;
  readonly ruleId: string;
  readonly ruleVersion: string;
  readonly recoverability?: TaxRecoverabilityContract | null;
}

export interface TaxExplanationContract {
  readonly countryPackCode: string;
  readonly countryPackVersion: string;
  readonly taxDate: string;
  readonly jurisdiction: string;
  readonly registrationStatus: string;
  readonly partyTreatment?: string | null;
  readonly productCategory?: string | null;
  readonly matchedRuleId?: string | null;
  readonly componentCodes: readonly string[];
  readonly rounding: string;
  readonly summary: string;
  readonly placeOfSupplyReason?: string | null;
  readonly partyEvidence?: string | null;
  readonly productEvidence?: string | null;
  readonly legalReasonCode?: string | null;
  readonly legalReasonText?: string | null;
}

export interface TaxRoundingEvidenceContract {
  readonly strategy: string;
  readonly moneyScale: number;
  readonly mode: string;
  readonly unroundedTaxTotal: string;
  readonly preReconciliationTotal: string;
  readonly residual: string;
  readonly allocationTarget: string;
  readonly finalTotal: string;
}

export interface TaxResultContract {
  readonly evaluationState: "Determined" | "Undetermined" | "Failed" | string;
  readonly treatment: string | null;
  readonly netAmount: string;
  readonly taxTotal: string;
  readonly grossAmount: string;
  readonly taxLines: readonly TaxLineContract[];
  readonly jurisdictionSnapshot: string;
  readonly registrationSnapshot: string;
  readonly ruleSetVersion?: string | null;
  readonly calculationVersion: string;
  readonly explanation: TaxExplanationContract;
  readonly roundingEvidence?: TaxRoundingEvidenceContract | null;
  readonly taxFxSnapshot?: string | null;
  readonly roundingPolicyVersion?: string | null;
  readonly quantitySnapshot?: string | null;
}

export interface TaxImportRowRequest {
  readonly rowNumber: number;
  readonly reference: string;
  readonly taxCategoryCode: string;
  readonly schemeCode?: string | null;
  readonly legalEntityId?: string | null;
  readonly countryCode?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly registrationCountryCode?: string | null;
  readonly registrationNumber?: string | null;
  readonly treatment?: string | null;
  readonly reverseChargeEligible?: boolean;
  readonly evidenceReference?: string | null;
  readonly productScope?: "Template" | "Variant" | null;
}

export interface TaxProfileImportPreviewRequest {
  readonly kind: "Product" | "Party";
  readonly legalEntityId: string;
  readonly rows: readonly TaxImportRowRequest[];
  readonly sourceReference?: string | null;
}

export interface TaxImportRowResult {
  readonly rowNumber: number;
  readonly reference: string;
  readonly outcome: string;
  readonly message?: string | null;
  readonly targetVersionId?: string | null;
  readonly targetVersion?: number | null;
  readonly businessReference?: string | null;
  readonly productScope?: string | null;
}

export interface TaxProfileImportPreviewDto {
  readonly batchId: string;
  readonly status: string;
  readonly rows: readonly TaxImportRowResult[];
  readonly acceptedCount: number;
  readonly rejectedCount: number;
  readonly skippedCount: number;
}

export interface TaxProfileImportCommitDto {
  readonly batchId: string;
  readonly status: string;
  readonly rows: readonly TaxImportRowResult[];
  readonly inputRows: number;
  readonly acceptedCount: number;
  readonly rejectedCount: number;
  readonly skippedCount: number;
  readonly createdCount: number;
  readonly versionedCount: number;
  readonly transactionPolicy: string;
}

export interface TaxProfileExportDto {
  readonly fileName: string;
  readonly contentType: string;
  readonly content: string;
}
