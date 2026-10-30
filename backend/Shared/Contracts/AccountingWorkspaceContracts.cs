using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Accounting;

/// <summary>Read-only Ledger composition contract; Ledger remains the sole owner of GL truth.</summary>
public sealed record LedgerAccountBalance(Guid AccountId, string AccountCode, string AccountName, string AccountType, string AccountSubtype,
    decimal OpeningDebit, decimal OpeningCredit, decimal PeriodDebit, decimal PeriodCredit, decimal ClosingDebit, decimal ClosingCredit,
    decimal SignedBalance, string BaseCurrencyCode);
public sealed record LedgerTrialBalanceSnapshot(Guid LegalEntityId, DateOnly? FromDate, DateOnly? ToDate, string BaseCurrencyCode,
    IReadOnlyList<LedgerAccountBalance> Accounts, decimal TotalDebit, decimal TotalCredit);
public sealed record LedgerGeneralLedgerLine(Guid EntryId, Guid LineId, string EntryNumber, DateOnly PostingDate, Guid AccountId,
    string AccountCode, string AccountName, decimal Debit, decimal Credit, decimal BaseDebit, decimal BaseCredit, string CurrencyCode,
    string SourceModule, string SourceReference, int Sequence, decimal RunningBalance);
public sealed record LedgerGeneralLedgerPage(IReadOnlyList<LedgerGeneralLedgerLine> Items, string? NextCursor);
public sealed record LedgerReadinessSnapshot(string State, string Mode, long Version, IReadOnlyList<string> BlockingReasons);
public sealed record LedgerPeriodSnapshot(Guid Id, DateOnly StartDate, DateOnly EndDate, string Status, long Version);
public sealed record LedgerCloseReadinessSnapshot(Guid PeriodId, bool CanClose, IReadOnlyList<string> Blockers);
public sealed record LedgerOpeningBatchSnapshot(Guid Id, DateOnly GoLiveDate, string Status, Guid? PostedEntryId);

public interface ILedgerAccountingWorkspaceCapabilities
{
    Task<LedgerTrialBalanceSnapshot> GetTrialBalanceAsync(TenantId tenantId, LegalEntityId legalEntityId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);
    Task<LedgerGeneralLedgerPage> GetGeneralLedgerAsync(TenantId tenantId, LegalEntityId legalEntityId, DateOnly? fromDate, DateOnly? toDate,
        Guid? accountId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
    Task<LedgerReadinessSnapshot> GetReadinessAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default);
    Task<LedgerPeriodSnapshot?> GetPeriodAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid periodId, CancellationToken cancellationToken = default);
    Task<LedgerCloseReadinessSnapshot> GetCloseReadinessAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid periodId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> FindAccountsAsync(TenantId tenantId, LegalEntityId legalEntityId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);
    Task SetPeriodStatusAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid periodId, string status, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default);
    Task<LedgerOpeningBatchSnapshot?> GetOpeningBatchAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid batchId, CancellationToken cancellationToken = default);
    Task LockOpeningBatchAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid batchId, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default);
}

public sealed record InventoryAccountingReconciliation(Guid LegalEntityId, DateOnly AsOfDate, decimal InventoryValue, decimal LedgerValue,
    decimal Difference, string CurrencyCode, string Status, IReadOnlyList<string> Diagnostics, bool Available);
public interface IInventoryAccountingRead
{
    Task<InventoryAccountingReconciliation> ReconcileInventoryAsync(TenantId tenantId, LegalEntityId legalEntityId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
