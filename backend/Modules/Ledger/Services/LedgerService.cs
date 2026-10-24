using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Services;

public sealed class LedgerService(LedgerRepository repository, LedgerOperationsRepository operations) : ILedgerPostingEngine
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
    public Task<LedgerPostingResult> ReverseAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid originalEntryId, DateOnly reversalDate, DateOnly? autoReversalDate, string reason, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default) => repository.ReverseAsync(tenantId, legalEntityId, originalEntryId, reversalDate, autoReversalDate, reason, actorId, correlationId, cancellationToken);
    public Task<LedgerPostingResult> ReverseAsync(TenantId tenant, LegalEntityId entity, Guid entryId, ReverseEntryRequest request, Guid actor, Guid correlation, CancellationToken ct) => operations.ReverseAsync(tenant, entity, entryId, request, actor, correlation, ct);
    public Task<GeneralJournalDraftDto> CreateGeneralJournalDraftAsync(TenantId tenant, LegalEntityId entity, CreateGeneralJournalDraftRequest request, Guid actor, CancellationToken ct) => operations.CreateGeneralJournalDraftAsync(tenant, entity, request, actor, ct);
    public Task<GeneralJournalDraftDto> SubmitGeneralJournalAsync(TenantId tenant, LegalEntityId entity, Guid id, Guid actor, Guid correlation, CancellationToken ct) => operations.SubmitGeneralJournalAsync(tenant, entity, id, actor, correlation, ct);
    public Task<GeneralJournalDraftDto> UpdateGeneralJournalDraftAsync(TenantId tenant, LegalEntityId entity, Guid id, CreateGeneralJournalDraftRequest request, Guid actor, CancellationToken ct) => operations.UpdateGeneralJournalDraftAsync(tenant, entity, id, request, actor, ct);
    public Task<GeneralJournalDraftDto> ApproveGeneralJournalAsync(TenantId tenant, LegalEntityId entity, Guid id, Guid actor, Guid correlation, CancellationToken ct) => operations.ApproveGeneralJournalAsync(tenant, entity, id, actor, correlation, ct);
    public Task<LedgerPostingResult> PostGeneralJournalAsync(TenantId tenant, LegalEntityId entity, Guid id, Guid actor, Guid correlation, CancellationToken ct) => operations.PostGeneralJournalAsync(tenant, entity, id, actor, correlation, ct);
    public Task<Guid> CreateOpeningBatchAsync(TenantId tenant, LegalEntityId entity, CreateOpeningBalanceBatchRequest request, Guid actor, CancellationToken ct) => operations.CreateOpeningBatchAsync(tenant, entity, request, actor, ct);
    public Task<OpeningBalancePreviewDto> PreviewOpeningBatchAsync(TenantId tenant, LegalEntityId entity, Guid id, CancellationToken ct) => operations.PreviewOpeningBatchAsync(tenant, entity, id, ct);
    public Task<LedgerPostingResult> PostOpeningBatchAsync(TenantId tenant, LegalEntityId entity, Guid id, Guid actor, Guid correlation, CancellationToken ct) => operations.PostOpeningBatchAsync(tenant, entity, id, actor, correlation, ct);
    public Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, int limit, CancellationToken ct) => operations.GetTrialBalanceAsync(tenant, entity, from, to, limit, ct);
    public Task<GeneralLedgerPageDto> GetGeneralLedgerAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, Guid? accountId, string? cursor, int limit, CancellationToken ct, Guid? journalId = null, string? sourceModule = null, string? sourceReference = null, Guid? dimensionDefinitionId = null, Guid? dimensionValueId = null, string? entryNumber = null) => operations.GetGeneralLedgerAsync(tenant, entity, from, to, accountId, cursor, limit, ct, journalId, sourceModule, sourceReference, dimensionDefinitionId, dimensionValueId, entryNumber);
    public Task<AccountBalanceDto> GetAccountBalanceAsync(TenantId tenant, LegalEntityId entity, Guid accountId, DateOnly? from, DateOnly? to, CancellationToken ct) => operations.GetAccountBalanceAsync(tenant, entity, accountId, from, to, ct);
    public Task<Guid> CreateDimensionDefinitionAsync(TenantId tenant, LegalEntityId entity, DimensionDefinitionRequest request, Guid actor, CancellationToken ct) => operations.CreateDimensionDefinitionAsync(tenant, entity, request, actor, ct);
    public Task<Guid> CreateDimensionValueAsync(TenantId tenant, LegalEntityId entity, DimensionValueRequest request, Guid actor, CancellationToken ct) => operations.CreateDimensionValueAsync(tenant, entity, request, actor, ct);
    public Task<Guid> CreateDimensionPolicyAsync(TenantId tenant, LegalEntityId entity, DimensionPolicyRequest request, Guid actor, CancellationToken ct) => operations.CreateDimensionPolicyAsync(tenant, entity, request, actor, ct);
    public Task<ProjectionRebuildResult> RebuildBalancesAsync(TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct) => operations.RebuildBalancesAsync(tenant, entity, fiscalYearId, periodId, ct);
    public Task<ProjectionRebuildResult> RebuildBalancesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => operations.RebuildBalancesAsync(tenant, entity, ct);
    public Task<ProjectionReconciliationResultDto> ReconcileBalancesAsync(TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct) => operations.ReconcileBalancesAsync(tenant, entity, fiscalYearId, periodId, ct);
    public Task<EntryExplanationDto> ExplainEntryAsync(TenantId tenant, LegalEntityId entity, Guid entryId, CancellationToken ct) => operations.ExplainEntryAsync(tenant, entity, entryId, ct);
}
