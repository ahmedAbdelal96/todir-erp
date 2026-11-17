using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Modules.Banking.Repositories;
using Shared.Contracts.Banking;
using Shared.Primitives.Identifiers;

namespace Modules.Banking.Services;

public sealed class BankingService(BankingRepository repository) : IBankingTreasuryAccountCapability
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
    public Task<ReconciliationDto> CreateReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRequest request, CancellationToken ct) => repository.CreateReconciliationAsync(tenant, actorId, legalEntityId, request, ct);
    public Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct) => repository.ListReconciliationsAsync(tenant, legalEntityId, accountId, page, pageSize, ct);
    public Task<ReconciliationDto?> GetReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetReconciliationAsync(tenant, legalEntityId, id, ct);
    public Task<ReconciliationDto> AddMatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid sessionId, CreateMatchRequest request, CancellationToken ct) => repository.AddMatchAsync(tenant, actorId, legalEntityId, sessionId, request, ct);
    public Task<ReconciliationDto> ChangeReconciliationStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct) => repository.ChangeReconciliationStateAsync(tenant, actorId, legalEntityId, id, target, version, ct);
    public Task<CashCountDto> CreateCashCountAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateCashCountRequest request, CancellationToken ct) => repository.CreateCashCountAsync(tenant, actorId, legalEntityId, accountId, request, ct);
    public Task<CashCountDto?> GetCashCountAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => repository.GetCashCountAsync(tenant, legalEntityId, id, ct);
    public Task<CashCountDto> ChangeCashCountStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct) => repository.ChangeCashCountStateAsync(tenant, actorId, legalEntityId, id, target, version, ct);
    public Task<IReadOnlyList<CashPositionDto>> GetCashPositionAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) => repository.GetCashPositionAsync(tenant, legalEntityId, ct);

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
