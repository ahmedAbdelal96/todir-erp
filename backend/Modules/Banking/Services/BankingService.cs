using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Modules.Banking.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Accounting;
using Shared.Contracts.Banking;
using Shared.Contracts.Ledger;
using Shared.Contracts.Payments;
using Shared.Primitives.Identifiers;
using Core.Transactions;

namespace Modules.Banking.Services;

public sealed class BankingService(BankingRepository repository, ILedgerControlBalanceRead ledger, ILedgerTreasuryBalanceRead treasuryLedger, ILedgerAccountingWorkspaceCapabilities ledgerWorkspace, ITransactionCoordinator transactions, IServiceProvider services) : IBankingTreasuryAccountCapability, ITransactionalBankingTreasuryAccountCapability
{
    public Task<TreasuryAccountPage> ListAccountsAsync(TenantId tenant, Guid legalEntityId, string? search, TreasuryAccountType? type, TreasuryAccountStatus? status, int page, int pageSize, CancellationToken ct) => repository.ListAccountsAsync(tenant, legalEntityId, search, type, status, page, pageSize, ct);
    public Task<TreasuryAccountDto?> GetAccountAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetAccountAsync(tenant, legalEntityId, id, ct);
    public async Task<TreasuryAccountDto> CreateAccountAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, CreateTreasuryAccountRequest request, CancellationToken ct)
    {
        var create = new TreasuryAccountCreateRequest(request.LegalEntityId, request.BranchId, request.Code, request.Name, request.Type, request.CurrencyPolicy, request.CurrencyCode, request.Purpose, request.LiquidityClass, request.AllowedUses ?? [], request.MaskedIdentifier, request.SecretRef);
        var account = await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var canonical = await repository.CreateCanonicalAccountInTransactionAsync(context, tenant, create, token);
            var projection = services.GetRequiredService<ITransactionalPaymentAccountCompatibilityCapability>();
            await projection.CreateCompatibilityInTransactionAsync(context, tenant, request.LegalEntityId, canonical.Id, request.BranchId, request.Code, request.Name, request.Type switch { TreasuryAccountType.Bank => "Bank", TreasuryAccountType.Cash => "Cashbox", TreasuryAccountType.DigitalWallet => "Wallet", _ => "ProviderClearing" }, request.CurrencyPolicy.ToString(), canonical.CurrencyCode, null, null, request.MaskedIdentifier, request.SecretRef, "Suspended", DateTimeOffset.UtcNow, token);
            return canonical;
        }, ct);
        return (await repository.GetAccountAsync(tenant, account.LegalEntityId, account.Id, ct))!;
    }
    public Task<TreasuryAccountDto> UpdateAccountAsync(TenantId tenant, Guid legalEntityId, Guid id, UpdateTreasuryAccountRequest request, CancellationToken ct) => repository.UpdateAccountAsync(tenant, legalEntityId, id, request, ct);
    public async Task<TreasuryAccountDto> ChangeStatusAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, string target, long? expectedVersion, CancellationToken ct)
    {
        var status = Enum.Parse<TreasuryAccountStatus>(target, true);
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await repository.ChangeStatusInTransactionAsync(context, tenant, legalEntityId, id, status, expectedVersion, token);
            await services.GetRequiredService<ITransactionalPaymentAccountCompatibilityCapability>().ChangeCompatibilityStatusInTransactionAsync(context, tenant, legalEntityId, id, status switch { TreasuryAccountStatus.Closed => "Archived", TreasuryAccountStatus.Suspended => "Suspended", _ => "Active" }, token);
            return true;
        }, ct);
        return (await repository.GetAccountAsync(tenant, legalEntityId, id, ct))!;
    }
    async Task<TreasuryAccountReference> IBankingTreasuryAccountCapability.ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, TreasuryAccountStatus status, long? expectedVersion, CancellationToken cancellationToken)
    {
        var account = await ChangeStatusAsync(tenantId, actorId, membershipId, correlationId, legalEntityId, id, status.ToString(), expectedVersion, cancellationToken);
        return new(account.Id, tenantId, account.LegalEntityId, account.BranchId, account.Code, account.Name, account.Type, account.Status, account.CurrencyPolicy.ToString(), account.CurrencyCode);
    }
    async Task<TreasuryAccountReference> ITransactionalBankingTreasuryAccountCapability.CreateInTransactionAsync(ITransactionContext context, TenantId tenantId, Guid actorId, Guid membershipId, Guid correlationId, TreasuryAccountCreateRequest request, CancellationToken cancellationToken)
    {
        var draft = await repository.CreateCanonicalAccountInTransactionAsync(context, tenantId, request, cancellationToken);
        return await repository.ChangeStatusInTransactionAsync(context, tenantId, request.LegalEntityId, draft.Id, TreasuryAccountStatus.Active, 1, cancellationToken);
    }
    Task<TreasuryAccountReference> ITransactionalBankingTreasuryAccountCapability.ChangeStatusInTransactionAsync(ITransactionContext context, TenantId tenantId, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, TreasuryAccountStatus status, long? expectedVersion, CancellationToken cancellationToken)
        => repository.ChangeStatusInTransactionAsync(context, tenantId, legalEntityId, id, status, expectedVersion, cancellationToken);
    public Task<StatementDto> ImportStatementAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid accountId, string idempotencyKey, ImportStatementRequest request, CancellationToken ct) => repository.ImportStatementAsync(tenant, actorId, membershipId, correlationId, legalEntityId, accountId, idempotencyKey, request, ct);
    public Task<StatementPage> ListStatementsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct) => repository.ListStatementsAsync(tenant, legalEntityId, accountId, page, pageSize, ct);
    public Task<StatementDto?> GetStatementAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetStatementAsync(tenant, legalEntityId, id, ct);
    public Task<StatementLinePage> ListStatementLinesAsync(TenantId tenant, Guid legalEntityId, Guid statementId, int page, int pageSize, CancellationToken ct) => repository.ListStatementLinesAsync(tenant, legalEntityId, statementId, page, pageSize, ct);
    public Task<StatementLineDto?> GetStatementLineAsync(TenantId tenant, Guid legalEntityId, Guid lineId, CancellationToken ct) => repository.GetStatementLineAsync(tenant, legalEntityId, lineId, ct);
    public Task<StatementDto> ValidateStatementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid statementId, ValidateStatementRequest request, CancellationToken ct) => repository.ValidateStatementAsync(tenant, actorId, legalEntityId, statementId, request, ct);
    public Task<StatementLineDto> ClassifyStatementLineAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid lineId, ClassifyStatementLineRequest request, CancellationToken ct) => repository.ClassifyStatementLineAsync(tenant, actorId, legalEntityId, lineId, request, ct);
    public async Task<ReconciliationDto> CreateReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRequest request, CancellationToken ct)
    {
        var statement = await repository.GetStatementAsync(tenant, legalEntityId, request.StatementId, ct) ?? throw new BankingException("banking.statement_not_found", "The statement was not found.");
        var snapshot = await treasuryLedger.GetTreasuryBookBalanceAsync(tenant, new(legalEntityId), statement.TreasuryAccountId, statement.CurrencyCode, request.AsOfDate, ct);
        return await repository.CreateReconciliationFromLedgerAsync(tenant, actorId, legalEntityId, request, snapshot, ct);
    }
    public Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct) => repository.ListReconciliationsAsync(tenant, legalEntityId, accountId, page, pageSize, ct);
    public Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct) => repository.ListReconciliationsAsync(tenant, legalEntityId, page, pageSize, ct);
    public Task<ReconciliationDto?> GetReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetReconciliationAsync(tenant, legalEntityId, id, ct);
    public Task<ReconciliationDto> AddMatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid sessionId, CreateMatchRequest request, CancellationToken ct)
        => repository.AddMatchAsync(tenant, actorId, legalEntityId, sessionId, request, ct, services.GetRequiredService<ITransactionalPaymentReconciliationReadCapability>());
    public async Task<ReconciliationDto> ChangeReconciliationStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct)
    {
        if (target != "Finalized") return await repository.ChangeReconciliationStateAsync(tenant, actorId, legalEntityId, id, target, version, ct);
        var scope = await repository.GetReconciliationScopeAsync(tenant, legalEntityId, id, ct) ?? throw new BankingException("banking.reconciliation_not_found", "The reconciliation was not found.");
        var snapshot = await treasuryLedger.GetTreasuryBookBalanceAsync(tenant, new LegalEntityId(legalEntityId), scope.TreasuryAccountId, scope.CurrencyCode, scope.AsOfDate, ct);
        return await repository.FinalizeReconciliationAsync(tenant, actorId, legalEntityId, id, version, snapshot, ct);
    }
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
    public Task<BankOriginatedCandidateDto> DecideBankOriginatedCandidateAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, Guid id, BankOriginatedDecisionRequest request, CancellationToken ct) => repository.DecideBankOriginatedCandidateAsync(tenant, actorId, membershipId, legalEntityId, id, request, ct);
    public Task<ExternalFeedTransactionDto> RecordExternalFeedTransactionAsync(TenantId tenant, ExternalFeedTransactionReceivedV1 request, CancellationToken ct) => repository.RecordExternalFeedTransactionAsync(tenant, request, ct);
    public Task<TransactionCodeMappingDto> CreateTransactionCodeMappingAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateTransactionCodeMappingRequest request, CancellationToken ct) => repository.CreateTransactionCodeMappingAsync(tenant, actorId, legalEntityId, request, ct);
    public Task<IReadOnlyList<TransactionCodeMappingDto>> ListTransactionCodeMappingsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) => repository.ListTransactionCodeMappingsAsync(tenant, legalEntityId, ct);
    public Task<ReconciliationRuleSetDto> CreateReconciliationRuleSetAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRuleSetRequest request, CancellationToken ct) => repository.CreateReconciliationRuleSetAsync(tenant, actorId, legalEntityId, request, ct);
    public Task<PosCashSettlementPage> ListPosCashSettlementsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, string? status, CancellationToken ct) => repository.ListPosCashSettlementsAsync(tenant, legalEntityId, page, pageSize, status, ct);
    public Task<PosCashSettlementDto> ConfirmPosCashSettlementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, ConfirmPosCashSettlementRequest request, CancellationToken ct) => repository.ConfirmPosCashSettlementAsync(tenant, actorId, legalEntityId, id, request, ct);
    public Task<ProviderSettlementPage> ListProviderSettlementsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, string? status, CancellationToken ct) => repository.ListProviderSettlementsAsync(tenant, legalEntityId, page, pageSize, status, ct);
    public Task<ProviderSettlementDto> MatchProviderSettlementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, MatchProviderSettlementRequest request, CancellationToken ct) => repository.MatchProviderSettlementAsync(tenant, actorId, legalEntityId, id, request, ct);
    public Task<BankActivityPage> ListBankActivityAsync(TenantId tenant, Guid legalEntityId, Guid? treasuryAccountId, string? classification, string? direction, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct) => repository.ListBankActivityAsync(tenant, legalEntityId, treasuryAccountId, classification, direction, fromDate, toDate, page, pageSize, ct);
    public async Task<CashToLedgerReportDto> GetCashToLedgerReportAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct)
    {
        try
        {
            var ledgerBalance = await ledger.GetActiveControlBalanceAsync(tenant, new(legalEntityId), "Cash", asOfDate, ct);
            var banking = await repository.GetCashToLedgerBaseBalanceAsync(tenant, legalEntityId, ledgerBalance.BaseCurrencyCode, ct);
            if (banking.BookBalance is null || banking.AsOf is null)
                return new(legalEntityId, ledgerBalance.BaseCurrencyCode, 0m, ledgerBalance.SignedBaseBalance, -ledgerBalance.SignedBaseBalance,
                    banking.ExternalBalance ?? 0m, banking.InTransitAmount, asOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), "Awaiting banking projection synchronization", false);
            var difference = banking.BookBalance.Value - ledgerBalance.SignedBaseBalance;
            return new(legalEntityId, ledgerBalance.BaseCurrencyCode, banking.BookBalance.Value, ledgerBalance.SignedBaseBalance, difference,
                banking.ExternalBalance ?? 0m, banking.InTransitAmount, banking.AsOf.Value, $"Ledger control role {ledgerBalance.ControlRole}; projection basis {banking.BalanceBasis}", difference == 0m);
        }
        catch (Exception ex) when (ex is not BankingException and not OperationCanceledException)
        {
            var banking = await repository.GetCashToLedgerBaseBalanceAsync(tenant, legalEntityId, "EGP", ct);
            return new(legalEntityId, "EGP", banking.BookBalance ?? 0m, 0m, banking.BookBalance ?? 0m,
                banking.ExternalBalance ?? 0m, banking.InTransitAmount, banking.AsOf ?? asOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), "Ledger control role Cash is not yet configured for this entity", false);
        }
    }
    public async Task<Modules.Banking.Entities.ChequeInTransitPage> ListChequeInTransitAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        var result = await services.GetRequiredService<IPaymentChequeReadCapability>().ListChequeInTransitAsync(tenant, legalEntityId, page, pageSize, ct);
        return new(result.Items.Select(item => new ChequeInTransitDto(item.PaymentId, item.LegalEntityId, item.PaymentNumber, item.Direction, item.Amount, item.CurrencyCode, item.PaymentAccountId, item.PaymentDate, item.State, item.BankReference, item.SourceReference)).ToArray(), result.Page, result.PageSize, result.HasMore, result.TotalCount);
    }
    public async Task<TransferDto> CreateTransferAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, CreateTransferRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (request.SourceTreasuryAccountId == request.DestinationTreasuryAccountId || request.Amount <= 0 || string.IsNullOrWhiteSpace(idempotencyKey)) throw new BankingException("banking.transfer_invalid", "Transfers require two accounts, a positive amount, and an idempotency key.");
        try
        {
            var result = await services.GetRequiredService<IInternalTransferCapability>().CreateAsync(tenant, actorId, membershipId, correlationId, new(request.LegalEntityId, request.SourceTreasuryAccountId, request.DestinationTreasuryAccountId, request.Amount, request.CurrencyCode, request.PaymentDate, request.PostingDate, request.ValueDate, request.Reference, idempotencyKey), ct);
            await repository.RecordTransferProjectionAsync(tenant, request.LegalEntityId, result.TransferId, request.SourceTreasuryAccountId, request.DestinationTreasuryAccountId, result.SourceAmount, result.DestinationAmount, result.SourceCurrencyCode, result.DestinationCurrencyCode, result.State, result.BaseCurrencyCode, result.FxSnapshot, ct);
            return new(result.TransferId, result.State, result.SourceAmount, result.SourceCurrencyCode, result.DestinationAmount, result.DestinationCurrencyCode, result.CompletedAt, result.BaseCurrencyCode, result.FxSnapshot);
        }
        catch (Exception ex) when (ex is not BankingException and not OperationCanceledException) { throw new BankingException("banking.transfer_failed", "The transfer could not be created by Payments.", ex); }
    }
    public Task<TransferReconciliationPage> ListTransferReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid? accountId, string? state, int page, int pageSize, CancellationToken ct) => repository.ListTransferReconciliationsAsync(tenant, legalEntityId, accountId, state, page, pageSize, ct);
    public Task<TransferReconciliationDto> ReconcileTransferAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, ReconcileTransferRequest request, CancellationToken ct) => repository.ReconcileTransferAsync(tenant, actorId, legalEntityId, id, request, ct);
    public async Task<OpeningReconciliationDto> CreateOpeningReconciliationAsync(TenantId tenant, Guid actorId, CreateOpeningReconciliationRequest request, CancellationToken ct)
    {
        var opening = await ledgerWorkspace.GetOpeningBatchAsync(tenant, new(request.LegalEntityId), request.LedgerOpeningBatchId, ct);
        if (opening is null || opening.Status is not ("Posted" or "Locked")) throw new BankingException("banking.opening_ledger_not_ready", "A posted Ledger opening batch is required before opening reconciliation.");
        return await repository.CreateOpeningReconciliationAsync(tenant, actorId, request, ct);
    }
    public async Task<OpeningReconciliationDto> FinalizeOpeningReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, FinalizeOpeningReconciliationRequest request, CancellationToken ct)
    {
        var result = await repository.GetOpeningReconciliationAsync(tenant, legalEntityId, id, ct) ?? throw new BankingException("banking.opening_not_found", "The opening reconciliation was not found.");
        if (result.Version != request.Version) throw new BankingException("banking.opening_concurrency", "The opening reconciliation changed. Refresh and try again.");
        if (result.Difference != 0m) throw new BankingException("banking.opening_unbalanced", "Book, external, and in-transit opening balances must reconcile before finalization.");
        await ledgerWorkspace.LockOpeningBatchAsync(tenant, new(legalEntityId), result.LedgerOpeningBatchId, actorId, Guid.CreateVersion7(), ct);
        return await repository.FinalizeOpeningReconciliationAsync(tenant, actorId, legalEntityId, id, request, ct);
    }
    public Task<OpeningReconciliationPage> ListOpeningReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid? treasuryAccountId, int page, int pageSize, CancellationToken ct) => repository.ListOpeningReconciliationsAsync(tenant, legalEntityId, treasuryAccountId, page, pageSize, ct);
    public Task<OpeningReconciliationDto?> GetOpeningReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetOpeningReconciliationAsync(tenant, legalEntityId, id, ct);

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
