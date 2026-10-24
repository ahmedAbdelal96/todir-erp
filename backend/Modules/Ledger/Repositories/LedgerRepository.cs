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

    public Task<bool> SetPeriodStatusAsync(TenantId tenant, LegalEntityId entity, Guid periodId, PeriodStatus status, CancellationToken ct) => SetPeriodStatusAsync(tenant, entity, periodId, status, Guid.Empty, Guid.CreateVersion7(), ct);

    public Task<bool> SetPeriodStatusAsync(TenantId tenant, LegalEntityId entity, Guid periodId, PeriodStatus status, Guid actor, Guid correlation, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var previous = await ScalarStringAsync(context, "select status from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", token, tenant.Value, entity.Value, periodId) ?? throw new LedgerDomainException("ledger.period_not_found", "The accounting period was not found.");
            if (string.Equals(previous, status.ToString(), StringComparison.Ordinal)) return false;
            var yearId = await ScalarGuidAsync(context, "select fiscal_year_id from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity.Value, periodId) ?? throw new LedgerDomainException("ledger.period_not_found", "The accounting period was not found.");
            await ExecuteAsync(context, "update finance_ledger.accounting_period set status=$4,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity.Value, periodId, status.ToString());
            var eventType = status == PeriodStatus.Closed ? LedgerEventTypes.PeriodClosedV1 : status == PeriodStatus.Open ? LedgerEventTypes.PeriodReopenedV1 : LedgerEventTypes.LedgerConfigurationChanged;
            var payload = JsonSerializer.Serialize(new PeriodStatusChangedEvent(periodId, yearId, entity.Value, previous, status.ToString()), JsonOptions);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), eventType, payload, tenant, clock.GetUtcNow(), correlation, periodId, "ledger"));
            if (actor != Guid.Empty) await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, entity.Value, null, "User", actor, null, "ledger", status == PeriodStatus.Open ? "period.reopened" : "period.status_changed", "accounting_period", periodId, JsonSerializer.Serialize(new { status = previous }), JsonSerializer.Serialize(new { status = status.ToString() }), status == PeriodStatus.Open ? "period reopen" : null, "api", correlation, null, clock.GetUtcNow(), null), token);
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
        if (fact.Payload is ExplicitLedgerPostingPayload explicitPosting && (fact.FactType is AccountingFactTypes.ManualGeneralJournalPosted or AccountingFactTypes.OpeningBalancePosted))
            return await PostExplicitLinesAsync(fact, explicitPosting, cancellationToken);
        if (!string.Equals(fact.FactType, AccountingFactTypes.CustomerInvoicePosted, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.posting_rule_missing", "The fact is catalogued but no Pass 1 posting rule is enabled for it.");
        if (fact.Payload is not CustomerInvoicePostedPayload invoice) throw new LedgerDomainException("ledger.fact_payload_invalid", "The fact payload does not match its typed contract.");
        if (invoice.NetAmount <= 0m || invoice.TaxAmount < 0m || invoice.GrossAmount != invoice.NetAmount + invoice.TaxAmount) throw new LedgerDomainException("ledger.fact_invalid", "Invoice amounts are inconsistent.");
        if (!string.Equals(invoice.CurrencyCode, invoice.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.fact_invalid", "Currency code is invalid.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, cancellationToken) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var invoiceRate = string.Equals(invoice.CurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : invoice.ExchangeRate ?? throw new LedgerDomainException("ledger.currency_mismatch", "A foreign-currency posting requires an immutable exchange-rate snapshot.");
        if (invoiceRate <= 0m) throw new LedgerDomainException("ledger.currency_mismatch", "Exchange rate must be positive.");
        var payloadJson = JsonSerializer.Serialize(invoice, JsonOptions); var payloadHash = AccountingFactFingerprint.Compute(invoice);
        var invoiceFact = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(fact.FactId, fact.TenantId, fact.LegalEntityId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, invoice, fact.CorrelationId, fact.ActorId);
        return await transactions.ExecuteAsync(fact.TenantId, async (context, ct) =>
        {
            if (RequiresRegisteredProducer(fact.FactType) && !await IsRegisteredProducerAsync(context, fact, ct))
                throw new LedgerDomainException("ledger.fact_producer_unregistered", "The producer is not registered for this accounting fact type and version.");
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
            var lines = new List<(Guid Account, decimal Debit, decimal Credit, string Role, decimal Amount, AccountDeterminationExplanation Evidence)> { (ar.Id, invoice.GrossAmount, 0m, "Receivable", invoice.GrossAmount, ar.Explanation), (revenue.Id, 0m, invoice.NetAmount, "SalesRevenue", invoice.NetAmount, revenue.Explanation) };
            foreach (var tax in invoice.Taxes) { var determination = await ResolveAccountAsync(context, invoiceFact, tax.AccountRole, null, null, tax.Code, ct); determinations.Add(determination.Explanation); lines.Add((determination.Id, 0m, tax.Amount, tax.AccountRole, tax.Amount, determination.Explanation)); }
            LedgerDomainRules.EnsureBalanced(lines.Sum(x => x.Debit), lines.Sum(x => x.Credit));
            var entryId = Guid.CreateVersion7(); var entryNumber = $"JE-{fact.FactId:N}"; var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, salesJournal, fact.FactId, fact.SourceModule, fact.SourceReference, entryNumber, fact.PostingDate, period, fact.FactType, fact.FactVersion, invoice.CurrencyCode, baseCurrency, rule, configuration, "Customer invoice posted", fact.CorrelationId, fact.ActorId, now);
            var sequence = 1; foreach (var line in lines) { var snapshot = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { rate = invoiceRate, reference = invoice.ExchangeRateReference, from = invoice.CurrencyCode, to = baseCurrency }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb }; var contextSnapshot = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { category = invoice.RevenueCategory, partyGroup = invoice.ReceivablePartyGroup, taxCode = line.Role == "SalesRevenue" ? null : invoice.Taxes.FirstOrDefault(t => t.AccountRole == line.Role)?.Code, specificity = line.Evidence.SpecificityRank }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb }; await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,account_role,account_mapping_id,account_mapping_version_id,determination_context,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate,exchange_rate_snapshot) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, Guid.CreateVersion7(), entryId, sequence++, line.Account, line.Debit, line.Credit, invoice.CurrencyCode, line.Amount, line.Role, line.Evidence.AccountRole, line.Evidence.MappingId, line.Evidence.MappingVersionId, contextSnapshot, line.Debit, line.Credit, line.Debit * invoiceRate, line.Credit * invoiceRate, invoiceRate, snapshot); }
            await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", ct, fact.TenantId.Value, fact.FactId, entryId, now);
            await RefreshAccountPeriodBalancesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, ct);
            var outbox = JsonSerializer.Serialize(new { fact.FactId, entryId, entryNumber, fact.FactType }, JsonOptions);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryPostedV1, outbox, fact.TenantId, now, fact.CorrelationId, fact.FactId, "ledger"));
            return new LedgerPostingResult(fact.FactId, entryId, entryNumber, "Posted", false, determinations);
        }, cancellationToken);
    }

    private static bool RequiresRegisteredProducer(string factType) =>
        factType is not AccountingFactTypes.ManualGeneralJournalPosted and not AccountingFactTypes.OpeningBalancePosted;

    private static async Task<bool> IsRegisteredProducerAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists (select 1 from finance_ledger.fact_producer_registration where tenant_id=$1 and legal_entity_id=$2 and producer_module=$3 and fact_type=$4 and fact_version=$5 and status='Active')", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value);
        command.Parameters.AddWithValue(fact.LegalEntityId.Value);
        command.Parameters.AddWithValue(fact.SourceModule);
        command.Parameters.AddWithValue(fact.FactType);
        command.Parameters.AddWithValue(fact.FactVersion);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<LedgerPostingResult> ReverseAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid originalEntryId, DateOnly reversalDate, DateOnly? autoReversalDate, string reason, Guid actorId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new LedgerDomainException("ledger.reversal_not_allowed", "A reversal reason is required.");
        return await transactions.ExecuteAsync(tenantId, async (context, ct) =>
        {
            await using var original = new NpgsqlCommand("select journal_id,fact_id,source_module,source_reference,entry_number,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
            original.Parameters.AddWithValue(tenantId.Value); original.Parameters.AddWithValue(legalEntityId.Value); original.Parameters.AddWithValue(originalEntryId);
            await using var reader = await original.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new LedgerDomainException("ledger.reversal_not_allowed", "The journal entry was not found.");
            var journalId = reader.GetGuid(0); var factId = reader.GetGuid(1); var sourceModule = reader.GetString(2); var sourceReference = reader.GetString(3); var originalNumber = reader.GetString(4); var factType = reader.GetString(5); var factVersion = reader.GetInt32(6); var currency = reader.GetString(7); var baseCurrency = reader.GetString(8); var ruleId = reader.GetGuid(9); var configId = reader.IsDBNull(10) ? (Guid?)null : reader.GetGuid(10); await reader.CloseAsync();
            await using var duplicate = new NpgsqlCommand("select reversal_entry_id from finance_ledger.journal_entry_reversal where tenant_id=$1 and legal_entity_id=$2 and original_entry_id=$3", context.Connection, context.Transaction); duplicate.Parameters.AddWithValue(tenantId.Value); duplicate.Parameters.AddWithValue(legalEntityId.Value); duplicate.Parameters.AddWithValue(originalEntryId); var existing = await duplicate.ExecuteScalarAsync(ct); if (existing is Guid existingId) throw new LedgerDomainException("ledger.already_reversed", $"The entry has already been reversed by {existingId}.");
            var period = await ResolvePeriodAsync(context, tenantId.Value, legalEntityId.Value, reversalDate, ct);
            var reversalId = Guid.CreateVersion7(); var reversalFactId = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var number = $"REV-{originalEntryId:N}"; var reversalSource = $"reversal:{sourceModule}:{sourceReference}"; var reversalPayload = JsonSerializer.Serialize(new { originalEntryId, reason }, JsonOptions); await ExecuteAsync(context, "insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,'JournalEntryReversed',1,'ledger',$4,$5,$6,$6,$7,$8,'Received',$9,$10,$11)", ct, tenantId.Value, legalEntityId.Value, reversalFactId, reversalSource, reversalSource, reversalDate, new NpgsqlParameter { Value = reversalPayload, NpgsqlDbType = NpgsqlDbType.Jsonb }, AccountingFactFingerprint.Compute(new ReversalFingerprint(originalEntryId, reason)), correlationId, actorId, now);
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", ct, tenantId.Value, legalEntityId.Value, reversalId, journalId, reversalFactId, "ledger", reversalSource, number, reversalDate, period, "Reversal", factVersion, currency, baseCurrency, ruleId, configId, $"Reversal of {originalNumber}: {reason}", correlationId, actorId, now);
            await using var lines = new NpgsqlCommand("select sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2 and journal_entry_id=$3 order by sequence_no", context.Connection, context.Transaction); lines.Parameters.AddWithValue(tenantId.Value); lines.Parameters.AddWithValue(legalEntityId.Value); lines.Parameters.AddWithValue(originalEntryId); await using var lineReader = await lines.ExecuteReaderAsync(ct); var reversalLines = new List<ReversalLine>(); while (await lineReader.ReadAsync(ct))
            {
                var txDebit = lineReader.IsDBNull(9) ? lineReader.GetDecimal(2) : lineReader.GetDecimal(9); var txCredit = lineReader.IsDBNull(10) ? lineReader.GetDecimal(3) : lineReader.GetDecimal(10); var baseDebit = lineReader.IsDBNull(11) ? lineReader.GetDecimal(2) : lineReader.GetDecimal(11); var baseCredit = lineReader.IsDBNull(12) ? lineReader.GetDecimal(3) : lineReader.GetDecimal(12); reversalLines.Add(new(lineReader.GetGuid(1), lineReader.GetString(4), lineReader.GetDecimal(5), lineReader.IsDBNull(6) ? null : lineReader.GetDecimal(6), lineReader.GetString(7), lineReader.IsDBNull(8) ? null : lineReader.GetString(8), txDebit, txCredit, baseDebit, baseCredit, lineReader.IsDBNull(13) ? null : lineReader.GetString(13), lineReader.IsDBNull(14) ? null : lineReader.GetString(14)));
            }
            await lineReader.CloseAsync(); var sequence = 1; foreach (var line in reversalLines)
            {
                await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)", ct, tenantId.Value, legalEntityId.Value, Guid.CreateVersion7(), reversalId, sequence++, line.AccountId, line.BaseCredit, line.BaseDebit, line.Currency, line.Amount, (object?)line.ExchangeRate ?? DBNull.Value, line.Role, (object?)line.Description ?? DBNull.Value, line.TransactionCredit, line.TransactionDebit, line.BaseCredit, line.BaseDebit, new NpgsqlParameter { Value = (object?)line.Snapshot ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Jsonb }, (object?)line.Reference ?? DBNull.Value);
            }
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry_reversal (tenant_id,legal_entity_id,id,original_entry_id,reversal_entry_id,reason,actor_id,reversal_date,auto_reversal_date,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", ct, tenantId.Value, legalEntityId.Value, Guid.CreateVersion7(), originalEntryId, reversalId, reason.Trim(), actorId, reversalDate, (object?)autoReversalDate ?? DBNull.Value, now);
            await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", ct, tenantId.Value, reversalFactId, reversalId, now); await RefreshAccountPeriodBalancesAsync(context, tenantId.Value, legalEntityId.Value, reversalId, ct);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryReversedV1, JsonSerializer.Serialize(new JournalEntryReversedEvent(originalEntryId, reversalId, legalEntityId.Value, reason)), tenantId, now, correlationId, originalEntryId, "ledger"));
            return new LedgerPostingResult(reversalFactId, reversalId, number, "Posted", false);
        }, cancellationToken);
    }

    private sealed record ReversalFingerprint(Guid OriginalEntryId, string Reason) : IAccountingFactPayload;
    private sealed record ReversalLine(Guid AccountId, string Currency, decimal Amount, decimal? ExchangeRate, string Role, string? Description, decimal TransactionDebit, decimal TransactionCredit, decimal BaseDebit, decimal BaseCredit, string? Snapshot, string? Reference);

    /// <summary>Enlists Ledger posting in a caller-owned PostgreSQL transaction.</summary>
    public async Task<LedgerPostingResult> PostExplicitInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload.Lines.Count == 0) throw new LedgerDomainException("ledger.entry_unbalanced", "At least one journal line is required."); LedgerDomainRules.EnsureBalanced(payload.Lines.Sum(line => line.BaseDebit), payload.Lines.Sum(line => line.BaseCredit));
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, cancellationToken) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable."); if (!string.Equals(baseCurrency, payload.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.currency_mismatch", "The posting base currency does not match the legal entity.");
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions); var payloadHash = AccountingFactFingerprint.Compute(payload); var existing = await FindFactAsync(context, fact, cancellationToken); if (existing is not null) { if (!string.Equals(existing.Value.Hash, payloadHash, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.fact_duplicate_conflict", "The source fact identity was already used with a different payload."); if (existing.Value.Entry is { } already) return new LedgerPostingResult(fact.FactId, already, existing.Value.EntryNumber!, "Posted", true); } else await ExecuteAsync(context, "insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Received',$13,$14,$15)", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, fact.FactId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, new NpgsqlParameter { Value = payloadJson, NpgsqlDbType = NpgsqlDbType.Jsonb }, payloadHash, fact.CorrelationId, fact.ActorId, clock.GetUtcNow());
        var period = await ResolvePeriodAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, fact.PostingDate, cancellationToken); var rule = await ResolveRuleAsync(context, fact.FactType, fact.FactVersion, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, cancellationToken); var config = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, cancellationToken); var entryId = Guid.CreateVersion7(); var entryNumber = $"JE-{fact.FactId:N}"; var now = clock.GetUtcNow(); await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, payload.JournalId, fact.FactId, fact.SourceModule, fact.SourceReference, entryNumber, fact.PostingDate, period, fact.FactType, fact.FactVersion, payload.Lines[0].TransactionCurrencyCode, payload.BaseCurrencyCode, rule, config, payload.Description, fact.CorrelationId, fact.ActorId, now);
        await EnsureExplicitAccountsAsync(context, fact, payload, cancellationToken);
        var dimensionRules = await LoadDimensionRulesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, payload.Lines.Select(line => line.AccountId).Distinct().ToArray(), payload.JournalId, fact.FactType, cancellationToken);
        var sequence = 1; foreach (var line in payload.Lines) { if (line.Debit < 0 || line.Credit < 0 || line.Debit > 0 && line.Credit > 0 || line.BaseDebit < 0 || line.BaseCredit < 0 || line.BaseDebit > 0 && line.BaseCredit > 0) throw new LedgerDomainException("ledger.fact_invalid", "Journal lines contain invalid amounts."); var lineId = Guid.CreateVersion7(); var amount = line.TransactionDebit > 0 ? line.TransactionDebit : line.TransactionCredit; var snapshot = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { rate = line.ExchangeRate, from = line.TransactionCurrencyCode, to = payload.BaseCurrencyCode }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb }; await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, lineId, entryId, sequence++, line.AccountId, line.BaseDebit, line.BaseCredit, line.TransactionCurrencyCode, amount, line.ExchangeRate, fact.FactType, line.Description ?? payload.Description, line.Description ?? fact.FactType, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, snapshot, line.LegacyReference); await ValidateAndPersistDimensionsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, lineId, line.Dimensions, dimensionRules.GetValueOrDefault(line.AccountId, []), cancellationToken); }
        await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", cancellationToken, fact.TenantId.Value, fact.FactId, entryId, now); await RefreshAccountPeriodBalancesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, cancellationToken); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryPostedV1, JsonSerializer.Serialize(new { fact.FactId, entryId, entryNumber, fact.FactType }, JsonOptions), fact.TenantId, now, fact.CorrelationId, fact.FactId, "ledger")); return new LedgerPostingResult(fact.FactId, entryId, entryNumber, "Posted", false);
    }

    private async Task<LedgerPostingResult> PostExplicitLinesAsync(AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload payload, CancellationToken cancellationToken)
    {
        if (payload.Lines.Count == 0) throw new LedgerDomainException("ledger.entry_unbalanced", "At least one journal line is required.");
        if (payload.Lines.Any(line => line.Debit < 0 || line.Credit < 0 || line.Debit > 0 && line.Credit > 0 || line.BaseDebit < 0 || line.BaseCredit < 0 || line.BaseDebit > 0 && line.BaseCredit > 0)) throw new LedgerDomainException("ledger.fact_invalid", "Journal lines contain invalid amounts.");
        LedgerDomainRules.EnsureBalanced(payload.Lines.Sum(line => line.BaseDebit), payload.Lines.Sum(line => line.BaseCredit));
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, cancellationToken) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        if (!string.Equals(baseCurrency, payload.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.currency_mismatch", "The posting base currency does not match the legal entity.");
        return await transactions.ExecuteAsync(fact.TenantId, async (context, ct) =>
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions); var payloadHash = AccountingFactFingerprint.Compute(payload); var existing = await FindFactAsync(context, fact, ct);
            if (existing is not null) { if (!string.Equals(existing.Value.Hash, payloadHash, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.fact_duplicate_conflict", "The source fact identity was already used with a different payload."); if (existing.Value.Entry is { } already) return new LedgerPostingResult(fact.FactId, already, existing.Value.EntryNumber!, "Posted", true); }
            else await ExecuteAsync(context, "insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Received',$13,$14,$15)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, fact.FactId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, new NpgsqlParameter { Value = payloadJson, NpgsqlDbType = NpgsqlDbType.Jsonb }, payloadHash, fact.CorrelationId, fact.ActorId, clock.GetUtcNow());
            var period = await ResolvePeriodAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, fact.PostingDate, ct); var rule = await ResolveRuleAsync(context, fact.FactType, fact.FactVersion, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct); var config = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct); var entryId = Guid.CreateVersion7(); var entryNumber = $"JE-{fact.FactId:N}"; var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, payload.JournalId, fact.FactId, fact.SourceModule, fact.SourceReference, entryNumber, fact.PostingDate, period, fact.FactType, fact.FactVersion, payload.Lines[0].TransactionCurrencyCode, payload.BaseCurrencyCode, rule, config, payload.Description, fact.CorrelationId, fact.ActorId, now);
            await EnsureExplicitAccountsAsync(context, fact, payload, ct);
            var dimensionRules = await LoadDimensionRulesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, payload.Lines.Select(line => line.AccountId).Distinct().ToArray(), payload.JournalId, fact.FactType, ct);
            var seq = 1; foreach (var line in payload.Lines)
            {
                var lineId = Guid.CreateVersion7(); var amount = line.TransactionDebit > 0 ? line.TransactionDebit : line.TransactionCredit; var rate = line.ExchangeRate <= 0 ? throw new LedgerDomainException("ledger.currency_mismatch", "Exchange rate must be positive.") : line.ExchangeRate;
                var snapshot = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { rate, from = line.TransactionCurrencyCode, to = payload.BaseCurrencyCode }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb };
                await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", ct, fact.TenantId.Value, fact.LegalEntityId.Value, lineId, entryId, seq++, line.AccountId, line.BaseDebit, line.BaseCredit, line.TransactionCurrencyCode, amount, rate, fact.FactType, line.Description ?? payload.Description, line.Description ?? fact.FactType, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, snapshot, line.LegacyReference);
                await ValidateAndPersistDimensionsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, lineId, line.Dimensions, dimensionRules.GetValueOrDefault(line.AccountId, []), ct);
            }
            await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", ct, fact.TenantId.Value, fact.FactId, entryId, now); await RefreshAccountPeriodBalancesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, ct); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryPostedV1, JsonSerializer.Serialize(new { fact.FactId, entryId, entryNumber, fact.FactType }, JsonOptions), fact.TenantId, now, fact.CorrelationId, fact.FactId, "ledger")); return new LedgerPostingResult(fact.FactId, entryId, entryNumber, "Posted", false);
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

    private static async Task<string?> ScalarStringAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); var result = await command.ExecuteScalarAsync(ct); return result is null or DBNull ? null : (string)result;
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
        await using var command = new NpgsqlCommand("with candidates as (select m.id as mapping_id,v.id as mapping_version_id,v.target_account_id,v.specificity_rank,count(*) over (partition by v.specificity_rank) as rank_count,max(v.specificity_rank) over () as max_rank,m.code,v.version_number,a.code as account_code from finance_ledger.account_mapping m join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id where m.tenant_id=$1 and m.legal_entity_id=$2 and r.code=$3 and m.status='Active' and v.status='Active' and a.status='Active' and a.allow_direct_posting=true and v.effective_from <= $4 and (v.effective_to is null or v.effective_to >= $4) and (v.product_category_code is null or v.product_category_code=$5) and (v.party_group_code is null or v.party_group_code=$6) and (v.tax_code is null or v.tax_code=$7)) select mapping_id,mapping_version_id,target_account_id,rank_count,code,version_number,account_code,specificity_rank from candidates where specificity_rank=max_rank", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(role); command.Parameters.AddWithValue(fact.PostingDate); command.Parameters.AddWithValue((object?)category ?? DBNull.Value); command.Parameters.AddWithValue((object?)partyGroup ?? DBNull.Value); command.Parameters.AddWithValue((object?)taxCode ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct); var accounts = new List<(Guid MappingId, Guid MappingVersionId, Guid Id, int Count, string Mapping, long Version, string AccountCode, int Rank)>(); while (await reader.ReadAsync(ct)) accounts.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt32(3), reader.GetString(4), reader.GetInt64(5), reader.GetString(6), reader.GetInt32(7))); if (accounts.Count == 0) throw new LedgerDomainException("ledger.account_mapping_missing", $"No active account mapping exists for role {role}."); if (accounts.Any(x => x.Count > 1) || accounts.Select(x => x.Id).Distinct().Count() != 1) throw new LedgerDomainException("ledger.account_mapping_conflict", $"More than one account mapping matches role {role}."); var selected = accounts[0]; return (selected.Id, new AccountDeterminationExplanation(role, selected.Mapping, selected.Version, selected.MappingId, selected.MappingVersionId, selected.AccountCode, selected.Id, fact.LegalEntityId.Value, selected.Rank, $"highest matching specificity rank {selected.Rank}; category={category ?? "*"}; partyGroup={partyGroup ?? "*"}; taxCode={taxCode ?? "*"}"));
    }

    private static async Task<Guid> ResolvePeriodAsync(ITransactionContext context, Guid tenantId, Guid entityId, DateOnly postingDate, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select p.id from finance_ledger.accounting_period p where p.tenant_id=$1 and p.legal_entity_id=$2 and p.start_date <= $3 and p.end_date >= $3 and p.status='Open' and not exists (select 1 from finance_ledger.financial_lock l where l.tenant_id=p.tenant_id and l.legal_entity_id=p.legal_entity_id and l.status='Active' and l.start_date <= $3 and (l.end_date is null or l.end_date >= $3))", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(postingDate); var ids = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0)); if (ids.Count == 0) throw new LedgerDomainException("ledger.period_locked", "Posting date has no open, unlocked accounting period."); if (ids.Count > 1) throw new LedgerDomainException("ledger.period_ambiguous", "Posting date resolves to more than one accounting period."); return ids[0];
    }

    private static async Task<Guid> ResolveRuleAsync(ITransactionContext context, string factType, int factVersion, DateOnly postingDate, Guid tenantId, Guid entityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select v.id from finance_ledger.posting_rule r join finance_ledger.posting_rule_version v on v.tenant_id=r.tenant_id and v.legal_entity_id=r.legal_entity_id and v.posting_rule_id=r.id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.fact_type=$3 and r.fact_version=$4 and r.status='Active' and v.status='Active' and v.effective_from <= $5 and (v.effective_to is null or v.effective_to >= $5) order by v.version_number desc limit 1", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(factType); command.Parameters.AddWithValue(factVersion); command.Parameters.AddWithValue(postingDate); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.posting_rule_missing", "No active posting rule matches this accounting fact.");
    }

    private static async Task<Guid> ResolveConfigurationAsync(ITransactionContext context, DateOnly postingDate, Guid tenantId, Guid entityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2 and status='Active' and effective_from <= $3 and (effective_to is null or effective_to >= $3) order by version_number desc limit 1", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(postingDate); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.configuration_missing", "No active accounting configuration matches the posting date.");
    }

    private static async Task<(string Hash, Guid? Entry, string? EntryNumber)?> FindFactAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select payload_hash,posted_entry_id,(select entry_number from finance_ledger.journal_entry e where e.tenant_id=f.tenant_id and e.id=f.posted_entry_id) from finance_ledger.accounting_fact f where tenant_id=$1 and legal_entity_id=$2 and (idempotency_key=$3 or (source_module=$4 and source_reference=$5 and fact_type=$6 and fact_version=$7)) for update", context.Connection, context.Transaction); command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(fact.IdempotencyKey); command.Parameters.AddWithValue(fact.SourceModule); command.Parameters.AddWithValue(fact.SourceReference); command.Parameters.AddWithValue(fact.FactType); command.Parameters.AddWithValue(fact.FactVersion); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2)) : null;
    }

    private static async Task EnsureExplicitAccountsAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload payload, CancellationToken ct)
    {
        var accountIds = payload.Lines.Select(line => line.AccountId).Distinct().ToArray();
        await using var active = new NpgsqlCommand("select count(*) from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and status='Active' and id=any($3)", context.Connection, context.Transaction);
        active.Parameters.AddWithValue(fact.TenantId.Value); active.Parameters.AddWithValue(fact.LegalEntityId.Value); active.Parameters.Add(new NpgsqlParameter { Value = accountIds, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid });
        if (Convert.ToInt32(await active.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != accountIds.Length) throw new LedgerDomainException("ledger.account_invalid", "One or more accounts are not active for posting.");
        if (fact.FactType != AccountingFactTypes.ManualGeneralJournalPosted) return;
        await using var control = new NpgsqlCommand("select exists (select 1 from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and id=any($3) and subtype in ('Receivable','Payable','Inventory','OutputTax','InputTax','Tax'))", context.Connection, context.Transaction);
        control.Parameters.AddWithValue(fact.TenantId.Value); control.Parameters.AddWithValue(fact.LegalEntityId.Value); control.Parameters.Add(new NpgsqlParameter { Value = accountIds, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid });
        if ((bool)(await control.ExecuteScalarAsync(ct) ?? false)) throw new LedgerDomainException("ledger.control_account_manual_post_blocked", "Manual journals cannot post directly to a control account.");
    }

    private sealed record DimensionRule(Guid Id, string Code, string? Requirement);

    private static async Task<Dictionary<Guid, List<DimensionRule>>> LoadDimensionRulesAsync(ITransactionContext context, Guid tenantId, Guid entityId, Guid[] accountIds, Guid journalId, string factType, CancellationToken ct)
    {
        await using var policyCommand = new NpgsqlCommand("select a.id,d.id,d.code,p.requirement from (select id,account_type from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and id=any($3)) a cross join finance_ledger.dimension_definition d left join finance_ledger.dimension_policy p on p.tenant_id=d.tenant_id and p.legal_entity_id=d.legal_entity_id and p.dimension_definition_id=d.id and ((p.scope_type='Account' and p.scope_reference=a.id::text) or (p.scope_type='AccountType' and p.scope_reference=a.account_type) or (p.scope_type='Journal' and p.scope_reference=$4::text) or (p.scope_type='FactType' and p.scope_reference=$5::text)) where d.tenant_id=$1 and d.legal_entity_id=$2 and d.status='Active'", context.Connection, context.Transaction);
        policyCommand.Parameters.AddWithValue(tenantId); policyCommand.Parameters.AddWithValue(entityId); policyCommand.Parameters.Add(new NpgsqlParameter { Value = accountIds, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid }); policyCommand.Parameters.AddWithValue(journalId.ToString("D")); policyCommand.Parameters.AddWithValue(factType);
        await using var policyReader = await policyCommand.ExecuteReaderAsync(ct); var rules = new Dictionary<Guid, List<DimensionRule>>(); while (await policyReader.ReadAsync(ct)) { var accountId = policyReader.GetGuid(0); if (!rules.TryGetValue(accountId, out var list)) rules[accountId] = list = []; list.Add(new(policyReader.GetGuid(1), policyReader.GetString(2), policyReader.IsDBNull(3) ? null : policyReader.GetString(3))); } await policyReader.CloseAsync(); return rules;
    }

    private static async Task ValidateAndPersistDimensionsAsync(ITransactionContext context, Guid tenantId, Guid entityId, Guid lineId, IReadOnlyList<LedgerDimensionAssignment> assignments, IReadOnlyList<DimensionRule> definitions, CancellationToken ct)
    {
        var byCode = assignments.GroupBy(a => a.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase); if (definitions.Any(d => d.Requirement == "Required" && !byCode.ContainsKey(d.Code))) throw new LedgerDomainException("ledger.dimension_missing", "A required accounting dimension is missing."); if (definitions.Any(d => d.Requirement == "Forbidden" && byCode.ContainsKey(d.Code))) throw new LedgerDomainException("ledger.dimension_invalid", "A forbidden accounting dimension was supplied.");
        foreach (var assignment in assignments)
        {
            var definition = definitions.FirstOrDefault(d => string.Equals(d.Code, assignment.Code, StringComparison.OrdinalIgnoreCase)); if (definition is null || definition.Id == Guid.Empty) throw new LedgerDomainException("ledger.dimension_invalid", "The accounting dimension is not valid for this posting context.");
            await using var value = new NpgsqlCommand("select 1 from finance_ledger.dimension_value where tenant_id=$1 and legal_entity_id=$2 and id=$3 and dimension_definition_id=$4 and code=$5 and status='Active'", context.Connection, context.Transaction); value.Parameters.AddWithValue(tenantId); value.Parameters.AddWithValue(entityId); value.Parameters.AddWithValue(assignment.ValueId); value.Parameters.AddWithValue(definition.Id); value.Parameters.AddWithValue(assignment.ValueCode); if (await value.ExecuteScalarAsync(ct) is null) throw new LedgerDomainException("ledger.dimension_invalid", "The accounting dimension value is not active for this legal entity.");
            await ExecuteAsync(context, "insert into finance_ledger.journal_line_dimension (tenant_id,legal_entity_id,journal_entry_line_id,dimension_definition_id,dimension_value_id) values ($1,$2,$3,$4,$5)", ct, tenantId, entityId, lineId, definition.Id, assignment.ValueId);
        }
    }

    private static async Task RefreshAccountPeriodBalancesAsync(ITransactionContext context, Guid tenantId, Guid entityId, Guid entryId, CancellationToken ct)
    {
        const string sql = "insert into finance_ledger.account_period_balance (tenant_id,legal_entity_id,account_id,fiscal_year_id,accounting_period_id,dimension_key,currency_code,opening_debit,opening_credit,period_debit,period_credit,closing_balance,updated_at) select e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,'',e.base_currency_code,case when e.fact_type='OpeningBalancePosted' then l.base_debit else 0 end,case when e.fact_type='OpeningBalancePosted' then l.base_credit else 0 end,case when e.fact_type='OpeningBalancePosted' then 0 else l.base_debit end,case when e.fact_type='OpeningBalancePosted' then 0 else l.base_credit end,case when e.fact_type='OpeningBalancePosted' then l.base_debit-l.base_credit else l.base_debit-l.base_credit end,now() from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 on conflict (tenant_id,legal_entity_id,account_id,accounting_period_id,dimension_key,currency_code) do update set opening_debit=finance_ledger.account_period_balance.opening_debit+excluded.opening_debit,opening_credit=finance_ledger.account_period_balance.opening_credit+excluded.opening_credit,period_debit=finance_ledger.account_period_balance.period_debit+excluded.period_debit,period_credit=finance_ledger.account_period_balance.period_credit+excluded.period_credit,closing_balance=finance_ledger.account_period_balance.closing_balance+excluded.closing_balance,updated_at=now()";
        await ExecuteAsync(context, sql, ct, tenantId, entityId, entryId);
    }
}
