using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Subledger;

public enum SubledgerDirectionContract { Receivable, Payable }
public enum SubledgerPolarityContract { Debit, Credit }

public sealed record PartyFinancialProfileCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    Guid? DefaultReceivablePaymentTermId,
    Guid? DefaultPayablePaymentTermId,
    string? CreditLimitAmount,
    string? CreditLimitCurrency,
    string? ReceivablePolicyCode,
    string? PayablePolicyCode,
    long ExpectedVersion);

public sealed record PartyFinancialProfileResult(
    Guid Id,
    Guid PartyId,
    Guid LegalEntityId,
    Guid? DefaultReceivablePaymentTermId,
    Guid? DefaultPayablePaymentTermId,
    string? CreditLimitAmount,
    string? CreditLimitCurrency,
    string? ReceivablePolicyCode,
    string? PayablePolicyCode,
    long Version,
    DateTimeOffset UpdatedAt);

public sealed record OpenItemSnapshotContract(
    string SourceReference,
    string? SourceScheduleReference,
    string? SourceDisplayNumber,
    DateOnly DocumentDate,
    DateOnly PostingDate,
    DateOnly DueDate,
    string TransactionCurrency,
    string OriginalTransactionAmount,
    string BaseCurrency,
    string OriginalBaseAmount,
    string ControlAccountRole,
    string ControlAccountReference,
    string ControlConfigurationReference,
    string? HistoricFxSnapshot,
    long SourceVersion);

public sealed record CreateOpenItemsCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    IReadOnlyList<OpenItemSnapshotContract> Items,
    Guid ActorId,
    Guid CorrelationId);

public sealed record RegisterPaymentCreditCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    SubledgerDirectionContract Direction,
    OpenItemSnapshotContract Credit,
    Guid ActorId,
    Guid CorrelationId);

public sealed record ApplicationLineCommand(Guid DebitItemId, Guid CreditItemId, string Amount);

public sealed record ApplyOpenItemsCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    SubledgerDirectionContract Direction,
    string IdempotencyKey,
    DateOnly ApplicationDate,
    DateOnly SettlementDate,
    IReadOnlyList<ApplicationLineCommand> Lines,
    Guid ActorId,
    Guid CorrelationId);

public sealed record ReverseApplicationCommand(
    LegalEntityId LegalEntityId,
    Guid ApplicationId,
    string IdempotencyKey,
    DateOnly EffectiveDate,
    string Reason,
    Guid ActorId,
    Guid CorrelationId);

public sealed record SubledgerApplicationResult(Guid BatchId, bool AlreadyApplied, IReadOnlyList<Guid> ApplicationIds);
public sealed record OpenItemListQuery(LegalEntityId LegalEntityId, Guid PartyId, SubledgerDirectionContract? Direction, bool OpenOnly, int Limit, string? Cursor);
public sealed record OpenItemPage(IReadOnlyList<PartyOpenItemResult> Items, string? NextCursor);
public sealed record PartyOpenItemResult(
    Guid Id,
    Guid LegalEntityId,
    Guid PartyId,
    SubledgerDirectionContract Direction,
    SubledgerPolarityContract Polarity,
    string SourceType,
    string SourceReference,
    string? SourceScheduleReference,
    string? SourceDisplayNumber,
    DateOnly DocumentDate,
    DateOnly PostingDate,
    DateOnly DueDate,
    string TransactionCurrency,
    string OriginalTransactionAmount,
    string SettledTransactionAmount,
    string OpenTransactionAmount,
    string BaseCurrency,
    string OriginalBaseAmount,
    string SettledBaseCarryingAmount,
    string OpenBaseCarryingAmount,
    string ControlAccountRole,
    string ControlAccountReference,
    string Status,
    long RowVersion);

/// <summary>In-process Subledger contracts; source creation is not exposed as arbitrary HTTP CRUD.</summary>
public interface ISubledgerCapabilities
{
    Task<PartyFinancialProfileResult?> GetFinancialProfileAsync(TenantId tenantId, LegalEntityId entityId, Guid partyId, CancellationToken cancellationToken = default);
    Task<PartyFinancialProfileResult> UpdateFinancialProfileAsync(TenantId tenantId, PartyFinancialProfileCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreateReceivableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreatePayableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<Guid> RegisterPaymentCreditAsync(TenantId tenantId, RegisterPaymentCreditCommand command, CancellationToken cancellationToken = default);
    Task<SubledgerApplicationResult> ApplyAsync(TenantId tenantId, ApplyOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<SubledgerApplicationResult> ReverseAsync(TenantId tenantId, ReverseApplicationCommand command, CancellationToken cancellationToken = default);
    Task<OpenItemPage> ListOpenItemsAsync(TenantId tenantId, OpenItemListQuery query, CancellationToken cancellationToken = default);
}
