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
