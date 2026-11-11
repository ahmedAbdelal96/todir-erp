using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Modules.FixedAssets;

public sealed record CreateAssetCategoryRequest(
    [property: Required, StringLength(100)] string Code,
    [property: Required, StringLength(200)] string Name,
    string AssetNature,
    string TrackingGranularity,
    bool RequiresLocation,
    bool RequiresCustody,
    bool RequiresDepreciation,
    decimal? CapitalizationThreshold,
    string DefaultDepreciationMethod = "StraightLine",
    int? DefaultUsefulLifeMonths = null,
    decimal DefaultResidualValue = 0m);

public sealed record CreateAssetRequest(
    Guid LegalEntityId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? AssetTag,
    string? ProductVariantRef,
    string? InventorySerialRef,
    string? SourceOriginType,
    DateOnly? AcquisitionDate,
    DateOnly? CapitalizationDate,
    DateOnly? PlacedInServiceDate,
    Guid? ParentAssetId,
    decimal? Quantity = null);

public sealed record AssignAssetRequest(
    string AssignmentType,
    Guid? EmployeeId,
    Guid? DepartmentId,
    string? CustodianName,
    Guid? LocationId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Condition,
    string? Accessories,
    string? AcknowledgementReference);
public sealed record CreateAssetLocationRequest(Guid LegalEntityId, Guid? ParentLocationId, string Code, string Name, string LocationType = "Site");
public sealed record CreateAssetHandoverRequest(Guid AssignmentId, string HandoverType, DateTimeOffset? AcknowledgedAt, string? Condition, JsonElement Evidence);
public sealed record AddAssetDocumentRequest(Guid FileRef, string Role, string? Title);
public sealed record CreateVerificationCampaignRequest(Guid LegalEntityId, string Name);
public sealed record VerifyAssetRequest(string Result, Guid? ObservedLocationId, string? Notes);

public sealed record CreateBookRequest(
    Guid LegalEntityId,
    string Code,
    string Name,
    bool PostsToLedger,
    string CurrencyCode,
    string DefaultDepreciationMethod = "StraightLine",
    string DefaultConvention = "FullMonth",
    int? DefaultUsefulLifeMonths = null);

public sealed record CreateAssetBookRequest(Guid LegalEntityId, Guid BookDefinitionId, decimal OriginalCost, decimal ResidualValue, int UsefulLifeMonths, string DepreciationMethod, DateOnly PlacedInServiceDate);

