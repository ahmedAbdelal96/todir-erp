using System.Text.Json;
using Shared.Contracts.Banking;

namespace Modules.Banking.DTOs;

public sealed record CreateTreasuryAccountRequest(Guid LegalEntityId, Guid? BranchId, string Code, string Name,
    TreasuryAccountType Type, TreasuryCurrencyPolicy CurrencyPolicy, string? CurrencyCode, string Purpose,
    string LiquidityClass, IReadOnlyList<string>? AllowedUses, string? MaskedIdentifier, string? SecretRef);
public sealed record UpdateTreasuryAccountRequest(string Name, string Purpose, string LiquidityClass, long Version);
public sealed record StatusRequest(string? Reason, long? Version, string? TargetState = null);
public sealed record ImportStatementRequest(string StatementNumber, DateOnly? OpeningDate, DateOnly? ClosingDate,
    string CurrencyCode, decimal? OpeningBalance, decimal? ClosingBalance, string SourceType, string? SourceReference,
    IReadOnlyList<ImportStatementLineRequest> Lines);
public sealed record ImportStatementLineRequest(string ExternalLineId, DateOnly BookingDate, DateOnly? ValueDate, string Direction,
    decimal Amount, string CurrencyCode, string? Reference, string? TransactionCode, string? CounterpartyName, string? RawPayload);
public sealed record CreateReconciliationRequest(Guid LegalEntityId, DateOnly AsOfDate, Guid StatementId, decimal BookBalance, string? Notes);
public sealed record CreateMatchRequest(IReadOnlyList<Guid> StatementLineIds, IReadOnlyList<PaymentMatchReference> Payments, decimal ExternalAmount, decimal SystemAmount, decimal Difference, string Source, string? Explanation);
public sealed record PaymentMatchReference(Guid PaymentId, decimal Amount);
public sealed record CreateCashCountRequest(DateOnly CutoffDate, Guid CustodianId, decimal ExpectedAmount, IReadOnlyList<CashDenominationRequest> Denominations);
public sealed record CashDenominationRequest(string Denomination, decimal UnitValue, int Quantity);
public sealed record CreateForecastItemRequest(Guid LegalEntityId, Guid ForecastVersionId, Guid? TreasuryAccountId, DateOnly DueDate, string Direction, decimal Amount, string CurrencyCode, string SourceType, string Description, JsonElement? SourceReference);
public sealed record ReconciliationStatusRequest(string TargetState, long Version);
public sealed record CashCountStatusRequest(string TargetState, long Version);
public sealed record ValidateStatementRequest(bool RequireBalanceContinuity = true);
public sealed record ClassifyStatementLineRequest(string Classification, string? Explanation);
public sealed record CreateForecastVersionRequest(Guid LegalEntityId, DateOnly AsOfDate, int HorizonDays, string? Status);
public sealed record CreateAccountChangeRequest(JsonElement Payload, string Reason, long Version);
public sealed record ChangeRequestDecisionRequest(string Decision, long Version);
public sealed record ResolveCashDifferenceRequest(string ReasonCode, string ResolutionType, decimal Amount, string? EvidenceReference, long Version);
public sealed record TransferCustodyRequest(Guid ToCustodianId, Guid? FromCustodianId, string Reason);
public sealed record CreateConnectionRequest(string ProviderCode, string ConnectionRef);
public sealed record UnmatchRequest(Guid MatchId, long Version);
public sealed record CreatePaymentProposalRequest(JsonElement? Evidence);
public sealed record BankOriginatedDecisionRequest(string Decision, long Version);
public sealed record CreateTransactionCodeMappingRequest(string TransactionCode, string Classification, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record CreateReconciliationRuleSetRequest(string Name, JsonElement Definition);
public sealed record CreateTransferRequest(Guid LegalEntityId, Guid SourceTreasuryAccountId, Guid DestinationTreasuryAccountId, decimal Amount, string CurrencyCode, DateOnly PaymentDate, DateOnly PostingDate, DateOnly? ValueDate, string? Reference);
public sealed record ReconcileTransferRequest(Guid? SourceStatementLineId, Guid? DestinationStatementLineId, long Version, string? Explanation);
public sealed record CreateOpeningReconciliationRequest(Guid LegalEntityId, Guid TreasuryAccountId, Guid LedgerOpeningBatchId,
    DateOnly OpeningAsOfDate, string CurrencyCode, decimal BookOpeningBalance, decimal ExternalOpeningBalance,
    decimal OutstandingInTransitAmount, string SourceSystem, string? LegacyAccountId, string BatchId);
public sealed record FinalizeOpeningReconciliationRequest(long Version);
public sealed record ConfirmPosCashSettlementRequest(Guid TreasuryAccountId, long Version);
public sealed record MatchProviderSettlementRequest(Guid StatementLineId, long Version);
