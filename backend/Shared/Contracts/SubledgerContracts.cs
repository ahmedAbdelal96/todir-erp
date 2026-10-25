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
    long SourceVersion,
    string? PaymentTermSnapshot = null);

public sealed record CreateOpenItemsCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    IReadOnlyList<OpenItemSnapshotContract> Items,
    Guid ActorId,
    Guid CorrelationId);

public sealed record CreateScheduledOpenItemsCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    Guid PaymentTermId,
    OpenItemSnapshotContract Source,
    Guid ActorId,
    Guid CorrelationId);

public sealed record RegisterPaymentCreditCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    SubledgerDirectionContract Direction,
    OpenItemSnapshotContract Credit,
    Guid ActorId,
    Guid CorrelationId);

public sealed record RegisterPartyCreditCommand(
    LegalEntityId LegalEntityId,
    Guid PartyId,
    SubledgerDirectionContract Direction,
    OpenItemSnapshotContract Credit,
    Guid ActorId,
    Guid CorrelationId);

public sealed record ApplicationLineCommand(Guid DebitItemId, Guid CreditItemId, string Amount, string? CreditAmount = null);
public sealed record ResolvedApplicationLineContract(Guid DebitItemId, Guid CreditItemId, string DebitAmount, string CreditAmount, string? DebitFxSnapshot, string? CreditFxSnapshot, string SettlementBaseAmount);

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
public sealed record PartyBalanceQuery(LegalEntityId LegalEntityId, Guid PartyId, SubledgerDirectionContract Direction, DateOnly AsOfDate);
public sealed record PartyCurrencyBalance(string CurrencyCode, string AmountDue, string AvailableCredits, string NetBalance, string OverdueExposure = "0");
public sealed record PartyBalanceResult(Guid PartyId, Guid LegalEntityId, SubledgerDirectionContract Direction, DateOnly AsOfDate, IReadOnlyList<PartyCurrencyBalance> Currencies, string? CreditLimitAmount, string? CreditLimitCurrency, string? AvailableCreditAmount, long ExposureVersion, DateTimeOffset AsOfInstant = default);
public sealed record AgingQuery(LegalEntityId LegalEntityId, Guid PartyId, SubledgerDirectionContract Direction, DateOnly AsOfDate);
public sealed record AgingBucketResult(string CurrencyCode, string Current, string Days1To30, string Days31To60, string Days61To90, string Days91Plus);
public sealed record PartyStatementQuery(LegalEntityId LegalEntityId, Guid PartyId, SubledgerDirectionContract Direction, DateOnly FromDate, DateOnly ToDate);
public sealed record PartyStatementLine(DateOnly EffectiveDate, DateOnly PostingDate, long SourceSequence, Guid SourceId, string SourceType, string SourceReference, string CurrencyCode, string Amount, string RunningBalance);
public sealed record PartyStatementResult(Guid PartyId, LegalEntityId LegalEntityId, SubledgerDirectionContract Direction, DateOnly FromDate, DateOnly ToDate, IReadOnlyList<PartyStatementLine> Lines, IReadOnlyDictionary<string, string> ClosingBalances);
public sealed record WriteOffPolicyCommand(LegalEntityId LegalEntityId, SubledgerDirectionContract Direction, string CurrencyCode, string MaximumAmount, string ApprovalThreshold, long ExpectedVersion);
public sealed record WriteOffPolicyResult(Guid Id, LegalEntityId LegalEntityId, SubledgerDirectionContract Direction, string CurrencyCode, string MaximumAmount, string ApprovalThreshold, bool AutomaticWriteOffEnabled, long Version, DateTimeOffset UpdatedAt);
public sealed record CreateWriteOffCommand(LegalEntityId LegalEntityId, Guid OpenItemId, string Amount, string ReasonCode, string ReasonText, DateOnly EffectiveDate, string IdempotencyKey, Guid ActorId, Guid CorrelationId);
public sealed record ApproveWriteOffCommand(LegalEntityId LegalEntityId, Guid RequestId, Guid ActorId, Guid CorrelationId);
public sealed record WriteOffResult(Guid Id, Guid RequestId, Guid OpenItemId, string Amount, string CurrencyCode, string BaseAmount, string BaseCurrencyCode, DateOnly EffectiveDate, Guid LedgerFactId, Guid ApprovedBy, bool AlreadyCompleted);
public sealed record ReconciliationQuery(LegalEntityId LegalEntityId, DateOnly AsOfDate);
public sealed record ReconciliationSourceContext(SubledgerDirectionContract Direction, string ControlRole, string ControlAccountReference, string ControlConfigurationReference, string BaseCurrencyCode, decimal SubledgerBaseBalance);
public sealed record ReconciliationDetail(Guid Id, SubledgerDirectionContract Direction, string ControlRole, string ControlAccountReference, string ControlConfigurationReference, Guid? LedgerAccountId, string SubledgerBaseBalance, string LedgerBaseBalance, string Variance, bool ContextResolved);
public sealed record ReconciliationResult(Guid Id, LegalEntityId LegalEntityId, DateOnly AsOfDate, string BaseCurrencyCode, string SubledgerBaseBalance, string LedgerBaseBalance, string Variance, bool IsBalanced, DateTimeOffset CreatedAt, IReadOnlyList<ReconciliationDetail> Details);
public sealed record CreateOpeningSubledgerBatchCommand(LegalEntityId LegalEntityId, string BatchIdentity, DateOnly GoLiveDate, string BaseCurrencyCode, string ControlConfigurationReference, string IdempotencyKey, Guid ActorId, Guid CorrelationId);
public sealed record OpeningSubledgerLineCommand(string RowKey, Guid PartyId, SubledgerDirectionContract Direction, SubledgerPolarityContract Polarity, string SourceReference, DateOnly DocumentDate, DateOnly DueDate, string TransactionCurrency, string TransactionAmount, string BaseCurrency, string BaseAmount, string ControlAccountRole, string ControlAccountReference, string ControlConfigurationReference, string? HistoricFxSnapshot = null);
public sealed record OpeningSubledgerBatchResult(Guid Id, string BatchIdentity, DateOnly GoLiveDate, string Status, int LineCount, IReadOnlyList<Guid> OpenItemIds, bool AlreadyCompleted);
public sealed record OpeningSubledgerBatchContext(Guid Id, string BatchIdentity, DateOnly GoLiveDate, string BaseCurrencyCode, string ControlConfigurationReference, string Status);
public sealed record OpeningSubledgerStoredLine(Guid Id, string RowKey, string PayloadHash, Guid? OpenItemId, OpeningSubledgerLineCommand Command);
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
    Task<PartyFinancialProfileResult> UpdateFinancialProfileAsync(TenantId tenantId, Guid membershipId, PartyFinancialProfileCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreateReceivableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreatePayableOpenItemsAsync(TenantId tenantId, CreateOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreateScheduledReceivableOpenItemsAsync(TenantId tenantId, CreateScheduledOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreateScheduledPayableOpenItemsAsync(TenantId tenantId, CreateScheduledOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<Guid> RegisterPaymentCreditAsync(TenantId tenantId, RegisterPaymentCreditCommand command, CancellationToken cancellationToken = default);
    Task<Guid> RegisterAdvanceAsync(TenantId tenantId, Guid membershipId, RegisterPartyCreditCommand command, CancellationToken cancellationToken = default);
    Task<Guid> RegisterCreditNoteAsync(TenantId tenantId, Guid membershipId, RegisterPartyCreditCommand command, CancellationToken cancellationToken = default);
    Task<SubledgerApplicationResult> ApplyAsync(TenantId tenantId, Guid membershipId, ApplyOpenItemsCommand command, CancellationToken cancellationToken = default);
    Task<SubledgerApplicationResult> ReverseAsync(TenantId tenantId, Guid membershipId, ReverseApplicationCommand command, CancellationToken cancellationToken = default);
    Task<OpenItemPage> ListOpenItemsAsync(TenantId tenantId, OpenItemListQuery query, CancellationToken cancellationToken = default);
    Task<PartyBalanceResult> GetPartyBalanceAsync(TenantId tenantId, PartyBalanceQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgingBucketResult>> GetAgingAsync(TenantId tenantId, Guid membershipId, AgingQuery query, CancellationToken cancellationToken = default);
    Task<PartyStatementResult> GetStatementAsync(TenantId tenantId, Guid membershipId, PartyStatementQuery query, CancellationToken cancellationToken = default);
    Task<WriteOffPolicyResult> UpdateWriteOffPolicyAsync(TenantId tenantId, Guid membershipId, WriteOffPolicyCommand command, CancellationToken cancellationToken = default);
    Task<Guid> CreateWriteOffRequestAsync(TenantId tenantId, Guid membershipId, CreateWriteOffCommand command, CancellationToken cancellationToken = default);
    Task<WriteOffResult> ApproveWriteOffAsync(TenantId tenantId, Guid membershipId, ApproveWriteOffCommand command, CancellationToken cancellationToken = default);
    Task<ReconciliationResult> ReconcileControlBalancesAsync(TenantId tenantId, Guid membershipId, ReconciliationQuery query, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default);
    Task<Guid> CreateOpeningSubledgerBatchAsync(TenantId tenantId, Guid membershipId, CreateOpeningSubledgerBatchCommand command, CancellationToken cancellationToken = default);
    Task AddOpeningSubledgerLinesAsync(TenantId tenantId, Guid membershipId, LegalEntityId legalEntityId, Guid batchId, IReadOnlyList<OpeningSubledgerLineCommand> lines, CancellationToken cancellationToken = default);
    Task<OpeningSubledgerBatchResult> FinalizeOpeningSubledgerBatchAsync(TenantId tenantId, Guid membershipId, LegalEntityId legalEntityId, Guid batchId, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default);
}
