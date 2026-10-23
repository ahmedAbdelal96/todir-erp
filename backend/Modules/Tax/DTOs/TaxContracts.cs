namespace Modules.Tax.DTOs;

public sealed record SetTaxCapabilityRequest(string Capability, DateOnly EffectiveFrom);
public sealed record TaxCapabilityDto(Guid LegalEntityId, string Capability, DateOnly EffectiveFrom, DateOnly? EffectiveTo, long Version);
public sealed record TaxCalculationRequest(
    Guid LegalEntityId,
    string Direction,
    string DocumentKind,
    string TransactionType,
    DateOnly DocumentDate,
    DateOnly? TaxDate,
    DateOnly? SupplyDate,
    DateOnly? PostingDate,
    Guid? PartyId,
    string? PartyCountryCode,
    string? PlaceOfSupplyCountryCode,
    string? JurisdictionCode,
    Guid? ProductTemplateId,
    Guid? ProductVariantId,
    decimal Quantity,
    Guid? UnitOfMeasureId,
    string PriceMode,
    string CommercialAmount,
    string DiscountAmount,
    string TaxableCharges,
    string TransactionCurrency,
    string BaseCurrency,
    Guid? RegistrationId,
    string? TaxSchemeCode = null,
    string? DocumentSemantics = null,
    IReadOnlyDictionary<string, string>? Dimensions = null);
public sealed record TaxCalculationBatchRequest(IReadOnlyList<TaxCalculationRequest> Items);

public sealed record TaxRegistrationRequest(string CountryCode, string SchemeCode, string? RegistrationNumber, string Status, DateOnly EffectiveFrom, string? Authority, string? EvidenceReference, Guid LegalEntityId);
public sealed record TaxRegistrationChangeRequest(string Status, DateOnly EffectiveFrom, string? RegistrationNumber, string? Authority, string? EvidenceReference);
public sealed record TaxRegistrationDto(Guid Id, Guid RegistrationSeriesId, Guid? SupersedesId, string CountryCode, string SchemeCode, string? RegistrationNumber, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Authority, string? EvidenceReference, long Version);
public sealed record TaxRegistrationMemberRequest(Guid LegalEntityId, DateOnly EffectiveFrom);
public sealed record TaxRegistrationMemberDto(Guid Id, Guid RegistrationId, Guid LegalEntityId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Status);
public sealed record TaxPartyProfileRequest(Guid PartyId, Guid? LegalEntityId, Guid? RegistrationId, string CountryCode, string? SchemeCode, Guid? CategoryId, string? Treatment, bool ReverseChargeEligible, string? RegistrationNumber, string? RegistrationCountryCode, string? RegistrationStatus, string? EvidenceReference, DateOnly EffectiveFrom);
public sealed record TaxProductProfileRequest(Guid ProductTemplateId, Guid? ProductVariantId, Guid? LegalEntityId, string? CountryCode, string? SchemeCode, Guid CategoryId, DateOnly EffectiveFrom);
public sealed record TaxProfileDto(Guid Id, Guid? PartyId, Guid? ProductTemplateId, Guid? ProductVariantId, Guid? LegalEntityId, Guid? RegistrationId, string CountryCode, string? SchemeCode, Guid? CategoryId, string? Treatment, bool? ReverseChargeEligible, string? RegistrationNumber, string? RegistrationCountryCode, string? RegistrationStatus, string? EvidenceReference, DateOnly EffectiveFrom, DateOnly? EffectiveTo, long Version);
public sealed record TaxRuleSetRequest(Guid? LegalEntityId, string CountryCode, int Version, string SourceKind, bool Mutable, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? SchemeCode = null);
public sealed record TaxRuleRequest(Guid? StackId, int Priority, Guid? RegistrationId, string? JurisdictionCode, string? Direction, Guid? PartyCategoryId, Guid? ProductCategoryId, string? DocumentKind, string? TransactionType, string? PlaceOfSupplyCountryCode, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Treatment, string SourceReference, string? LegalReasonCode, bool LockedByCountryPack = false);
public sealed record TaxRuleSetDto(Guid Id, Guid? LegalEntityId, string CountryCode, string? SchemeCode, int Version, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string SourceKind, bool Mutable);
public sealed record TaxRuleDto(Guid Id, Guid RuleSetId, Guid? StackId, int Priority, string Treatment, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string SourceKind, bool LockedByCountryPack);
public sealed record TaxReferenceDataDto(IReadOnlyList<object> Schemes, IReadOnlyList<object> Types, IReadOnlyList<object> Treatments, IReadOnlyList<object> Categories, IReadOnlyList<object> Jurisdictions, IReadOnlyList<object> Policies);

