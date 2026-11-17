using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Modules.Banking.Repositories;
using Shared.Contracts.Banking;
using Shared.Primitives.Identifiers;

namespace Modules.Banking.Services;

public sealed class BankingService(BankingRepository repository, IInternalTransferCapability transfers) : IBankingTreasuryAccountCapability
{
    public Task<TreasuryAccountPage> ListAccountsAsync(TenantId tenant, Guid legalEntityId, string? search, TreasuryAccountType? type, TreasuryAccountStatus? status, int page, int pageSize, CancellationToken ct) => repository.ListAccountsAsync(tenant, legalEntityId, search, type, status, page, pageSize, ct);
    public Task<TreasuryAccountDto?> GetAccountAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetAccountAsync(tenant, legalEntityId, id, ct);
    public Task<TreasuryAccountDto> CreateAccountAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, CreateTreasuryAccountRequest request, CancellationToken ct) => repository.CreateAccountAsync(tenant, actorId, membershipId, correlationId, new(request.LegalEntityId, request.BranchId, request.Code, request.Name, request.Type, request.CurrencyPolicy, request.CurrencyCode, request.Purpose, request.LiquidityClass, request.AllowedUses ?? [], request.MaskedIdentifier, request.SecretRef), ct);
    public Task<TreasuryAccountDto> UpdateAccountAsync(TenantId tenant, Guid legalEntityId, Guid id, UpdateTreasuryAccountRequest request, CancellationToken ct) => repository.UpdateAccountAsync(tenant, legalEntityId, id, request, ct);
    public Task<TreasuryAccountDto> ChangeStatusAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, string target, long? expectedVersion, CancellationToken ct) => repository.ChangeStatusAsync(tenant, actorId, membershipId, correlationId, legalEntityId, id, target, expectedVersion, ct);
    async Task<TreasuryAccountReference> IBankingTreasuryAccountCapability.ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, TreasuryAccountStatus status, long? expectedVersion, CancellationToken cancellationToken)
    {
        var account = await repository.ChangeStatusAsync(tenantId, actorId, membershipId, correlationId, legalEntityId, id, status.ToString(), expectedVersion, cancellationToken);
        return new(account.Id, tenantId, account.LegalEntityId, account.BranchId, account.Code, account.Name, account.Type, account.Status, account.CurrencyPolicy.ToString(), account.CurrencyCode);
    }
    public Task<StatementDto> ImportStatementAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid accountId, string idempotencyKey, ImportStatementRequest request, CancellationToken ct) => repository.ImportStatementAsync(tenant, actorId, membershipId, correlationId, legalEntityId, accountId, idempotencyKey, request, ct);
    public Task<StatementPage> ListStatementsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct) => repository.ListStatementsAsync(tenant, legalEntityId, accountId, page, pageSize, ct);
    public Task<StatementDto?> GetStatementAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetStatementAsync(tenant, legalEntityId, id, ct);
    public Task<StatementLinePage> ListStatementLinesAsync(TenantId tenant, Guid legalEntityId, Guid statementId, int page, int pageSize, CancellationToken ct) => repository.ListStatementLinesAsync(tenant, legalEntityId, statementId, page, pageSize, ct);
    public Task<StatementLineDto?> GetStatementLineAsync(TenantId tenant, Guid legalEntityId, Guid lineId, CancellationToken ct) => repository.GetStatementLineAsync(tenant, legalEntityId, lineId, ct);
    public Task<StatementDto> ValidateStatementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid statementId, ValidateStatementRequest request, CancellationToken ct) => repository.ValidateStatementAsync(tenant, actorId, legalEntityId, statementId, request, ct);
    public Task<StatementLineDto> ClassifyStatementLineAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid lineId, ClassifyStatementLineRequest request, CancellationToken ct) => repository.ClassifyStatementLineAsync(tenant, actorId, legalEntityId, lineId, request, ct);
    public Task<ReconciliationDto> CreateReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRequest request, CancellationToken ct) => repository.CreateReconciliationAsync(tenant, actorId, legalEntityId, request, ct);
    public Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct) => repository.ListReconciliationsAsync(tenant, legalEntityId, accountId, page, pageSize, ct);
    public Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct) => repository.ListReconciliationsAsync(tenant, legalEntityId, page, pageSize, ct);
    public Task<ReconciliationDto?> GetReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetReconciliationAsync(tenant, legalEntityId, id, ct);
    public Task<ReconciliationDto> AddMatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid sessionId, CreateMatchRequest request, CancellationToken ct) => repository.AddMatchAsync(tenant, actorId, legalEntityId, sessionId, request, ct);
    public Task<ReconciliationDto> ChangeReconciliationStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct) => repository.ChangeReconciliationStateAsync(tenant, actorId, legalEntityId, id, target, version, ct);
    public Task<IReadOnlyList<ReconciliationSuggestionDto>> GetReconciliationSuggestionsAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetReconciliationSuggestionsAsync(tenant, legalEntityId, id, ct);
    public Task<ReconciliationDto> UnmatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, UnmatchRequest request, CancellationToken ct) => repository.UnmatchAsync(tenant, actorId, legalEntityId, id, request, ct);
    public Task<CashCountDto> CreateCashCountAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateCashCountRequest request, CancellationToken ct) => repository.CreateCashCountAsync(tenant, actorId, legalEntityId, accountId, request, ct);
    public Task<CashCountDto?> GetCashCountAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetCashCountAsync(tenant, legalEntityId, id, ct);
    public Task<CashCountPage> ListCashCountsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct) => repository.ListCashCountsAsync(tenant, legalEntityId, accountId, page, pageSize, ct);
    public Task<CashCountDto> ChangeCashCountStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct) => repository.ChangeCashCountStateAsync(tenant, actorId, legalEntityId, id, target, version, ct);
    public Task<IReadOnlyList<CashPositionDto>> GetCashPositionAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) => repository.GetCashPositionAsync(tenant, legalEntityId, ct);
    public Task<ForecastVersionDto> CreateForecastVersionAsync(TenantId tenant, Guid actorId, CreateForecastVersionRequest request, CancellationToken ct) => repository.CreateForecastVersionAsync(tenant, actorId, request, ct);
    public Task<ForecastItemDto> CreateForecastItemAsync(TenantId tenant, Guid actorId, CreateForecastItemRequest request, CancellationToken ct) => repository.CreateForecastItemAsync(tenant, actorId, request, ct);
    public Task<ForecastPage?> GetForecastAsync(TenantId tenant, Guid legalEntityId, DateOnly? asOfDate, int? horizonDays, CancellationToken ct) => repository.GetForecastAsync(tenant, legalEntityId, asOfDate, horizonDays, ct);
    public Task<ForecastPage?> GetForecastVersionAsync(TenantId tenant, Guid legalEntityId, Guid versionId, CancellationToken ct) => repository.GetForecastVersionAsync(tenant, legalEntityId, versionId, ct);
    public Task<AccountChangeRequestDto> CreateAccountChangeRequestAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateAccountChangeRequest request, CancellationToken ct) => repository.CreateAccountChangeRequestAsync(tenant, actorId, legalEntityId, accountId, request, ct);
    public Task<AccountChangeRequestDto> DecideAccountChangeRequestAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, ChangeRequestDecisionRequest request, CancellationToken ct) => repository.DecideAccountChangeRequestAsync(tenant, actorId, legalEntityId, id, request, ct);
    public Task<AccountChangeRequestDto?> GetAccountChangeRequestAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetAccountChangeRequestAsync(tenant, legalEntityId, id, ct);
    public Task<CashDifferenceResolutionDto> ResolveCashDifferenceAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid cashCountId, ResolveCashDifferenceRequest request, CancellationToken ct) => repository.ResolveCashDifferenceAsync(tenant, actorId, legalEntityId, cashCountId, request, ct);
    public Task<CustodyTransferDto> TransferCashCustodyAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, TransferCustodyRequest request, CancellationToken ct) => repository.TransferCashCustodyAsync(tenant, actorId, legalEntityId, accountId, request, ct);
    public Task<IReadOnlyList<ConnectionDto>> ListConnectionsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, CancellationToken ct) => repository.ListConnectionsAsync(tenant, legalEntityId, accountId, ct);
    public Task<ConnectionDto> CreateConnectionAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateConnectionRequest request, CancellationToken ct) => repository.CreateConnectionAsync(tenant, actorId, legalEntityId, accountId, request, ct);
    public Task<ConnectionDto> ChangeConnectionStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct) => repository.ChangeConnectionStateAsync(tenant, actorId, legalEntityId, id, target, version, ct);
    public Task<BankOriginatedCandidateDto> CreatePaymentProposalAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid lineId, CreatePaymentProposalRequest request, CancellationToken ct) => repository.CreatePaymentProposalAsync(tenant, actorId, legalEntityId, lineId, request, ct);
    public Task<BankOriginatedCandidateDto> DecideBankOriginatedCandidateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, BankOriginatedDecisionRequest request, CancellationToken ct) => repository.DecideBankOriginatedCandidateAsync(tenant, actorId, legalEntityId, id, request, ct);
    public Task<TransactionCodeMappingDto> CreateTransactionCodeMappingAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateTransactionCodeMappingRequest request, CancellationToken ct) => repository.CreateTransactionCodeMappingAsync(tenant, actorId, legalEntityId, request, ct);
    public Task<IReadOnlyList<TransactionCodeMappingDto>> ListTransactionCodeMappingsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) => repository.ListTransactionCodeMappingsAsync(tenant, legalEntityId, ct);
    public Task<ReconciliationRuleSetDto> CreateReconciliationRuleSetAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRuleSetRequest request, CancellationToken ct) => repository.CreateReconciliationRuleSetAsync(tenant, actorId, legalEntityId, request, ct);
    public async Task<TransferDto> CreateTransferAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, CreateTransferRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (request.SourceTreasuryAccountId == request.DestinationTreasuryAccountId || request.Amount <= 0 || string.IsNullOrWhiteSpace(idempotencyKey)) throw new BankingException("banking.transfer_invalid", "Transfers require two accounts, a positive amount, and an idempotency key.");
        try
        {
            var result = await transfers.CreateAsync(tenant, actorId, membershipId, correlationId, new(request.LegalEntityId, request.SourceTreasuryAccountId, request.DestinationTreasuryAccountId, request.Amount, request.CurrencyCode, request.PaymentDate, request.PostingDate, request.ValueDate, request.Reference, idempotencyKey), ct);
            await repository.RecordTransferProjectionAsync(tenant, request.LegalEntityId, result.TransferId, request.SourceTreasuryAccountId, request.DestinationTreasuryAccountId, result.SourceAmount, result.DestinationAmount, result.SourceCurrencyCode, result.DestinationCurrencyCode, result.State, ct);
            return new(result.TransferId, result.State, result.SourceAmount, result.SourceCurrencyCode, result.DestinationAmount, result.DestinationCurrencyCode, result.CompletedAt);
        }
        catch (Exception ex) when (ex is not BankingException) { throw new BankingException("banking.transfer_failed", "The transfer could not be created by Payments.", ex); }
    }

    async Task<TreasuryAccountReference> IBankingTreasuryAccountCapability.CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, TreasuryAccountCreateRequest request, Guid correlationId, CancellationToken cancellationToken)
    {
        var account = await repository.CreateAccountAsync(tenantId, actorId, membershipId, correlationId, request, cancellationToken);
        return new(account.Id, tenantId, account.LegalEntityId, account.BranchId, account.Code, account.Name, account.Type, account.Status, account.CurrencyPolicy.ToString(), account.CurrencyCode);
    }

    public async Task<TreasuryAccountReference?> GetAsync(TenantId tenantId, Guid legalEntityId, Guid id, CancellationToken cancellationToken)
    {
        var account = await repository.GetAccountAsync(tenantId, legalEntityId, id, cancellationToken);
        return account is null ? null : new(account.Id, tenantId, account.LegalEntityId, account.BranchId, account.Code, account.Name, account.Type, account.Status, account.CurrencyPolicy.ToString(), account.CurrencyCode);
    }

    public async Task<bool> IsEligibleForPaymentAsync(TenantId tenantId, Guid legalEntityId, Guid id, string currencyCode, CancellationToken cancellationToken)
    {
        var account = await repository.GetAccountAsync(tenantId, legalEntityId, id, cancellationToken);
        return account is { Status: TreasuryAccountStatus.Active } && (account.CurrencyPolicy == TreasuryCurrencyPolicy.MultiCurrency || string.Equals(account.CurrencyCode, currencyCode.Trim().ToUpperInvariant(), StringComparison.Ordinal));
    }
}
