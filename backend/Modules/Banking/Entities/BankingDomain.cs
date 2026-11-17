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
public sealed record CashPositionDto(Guid LegalEntityId, Guid TreasuryAccountId, string AccountCode, string AccountName,
    TreasuryAccountType AccountType, string CurrencyCode, decimal BookBalance, decimal ExternalBalance, decimal AvailableBalance,
    decimal InTransitAmount, string BalanceBasis, DateTimeOffset AsOf, DateTimeOffset? ExternalAsOf, bool IsStale);
public sealed record ForecastItemDto(Guid Id, DateOnly DueDate, string Direction, decimal Amount, string CurrencyCode,
    string SourceType, string Description, string Confidence, Guid? TreasuryAccountId);
public sealed record ForecastPage(Guid Id, DateOnly AsOfDate, int HorizonDays, string Status,
    IReadOnlyList<ForecastItemDto> Items);