public sealed record TaxCoverageRequest(Guid LegalEntityId, DateOnly At, string CountryCode, string? SchemeCode = null, Guid? ProductTemplateId = null, Guid? ProductVariantId = null, Guid? PartyId = null, string? Direction = null, string? DocumentKind = null, string? TransactionType = null, string? JurisdictionCode = null);
public sealed record TaxCoverageIssue(string Code, string Message, string? BusinessReference = null);
public sealed record TaxCoverageResult(Guid LegalEntityId, DateOnly At, string Health, string Capability, string CountryPackStatus, string RegistrationStatus, bool RegistrationEffective, string TaxPolicyStatus, string RoundingPolicyStatus, string ProductClassificationCoverage, string PartyProfileReadiness, string ActiveRuleSetStatus, IReadOnlyList<TaxCoverageIssue> Issues);

public sealed record TaxImportRowRequest(int RowNumber, string Reference, string TaxCategoryCode, string? SchemeCode, Guid? LegalEntityId, string? CountryCode, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? RegistrationCountryCode = null, string? RegistrationNumber = null, string? Treatment = null, bool ReverseChargeEligible = false, string? EvidenceReference = null, string? ProductScope = null);
public sealed record TaxProfileImportPreviewRequest(string Kind, Guid LegalEntityId, IReadOnlyList<TaxImportRowRequest> Rows, string? SourceReference = null);
public sealed record TaxImportRowResult(int RowNumber, string Reference, string Outcome, string? Message, Guid? TargetVersionId = null, long? TargetVersion = null, string? BusinessReference = null, string? ProductScope = null);
public sealed record TaxProfileImportPreviewDto(Guid BatchId, string Status, IReadOnlyList<TaxImportRowResult> Rows, int AcceptedCount, int RejectedCount, int SkippedCount);
public sealed record TaxProfileImportCommitRequest(Guid BatchId);
public sealed record TaxProfileImportCommitDto(Guid BatchId, string Status, IReadOnlyList<TaxImportRowResult> Rows, int InputRows, int AcceptedCount, int RejectedCount, int SkippedCount, int CreatedCount, int VersionedCount, string TransactionPolicy);
public sealed record TaxProfileExportRequest(string Kind, Guid LegalEntityId, DateOnly? At = null);
public sealed record TaxProfileExportDto(string FileName, string ContentType, string Content);

public sealed record TaxCountryPackCapabilitiesDto(bool Vat, bool Excise, bool Withholding, bool TaxGrouping, bool EInvoice);
public sealed record InstallTaxCountryPackRequest(
    string CountryCode,
    string PackCode,
    string PackVersion,
    string EngineContractVersion,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceKind,
    string SourceAuthority,
    string SourceTitle,
    string SourceReference,
    IReadOnlyList<string> RuleSetReferences,
    TaxCountryPackCapabilitiesDto Capabilities,
    int GoldenVectorCount,
    string GoldenVectorStatus);
public sealed record TaxCountryPackDto(
    Guid Id,
    string PackId,
    string CountryCode,
    string PackCode,
    string PackVersion,
    string EngineContractVersion,
    string Status,
    string ReviewStatus,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceKind,
    string SourceAuthority,
    string SourceTitle,
    string SourceReference,
    TaxCountryPackCapabilitiesDto Capabilities,
    IReadOnlyList<string> RuleSetReferences,
    int GoldenVectorCount,
    string GoldenVectorStatus,
    string ValidationStatus,
    IReadOnlyList<TaxCountryPackValidationIssueDto> ValidationIssues,
    DateTimeOffset InstalledAt,
    Guid? InstalledBy,
    DateTimeOffset? ValidatedAt,
    Guid? ValidatedBy,
    DateTimeOffset? ScheduledAt,
    Guid? ScheduledBy,
    DateOnly? ScheduledEffectiveFrom,
    DateTimeOffset? ActivatedAt,
    Guid? ActivatedBy,
    DateTimeOffset? SupersededAt,
    Guid? SupersededBy,
    DateTimeOffset? RetiredAt,
    Guid? RetiredBy,
    string? RetirementReason);
public sealed record TaxCountryPackValidationDto(Guid PackId, bool Valid, IReadOnlyList<TaxCountryPackValidationIssueDto> Issues);
public sealed record TaxCountryPackValidationIssueDto(string Code, string Message, string? ItemReference = null);
public sealed record TaxCountryPackActionRequest(DateOnly? EffectiveFrom = null, string? Reason = null);
public sealed record TaxDraftImpactRequest(Guid LegalEntityId, DateOnly TaxDate, string CountryCode, string? SchemeCode, string? PreviousPackVersion, int? PreviousRuleSetVersion, string? PreviousTaxFingerprint);
public sealed record TaxDraftImpactResult(bool TaxRecalculationRequired, string Decision, string? Reason, string? CurrentPackVersion, int? CurrentRuleSetVersion, string? CurrentTaxFingerprint);
