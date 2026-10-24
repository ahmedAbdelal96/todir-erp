using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Npgsql;
using NpgsqlTypes;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Repositories;

public sealed class LedgerRepository(
    NpgsqlDataSource dataSource,
    ITransactionCoordinator transactions,
    ILegalEntityFinancialReadStore financialRead,
    TimeProvider clock) : ILedgerPostingEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AccountDto>> ListAccountsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,status,version from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 order by code", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<AccountDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), entity.Value, reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<AccountType>(reader.GetString(4)), reader.GetString(5), Enum.Parse<NormalBalance>(reader.GetString(6)), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetBoolean(8), Enum.Parse<AccountStatus>(reader.GetString(9)), reader.GetInt64(10)));
        return result;
    }

    public Task<AccountDto> CreateAccountAsync(TenantId tenant, LegalEntityId entity, CreateAccountRequest request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var normal = LedgerDomainRules.NormalBalanceFor(request.Type);
            await ExecuteAsync(context, "insert into finance_ledger.account (tenant_id,legal_entity_id,id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Active',1,$12,$12)", token, tenant.Value, entity.Value, id, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.Type.ToString(), request.Subtype.Trim(), normal.ToString(), (object?)request.ParentAccountId ?? DBNull.Value, request.AllowDirectPosting, now);
            return new AccountDto(id, entity.Value, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.Type, request.Subtype.Trim(), normal, request.ParentAccountId, request.AllowDirectPosting, AccountStatus.Active, 1);
        }, ct);

    public Task<bool> ArchiveAccountAsync(TenantId tenant, LegalEntityId entity, Guid accountId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await ExecuteAsync(context, "update finance_ledger.account set status='Archived',version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", token, tenant.Value, entity.Value, accountId);
            return true;
        }, ct);

    public async Task<IReadOnlyList<JournalDto>> ListJournalsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,code,name_ar,name_en,journal_type,status,version from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 order by code", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<JournalDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), entity.Value, reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<JournalType>(reader.GetString(4)), reader.GetString(5), reader.GetInt64(6)));
        return result;
    }

    public Task<JournalDto> CreateJournalAsync(TenantId tenant, LegalEntityId entity, CreateJournalRequest request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.journal (tenant_id,legal_entity_id,id,code,name_ar,name_en,journal_type,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Active',1,$8,$8)", token, tenant.Value, entity.Value, id, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.Type.ToString(), now);
            return new JournalDto(id, entity.Value, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.Type, "Active", 1);
        }, ct);

    public async Task<IReadOnlyList<FiscalYearDto>> ListFiscalYearsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,code,start_date,end_date,status,version from finance_ledger.fiscal_year where tenant_id=$1 and legal_entity_id=$2 order by start_date", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<FiscalYearDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), entity.Value, reader.GetString(1), DateOnly.FromDateTime(reader.GetDateTime(2)), DateOnly.FromDateTime(reader.GetDateTime(3)), Enum.Parse<FiscalYearStatus>(reader.GetString(4)), reader.GetInt64(5)));
        return result;
    }

    public Task<FiscalYearDto> CreateFiscalYearAsync(TenantId tenant, LegalEntityId entity, CreateFiscalYearRequest request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.StartDate > request.EndDate) throw new LedgerDomainException("ledger.fiscal_year_invalid", "Fiscal year start must be before its end.");
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.fiscal_year (tenant_id,legal_entity_id,id,code,start_date,end_date,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,'Open',1,$7,$7)", token, tenant.Value, entity.Value, id, request.Code.Trim(), request.StartDate, request.EndDate, now);
            return new FiscalYearDto(id, entity.Value, request.Code.Trim(), request.StartDate, request.EndDate, FiscalYearStatus.Open, 1);
        }, ct);

    public async Task<IReadOnlyList<PeriodDto>> ListPeriodsAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,fiscal_year_id,code,start_date,end_date,sequence_no,status,version from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 order by start_date", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<PeriodDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), DateOnly.FromDateTime(reader.GetDateTime(3)), DateOnly.FromDateTime(reader.GetDateTime(4)), reader.GetInt32(5), Enum.Parse<PeriodStatus>(reader.GetString(6)), reader.GetInt64(7)));
        return result;
    }

    public Task<PeriodDto> CreatePeriodAsync(TenantId tenant, LegalEntityId entity, CreatePeriodRequest request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.StartDate > request.EndDate) throw new LedgerDomainException("ledger.period_invalid", "Accounting period start must be before its end.");
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.accounting_period (tenant_id,legal_entity_id,id,fiscal_year_id,code,start_date,end_date,sequence_no,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Open',1,$9,$9)", token, tenant.Value, entity.Value, id, request.FiscalYearId, request.Code.Trim(), request.StartDate, request.EndDate, request.Sequence, now);
            return new PeriodDto(id, request.FiscalYearId, request.Code.Trim(), request.StartDate, request.EndDate, request.Sequence, PeriodStatus.Open, 1);
        }, ct);

    public Task<bool> SetPeriodStatusAsync(TenantId tenant, LegalEntityId entity, Guid periodId, PeriodStatus status, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await ExecuteAsync(context, "update finance_ledger.accounting_period set status=$4,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity.Value, periodId, status.ToString());
            return true;
        }, ct);

    public Task<FinancialLockDto> CreateLockAsync(TenantId tenant, LegalEntityId entity, CreateFinancialLockRequest request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.financial_lock (tenant_id,legal_entity_id,id,lock_type,start_date,end_date,status,reason,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,'Active',$7,1,$8,$8)", token, tenant.Value, entity.Value, id, request.LockType.ToString(), request.StartDate, (object?)request.EndDate ?? DBNull.Value, request.Reason.Trim(), now);
            return new FinancialLockDto(id, entity.Value, request.LockType, request.StartDate, request.EndDate, "Active", request.Reason.Trim(), 1);
        }, ct);

    public async Task<IReadOnlyList<FinancialLockDto>> ListLocksAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,lock_type,start_date,end_date,status,reason,version from finance_ledger.financial_lock where tenant_id=$1 and legal_entity_id=$2 order by start_date", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<FinancialLockDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), entity.Value, Enum.Parse<FinancialLockType>(reader.GetString(1)), DateOnly.FromDateTime(reader.GetDateTime(2)), reader.IsDBNull(3) ? null : DateOnly.FromDateTime(reader.GetDateTime(3)), reader.GetString(4), reader.GetString(5), reader.GetInt64(6)));
        return result;
    }

    public async Task<IReadOnlyList<JournalEntryDto>> ListEntriesAsync(TenantId tenant, LegalEntityId entity, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200); await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select e.id,e.journal_id,e.fact_id,e.entry_number,e.posting_date,p.start_date,e.status,e.currency_code,l.sequence_no,l.account_id,l.debit,l.credit,l.currency_code,l.amount,l.semantic_role,l.description from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 order by e.posting_date desc,e.entry_number desc,l.sequence_no limit $3", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(limit); await using var reader = await command.ExecuteReaderAsync(ct); var entries = new Dictionary<Guid, (Guid Journal, Guid Fact, string Number, DateOnly Posting, DateOnly Period, string Status, string Currency, List<EntryLineDto> Lines)>();
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0); if (!entries.TryGetValue(id, out var entry)) entry = (reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), DateOnly.FromDateTime(reader.GetDateTime(4)), DateOnly.FromDateTime(reader.GetDateTime(5)), reader.GetString(6), reader.GetString(7), []);
            entry.Lines.Add(new(reader.GetInt32(8), reader.GetGuid(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetString(12), reader.GetDecimal(13), reader.GetString(14), reader.IsDBNull(15) ? null : reader.GetString(15))); entries[id] = entry;
        }
        return entries.Select(item => new JournalEntryDto(item.Key, item.Value.Journal, item.Value.Fact, item.Value.Number, item.Value.Posting, item.Value.Period, item.Value.Status, item.Value.Currency, item.Value.Lines)).ToArray();
    }

    public Task<LedgerPostingResult> PostCustomerInvoiceAsync(AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken cancellationToken = default) =>
        PostAsync(new AccountingFactEnvelope<IAccountingFactPayload>(fact.FactId, fact.TenantId, fact.LegalEntityId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, fact.Payload, fact.CorrelationId, fact.ActorId), cancellationToken);

    public async Task<LedgerPostingResult> PostAsync(AccountingFactEnvelope<IAccountingFactPayload> fact, CancellationToken cancellationToken = default)
    {
        if (!AccountingFactTypes.Known.Contains(fact.FactType)) throw new LedgerDomainException("ledger.fact_unsupported", "This accounting fact type is not in the canonical catalog.");
        if (fact.FactVersion != 1) throw new LedgerDomainException("ledger.fact_unsupported", "This fact version is not enabled by the Pass 1 posting engine.");
        if (!string.Equals(fact.FactType, AccountingFactTypes.CustomerInvoicePosted, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.posting_rule_missing", "The fact is catalogued but no Pass 1 posting rule is enabled for it.");
        if (fact.Payload is not CustomerInvoicePostedPayload invoice) throw new LedgerDomainException("ledger.fact_payload_invalid", "The fact payload does not match its typed contract.");
        if (invoice.NetAmount <= 0m || invoice.TaxAmount < 0m || invoice.GrossAmount != invoice.NetAmount + invoice.TaxAmount) throw new LedgerDomainException("ledger.fact_invalid", "Invoice amounts are inconsistent.");
        if (!string.Equals(invoice.CurrencyCode, invoice.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.fact_invalid", "Currency code is invalid.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, cancellationToken) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var payloadJson = JsonSerializer.Serialize(invoice, JsonOptions); var payloadHash = AccountingFactFingerprint.Compute(invoice);
        var invoiceFact = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(fact.FactId, fact.TenantId, fact.LegalEntityId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, invoice, fact.CorrelationId, fact.ActorId);
        return await transactions.ExecuteAsync(fact.TenantId, async (context, ct) =>
        {
            var existing = await FindFactAsync(context, invoiceFact, ct);
            if (existing is not null)
            {
                if (!string.Equals(existing.Value.Hash, payloadHash, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.fact_duplicate_conflict", "The source fact identity was already used with a different payload.");
                if (existing.Value.Entry is { } entry) return new LedgerPostingResult(fact.FactId, entry, existing.Value.EntryNumber!, "Posted", true);
            }
            else
            {
                await ExecuteAsync(context, "insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Received',$13,$14,$15) on conflict do nothing", ct, fact.TenantId.Value, fact.LegalEntityId.Value, fact.FactId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, new NpgsqlParameter { Value = payloadJson, NpgsqlDbType = NpgsqlDbType.Jsonb }, payloadHash, fact.CorrelationId, fact.ActorId, clock.GetUtcNow());
                existing = await FindFactAsync(context, invoiceFact, ct);
                if (existing is null) throw new LedgerDomainException("ledger.fact_not_persisted", "Accounting fact could not be persisted.");
                if (!string.Equals(existing.Value.Hash, payloadHash, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.fact_duplicate_conflict", "The source fact identity was already used with a different payload.");
                if (existing.Value.Entry is { } entry) return new LedgerPostingResult(fact.FactId, entry, existing.Value.EntryNumber!, "Posted", true);
            }

            var period = await ResolvePeriodAsync(context, invoiceFact, ct);
            var rule = await ResolveRuleAsync(context, invoiceFact, ct);
            var configuration = await ResolveConfigurationAsync(context, invoiceFact, ct);
            var salesJournal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='SALES' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "Sales journal is not configured.");
            var determinations = new List<AccountDeterminationExplanation>();
            var ar = await ResolveAccountAsync(context, invoiceFact, "Receivable", null, invoice.ReceivablePartyGroup, null, ct); determinations.Add(ar.Explanation);
            var revenue = await ResolveAccountAsync(context, invoiceFact, "SalesRevenue", invoice.RevenueCategory, null, null, ct); determinations.Add(revenue.Explanation);
            var lines = new List<(Guid Account, decimal Debit, decimal Credit, string Role, decimal Amount)> { (ar.Id, invoice.GrossAmount, 0m, "Receivable", invoice.GrossAmount), (revenue.Id, 0m, invoice.NetAmount, "SalesRevenue", invoice.NetAmount) };
            foreach (var tax in invoice.Taxes) { var determination = await ResolveAccountAsync(context, invoiceFact, tax.AccountRole, null, null, tax.Code, ct); determinations.Add(determination.Explanation); lines.Add((determination.Id, 0m, tax.Amount, tax.AccountRole, tax.Amount)); }
            LedgerDomainRules.EnsureBalanced(lines.Sum(x => x.Debit), lines.Sum(x => x.Credit));
            var entryId = Guid.CreateVersion7(); var entryNumber = $"JE-{fact.FactId:N}"; var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, salesJournal, fact.FactId, fact.SourceModule, fact.SourceReference, entryNumber, fact.PostingDate, period, fact.FactType, fact.FactVersion, invoice.CurrencyCode, baseCurrency, rule, configuration, "Customer invoice posted", fact.CorrelationId, fact.ActorId, now);
            var sequence = 1; foreach (var line in lines) { await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, Guid.CreateVersion7(), entryId, sequence++, line.Account, line.Debit, line.Credit, invoice.CurrencyCode, line.Amount, line.Role); }
            await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", ct, fact.TenantId.Value, fact.FactId, entryId, now);
            var outbox = JsonSerializer.Serialize(new { fact.FactId, entryId, entryNumber, fact.FactType }, JsonOptions);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "JournalEntryPostedV1", outbox, fact.TenantId, now, fact.CorrelationId, fact.FactId, "ledger"));
            return new LedgerPostingResult(fact.FactId, entryId, entryNumber, "Posted", false, determinations);
        }, cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection;
    }

    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
        foreach (var value in values) { if (value is NpgsqlParameter parameter) command.Parameters.Add(parameter); else command.Parameters.AddWithValue(value ?? DBNull.Value); }
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid?> ScalarGuidAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); var result = await command.ExecuteScalarAsync(ct); return result is null or DBNull ? null : (Guid)result;
    }

    private static async Task<(string Hash, Guid? Entry, string? EntryNumber)?> FindFactAsync(ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select payload_hash,posted_entry_id,(select entry_number from finance_ledger.journal_entry e where e.tenant_id=f.tenant_id and e.id=f.posted_entry_id) from finance_ledger.accounting_fact f where tenant_id=$1 and legal_entity_id=$2 and (idempotency_key=$3 or (source_module=$4 and source_reference=$5 and fact_type=$6 and fact_version=$7)) for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(fact.IdempotencyKey); command.Parameters.AddWithValue(fact.SourceModule); command.Parameters.AddWithValue(fact.SourceReference); command.Parameters.AddWithValue(fact.FactType); command.Parameters.AddWithValue(fact.FactVersion); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2)) : null;
    }

    private static async Task<Guid> ResolvePeriodAsync(ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select p.id from finance_ledger.accounting_period p where p.tenant_id=$1 and p.legal_entity_id=$2 and p.start_date <= $3 and p.end_date >= $3 and p.status='Open' and not exists (select 1 from finance_ledger.financial_lock l where l.tenant_id=p.tenant_id and l.legal_entity_id=p.legal_entity_id and l.status='Active' and l.start_date <= $3 and (l.end_date is null or l.end_date >= $3))", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(fact.PostingDate); var periods = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) periods.Add(reader.GetGuid(0));
        if (periods.Count == 0) throw new LedgerDomainException("ledger.period_locked", "Posting date has no open, unlocked accounting period."); if (periods.Count > 1) throw new LedgerDomainException("ledger.period_ambiguous", "Posting date resolves to more than one accounting period."); return periods[0];
    }

    private static async Task<Guid> ResolveRuleAsync(ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select v.id from finance_ledger.posting_rule r join finance_ledger.posting_rule_version v on v.tenant_id=r.tenant_id and v.legal_entity_id=r.legal_entity_id and v.posting_rule_id=r.id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.fact_type=$3 and r.fact_version=$4 and r.status='Active' and v.status='Active' and v.effective_from <= $5 and (v.effective_to is null or v.effective_to >= $5) order by v.version_number desc limit 1", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(fact.FactType); command.Parameters.AddWithValue(fact.FactVersion); command.Parameters.AddWithValue(fact.PostingDate); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.posting_rule_missing", "No active posting rule matches this accounting fact.");
    }

    private static async Task<Guid> ResolveConfigurationAsync(ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2 and status='Active' and effective_from <= $3 and (effective_to is null or effective_to >= $3) order by version_number desc limit 1", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(fact.PostingDate); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.configuration_missing", "No active accounting configuration matches the posting date.");
    }

    private static async Task<(Guid Id, AccountDeterminationExplanation Explanation)> ResolveAccountAsync(ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, string role, string? category, string? partyGroup, string? taxCode, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("with candidates as (select v.target_account_id,v.specificity_rank,count(*) over (partition by v.specificity_rank) as rank_count,max(v.specificity_rank) over () as max_rank,m.code,v.version_number,a.code as account_code from finance_ledger.account_mapping m join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id where m.tenant_id=$1 and m.legal_entity_id=$2 and r.code=$3 and m.status='Active' and v.status='Active' and a.status='Active' and a.allow_direct_posting=true and v.effective_from <= $4 and (v.effective_to is null or v.effective_to >= $4) and (v.product_category_code is null or v.product_category_code=$5) and (v.party_group_code is null or v.party_group_code=$6) and (v.tax_code is null or v.tax_code=$7)) select target_account_id,rank_count,code,version_number,account_code,specificity_rank from candidates where specificity_rank=max_rank", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(role); command.Parameters.AddWithValue(fact.PostingDate); command.Parameters.AddWithValue((object?)category ?? DBNull.Value); command.Parameters.AddWithValue((object?)partyGroup ?? DBNull.Value); command.Parameters.AddWithValue((object?)taxCode ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct); var accounts = new List<(Guid Id, int Count, string Mapping, long Version, string AccountCode, int Rank)>(); while (await reader.ReadAsync(ct)) accounts.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt64(3), reader.GetString(4), reader.GetInt32(5))); if (accounts.Count == 0) throw new LedgerDomainException("ledger.account_mapping_missing", $"No active account mapping exists for role {role}."); if (accounts.Any(x => x.Count > 1) || accounts.Select(x => x.Id).Distinct().Count() != 1) throw new LedgerDomainException("ledger.account_mapping_conflict", $"More than one account mapping matches role {role}."); var selected = accounts[0]; return (selected.Id, new AccountDeterminationExplanation(role, selected.Mapping, selected.Version, selected.AccountCode, selected.Id, fact.LegalEntityId.Value, selected.Rank, $"highest matching specificity rank {selected.Rank}; category={category ?? "*"}; partyGroup={partyGroup ?? "*"}; taxCode={taxCode ?? "*"}"));
    }
}
