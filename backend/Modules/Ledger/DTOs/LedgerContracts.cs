using Modules.Ledger.Entities;

namespace Modules.Ledger.DTOs;

public sealed record CreateAccountRequest(string Code, string NameAr, string NameEn, AccountType Type, string Subtype, Guid? ParentAccountId, bool AllowDirectPosting = true);
public sealed record AccountDto(Guid Id, Guid LegalEntityId, string Code, string NameAr, string NameEn, AccountType Type, string Subtype, NormalBalance NormalBalance, Guid? ParentAccountId, bool AllowDirectPosting, AccountStatus Status, long Version);
public sealed record CreateJournalRequest(string Code, string NameAr, string NameEn, JournalType Type);
public sealed record JournalDto(Guid Id, Guid LegalEntityId, string Code, string NameAr, string NameEn, JournalType Type, string Status, long Version);
public sealed record CreateFiscalYearRequest(string Code, DateOnly StartDate, DateOnly EndDate);
public sealed record FiscalYearDto(Guid Id, Guid LegalEntityId, string Code, DateOnly StartDate, DateOnly EndDate, FiscalYearStatus Status, long Version);
public sealed record CreatePeriodRequest(Guid FiscalYearId, string Code, DateOnly StartDate, DateOnly EndDate, int Sequence);
public sealed record PeriodDto(Guid Id, Guid FiscalYearId, string Code, DateOnly StartDate, DateOnly EndDate, int Sequence, PeriodStatus Status, long Version);
public sealed record CreateFinancialLockRequest(FinancialLockType LockType, DateOnly StartDate, DateOnly? EndDate, string Reason);
public sealed record FinancialLockDto(Guid Id, Guid LegalEntityId, FinancialLockType LockType, DateOnly StartDate, DateOnly? EndDate, string Status, string Reason, long Version);
public sealed record EntryLineDto(int Sequence, Guid AccountId, decimal Debit, decimal Credit, string CurrencyCode, decimal Amount, string Role, string? Description);
public sealed record JournalEntryDto(Guid Id, Guid JournalId, Guid? FactId, string EntryNumber, DateOnly PostingDate, DateOnly PeriodDate, string Status, string CurrencyCode, IReadOnlyList<EntryLineDto> Lines);
public sealed record ReverseEntryRequest(DateOnly ReversalDate, string Reason, DateOnly? AutoReversalDate = null);
public sealed record DimensionDefinitionRequest(string Code, string NameAr, string NameEn, string ValueOwnerModule = "ledger");
public sealed record DimensionValueRequest(Guid DefinitionId, string Code, string NameAr, string NameEn, string? ExternalOwnerModule = null, Guid? ExternalReferenceId = null);
public sealed record DimensionPolicyRequest(Guid DefinitionId, string ScopeType, string ScopeReference, string Requirement);
public enum GeneralJournalDraftStatus { Draft, Submitted, Approved, Posted }
public sealed record GeneralJournalLineRequest(Guid AccountId, decimal Debit, decimal Credit, string TransactionCurrencyCode, decimal TransactionDebit, decimal TransactionCredit, decimal BaseDebit, decimal BaseCredit, decimal ExchangeRate = 1m, IReadOnlyList<Guid>? DimensionValueIds = null, string? Description = null);
public sealed record CreateGeneralJournalDraftRequest(Guid JournalId, DateOnly PostingDate, string Description, string BaseCurrencyCode, string IdempotencyKey, IReadOnlyList<GeneralJournalLineRequest> Lines);
public sealed record GeneralJournalDraftDto(Guid Id, Guid LegalEntityId, Guid JournalId, DateOnly PostingDate, string Description, string BaseCurrencyCode, GeneralJournalDraftStatus Status, IReadOnlyList<GeneralJournalLineRequest> Lines);
public sealed record OpeningBalanceLineRequest(Guid AccountId, decimal Debit, decimal Credit, string CurrencyCode, decimal BaseDebit, decimal BaseCredit, decimal ExchangeRate = 1m, string? LegacyAccountCode = null, string? LegacyReference = null);
public sealed record CreateOpeningBalanceBatchRequest(string BatchIdentity, string FileHash, string? LegacySystem, DateOnly GoLiveDate, IReadOnlyList<OpeningBalanceLineRequest> Lines);
public sealed record OpeningBalancePreviewDto(Guid BatchId, bool Balanced, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<string> ValidationErrors, IReadOnlyList<OpeningBalanceLineRequest> Lines);
public sealed record TrialBalanceRowDto(Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, decimal Balance, string CurrencyCode);
public sealed record GeneralLedgerRowDto(Guid EntryId, Guid LineId, string EntryNumber, DateOnly PostingDate, Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, decimal BaseDebit, decimal BaseCredit, string CurrencyCode, string SourceModule, string SourceReference, int Sequence);
public sealed record GeneralLedgerPageDto(IReadOnlyList<GeneralLedgerRowDto> Items, string? NextCursor);
public sealed record AccountBalanceDto(Guid AccountId, decimal Debit, decimal Credit, decimal Balance, string CurrencyCode, DateOnly? From, DateOnly? To);
public sealed record EntryExplanationDimensionDto(string Code, Guid ValueId, string ValueCode);
public sealed record EntryExplanationLineDto(Guid LineId, int Sequence, string AccountRole, Guid AccountId, string AccountCode, string AccountName, Guid? MappingId, long? MappingVersion, Guid? MappingVersionId, decimal Debit, decimal Credit, string CurrencyCode, decimal TransactionDebit, decimal TransactionCredit, decimal BaseDebit, decimal BaseCredit, decimal? ExchangeRate, string WhyThisAccount, IReadOnlyList<EntryExplanationDimensionDto> Dimensions);
public sealed record EntryExplanationDto(Guid EntryId, string EntryNumber, Guid JournalId, Guid? FactId, DateOnly PostingDate, string AccountingPeriod, string FactType, int FactVersion, Guid PostingRuleVersionId, Guid AccountingConfigurationVersionId, string BaseCurrencyCode, string SourceModule, string SourceReference, IReadOnlyList<EntryExplanationLineDto> Lines);
public sealed record ProjectionReconciliationDifferenceDto(Guid AccountId, Guid PeriodId, Guid? FiscalYearId, string CurrencyCode, decimal CanonicalDebit, decimal ProjectionDebit, decimal DebitDifference, decimal CanonicalCredit, decimal ProjectionCredit, decimal CreditDifference, decimal CanonicalBalance, decimal ProjectionBalance, decimal BalanceDifference, string Status);
public sealed record ProjectionReconciliationResultDto(bool Healthy, IReadOnlyList<ProjectionReconciliationDifferenceDto> Differences);
public sealed record ProjectionRebuildResult(int RowsRebuilt, bool Reconciled);

