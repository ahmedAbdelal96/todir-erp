using System.Text.Json;
using Shared.Contracts.Banking;

namespace Modules.Banking.Entities;

public sealed class BankingException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}

public sealed record TreasuryAccountDto(Guid Id, Guid LegalEntityId, Guid? BranchId, string Code, string Name,
    TreasuryAccountType Type, TreasuryAccountStatus Status, TreasuryCurrencyPolicy CurrencyPolicy, string? CurrencyCode,
    string Purpose, string LiquidityClass, string? MaskedIdentifier, bool HasSensitiveIdentifier, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record TreasuryAccountPage(IReadOnlyList<TreasuryAccountDto> Items, int Page, int PageSize, bool HasMore, int TotalCount);
public sealed record StatementDto(Guid Id, Guid TreasuryAccountId, string StatementNumber, DateOnly? OpeningDate, DateOnly? ClosingDate,
    string CurrencyCode, string ProcessingState, string ReconciliationState, decimal? OpeningBalance, decimal? ClosingBalance, int LineCount, long Version);
public sealed record StatementPage(IReadOnlyList<StatementDto> Items, int Page, int PageSize, bool HasMore, int TotalCount);
public sealed record StatementLineDto(Guid Id, Guid StatementId, string ExternalLineId, DateOnly BookingDate, DateOnly? ValueDate,
    string Direction, decimal Amount, string CurrencyCode, string? Reference, string? TransactionCode, string Classification, decimal ClaimedAmount, long Version);
public sealed record StatementLinePage(IReadOnlyList<StatementLineDto> Items, int Page, int PageSize, bool HasMore, int TotalCount);
public sealed record ReconciliationDto(Guid Id, Guid TreasuryAccountId, DateOnly AsOfDate, string State, decimal ExternalBalance,
    decimal BookBalance, decimal Difference, int MatchCount, long Version);
public sealed record ReconciliationPage(IReadOnlyList<ReconciliationDto> Items, int Page, int PageSize, bool HasMore, int TotalCount);
public sealed record CashCountDto(Guid Id, Guid TreasuryAccountId, DateOnly CutoffDate, string State, decimal ExpectedAmount,
    decimal CountedAmount, decimal Difference, Guid CustodianId, long Version);
public sealed record CashCountPage(IReadOnlyList<CashCountDto> Items, int Page, int PageSize, bool HasMore, int TotalCount);
public sealed record CashPositionDto(Guid LegalEntityId, Guid TreasuryAccountId, string AccountCode, string AccountName,
    TreasuryAccountType AccountType, string CurrencyCode, decimal BookBalance, decimal ExternalBalance, decimal AvailableBalance,
    decimal InTransitAmount, string BalanceBasis, DateTimeOffset AsOf, DateTimeOffset? ExternalAsOf, bool IsStale);
public sealed record ForecastItemDto(Guid Id, DateOnly DueDate, string Direction, decimal Amount, string CurrencyCode,
    string SourceType, string Description, string Confidence, Guid? TreasuryAccountId);
public sealed record ForecastPage(Guid Id, DateOnly AsOfDate, int HorizonDays, string Status,
    IReadOnlyList<ForecastItemDto> Items);
public sealed record AccountChangeRequestDto(Guid Id, Guid TreasuryAccountId, string Status, JsonElement Payload,
    string Reason, Guid RequestedBy, Guid? ApprovedBy, long Version, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);
public sealed record ForecastVersionDto(Guid Id, DateOnly AsOfDate, int HorizonDays, string Status, DateTimeOffset CreatedAt);
public sealed record CashDifferenceResolutionDto(Guid Id, Guid CashCountId, string ReasonCode, string ResolutionType,
    decimal Amount, string? EvidenceReference, Guid? ApprovedBy, DateTimeOffset CreatedAt);
public sealed record CustodyTransferDto(Guid Id, Guid TreasuryAccountId, Guid? FromCustodianId, Guid ToCustodianId,
    DateTimeOffset EffectiveAt, string Reason, Guid CreatedBy);
public sealed record ConnectionDto(Guid Id, Guid TreasuryAccountId, string ProviderCode, string ConnectionRef, string Status,
    DateTimeOffset? LastSuccessAt, DateTimeOffset? LastFailureAt, string? FailureCode, long Version);
public sealed record ReconciliationSuggestionDto(Guid StatementLineId, decimal Amount, string CurrencyCode, string? Reference, string? TransactionCode, string Reason);
public sealed record BankOriginatedCandidateDto(Guid Id, Guid StatementLineId, string EventType, string Status, decimal Amount, string CurrencyCode, JsonElement Evidence, DateTimeOffset ProposedAt, Guid? ReviewedBy, DateTimeOffset? ReviewedAt);
public sealed record TransactionCodeMappingDto(Guid Id, string TransactionCode, string Classification, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int Version);
public sealed record ReconciliationRuleSetDto(Guid Id, string Name, string Status, long Version, int RuleVersion);
public sealed record TransferDto(Guid TransferId, string State, decimal SourceAmount, string SourceCurrencyCode, decimal DestinationAmount, string DestinationCurrencyCode, DateTimeOffset CompletedAt);
