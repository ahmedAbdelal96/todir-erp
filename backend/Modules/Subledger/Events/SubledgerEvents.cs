namespace Modules.Subledger.Events;

public static class SubledgerEventTypes
{
    public const string OpenItemCreatedV1 = "OpenItemCreatedV1";
    public const string ApplicationConfirmedV1 = "ApplicationConfirmedV1";
    public const string ApplicationReversedV1 = "ApplicationReversedV1";
    public const string PartyFinancialProfileChangedV1 = "PartyFinancialProfileChangedV1";
    public const string CreditLimitChangedV1 = "CreditLimitChangedV1";
    public const string WriteOffApprovedV1 = "WriteOffApprovedV1";
    public const string OpeningBatchFinalizedV1 = "OpeningBatchFinalizedV1";
}

public sealed record OpenItemCreatedEventV1(Guid ItemId, Guid LegalEntityId, Guid PartyId, string Direction, string SourceType);
public sealed record ApplicationConfirmedEventV1(Guid BatchId, Guid LegalEntityId, Guid PartyId, string Direction, int ApplicationCount);
public sealed record ApplicationReversedEventV1(Guid BatchId, Guid OriginalApplicationId, Guid LegalEntityId, Guid PartyId, string Direction);
public sealed record PartyFinancialProfileChangedEventV1(Guid ProfileId, Guid LegalEntityId, Guid PartyId, long Version);
public sealed record CreditLimitChangedEventV1(Guid ProfileId, Guid LegalEntityId, Guid PartyId, long Version);
public sealed record WriteOffApprovedEventV1(Guid WriteOffId, Guid RequestId, Guid LegalEntityId, Guid OpenItemId, Guid LedgerFactId);
public sealed record OpeningBatchFinalizedEventV1(Guid BatchId, Guid LegalEntityId, int ItemCount);
