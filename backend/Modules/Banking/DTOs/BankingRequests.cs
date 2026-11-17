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
public sealed record CreateForecastItemRequest(Guid LegalEntityId, Guid? TreasuryAccountId, DateOnly DueDate, string Direction, decimal Amount, string CurrencyCode, string SourceType, string Description, JsonElement? SourceReference);
public sealed record ReconciliationStatusRequest(string TargetState, long Version);
public sealed record CashCountStatusRequest(string TargetState, long Version);
