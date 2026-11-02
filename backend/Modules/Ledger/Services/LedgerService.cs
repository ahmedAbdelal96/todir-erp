using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Repositories;
using Shared.Contracts.Ledger;
using Shared.Contracts.Accounting;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Services;

public sealed class LedgerService(LedgerRepository repository, LedgerOperationsRepository operations, LedgerSetupRepository setup, LedgerMappingConfigurationRepository mappings, LedgerUxRepository ux, LedgerPreviewService preview) : ILedgerPostingEngine, ILedgerControlBalanceRead, ILedgerAccountingWorkspaceCapabilities
{
    public Task<Guid> GetActiveConfigurationVersionIdAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => repository.GetActiveConfigurationVersionIdAsync(tenantId, legalEntityId, cancellationToken);
    public Task<MappingConfigurationDto> CreateMappingConfigurationDraftAsync(TenantId tenant, LegalEntityId entity, CreateMappingConfigurationDraftRequest request, Guid actor, Guid correlationId, CancellationToken ct) => mappings.CreateDraftAsync(tenant, entity, request, actor, correlationId, ct);
    public Task<MappingConfigurationDto> GetMappingConfigurationAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, CancellationToken ct) => mappings.GetConfigurationAsync(tenant, entity, configurationId, ct);
    public Task<MappingConfigurationItemDto> UpdateMappingConfigurationDraftAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, Guid mappingVersionId, UpdateMappingConfigurationDraftRequest request, Guid actor, Guid correlationId, CancellationToken ct) => mappings.UpdateDraftMappingAsync(tenant, entity, configurationId, mappingVersionId, request, actor, correlationId, ct);
    public Task<MappingSimulationResult> SimulateMappingAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, MappingSimulationRequest request, CancellationToken ct) => mappings.SimulateAsync(tenant, entity, configurationId, request, ct);
    public Task<MappingConfigurationValidationDto> ValidateMappingConfigurationAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, Guid actor, Guid correlationId, CancellationToken ct) => mappings.ValidateAsync(tenant, entity, configurationId, actor, correlationId, ct);
    public Task<MappingConfigurationDto> ActivateMappingConfigurationAsync(TenantId tenant, LegalEntityId entity, Guid configurationId, Guid actor, Guid correlationId, CancellationToken ct) => mappings.ActivateAsync(tenant, entity, configurationId, actor, correlationId, ct);
    public Task<LedgerControlBalanceSnapshot> GetControlBalanceAsync(TenantId tenantId, LegalEntityId legalEntityId, string controlRole, Guid configurationVersionId, DateOnly asOfDate, CancellationToken cancellationToken = default) => repository.GetControlBalanceAsync(tenantId, legalEntityId, controlRole, configurationVersionId, asOfDate, cancellationToken);
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
    public Task<bool> SetPeriodStatusAsync(TenantId tenant, LegalEntityId entity, Guid id, PeriodStatus status, Guid actor, Guid correlation, CancellationToken ct) => repository.SetPeriodStatusAsync(tenant, entity, id, status, actor, correlation, ct);
    public Task<FinancialLockDto> CreateLockAsync(TenantId tenant, LegalEntityId entity, CreateFinancialLockRequest request, CancellationToken ct) => repository.CreateLockAsync(tenant, entity, request, ct);
    public Task<IReadOnlyList<FinancialLockDto>> ListLocksAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => repository.ListLocksAsync(tenant, entity, ct);
    public Task<IReadOnlyList<JournalEntryDto>> ListEntriesAsync(TenantId tenant, LegalEntityId entity, int limit, CancellationToken ct) => repository.ListEntriesAsync(tenant, entity, limit, ct);
    public Task<JournalEntryPageDto> ListEntriesPageAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, string? search, string? status, string? cursor, int limit, CancellationToken ct, string? sort = null, int? page = null) => repository.ListEntriesPageAsync(tenant, entity, from, to, search, status, cursor, limit, ct, sort, page);
    public Task<LedgerPostingResult> PostCustomerInvoiceAsync(AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken cancellationToken = default) => repository.PostCustomerInvoiceAsync(fact, cancellationToken);
    public Task<LedgerPostingResult> PostAsync(AccountingFactEnvelope<IAccountingFactPayload> fact, CancellationToken cancellationToken = default) => repository.PostAsync(fact, cancellationToken);
    public Task<LedgerPostingResult> PostUnrealizedFxRevaluationAsync(AccountingFactEnvelope<IAccountingFactPayload> fact, UnrealizedFxRevaluationPayload payload, CancellationToken cancellationToken = default) => repository.PostUnrealizedFxRevaluationAsync(fact, payload, cancellationToken);
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
    public Task<TrialBalancePageDto> GetTrialBalancePageAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, string? search, string? cursor, int limit, CancellationToken ct, string? sort = null, int? page = null) => operations.GetTrialBalancePageAsync(tenant, entity, from, to, search, cursor, limit, ct, sort, page);
    public Task<GeneralLedgerPageDto> GetGeneralLedgerAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, Guid? accountId, string? cursor, int limit, CancellationToken ct, Guid? journalId = null, string? sourceModule = null, string? sourceReference = null, Guid? dimensionDefinitionId = null, Guid? dimensionValueId = null, string? entryNumber = null, int? page = null) => operations.GetGeneralLedgerAsync(tenant, entity, from, to, accountId, cursor, limit, ct, journalId, sourceModule, sourceReference, dimensionDefinitionId, dimensionValueId, entryNumber, page);
    public Task<AccountBalanceDto> GetAccountBalanceAsync(TenantId tenant, LegalEntityId entity, Guid accountId, DateOnly? from, DateOnly? to, CancellationToken ct) => operations.GetAccountBalanceAsync(tenant, entity, accountId, from, to, ct);
    public Task<Guid> CreateDimensionDefinitionAsync(TenantId tenant, LegalEntityId entity, DimensionDefinitionRequest request, Guid actor, CancellationToken ct) => operations.CreateDimensionDefinitionAsync(tenant, entity, request, actor, ct);
    public Task<Guid> CreateDimensionValueAsync(TenantId tenant, LegalEntityId entity, DimensionValueRequest request, Guid actor, CancellationToken ct) => operations.CreateDimensionValueAsync(tenant, entity, request, actor, ct);
    public Task<Guid> CreateDimensionPolicyAsync(TenantId tenant, LegalEntityId entity, DimensionPolicyRequest request, Guid actor, CancellationToken ct) => operations.CreateDimensionPolicyAsync(tenant, entity, request, actor, ct);
    public Task<ProjectionRebuildResult> RebuildBalancesAsync(TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct) => operations.RebuildBalancesAsync(tenant, entity, fiscalYearId, periodId, ct);
    public Task<ProjectionRebuildResult> RebuildBalancesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => operations.RebuildBalancesAsync(tenant, entity, ct);
    public Task<ProjectionReconciliationResultDto> ReconcileBalancesAsync(TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct) => operations.ReconcileBalancesAsync(tenant, entity, fiscalYearId, periodId, ct);
    public Task<EntryExplanationDto> ExplainEntryAsync(TenantId tenant, LegalEntityId entity, Guid entryId, CancellationToken ct) => operations.ExplainEntryAsync(tenant, entity, entryId, ct);
    public Task<ManagedLedgerSetupDto> InitializeManagedLedgerAsync(TenantId tenant, LegalEntityId entity, ManagedLedgerSetupRequest request, Guid actor, Guid correlation, CancellationToken ct) => setup.InitializeAsync(tenant, entity, request, actor, correlation, ct);
    public Task<LedgerReadinessDto> GetReadinessAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => setup.GetReadinessAsync(tenant, entity, ct);
    public Task<LedgerModeDto> GetModeAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => setup.GetModeAsync(tenant, entity, ct);
    public Task<LedgerModeDto> ChangeModeAsync(TenantId tenant, LegalEntityId entity, LedgerMode mode, Guid actor, Guid correlation, CancellationToken ct) => setup.ChangeModeAsync(tenant, entity, mode, actor, correlation, ct);
    public Task<AccountSearchPageDto> SearchAccountsAsync(TenantId tenant, LegalEntityId entity, string? query, string? cursor, int limit, CancellationToken ct) => ux.SearchAccountsAsync(tenant, entity, query, cursor, limit, ct);
    public Task<AccountWorkspaceDto> GetAccountWorkspaceAsync(TenantId tenant, LegalEntityId entity, Guid accountId, CancellationToken ct) => ux.GetAccountWorkspaceAsync(tenant, entity, accountId, ct);
    public Task<PeriodCloseCheckDto> GetPeriodCloseReadinessAsync(TenantId tenant, LegalEntityId entity, Guid periodId, CancellationToken ct) => ux.GetPeriodCloseReadinessAsync(tenant, entity, periodId, ct);
    public Task<PostingPreviewDto> PreviewExplicitAsync(TenantId tenant, LegalEntityId entity, PostingPreviewRequest request, Guid actor, Guid correlation, CancellationToken ct) => preview.PreviewExplicitAsync(tenant, entity, request, actor, correlation, ct);

    async Task<LedgerTrialBalanceSnapshot> ILedgerAccountingWorkspaceCapabilities.GetTrialBalanceAsync(TenantId tenantId, LegalEntityId legalEntityId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var mapped = await operations.GetAccountingTrialBalanceAsync(tenantId, legalEntityId, fromDate, toDate, cancellationToken);
        var currency = mapped.Select(row => row.BaseCurrencyCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.OrdinalIgnoreCase).SingleOrDefault() ?? string.Empty;
        return new LedgerTrialBalanceSnapshot(legalEntityId.Value, fromDate, toDate, currency, mapped, mapped.Sum(row => row.PeriodDebit), mapped.Sum(row => row.PeriodCredit));
    }

    async Task<LedgerGeneralLedgerPage> ILedgerAccountingWorkspaceCapabilities.GetGeneralLedgerAsync(TenantId tenantId, LegalEntityId legalEntityId, DateOnly? fromDate, DateOnly? toDate, Guid? accountId, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        return await operations.GetAccountingGeneralLedgerAsync(tenantId, legalEntityId, fromDate, toDate, accountId, cursor, pageSize, cancellationToken);
    }

    async Task<LedgerReadinessSnapshot> ILedgerAccountingWorkspaceCapabilities.GetReadinessAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken)
    {
        var readiness = await GetReadinessAsync(tenantId, legalEntityId, cancellationToken);
        return new LedgerReadinessSnapshot(readiness.State.ToString(), readiness.Mode.ToString(), readiness.Version,
            readiness.Capabilities.Where(capability => capability.State == LedgerReadinessState.Blocked).SelectMany(capability => capability.Reasons).Select(reason => reason.Code).ToArray());
    }

    public async Task<LedgerPeriodSnapshot?> GetPeriodAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid periodId, CancellationToken cancellationToken = default)
    {
        var period = (await ListPeriodsAsync(tenantId, legalEntityId, cancellationToken)).SingleOrDefault(item => item.Id == periodId);
        return period is null ? null : new LedgerPeriodSnapshot(period.Id, period.StartDate, period.EndDate, period.Status.ToString(), period.Version);
    }

    public async Task<LedgerCloseReadinessSnapshot> GetCloseReadinessAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid periodId, CancellationToken cancellationToken = default)
    {
        var readiness = await GetPeriodCloseReadinessAsync(tenantId, legalEntityId, periodId, cancellationToken);
        return new LedgerCloseReadinessSnapshot(periodId, readiness.Ready, readiness.Blockers.Select(blocker => blocker.Code).ToArray());
    }

    public async Task<IReadOnlySet<Guid>> FindAccountsAsync(TenantId tenantId, LegalEntityId legalEntityId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new HashSet<Guid>();
        var requested = accountIds.ToHashSet();
        var existing = await ListAccountsAsync(tenantId, legalEntityId, cancellationToken);
        return existing.Where(account => requested.Contains(account.Id)).Select(account => account.Id).ToHashSet();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetAccountTypesAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) =>
        (await ListAccountsAsync(tenantId, legalEntityId, cancellationToken)).ToDictionary(account => account.Id, account => account.Type.ToString());

    public Task SetPeriodStatusAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid periodId, string status, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PeriodStatus>(status, true, out var parsed)) throw new LedgerDomainException("ledger.period_status_invalid", "The accounting period status is invalid.");
        return SetPeriodStatusAsync(tenantId, legalEntityId, periodId, parsed, actorId, correlationId, cancellationToken);
    }

    public Task<LedgerOpeningBatchSnapshot?> GetOpeningBatchAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid batchId, CancellationToken cancellationToken = default) => operations.GetOpeningBatchSnapshotAsync(tenantId, legalEntityId, batchId, cancellationToken);
    public Task LockOpeningBatchAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid batchId, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default) => operations.LockOpeningBatchAsync(tenantId, legalEntityId, batchId, actorId, correlationId, cancellationToken);
}