public sealed record CreateDepreciationRunRequest(Guid LegalEntityId, Guid BookId, DateOnly PeriodStart, DateOnly PeriodEnd, string? IdempotencyKey);
public sealed record ReverseDepreciationRunRequest(DateOnly ReversalDate, string Reason, string? IdempotencyKey);
public sealed record DisposeAssetRequest(Guid LegalEntityId, string DisposalType, DateOnly DisposalDate, decimal Proceeds, decimal DirectCosts, string Reason, string? IdempotencyKey, decimal? Quantity = null, string? SaleReference = null);
public sealed record CreateCapitalizationCandidateRequest(Guid LegalEntityId, string SourceType, string SourceReference, decimal EligibleAmount, JsonElement SourceSnapshot, string? EconomicSourceKey = null);
public sealed record CreateCapitalizationCandidateFromInvoiceRequest(Guid LegalEntityId, Guid InvoiceId);
public sealed record CreateCapitalizationCandidateFromInventoryRequest(Guid LegalEntityId, Guid ProductVariantId, Guid? SerialId, DateOnly AsOfDate);
public sealed record CreateCapitalizationCandidateFromExpenseRequest(Guid LegalEntityId, Guid ExpenseId);
public sealed record CreateCapitalizationCandidateFromPurchasingRequest(Guid LegalEntityId, Guid PurchaseOrderId);
public sealed record ReviewCapitalizationCandidateRequest(string Decision, string? Reason);
public sealed record AllocateCapitalizationCandidateRequest(Guid AssetId, decimal Amount, string AllocationType = "Acquisition", DateOnly? EffectiveDate = null);
public sealed record CorrectSourceAllocationRequest(Guid AllocationId, decimal Amount, string Reason, string IdempotencyKey);
public sealed record CreateCipAssetRequest(Guid LegalEntityId, Guid CategoryId, string Name, DateOnly AcquisitionDate, string SourceType, string SourceReference, decimal Amount, string? Description);
public sealed record AddCipCostSourceRequest(string SourceType, string SourceReference, decimal Amount, Guid? ReversalOfId = null);
public sealed record CapitalizeCipRequest(Guid TargetCategoryId, string? Name, DateOnly CapitalizationDate, DateOnly? PlacedInServiceDate = null);
public sealed record ChangeAssetBookCostRequest(Guid LegalEntityId, Guid BookId, decimal Amount, DateOnly EffectiveDate, string Reason, string? IdempotencyKey);
public sealed record CreateAssetBookEstimateChangeRequest(Guid LegalEntityId, DateOnly EffectiveFrom, int? UsefulLifeMonths, decimal? ResidualValue, string Reason, string? IdempotencyKey);
public sealed record CreateAssetImpairmentRequest(Guid LegalEntityId, Guid BookId, decimal Amount, DateOnly EffectiveDate, string Reason, Guid ApprovalRequestId, Guid? EvidenceFileRef, string? IdempotencyKey);
public sealed record CreateAssetRevaluationRequest(Guid LegalEntityId, Guid BookId, decimal ProposedGrossCost, decimal ProposedResidualValue, DateOnly EffectiveDate, string Reason, Guid ApprovalRequestId, string ProviderCode, string ProviderVersion);
public sealed record RequestAssetImpairmentApprovalRequest(Guid LegalEntityId, Guid BookId, decimal Amount, DateOnly EffectiveDate, string Reason, Guid? PolicyVersionId = null);
public sealed record RequestAssetRevaluationApprovalRequest(Guid LegalEntityId, Guid BookId, decimal ProposedGrossCost, decimal ProposedResidualValue, DateOnly EffectiveDate, string Reason, string ProviderCode, string ProviderVersion, Guid? PolicyVersionId = null);
public sealed record CreateOpeningImportRequest(Guid LegalEntityId, string SourceSystem, string BatchReference, DateOnly OpeningDate, IReadOnlyList<OpeningImportLineRequest> Lines);
public sealed record OpeningImportLineRequest(string LegacyAssetNumber, Guid CategoryId, string Name, Guid BookDefinitionId, DateOnly? AcquisitionDate, DateOnly PlacedInServiceDate, decimal OriginalCost, decimal AccumulatedDepreciation, decimal NetBookValue, decimal ResidualValue, int? UsefulLifeMonths, string DepreciationMethod = "StraightLine");

