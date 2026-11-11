namespace Shared.Contracts.FixedAssets;

public sealed record FixedAssetCreatedV1(Guid AssetId, Guid LegalEntityId, string AssetNumber, string LifecycleState, string AssetNature, DateTimeOffset OccurredAt);
public sealed record FixedAssetAssignedV1(Guid AssetId, Guid AssignmentId, Guid LegalEntityId, Guid? EmployeeId, Guid? DepartmentId, Guid? LocationId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, DateTimeOffset OccurredAt);
public sealed record FixedAssetCapitalizedV1(Guid AssetId, Guid LegalEntityId, DateOnly CapitalizationDate, DateOnly? PlacedInServiceDate, decimal TotalCost, DateTimeOffset OccurredAt);
public sealed record FixedAssetDepreciatedV1(Guid RunId, Guid LegalEntityId, Guid BookDefinitionId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal TotalAmount, int LineCount, DateTimeOffset OccurredAt);
public sealed record FixedAssetDisposedV1(Guid AssetId, Guid LegalEntityId, string DisposalType, DateOnly DisposalDate, decimal NetBookValue, decimal GainLoss, DateTimeOffset OccurredAt);