public enum LedgerMode { Managed, Full }
public enum LedgerReadinessState { Ready, Warning, Blocked }
public sealed record LedgerReadinessReason(string Code, string Message, string? Capability = null);
public sealed record LedgerCapabilityReadiness(string Capability, LedgerReadinessState State, IReadOnlyList<LedgerReadinessReason> Reasons);
public sealed record LedgerReadinessDto(Guid LegalEntityId, LedgerMode Mode, LedgerReadinessState State, IReadOnlyList<LedgerCapabilityReadiness> Capabilities, long Version);
public sealed record ManagedLedgerSetupRequest(string TemplateCode = "development-core", LedgerMode Mode = LedgerMode.Managed, DateOnly? FiscalYearStart = null, DateOnly? FiscalYearEnd = null);
public sealed record ManagedLedgerSetupDto(Guid LegalEntityId, LedgerMode Mode, bool Initialized, Guid ConfigurationVersionId, int AccountsCreated, int JournalsCreated, int RolesCreated, int MappingsCreated, int PeriodsCreated, LedgerReadinessDto Readiness);
public sealed record LedgerModeDto(Guid LegalEntityId, LedgerMode Mode, long Version);
public sealed record AccountSearchPageDto(IReadOnlyList<AccountDto> Items, string? NextCursor);
public sealed record AccountWorkspaceDto(AccountDto Account, IReadOnlyList<GeneralLedgerRowDto> RecentActivity, AccountBalanceDto Balance, IReadOnlyList<EntryExplanationDimensionDto> Dimensions, IReadOnlyList<LedgerReadinessReason> Configuration, IReadOnlyList<AuditSummaryDto> Audit);
public sealed record AuditSummaryDto(string Action, Guid EntityId, Guid? ActorId, DateTimeOffset OccurredAt, string? Reason);
public sealed record PeriodCloseCheckDto(Guid PeriodId, bool Ready, IReadOnlyList<LedgerReadinessReason> Checks, IReadOnlyList<LedgerReadinessReason> Warnings, IReadOnlyList<LedgerReadinessReason> Blockers);
public sealed record PostingPreviewRequest(DateOnly PostingDate, Guid JournalId, string Description, string BaseCurrencyCode, IReadOnlyList<GeneralJournalLineRequest> Lines);
public sealed record PostingPreviewLineDto(int Sequence, Guid AccountId, decimal Debit, decimal Credit, decimal BaseDebit, decimal BaseCredit, string CurrencyCode, decimal TransactionDebit, decimal TransactionCredit, decimal? ExchangeRate, string AccountRole, Guid? MappingId, long? MappingVersion, Guid? MappingVersionId, string? Description, IReadOnlyList<EntryExplanationDimensionDto> Dimensions);
public sealed record PostingPreviewDto(Guid JournalId, DateOnly PostingDate, Guid PostingRuleVersionId, Guid AccountingConfigurationVersionId, string BaseCurrencyCode, IReadOnlyList<PostingPreviewLineDto> Lines, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<LedgerReadinessReason> Warnings);