public sealed record AssetCategoryView(Guid Id, string Code, string Name, string AssetNature, string TrackingGranularity, bool RequiresLocation, bool RequiresCustody, bool RequiresDepreciation, decimal? CapitalizationThreshold, string DefaultDepreciationMethod, int? DefaultUsefulLifeMonths, decimal DefaultResidualValue);
public sealed record AssetView(Guid Id, Guid LegalEntityId, Guid CategoryId, string AssetNumber, string Name, string? Description, string? AssetTag, string LifecycleState, string AssetNature, string TrackingGranularity, string? ProductVariantRef, string? InventorySerialRef, DateOnly? AcquisitionDate, DateOnly? CapitalizationDate, DateOnly? PlacedInServiceDate, decimal Quantity, DateTimeOffset CreatedAt, long RowVersion);
public sealed record AssetAssignmentView(Guid Id, Guid AssetId, string AssignmentType, Guid? EmployeeId, Guid? DepartmentId, string? CustodianName, Guid? LocationId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Condition, string? Accessories, string? AcknowledgementReference);
public sealed record AssetLocationView(Guid Id, Guid LegalEntityId, Guid? ParentLocationId, string Code, string Name, string LocationType, bool Active);
public sealed record AssetHandoverView(Guid Id, Guid AssetId, Guid AssignmentId, string HandoverType, DateTimeOffset? AcknowledgedAt, string? Condition, JsonElement Evidence, DateTimeOffset CreatedAt);
public sealed record AssetDocumentView(Guid Id, Guid AssetId, Guid FileRef, string Role, string? Title, string Sha256, string ContentType, long SizeBytes, string Classification, DateTimeOffset CreatedAt);
public sealed record AssetScanView(Guid AssetId, string AssetNumber, string Name, string? AssetTag, string LifecycleState, string QrPayload);
public sealed record AssetTimelineEventView(Guid Id, string EventType, DateOnly? EffectiveDate, DateTimeOffset OccurredAt, JsonElement Payload);
public sealed record VerificationCampaignView(Guid Id, Guid LegalEntityId, string Name, DateTimeOffset SnapshotAt, string Status, int LineCount, int PendingCount, DateTimeOffset CreatedAt);
public sealed record AssetBookView(Guid Id, Guid LegalEntityId, string Code, string Name, bool PostsToLedger, string CurrencyCode, string DefaultDepreciationMethod, string DefaultConvention, int? DefaultUsefulLifeMonths);
public sealed record DepreciationRunView(Guid Id, Guid LegalEntityId, Guid BookId, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, decimal TotalAmount, int LineCount, DateTimeOffset CreatedAt);
public sealed record DisposalView(Guid AssetId, string DisposalType, DateOnly DisposalDate, decimal GrossCost, decimal AccumulatedDepreciation, decimal NetBookValue, decimal Proceeds, decimal DirectCosts, decimal GainLoss, string Status, decimal AccumulatedImpairment = 0m, decimal RevaluationEffects = 0m);
public sealed record CapitalizationCandidateView(Guid Id, Guid LegalEntityId, string SourceType, string SourceReference, JsonElement SourceSnapshot, decimal EligibleAmount, decimal AllocatedAmount, string Status, string? Decision, DateTimeOffset CreatedAt);
public sealed record SourceAllocationView(Guid Id, Guid CandidateId, Guid AssetId, decimal Amount, string AllocationType, DateTimeOffset CreatedAt, bool AlreadyApplied = false);
public sealed record SourceAllocationCorrectionView(Guid Id, Guid AllocationId, Guid CandidateId, Guid AssetId, decimal Amount, decimal RemainingAllocationAmount, decimal CandidateAllocatedAmount, string Reason, DateTimeOffset CreatedAt, bool AlreadyApplied);
public sealed record CipAssetView(Guid AssetId, string AssetNumber, string Name, string LifecycleState, decimal TotalCost, int CostSourceCount, DateOnly? CapitalizationDate, DateOnly? PlacedInServiceDate);
public sealed record AssetBookCostChangeView(Guid AssetId, Guid BookId, decimal Amount, string TransactionType, decimal PreviousCost, decimal NewCost, string? LedgerFactReference, bool AlreadyApplied);
public sealed record AssetBookEstimateChangeView(Guid AssetId, Guid BookId, DateOnly EffectiveFrom, int? UsefulLifeMonths, decimal? ResidualValue, string Reason, string IdempotencyKey, DateTimeOffset CreatedAt, bool AlreadyApplied);
public sealed record AssetDepreciationScheduleLineView(Guid AssetId, Guid BookId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal DepreciationAmount, decimal CumulativeDepreciation, decimal ProjectedNetBookValue, string Method);
public sealed record AssetImpairmentView(Guid AssetId, Guid BookId, decimal Amount, DateOnly EffectiveDate, string Reason, Guid ApprovalRequestId, string Status, string? LedgerFactReference, DateTimeOffset CreatedAt, bool AlreadyApplied);
public sealed record AssetRevaluationView(Guid AssetId, Guid BookId, decimal ProposedGrossCost, decimal ProposedResidualValue, DateOnly EffectiveDate, string Reason, Guid ApprovalRequestId, string ProviderCode, string ProviderVersion, string Status, DateTimeOffset CreatedAt);
public sealed record OpeningImportView(Guid Id, Guid LegalEntityId, string SourceSystem, string BatchReference, DateOnly OpeningDate, string Status, int LineCount, int ValidLineCount, DateTimeOffset CreatedAt);
public sealed record AssetBookBalanceView(Guid AssetId, Guid BookId, Guid LegalEntityId, decimal OriginalCost, decimal AccumulatedDepreciation, decimal NetBookValue, string LifecycleState, int TransactionCount, DateOnly? LastTransactionDate, decimal AccumulatedImpairment = 0m, decimal RevaluationEffects = 0m);
public sealed record AssetGlReconciliationView(Guid LegalEntityId, Guid BookId, DateOnly AsOfDate, decimal AssetGrossCost, decimal LedgerGrossCost, decimal GrossCostDifference, decimal AssetAccumulatedDepreciation, decimal LedgerAccumulatedDepreciation, decimal AccumulatedDepreciationDifference, decimal AssetNetBookValue, string CurrencyCode, string Status, decimal AssetAccumulatedImpairment = 0m, decimal RevaluationEffects = 0m);

