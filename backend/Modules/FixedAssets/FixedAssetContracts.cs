using System.ComponentModel.DataAnnotations;

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
public sealed record DisposeAssetRequest(Guid LegalEntityId, string DisposalType, DateOnly DisposalDate, decimal Proceeds, decimal DirectCosts, string Reason, string? IdempotencyKey);

public sealed record AssetCategoryView(Guid Id, string Code, string Name, string AssetNature, string TrackingGranularity, bool RequiresLocation, bool RequiresCustody, bool RequiresDepreciation, decimal? CapitalizationThreshold, string DefaultDepreciationMethod, int? DefaultUsefulLifeMonths, decimal DefaultResidualValue);
public sealed record AssetView(Guid Id, Guid LegalEntityId, Guid CategoryId, string AssetNumber, string Name, string? Description, string? AssetTag, string LifecycleState, string AssetNature, string TrackingGranularity, string? ProductVariantRef, string? InventorySerialRef, DateOnly? AcquisitionDate, DateOnly? CapitalizationDate, DateOnly? PlacedInServiceDate, decimal Quantity, DateTimeOffset CreatedAt, long RowVersion);
public sealed record AssetAssignmentView(Guid Id, Guid AssetId, string AssignmentType, Guid? EmployeeId, Guid? DepartmentId, string? CustodianName, Guid? LocationId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Condition, string? Accessories, string? AcknowledgementReference);
public sealed record AssetBookView(Guid Id, Guid LegalEntityId, string Code, string Name, bool PostsToLedger, string CurrencyCode, string DefaultDepreciationMethod, string DefaultConvention, int? DefaultUsefulLifeMonths);
public sealed record DepreciationRunView(Guid Id, Guid LegalEntityId, Guid BookId, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, decimal TotalAmount, int LineCount, DateTimeOffset CreatedAt);
public sealed record DisposalView(Guid AssetId, string DisposalType, DateOnly DisposalDate, decimal GrossCost, decimal AccumulatedDepreciation, decimal NetBookValue, decimal Proceeds, decimal DirectCosts, decimal GainLoss, string Status);

