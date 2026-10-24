using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Services;

public sealed class LedgerService(LedgerRepository repository) : ILedgerPostingEngine
{
    public Task<IReadOnlyList<AccountDto>> ListAccountsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListAccountsAsync(tenant, entity, ct);
    public Task<AccountDto> CreateAccountAsync(TenantId tenant, LegalEntityId entity, CreateAccountRequest request, CancellationToken ct) => repository.CreateAccountAsync(tenant, entity, request, ct);
    public Task<bool> ArchiveAccountAsync(TenantId tenant, LegalEntityId entity, Guid id, CancellationToken ct) => repository.ArchiveAccountAsync(tenant, entity, id, ct);
    public Task<IReadOnlyList<JournalDto>> ListJournalsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListJournalsAsync(tenant, entity, ct);
    public Task<JournalDto> CreateJournalAsync(TenantId tenant, LegalEntityId entity, CreateJournalRequest request, CancellationToken ct) => repository.CreateJournalAsync(tenant, entity, request, ct);
    public Task<IReadOnlyList<FiscalYearDto>> ListFiscalYearsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListFiscalYearsAsync(tenant, entity, ct);
    public Task<FiscalYearDto> CreateFiscalYearAsync(TenantId tenant, LegalEntityId entity, CreateFiscalYearRequest request, CancellationToken ct) => repository.CreateFiscalYearAsync(tenant, entity, request, ct);
    public Task<IReadOnlyList<PeriodDto>> ListPeriodsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListPeriodsAsync(tenant, entity, ct);
    public Task<PeriodDto> CreatePeriodAsync(TenantId tenant, LegalEntityId entity, CreatePeriodRequest request, CancellationToken ct) => repository.CreatePeriodAsync(tenant, entity, request, ct);
    public Task<bool> SetPeriodStatusAsync(TenantId tenant, LegalEntityId entity, Guid id, PeriodStatus status, CancellationToken ct) => repository.SetPeriodStatusAsync(tenant, entity, id, status, ct);
    public Task<FinancialLockDto> CreateLockAsync(TenantId tenant, LegalEntityId entity, CreateFinancialLockRequest request, CancellationToken ct) => repository.CreateLockAsync(tenant, entity, request, ct);
    public Task<IReadOnlyList<FinancialLockDto>> ListLocksAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListLocksAsync(tenant, entity, ct);
    public Task<IReadOnlyList<JournalEntryDto>> ListEntriesAsync(TenantId tenant, LegalEntityId entity, int limit, CancellationToken ct) => repository.ListEntriesAsync(tenant, entity, limit, ct);
    public Task<LedgerPostingResult> PostCustomerInvoiceAsync(AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken cancellationToken = default) => repository.PostCustomerInvoiceAsync(fact, cancellationToken);
    public Task<LedgerPostingResult> PostAsync(AccountingFactEnvelope<IAccountingFactPayload> fact, CancellationToken cancellationToken = default) => repository.PostAsync(fact, cancellationToken);
}
