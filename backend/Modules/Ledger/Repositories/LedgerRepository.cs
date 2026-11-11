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
    TimeProvider clock) : ILedgerPostingEngine, ILedgerControlBalanceRead
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<Guid> GetActiveConfigurationVersionIdAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => transactions.ExecuteAsync(tenantId, async (context, token) =>
    {
        await using var command = new NpgsqlCommand("select c.active_configuration_version_id from finance_ledger.ledger_entity_configuration c join finance_ledger.accounting_configuration_version v on v.tenant_id=c.tenant_id and v.legal_entity_id=c.legal_entity_id and v.id=c.active_configuration_version_id and v.status='Active' where c.tenant_id=$1 and c.legal_entity_id=$2", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.Value);
        return await command.ExecuteScalarAsync(token) is Guid id ? id : throw new LedgerDomainException("ledger.configuration_missing", "No active accounting configuration exists for this legal entity.");
    }, cancellationToken);

    public async Task<LedgerControlBalanceSnapshot> GetControlBalanceAsync(TenantId tenantId, LegalEntityId legalEntityId, string controlRole, Guid configurationVersionId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        if (controlRole is not ("Receivable" or "Payable" or "Inventory" or "FixedAssetCost" or "FixedAssetAccumulatedDepreciation" or "FixedAssetAccumulatedImpairment" or "FixedAssetCapitalizationOffset" or "FixedAssetImpairment" or "FixedAssetCIP") || configurationVersionId == Guid.Empty)
            throw new LedgerDomainException("ledger.control_balance_query_invalid", "A valid control role and accounting configuration are required.");
        await using var connection = await OpenAsync(tenantId, cancellationToken);
        const string sql = """
            with config as (
              select id,effective_from from finance_ledger.accounting_configuration_version
              where tenant_id=$1 and legal_entity_id=$2 and id=$3
            ), aliases as (
              select case when $4='Receivable' then array['Receivable','AccountsReceivableControl']::text[] when $4='Payable' then array['Payable','AccountsPayableControl']::text[] when $4='Inventory' then array['InventoryAsset']::text[] else array[$4]::text[] end as codes
            ), candidates as (
              select v.target_account_id,(r.code=$4) as exact_role,v.specificity_rank
              from finance_ledger.account_role r
              join finance_ledger.account_mapping m on m.tenant_id=r.tenant_id and m.legal_entity_id=r.legal_entity_id and m.role_id=r.id
              join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id
              join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id
              cross join config c cross join aliases x
              where r.tenant_id=$1 and r.legal_entity_id=$2 and r.code=any(x.codes) and r.status='Active'
                and m.status='Active' and v.status='Active' and v.accounting_configuration_version_id=c.id and a.status='Active' and v.effective_from<=c.effective_from and (v.effective_to is null or v.effective_to>=c.effective_from)
            ), selected as (
              select target_account_id from candidates
              where exact_role=(select coalesce(bool_or(exact_role),false) from candidates)
                and specificity_rank=(select max(specificity_rank) from candidates where exact_role=(select coalesce(bool_or(exact_role),false) from candidates))
            )
            select (select count(*) from selected),(select target_account_id from selected limit 1),
              coalesce((select sum(l.base_debit-l.base_credit) from finance_ledger.journal_entry e
                join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
                where e.tenant_id=$1 and e.legal_entity_id=$2 and e.accounting_configuration_version_id=$3 and e.posting_date<=$5
                  and l.account_id=any(array(select target_account_id from selected))),0)
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.Value); command.Parameters.AddWithValue(configurationVersionId);
        command.Parameters.AddWithValue(controlRole); command.Parameters.AddWithValue(asOfDate);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LedgerDomainException("ledger.control_mapping_missing", "No control account mapping exists for this historical accounting configuration.");
        var matches = reader.GetInt64(0);
        if (matches == 0) throw new LedgerDomainException("ledger.control_mapping_missing", "No control account mapping exists for this historical accounting configuration.");
        if (matches != 1) throw new LedgerDomainException("ledger.control_mapping_conflict", "More than one control account mapping matches this accounting configuration.");
        var accountId = reader.GetGuid(1);
        var signedBalance = reader.GetDecimal(2);
        if (controlRole == "Payable") signedBalance = -signedBalance;
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(tenantId, legalEntityId, cancellationToken)
            ?? throw new LedgerDomainException("ledger.entity_missing", "The legal entity financial context is unavailable.");
        return new(accountId, controlRole, signedBalance, baseCurrency, configurationVersionId);
    }

    public async Task<LedgerControlBalanceSnapshot> GetActiveControlBalanceAsync(TenantId tenantId, LegalEntityId legalEntityId, string controlRole, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var configurationVersionId = await GetActiveConfigurationVersionIdAsync(tenantId, legalEntityId, cancellationToken);
        return await GetControlBalanceAsync(tenantId, legalEntityId, controlRole, configurationVersionId, asOfDate, cancellationToken);
    }

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

    public async Task<JournalEntryPageDto> ListEntriesPageAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to,
        string? search, string? status, string? cursor, int limit, CancellationToken ct, string? sort = null, int? page = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var sortKey = sort?.Trim() switch
        {
            "postingDateAsc" => "postingDateAsc",
            "postingDateDesc" or null or "" => "postingDateDesc",
            _ => throw new LedgerDomainException("ledger.sort_invalid", "The journal-entry sort is invalid.")
        };
        var descending = sortKey == "postingDateDesc";
        var order = descending ? "desc" : "asc";
        var cursorOperator = descending ? "<" : ">";
        DateOnly? cursorDate = null; string? cursorNumber = null; Guid? cursorId = null;
        if (!page.HasValue && !string.IsNullOrWhiteSpace(cursor))
        {
            string[] parts;
            try { parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); }
            catch (FormatException) { throw new LedgerDomainException("ledger.cursor_invalid", "The journal-entry cursor is invalid."); }
            if (parts.Length == 4 && !string.Equals(parts[0], sortKey, StringComparison.Ordinal))
                throw new LedgerDomainException("ledger.cursor_invalid", "The journal-entry cursor does not match the requested sort.");
            var offset = parts.Length == 4 ? 1 : 0;
            if ((parts.Length != 3 && parts.Length != 4) || !DateOnly.TryParse(parts[offset], out var parsedDate) || !Guid.TryParse(parts[offset + 2], out var parsedId))
                throw new LedgerDomainException("ledger.cursor_invalid", "The journal-entry cursor is invalid.");
            cursorDate = parsedDate; cursorNumber = parts[offset + 1]; cursorId = parsedId;
        }

        await using var connection = await OpenAsync(tenant, ct);
        if (page is not null)
        {
            var pageNumber = Math.Max(1, page.Value);
            var offset = (pageNumber - 1) * limit;
            await using var countCommand = new NpgsqlCommand("select count(*) from finance_ledger.journal_entry e where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::text is null or e.entry_number ilike $5 || '%') and ($6::text is null or e.status=$6)", connection);
            countCommand.Parameters.AddWithValue(tenant.Value); countCommand.Parameters.AddWithValue(entity.Value);
            countCommand.Parameters.AddWithValue((object?)from ?? DBNull.Value); countCommand.Parameters.AddWithValue((object?)to ?? DBNull.Value);
            countCommand.Parameters.AddWithValue((object?)CleanCursorText(search) ?? DBNull.Value); countCommand.Parameters.AddWithValue((object?)status ?? DBNull.Value);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            var offsetSql = $"""
                with filtered as (
                    select e.id,e.journal_id,e.fact_id,e.entry_number,e.posting_date,p.start_date,e.status,e.currency_code
                    from finance_ledger.journal_entry e
                    join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id
                    where e.tenant_id=$1 and e.legal_entity_id=$2
                      and ($3::date is null or e.posting_date >= $3)
                      and ($4::date is null or e.posting_date <= $4)
                      and ($5::text is null or e.entry_number ilike $5 || '%')
                      and ($6::text is null or e.status=$6)
                    order by e.posting_date {order},e.entry_number {order},e.id {order}
                    offset $7 limit $8
                )
                select filtered.id,filtered.journal_id,filtered.fact_id,filtered.entry_number,filtered.posting_date,filtered.start_date,filtered.status,filtered.currency_code,
                       l.sequence_no,l.account_id,l.debit,l.credit,l.currency_code,l.amount,l.semantic_role,l.description
                from filtered
                join finance_ledger.journal_entry_line l on l.tenant_id=$1 and l.legal_entity_id=$2 and l.journal_entry_id=filtered.id
                order by filtered.posting_date {order},filtered.entry_number {order},filtered.id {order},l.sequence_no,l.id
                """;
            await using var offsetCommand = new NpgsqlCommand(offsetSql, connection);
            offsetCommand.Parameters.AddWithValue(tenant.Value); offsetCommand.Parameters.AddWithValue(entity.Value);
            offsetCommand.Parameters.AddWithValue((object?)from ?? DBNull.Value); offsetCommand.Parameters.AddWithValue((object?)to ?? DBNull.Value);
            offsetCommand.Parameters.AddWithValue((object?)CleanCursorText(search) ?? DBNull.Value); offsetCommand.Parameters.AddWithValue((object?)status ?? DBNull.Value);
            offsetCommand.Parameters.AddWithValue(offset); offsetCommand.Parameters.AddWithValue(limit);
            await using var offsetReader = await offsetCommand.ExecuteReaderAsync(ct);
            var offsetEntries = new Dictionary<Guid, (Guid Journal, Guid? Fact, string Number, DateOnly Posting, DateOnly Period, string Status, string Currency, List<EntryLineDto> Lines)>();
            var orderedOffsetIds = new List<Guid>();
            while (await offsetReader.ReadAsync(ct))
            {
                var id = offsetReader.GetGuid(0);
                if (!offsetEntries.TryGetValue(id, out var entry))
                {
                    entry = (offsetReader.GetGuid(1), offsetReader.IsDBNull(2) ? null : offsetReader.GetGuid(2), offsetReader.GetString(3), DateOnly.FromDateTime(offsetReader.GetDateTime(4)), DateOnly.FromDateTime(offsetReader.GetDateTime(5)), offsetReader.GetString(6), offsetReader.GetString(7), []);
                    orderedOffsetIds.Add(id);
                }
                entry.Lines.Add(new(offsetReader.GetInt32(8), offsetReader.GetGuid(9), offsetReader.GetDecimal(10), offsetReader.GetDecimal(11), offsetReader.GetString(12), offsetReader.GetDecimal(13), offsetReader.GetString(14), offsetReader.IsDBNull(15) ? null : offsetReader.GetString(15)));
                offsetEntries[id] = entry;
            }
            var offsetItems = orderedOffsetIds.Select(id => { var entry = offsetEntries[id]; return new JournalEntryDto(id, entry.Journal, entry.Fact, entry.Number, entry.Posting, entry.Period, entry.Status, entry.Currency, entry.Lines); }).ToArray();
            return new JournalEntryPageDto(offsetItems, null, totalCount, pageNumber, limit);
        }
        var sql = $"""
            with page as (
                select e.id,e.journal_id,e.fact_id,e.entry_number,e.posting_date,p.start_date,e.status,e.currency_code
                from finance_ledger.journal_entry e
                join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id
                where e.tenant_id=$1 and e.legal_entity_id=$2
                  and ($3::date is null or e.posting_date >= $3)
                  and ($4::date is null or e.posting_date <= $4)
                  and ($5::text is null or e.entry_number ilike $5 || '%')
                  and ($6::text is null or e.status=$6)
                  and ($7::date is null or (e.posting_date,e.entry_number,e.id) {cursorOperator} ($7,$8,$9))
                order by e.posting_date {order},e.entry_number {order},e.id {order}
                limit $10
            )
            select page.id,page.journal_id,page.fact_id,page.entry_number,page.posting_date,page.start_date,page.status,page.currency_code,
                   l.sequence_no,l.account_id,l.debit,l.credit,l.currency_code,l.amount,l.semantic_role,l.description
            from page
            join finance_ledger.journal_entry_line l on l.tenant_id=$1 and l.legal_entity_id=$2 and l.journal_entry_id=page.id
            order by page.posting_date {order},page.entry_number {order},page.id {order},l.sequence_no,l.id
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)CleanCursorText(search) ?? DBNull.Value); command.Parameters.AddWithValue((object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)cursorDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)cursorNumber ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)cursorId ?? DBNull.Value); command.Parameters.AddWithValue(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var entries = new Dictionary<Guid, (Guid Journal, Guid? Fact, string Number, DateOnly Posting, DateOnly Period, string Status, string Currency, List<EntryLineDto> Lines)>();
        var orderedIds = new List<Guid>();
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            if (!entries.TryGetValue(id, out var entry))
            {
                entry = (reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3),
                    DateOnly.FromDateTime(reader.GetDateTime(4)), DateOnly.FromDateTime(reader.GetDateTime(5)), reader.GetString(6), reader.GetString(7), []);
                orderedIds.Add(id);
            }
            entry.Lines.Add(new(reader.GetInt32(8), reader.GetGuid(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetString(12), reader.GetDecimal(13), reader.GetString(14), reader.IsDBNull(15) ? null : reader.GetString(15)));
            entries[id] = entry;
        }

        var hasMore = orderedIds.Count > limit;
        if (hasMore) orderedIds.RemoveAt(orderedIds.Count - 1);
        var items = orderedIds.Select(id =>
        {
            var entry = entries[id];
            return new JournalEntryDto(id, entry.Journal, entry.Fact, entry.Number, entry.Posting, entry.Period, entry.Status, entry.Currency, entry.Lines);
        }).ToArray();
        var last = items.LastOrDefault();
        var next = hasMore && last is not null
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sortKey}|{last.PostingDate:yyyy-MM-dd}|{last.EntryNumber}|{last.Id}"))
            : null;
        return new JournalEntryPageDto(items, next);
    }

    private static string? CleanCursorText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public Task<LedgerPostingResult> PostCustomerInvoiceAsync(AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, CancellationToken cancellationToken = default) =>
        PostAsync(new AccountingFactEnvelope<IAccountingFactPayload>(fact.FactId, fact.TenantId, fact.LegalEntityId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, fact.Payload, fact.CorrelationId, fact.ActorId), cancellationToken);

    public async Task<LedgerPostingResult> PostInvoiceInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        InvoiceLedgerPostingContract invoice, CancellationToken cancellationToken)
    {
        if (fact.SourceModule != "invoicing" || fact.FactVersion != 1
            || fact.FactType is not (AccountingFactTypes.CustomerInvoicePosted or AccountingFactTypes.VendorBillPosted or AccountingFactTypes.CustomerCreditNotePosted or AccountingFactTypes.VendorCreditNotePosted or AccountingFactTypes.CustomerDebitNotePosted or AccountingFactTypes.VendorDebitNotePosted))
            throw new LedgerDomainException("ledger.fact_unsupported", "The invoice fact type is not supported by the transactional invoice posting contract.");
        if (invoice.Direction is not ("Receivable" or "Payable") || invoice.DocumentType is not ("Invoice" or "CreditNote" or "DebitNote")
            || invoice.NetAmount <= 0m || invoice.TaxAmount < 0m || invoice.GrossAmount != invoice.NetAmount + invoice.TaxAmount
            || invoice.Lines.Count == 0 || invoice.Lines.Any(line => string.IsNullOrWhiteSpace(line.CategoryCode) || line.NetAmount < 0m)
            || invoice.Lines.Sum(line => line.NetAmount) != invoice.NetAmount || invoice.Taxes.Sum(tax => tax.Amount) != invoice.TaxAmount)
            throw new LedgerDomainException("ledger.fact_invalid", "Invoice amounts or accounting categories are inconsistent.");
        var expectedFactType = (invoice.Direction, invoice.DocumentType) switch
        {
            ("Receivable", "Invoice") => AccountingFactTypes.CustomerInvoicePosted,
            ("Receivable", "CreditNote") => AccountingFactTypes.CustomerCreditNotePosted,
            ("Receivable", "DebitNote") => AccountingFactTypes.CustomerDebitNotePosted,
            ("Payable", "Invoice") => AccountingFactTypes.VendorBillPosted,
            ("Payable", "CreditNote") => AccountingFactTypes.VendorCreditNotePosted,
            ("Payable", "DebitNote") => AccountingFactTypes.VendorDebitNotePosted,
            _ => throw new LedgerDomainException("ledger.fact_invalid", "Invoice direction and document type are invalid.")
        };
        if (!string.Equals(fact.FactType, expectedFactType, StringComparison.Ordinal))
            throw new LedgerDomainException("ledger.fact_invalid", "The semantic accounting fact does not match the invoice direction and document type.");
        var payloadJson = JsonSerializer.Serialize(invoice, JsonOptions);
        var payloadHash = AccountingFactFingerprint.Compute(invoice);
        var prior = await FindInvoiceFactAsync(context, fact, cancellationToken);
        if (prior is not null)
        {
            if (!string.Equals(prior.Value.Hash, payloadHash, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.fact_duplicate_conflict", "The source invoice fact already exists with different financial facts.");
            if (prior.Value.Entry is { } priorEntry) return new(fact.FactId, priorEntry, prior.Value.EntryNumber!, "Posted", true);
        }
        else
        {
            if (!await IsRegisteredProducerAsync(context, fact, cancellationToken)) throw new LedgerDomainException("ledger.fact_producer_unregistered", "Invoicing is not registered for this accounting fact type and version.");
            await ExecuteAsync(context, "insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Received',$13,$14,$15)", cancellationToken,
                fact.TenantId.Value, fact.LegalEntityId.Value, fact.FactId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey,
                fact.BusinessDate, fact.PostingDate, new NpgsqlParameter { Value = payloadJson, NpgsqlDbType = NpgsqlDbType.Jsonb }, payloadHash, fact.CorrelationId, fact.ActorId, clock.GetUtcNow());
        }

        var customer = invoice.Direction == "Receivable";
        var reversal = invoice.DocumentType == "CreditNote";
        var postingEnvelope = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(fact.FactId, fact.TenantId, fact.LegalEntityId, fact.FactType, fact.FactVersion,
            fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate,
            new(invoice.NetAmount, invoice.TaxAmount, invoice.GrossAmount, invoice.CurrencyCode, invoice.PartyGroup, invoice.Lines[0].CategoryCode, invoice.Taxes, invoice.ExchangeRate, invoice.ExchangeRateReference), fact.CorrelationId, fact.ActorId);
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, cancellationToken)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var exchangeRate = string.Equals(invoice.CurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase) ? 1m
            : invoice.ExchangeRate ?? throw new LedgerDomainException("ledger.currency_mismatch", "A foreign-currency invoice requires an approved exchange-rate snapshot.");
        if (exchangeRate <= 0m) throw new LedgerDomainException("ledger.currency_mismatch", "Exchange rate must be positive.");
        var period = await ResolvePeriodAsync(context, postingEnvelope, cancellationToken);
        var rule = await ResolveRuleAsync(context, postingEnvelope, cancellationToken);
        var configuration = await ResolveConfigurationAsync(context, postingEnvelope, cancellationToken);
        var journalCode = customer ? "SALES" : "PURCHASE";
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code=$3 and status='Active'", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, journalCode)
            ?? throw new LedgerDomainException("ledger.journal_missing", $"The {journalCode} journal is not configured.");
        var determinations = new List<AccountDeterminationExplanation>();
        var controlRole = customer ? "Receivable" : "Payable";
        var control = await ResolveAccountAsync(context, postingEnvelope, configuration, controlRole, null, invoice.PartyGroup, null, cancellationToken);
        determinations.Add(control.Explanation);
        var entries = new List<(Guid Account, decimal Debit, decimal Credit, string Role, decimal Amount, AccountDeterminationExplanation Evidence)>();
        var controlDebit = customer != reversal;
        entries.Add((control.Id, controlDebit ? invoice.GrossAmount : 0m, controlDebit ? 0m : invoice.GrossAmount, controlRole, invoice.GrossAmount, control.Explanation));
        var revenueRole = customer ? "SalesRevenue" : "PurchaseExpense";
        var lineGroups = invoice.Lines.GroupBy(line => line.CategoryCode, StringComparer.Ordinal).Select(group => new InvoiceLedgerLineContract(group.Key, group.Sum(line => line.NetAmount))).ToArray();
        var categoryAccounts = await ResolveInvoiceAccountsAsync(context, postingEnvelope, configuration,
            revenueRole, lineGroups.Select(line => new InvoiceAccountLookup(line.CategoryCode, null)).ToArray(), cancellationToken);
        foreach (var item in lineGroups)
        {
            var resolved = categoryAccounts[new(item.CategoryCode, null)];
            determinations.Add(resolved.Explanation);
            var credit = customer != reversal;
            entries.Add((resolved.Id, credit ? 0m : item.NetAmount, credit ? item.NetAmount : 0m, revenueRole, item.NetAmount, resolved.Explanation));
        }
        var taxGroups = invoice.Taxes.GroupBy(tax => (tax.AccountRole, tax.Code)).Select(group => (group.Key.AccountRole, group.Key.Code, Amount: group.Sum(tax => tax.Amount))).ToArray();
        foreach (var taxRoleGroup in taxGroups.GroupBy(tax => tax.AccountRole, StringComparer.Ordinal))
        {
            var role = string.IsNullOrWhiteSpace(taxRoleGroup.Key) ? (customer ? "OutputTax" : "InputTax") : taxRoleGroup.Key;
            var taxAccounts = await ResolveInvoiceAccountsAsync(context, postingEnvelope, configuration, role,
                taxRoleGroup.Select(tax => new InvoiceAccountLookup(null, tax.Code)).ToArray(), cancellationToken);
            foreach (var tax in taxRoleGroup)
            {
                var resolved = taxAccounts[new(null, tax.Code)];
                determinations.Add(resolved.Explanation);
                var credit = customer != reversal;
                entries.Add((resolved.Id, credit ? 0m : tax.Amount, credit ? tax.Amount : 0m, role, tax.Amount, resolved.Explanation));
            }
        }
        LedgerDomainRules.EnsureBalanced(entries.Sum(line => line.Debit), entries.Sum(line => line.Credit));
        var entryId = Guid.CreateVersion7();
        var entryNumber = $"JE-{fact.FactId:N}";
        var now = clock.GetUtcNow();
        await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", cancellationToken,
            fact.TenantId.Value, fact.LegalEntityId.Value, entryId, journal, fact.FactId, fact.SourceModule, fact.SourceReference, entryNumber, fact.PostingDate, period, fact.FactType,
            fact.FactVersion, invoice.CurrencyCode, baseCurrency, rule, configuration, $"{invoice.Direction} {invoice.DocumentType} posted", fact.CorrelationId, fact.ActorId, now);
        var sequence = 1;
        foreach (var line in entries)
        {
            var exchangeSnapshot = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { rate = exchangeRate, reference = invoice.ExchangeRateReference, from = invoice.CurrencyCode, to = baseCurrency }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb };
            var evidence = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { specificity = line.Evidence.SpecificityRank, role = line.Role }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb };
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,account_role,account_mapping_id,account_mapping_version_id,determination_context,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate,exchange_rate_snapshot) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21)", cancellationToken,
                fact.TenantId.Value, fact.LegalEntityId.Value, Guid.CreateVersion7(), entryId, sequence++, line.Account, line.Debit, line.Credit, invoice.CurrencyCode, line.Amount,
                line.Role, line.Evidence.AccountRole, line.Evidence.MappingId, line.Evidence.MappingVersionId, evidence, line.Debit, line.Credit, line.Debit * exchangeRate,
                line.Credit * exchangeRate, exchangeRate, exchangeSnapshot);
        }
        await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", cancellationToken, fact.TenantId.Value, fact.FactId, entryId, now);
        await RefreshAccountPeriodBalancesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, cancellationToken);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryPostedV1, JsonSerializer.Serialize(new { fact.FactId, entryId, entryNumber, fact.FactType }, JsonOptions), fact.TenantId, now, fact.CorrelationId, fact.FactId, "ledger"));
        return new(fact.FactId, entryId, entryNumber, "Posted", false, determinations);
    }

    public async Task<InvoiceAccountingPreview> PreviewInvoiceInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, InvoiceLedgerPostingContract invoice, CancellationToken cancellationToken)
    {
        var posted = await PostInvoiceInTransactionAsync(context, fact, invoice, cancellationToken);
        await using var command = new NpgsqlCommand("select j.code,e.base_currency_code,e.posting_rule_version_id,e.accounting_configuration_version_id,l.sequence_no,a.code,l.account_role,l.debit,l.credit,l.currency_code,l.base_debit,l.base_credit,m.code,mv.version_number from finance_ledger.journal_entry e join finance_ledger.journal j on j.tenant_id=e.tenant_id and j.legal_entity_id=e.legal_entity_id and j.id=e.journal_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id left join finance_ledger.account_mapping m on m.tenant_id=l.tenant_id and m.legal_entity_id=l.legal_entity_id and m.id=l.account_mapping_id left join finance_ledger.account_mapping_version mv on mv.tenant_id=l.tenant_id and mv.legal_entity_id=l.legal_entity_id and mv.id=l.account_mapping_version_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 order by l.sequence_no", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value);
        command.Parameters.AddWithValue(fact.LegalEntityId.Value);
        command.Parameters.AddWithValue(posted.JournalEntryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<InvoiceAccountingPreviewLine>();
        string? journalCode = null;
        string? baseCurrency = null;
        var rule = Guid.Empty;
        var configuration = Guid.Empty;
        while (await reader.ReadAsync(cancellationToken))
        {
            journalCode ??= reader.GetString(0);
            baseCurrency ??= reader.GetString(1);
            rule = reader.GetGuid(2);
            configuration = reader.GetGuid(3);
            lines.Add(new(reader.GetInt32(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8),
                reader.GetString(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetInt64(13)));
        }
        if (lines.Count == 0 || journalCode is null || baseCurrency is null)
            throw new LedgerDomainException("ledger.preview_unavailable", "The invoice accounting preview could not be read from the posting engine result.");
        return new(journalCode, baseCurrency, rule, configuration, lines, lines.Sum(line => line.BaseDebit), lines.Sum(line => line.BaseCredit));
    }

    public static async Task<Guid> GetActiveConfigurationVersionIdAsync(ITransactionContext context, TenantId tenantId, LegalEntityId legalEntityId, CancellationToken ct)
    {
        var id = await ScalarGuidAsync(context, "select c.active_configuration_version_id from finance_ledger.ledger_entity_configuration c join finance_ledger.accounting_configuration_version v on v.tenant_id=c.tenant_id and v.legal_entity_id=c.legal_entity_id and v.id=c.active_configuration_version_id and v.status='Active' where c.tenant_id=$1 and c.legal_entity_id=$2 for share of c", ct, tenantId.Value, legalEntityId.Value);
        return id ?? throw new LedgerDomainException("ledger.configuration_missing", "No active accounting configuration exists for this legal entity.");
    }

    private static async Task<(string Hash, Guid? Entry, string? EntryNumber)?> FindInvoiceFactAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select payload_hash,posted_entry_id,(select entry_number from finance_ledger.journal_entry e where e.tenant_id=f.tenant_id and e.id=f.posted_entry_id) from finance_ledger.accounting_fact f where tenant_id=$1 and legal_entity_id=$2 and (idempotency_key=$3 or (source_module=$4 and source_reference=$5 and fact_type=$6 and fact_version=$7)) for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(fact.IdempotencyKey); command.Parameters.AddWithValue(fact.SourceModule); command.Parameters.AddWithValue(fact.SourceReference); command.Parameters.AddWithValue(fact.FactType); command.Parameters.AddWithValue(fact.FactVersion);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2)) : null;
    }

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
            var ar = await ResolveAccountAsync(context, invoiceFact, configuration, "Receivable", null, invoice.ReceivablePartyGroup, null, ct); determinations.Add(ar.Explanation);
            var revenue = await ResolveAccountAsync(context, invoiceFact, configuration, "SalesRevenue", invoice.RevenueCategory, null, null, ct); determinations.Add(revenue.Explanation);
            var lines = new List<(Guid Account, decimal Debit, decimal Credit, string Role, decimal Amount, AccountDeterminationExplanation Evidence)> { (ar.Id, invoice.GrossAmount, 0m, "Receivable", invoice.GrossAmount, ar.Explanation), (revenue.Id, 0m, invoice.NetAmount, "SalesRevenue", invoice.NetAmount, revenue.Explanation) };
            foreach (var tax in invoice.Taxes) { var determination = await ResolveAccountAsync(context, invoiceFact, configuration, tax.AccountRole, null, null, tax.Code, ct); determinations.Add(determination.Explanation); lines.Add((determination.Id, 0m, tax.Amount, tax.AccountRole, tax.Amount, determination.Explanation)); }
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
    public Task<LedgerPostingResult> PostExplicitInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        ExplicitLedgerPostingPayload payload, Guid? configurationVersionOverride = null, CancellationToken cancellationToken = default) =>
        PostExplicitCoreInTransactionAsync(context, fact, payload, configurationVersionOverride, null, cancellationToken);

    public Task<LedgerPostingResult> PostSemanticExplicitInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, ExplicitLedgerPostingPayload posting, IAccountingFactPayload semanticPayload,
        Guid? configurationVersionOverride = null, CancellationToken cancellationToken = default) =>
        PostExplicitCoreInTransactionAsync(context, fact, posting, configurationVersionOverride, semanticPayload, cancellationToken);

    public async Task<LedgerPostingResult> PostPayrollInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, PayrollPostedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "payroll" || fact.FactType != AccountingFactTypes.PayrollPosted
            || payload.GrossAmount < 0m || payload.PayableAmount < 0m || payload.DeductionsAmount < 0m
            || payload.PayableAmount + payload.DeductionsAmount != payload.GrossAmount
            || payload.TaxAmount < 0m || payload.InsuranceAmount < 0m || payload.RecoveryAmount < 0m
            || payload.TaxAmount + payload.InsuranceAmount + payload.RecoveryAmount > payload.DeductionsAmount
            || payload.CurrencyCode.Trim().Length != 3)
            throw new LedgerDomainException("ledger.payroll_fact_invalid", "The Payroll accounting fact is incomplete or financially inconsistent.");
        if (!await IsRegisteredProducerAsync(context, fact, ct))
            throw new LedgerDomainException("ledger.fact_producer_unregistered", "Payroll is not registered for this accounting fact type.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            throw new LedgerDomainException("ledger.currency_mismatch", "Payroll posting currently requires the legal-entity base currency.");
        var configuration = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        var expense = await ResolveSemanticAccountIdAsync(context, fact, configuration, "PayrollExpense", ct);
        var payable = await ResolveSemanticAccountIdAsync(context, fact, configuration, "PayrollPayable", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var lines = new List<LedgerPostingLinePayload>
        {
            new(expense, payload.GrossAmount, 0m, currency, payload.GrossAmount, 0m, payload.GrossAmount, 0m, 1m, [], "Payroll expense", null),
            new(payable, 0m, payload.PayableAmount, currency, 0m, payload.PayableAmount, 0m, payload.PayableAmount, 1m, [], "Payroll payable", null)
        };
        if (payload.DeductionsAmount > 0m)
        {
            var residual = payload.DeductionsAmount;
            if (payload.TaxAmount > 0m)
            {
                var account = await ResolveSemanticAccountIdAsync(context, fact, configuration, "PayrollTaxPayable", ct);
                lines.Add(new(account, 0m, payload.TaxAmount, currency, 0m, payload.TaxAmount, 0m, payload.TaxAmount, 1m, [], "Payroll tax payable", null)); residual -= payload.TaxAmount;
            }
            if (payload.InsuranceAmount > 0m)
            {
                var account = await ResolveSemanticAccountIdAsync(context, fact, configuration, "PayrollInsurancePayable", ct);
                lines.Add(new(account, 0m, payload.InsuranceAmount, currency, 0m, payload.InsuranceAmount, 0m, payload.InsuranceAmount, 1m, [], "Payroll insurance payable", null)); residual -= payload.InsuranceAmount;
            }
            if (payload.RecoveryAmount > 0m)
            {
                var account = await ResolveSemanticAccountIdAsync(context, fact, configuration, "EmployeeReceivable", ct);
                lines.Add(new(account, 0m, payload.RecoveryAmount, currency, 0m, payload.RecoveryAmount, 0m, payload.RecoveryAmount, 1m, [], "Employee loan recovery", null)); residual -= payload.RecoveryAmount;
            }
            if (residual > 0m)
                lines.Add(new(payable, 0m, residual, currency, 0m, residual, 0m, residual, 1m, [], "Other payroll deductions payable", null));
        }
        var posting = new ExplicitLedgerPostingPayload(journal, "Payroll posting", currency, lines);
        return await PostSemanticExplicitInTransactionAsync(context, fact, posting, payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostExpenseInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, ExpenseRecognizedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "expenses" || fact.FactType != AccountingFactTypes.ExpenseRecognized
            || payload.Amount <= 0m || payload.CurrencyCode.Trim().Length != 3 || string.IsNullOrWhiteSpace(payload.ExpenseCategory))
            throw new LedgerDomainException("ledger.expense_fact_invalid", "The Expenses accounting fact is incomplete or financially inconsistent.");
        if (!await IsRegisteredProducerAsync(context, fact, ct))
            throw new LedgerDomainException("ledger.fact_producer_unregistered", "Expenses is not registered for this accounting fact type.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant();
        var factBaseCurrency = string.IsNullOrWhiteSpace(payload.BaseCurrencyCode) ? baseCurrency : payload.BaseCurrencyCode.Trim().ToUpperInvariant();
        var baseAmount = payload.BaseAmount ?? payload.Amount;
        if (factBaseCurrency != baseCurrency || baseAmount <= 0m)
            throw new LedgerDomainException("ledger.currency_mismatch", "Expense posting requires an immutable legal-entity base-currency snapshot.");
        if (!string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase) && payload.BaseAmount is null)
            throw new LedgerDomainException("ledger.currency_mismatch", "Foreign-currency expense posting requires an approved base amount.");
        var exchangeRate = payload.ExchangeRate ?? baseAmount / payload.Amount;
        if (exchangeRate <= 0m)
            throw new LedgerDomainException("ledger.currency_mismatch", "Expense exchange rate must be positive.");
        var configuration = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        var expenseAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "PurchaseExpense", ct);
        var reimbursementAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration,
            payload.SettlementAccountRole is "Cash" or "EmployeeReimbursementPayable" ? payload.SettlementAccountRole : "EmployeeReimbursementPayable", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var lines = new[]
        {
            new LedgerPostingLinePayload(expenseAccount, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, exchangeRate, [], $"Expense: {payload.ExpenseCategory}", fact.SourceReference),
            new LedgerPostingLinePayload(reimbursementAccount, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, exchangeRate, [], payload.SettlementAccountRole == "Cash" ? "Company-paid cash settlement" : "Employee reimbursement payable", fact.SourceReference)
        };
        return await PostSemanticExplicitInTransactionAsync(context, fact,
            new ExplicitLedgerPostingPayload(journal, $"Expense recognized: {payload.ExpenseCategory}", baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostInventoryValuationInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, InventoryValuationFactPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "inventory" || fact.FactType is not (AccountingFactTypes.InventoryReceiptValued or AccountingFactTypes.InventoryIssueValued or AccountingFactTypes.InventoryCostAdjustment)
            || payload.Amount <= 0m || payload.Quantity <= 0m || payload.UnitCost < 0m || payload.ProductVariantId is null || payload.MovementId is null
            || string.IsNullOrWhiteSpace(payload.InventoryGroup) || string.IsNullOrWhiteSpace(payload.ValuationRole))
            throw new LedgerDomainException("ledger.inventory_fact_invalid", "The Inventory valuation fact is missing its immutable quantity, cost or source identity.");
        if (!await IsRegisteredProducerAsync(context, fact, ct))
            throw new LedgerDomainException("ledger.fact_producer_unregistered", "Inventory is not registered for this accounting fact type.");

        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var transactionCurrency = payload.CurrencyCode.Trim().ToUpperInvariant();
        var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? transactionCurrency;
        var baseAmount = payload.BaseAmount ?? payload.Amount;
        if (transactionCurrency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m
            || transactionCurrency != baseCurrency && (payload.BaseAmount is null || string.IsNullOrWhiteSpace(payload.BaseCurrencyCode)))
            throw new LedgerDomainException("ledger.currency_mismatch", "Inventory valuation requires an immutable base-currency snapshot for this legal entity.");

        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var asset = await ResolveSemanticAccountIdAsync(context, fact, configuration, "InventoryAsset", ct);
        var counterpartRole = fact.FactType switch
        {
            AccountingFactTypes.InventoryReceiptValued => payload.ValuationRole == "Opening" ? "RetainedEarnings" : "InventoryOffset",
            AccountingFactTypes.InventoryIssueValued => "COGS",
            AccountingFactTypes.InventoryCostAdjustment => payload.ValuationRole == "Increase" ? "InventoryAdjustmentGain" : "InventoryAdjustmentLoss",
            _ => throw new LedgerDomainException("ledger.inventory_fact_invalid", "This Inventory valuation fact type is not supported.")
        };
        var counterpart = await ResolveSemanticAccountIdAsync(context, fact, configuration, counterpartRole, ct);
        var journalId = await ScalarGuidAsync(context,
            "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'",
            ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var exchangeRate = baseAmount / payload.Amount;
        LedgerPostingLinePayload Debit(Guid account, decimal amount, decimal baseValue, string description) =>
            new(account, baseValue, 0m, transactionCurrency, amount, 0m, baseValue, 0m, exchangeRate, [], description, payload.MovementId.Value.ToString("D"));
        LedgerPostingLinePayload Credit(Guid account, decimal amount, decimal baseValue, string description) =>
            new(account, 0m, baseValue, transactionCurrency, 0m, amount, 0m, baseValue, exchangeRate, [], description, payload.MovementId.Value.ToString("D"));
        var description = $"Inventory {payload.ValuationRole}: {payload.Quantity} units at {payload.UnitCost} {transactionCurrency}";
        var lines = fact.FactType switch
        {
            AccountingFactTypes.InventoryReceiptValued => new[] { Debit(asset, payload.Amount, baseAmount, description), Credit(counterpart, payload.Amount, baseAmount, description) },
            AccountingFactTypes.InventoryIssueValued => new[] { Debit(counterpart, payload.Amount, baseAmount, description), Credit(asset, payload.Amount, baseAmount, description) },
            _ when payload.ValuationRole == "Increase" => new[] { Debit(asset, payload.Amount, baseAmount, description), Credit(counterpart, payload.Amount, baseAmount, description) },
            _ => new[] { Debit(counterpart, payload.Amount, baseAmount, description), Credit(asset, payload.Amount, baseAmount, description) }
        };
        var semanticFact = fact with { Payload = payload };
        return await PostSemanticExplicitInTransactionAsync(context, semanticFact,
            new ExplicitLedgerPostingPayload(journalId, description, baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostFixedAssetAcquisitionInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetAcquisitionPostedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "fixed-assets" || fact.FactType != AccountingFactTypes.FixedAssetAcquisitionPosted || payload.Amount <= 0m || string.IsNullOrWhiteSpace(payload.AssetReference) || string.IsNullOrWhiteSpace(payload.AssetCategory))
            throw new LedgerDomainException("ledger.fixed_asset_fact_invalid", "The fixed asset acquisition fact is incomplete.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant(); var baseAmount = payload.BaseAmount ?? payload.Amount; var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? currency;
        if (currency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m || currency != baseCurrency && payload.BaseAmount is null) throw new LedgerDomainException("ledger.currency_mismatch", "Fixed asset acquisition requires a base-currency snapshot.");
        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var asset = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetCost", ct);
        var offset = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetCapitalizationOffset", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = baseAmount / payload.Amount; var description = $"Fixed asset acquisition {payload.AssetReference}: {payload.AssetCategory}";
        var lines = new[] { new LedgerPostingLinePayload(asset, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, rate, [], description, payload.AssetReference), new LedgerPostingLinePayload(offset, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, rate, [], description, payload.AssetReference) };
        return await PostSemanticExplicitInTransactionAsync(context, fact, new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostFixedAssetDepreciationInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDepreciationPostedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "fixed-assets" || fact.FactType != AccountingFactTypes.FixedAssetDepreciationPosted || payload.Amount <= 0m || string.IsNullOrWhiteSpace(payload.AssetBookReference))
            throw new LedgerDomainException("ledger.fixed_asset_fact_invalid", "The fixed asset depreciation fact is incomplete.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant(); var baseAmount = payload.BaseAmount ?? payload.Amount; var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? currency;
        if (currency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m || currency != baseCurrency && payload.BaseAmount is null) throw new LedgerDomainException("ledger.currency_mismatch", "Fixed asset depreciation requires a base-currency snapshot.");
        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var expense = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetDepreciationExpense", ct);
        var accumulated = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetAccumulatedDepreciation", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = baseAmount / payload.Amount; var description = $"Depreciation {payload.AssetBookReference}: {payload.DepreciationExpenseCategory}";
        var lines = new[] { new LedgerPostingLinePayload(expense, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, rate, [], description, payload.AssetBookReference), new LedgerPostingLinePayload(accumulated, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, rate, [], description, payload.AssetBookReference) };
        return await PostSemanticExplicitInTransactionAsync(context, fact, new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostFixedAssetDepreciationReversalInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDepreciationReversedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "fixed-assets" || fact.FactType != AccountingFactTypes.FixedAssetDepreciationReversed || payload.Amount <= 0m || string.IsNullOrWhiteSpace(payload.AssetBookReference))
            throw new LedgerDomainException("ledger.fixed_asset_reversal_fact_invalid", "The fixed asset depreciation reversal fact is incomplete.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant(); var baseAmount = payload.BaseAmount ?? payload.Amount; var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? currency;
        if (currency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m || currency != baseCurrency && payload.BaseAmount is null) throw new LedgerDomainException("ledger.currency_mismatch", "Fixed asset depreciation reversal requires a base-currency snapshot.");
        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var expense = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetDepreciationExpense", ct);
        var accumulated = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetAccumulatedDepreciation", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = baseAmount / payload.Amount; var description = $"Depreciation reversal {payload.AssetBookReference}: {payload.DepreciationExpenseCategory}";
        var lines = new[] { new LedgerPostingLinePayload(accumulated, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, rate, [], description, payload.AssetBookReference), new LedgerPostingLinePayload(expense, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, rate, [], description, payload.AssetBookReference) };
        return await PostSemanticExplicitInTransactionAsync(context, fact, new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostFixedAssetDisposalInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetDisposalPostedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "fixed-assets" || fact.FactType != AccountingFactTypes.FixedAssetDisposalPosted || payload.GrossCost < 0m || payload.AccumulatedDepreciation < 0m || payload.AccumulatedImpairment < 0m || payload.NetBookValue < 0m || payload.Proceeds < 0m || payload.DirectCosts < 0m || string.IsNullOrWhiteSpace(payload.AssetReference) || payload.GrossCost - payload.AccumulatedDepreciation - payload.AccumulatedImpairment != payload.NetBookValue || payload.Proceeds - payload.DirectCosts - payload.NetBookValue != payload.GainLoss)
            throw new LedgerDomainException("ledger.fixed_asset_fact_invalid", "The fixed asset disposal fact is financially inconsistent.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant(); if (currency.Length != 3) throw new LedgerDomainException("ledger.currency_mismatch", "A three-letter currency code is required.");
        var gross = payload.BaseGrossCost ?? payload.GrossCost; var accumulatedAmount = payload.BaseAccumulatedDepreciation ?? payload.AccumulatedDepreciation; var impairmentAmount = payload.AccumulatedImpairment; var proceeds = payload.BaseProceeds ?? payload.Proceeds; var directCosts = payload.BaseDirectCosts ?? payload.DirectCosts; var baseNbv = gross - accumulatedAmount - impairmentAmount; var baseGainLoss = proceeds - directCosts - baseNbv;
        if (gross < 0m || accumulatedAmount < 0m || impairmentAmount < 0m || proceeds < 0m || directCosts < 0m || baseNbv < 0m || baseGainLoss != (payload.GainLoss == 0m ? baseGainLoss : payload.GainLoss)) throw new LedgerDomainException("ledger.fixed_asset_fact_invalid", "The base-currency disposal values are inconsistent.");
        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var cost = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetCost", ct); var accumulated = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetAccumulatedDepreciation", ct); var accumulatedImpairment = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetAccumulatedImpairment", ct); var cash = await ResolveSemanticAccountIdAsync(context, fact, configuration, "Cash", ct);
        var gain = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetDisposalGain", ct); var loss = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetDisposalLoss", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var description = $"Fixed asset disposal {payload.AssetReference}"; var lines = new List<LedgerPostingLinePayload>();
        if (accumulatedAmount > 0m) lines.Add(new(accumulated, accumulatedAmount, 0m, currency, payload.AccumulatedDepreciation, 0m, accumulatedAmount, 0m, 1m, [], description, payload.AssetReference));
        if (impairmentAmount > 0m) lines.Add(new(accumulatedImpairment, impairmentAmount, 0m, currency, impairmentAmount, 0m, impairmentAmount, 0m, 1m, [], description, payload.AssetReference));
        if (proceeds > 0m) lines.Add(new(cash, proceeds, 0m, currency, payload.Proceeds, 0m, proceeds, 0m, 1m, [], description, payload.AssetReference));
        if (baseGainLoss < 0m) lines.Add(new(loss, -baseGainLoss, 0m, currency, -payload.GainLoss, 0m, -baseGainLoss, 0m, 1m, [], description, payload.AssetReference));
        if (baseGainLoss > 0m) lines.Add(new(gain, 0m, baseGainLoss, currency, 0m, payload.GainLoss, 0m, baseGainLoss, 1m, [], description, payload.AssetReference));
        lines.Add(new(cost, 0m, gross, currency, 0m, payload.GrossCost, 0m, gross, 1m, [], description, payload.AssetReference));
        if (directCosts > 0m) lines.Add(new(cash, 0m, directCosts, currency, 0m, payload.DirectCosts, 0m, directCosts, 1m, [], description, payload.AssetReference));
        return await PostSemanticExplicitInTransactionAsync(context, fact, new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines), payload, configuration, ct);
    }

    public Task<LedgerPostingResult> PostFixedAssetCostAddedInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetCostAdjustmentPostedPayload payload, CancellationToken ct) => PostFixedAssetCostAdjustmentInTransactionAsync(context, fact, payload, true, ct);

    public Task<LedgerPostingResult> PostFixedAssetCostReducedInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetCostAdjustmentPostedPayload payload, CancellationToken ct) => PostFixedAssetCostAdjustmentInTransactionAsync(context, fact, payload, false, ct);

    private async Task<LedgerPostingResult> PostFixedAssetCostAdjustmentInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetCostAdjustmentPostedPayload payload, bool added, CancellationToken ct)
    {
        var expectedFactType = added ? AccountingFactTypes.FixedAssetCostAdded : AccountingFactTypes.FixedAssetCostReduced;
        if (fact.SourceModule != "fixed-assets" || fact.FactType != expectedFactType || payload.Amount <= 0m || string.IsNullOrWhiteSpace(payload.AssetBookReference) || string.IsNullOrWhiteSpace(payload.AssetReference) || string.IsNullOrWhiteSpace(payload.Reason))
            throw new LedgerDomainException("ledger.fixed_asset_cost_fact_invalid", "The fixed asset cost adjustment fact is incomplete.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant(); var baseAmount = payload.BaseAmount ?? payload.Amount; var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? currency;
        if (currency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m || currency != baseCurrency && payload.BaseAmount is null) throw new LedgerDomainException("ledger.currency_mismatch", "Fixed asset cost adjustments require a base-currency snapshot.");
        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var cost = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetCost", ct);
        var offset = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetCapitalizationOffset", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = baseAmount / payload.Amount; var description = $"Fixed asset {(added ? "cost addition" : "cost reduction")} {payload.AssetReference}: {payload.Reason}";
        var lines = added
            ? new[] { new LedgerPostingLinePayload(cost, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, rate, [], description, payload.AssetBookReference), new LedgerPostingLinePayload(offset, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, rate, [], description, payload.AssetBookReference) }
            : new[] { new LedgerPostingLinePayload(offset, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, rate, [], description, payload.AssetBookReference), new LedgerPostingLinePayload(cost, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, rate, [], description, payload.AssetBookReference) };
        return await PostSemanticExplicitInTransactionAsync(context, fact, new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostFixedAssetImpairmentInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, FixedAssetImpairmentPostedPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "fixed-assets" || fact.FactType != AccountingFactTypes.FixedAssetImpairmentPosted || payload.Amount <= 0m || string.IsNullOrWhiteSpace(payload.AssetBookReference) || string.IsNullOrWhiteSpace(payload.AssetReference) || string.IsNullOrWhiteSpace(payload.Reason))
            throw new LedgerDomainException("ledger.fixed_asset_impairment_invalid", "The fixed asset impairment fact is incomplete.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant(); var baseAmount = payload.BaseAmount ?? payload.Amount; var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? currency;
        if (currency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m || currency != baseCurrency && payload.BaseAmount is null) throw new LedgerDomainException("ledger.currency_mismatch", "Fixed asset impairment requires a base-currency snapshot.");
        var configuration = await GetActiveConfigurationVersionIdAsync(context, fact.TenantId, fact.LegalEntityId, ct);
        var expense = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetImpairment", ct);
        var accumulated = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FixedAssetAccumulatedImpairment", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = baseAmount / payload.Amount; var description = $"Fixed asset impairment {payload.AssetReference}: {payload.Reason}";
        var lines = new[]
        {
            new LedgerPostingLinePayload(expense, baseAmount, 0m, currency, payload.Amount, 0m, baseAmount, 0m, rate, [], description, payload.AssetBookReference),
            new LedgerPostingLinePayload(accumulated, 0m, baseAmount, currency, 0m, payload.Amount, 0m, baseAmount, rate, [], description, payload.AssetBookReference)
        };
        return await PostSemanticExplicitInTransactionAsync(context, fact, new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> ReverseInventoryValuationInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, InventoryValuationReversalPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "inventory" || fact.FactType != AccountingFactTypes.InventoryValuationReversal
            || payload.OriginalFactId == Guid.Empty || payload.OriginalMovementId == Guid.Empty || payload.ReversalMovementId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.Reason))
            throw new LedgerDomainException("ledger.inventory_reversal_invalid", "The Inventory valuation reversal is missing its original or corrective movement identity.");
        if (!await IsRegisteredProducerAsync(context, fact, ct))
            throw new LedgerDomainException("ledger.fact_producer_unregistered", "Inventory is not registered for this accounting fact type.");

        var originalEntryId = await ScalarGuidAsync(context,
            "select posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and source_module='inventory' and status='Posted'",
            ct, fact.TenantId.Value, fact.LegalEntityId.Value, payload.OriginalFactId)
            ?? throw new LedgerDomainException("ledger.inventory_original_fact_missing", "The original Inventory valuation fact has no posted Ledger entry.");
        await using var original = new NpgsqlCommand("select journal_id,entry_number,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
        original.Parameters.AddWithValue(fact.TenantId.Value); original.Parameters.AddWithValue(fact.LegalEntityId.Value); original.Parameters.AddWithValue(originalEntryId);
        Guid journalId; string originalNumber; string currency; string baseCurrency; Guid ruleVersionId; Guid? configurationId;
        await using (var reader = await original.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) throw new LedgerDomainException("ledger.inventory_original_fact_missing", "The original Inventory Ledger entry no longer exists.");
            journalId = reader.GetGuid(0); originalNumber = reader.GetString(1); currency = reader.GetString(2); baseCurrency = reader.GetString(3);
            ruleVersionId = reader.GetGuid(4); configurationId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
        }
        if (await ScalarGuidAsync(context, "select reversal_entry_id from finance_ledger.journal_entry_reversal where tenant_id=$1 and legal_entity_id=$2 and original_entry_id=$3", ct,
            fact.TenantId.Value, fact.LegalEntityId.Value, originalEntryId) is not null)
            throw new LedgerDomainException("ledger.already_reversed", "The original Inventory valuation entry has already been reversed.");

        var now = clock.GetUtcNow();
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        await ExecuteAsync(context, "insert into finance_ledger.accounting_fact(tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Received',$13,$14,$15)", ct,
            fact.TenantId.Value, fact.LegalEntityId.Value, fact.FactId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference,
            fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, new NpgsqlParameter { Value = payloadJson, NpgsqlDbType = NpgsqlDbType.Jsonb },
            AccountingFactFingerprint.Compute(payload), fact.CorrelationId, fact.ActorId, now);
        var periodId = await ResolvePeriodAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, fact.PostingDate, ct);
        var reversalEntryId = Guid.CreateVersion7();
        var entryNumber = $"REV-{originalEntryId:N}";
        var sourceReference = $"inventory-reversal:{payload.ReversalMovementId:D}";
        await ExecuteAsync(context, "insert into finance_ledger.journal_entry(tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values($1,$2,$3,$4,$5,'inventory',$6,$7,$8,$9,'InventoryValuationReversal',1,$10,$11,$12,$13,$14,$15,$16,$17)", ct,
            fact.TenantId.Value, fact.LegalEntityId.Value, reversalEntryId, journalId, fact.FactId, sourceReference, entryNumber, fact.PostingDate,
            periodId, currency, baseCurrency, ruleVersionId, configurationId ?? (object)DBNull.Value,
            $"Inventory correction for {originalNumber}: {payload.Reason.Trim()}", fact.CorrelationId, fact.ActorId, now);

        var lines = new List<(int Sequence, Guid AccountId, string Currency, decimal Amount, decimal? Rate, string Role, string? Description,
            decimal TxDebit, decimal TxCredit, decimal BaseDebit, decimal BaseCredit, string? Snapshot, string? Reference)>();
        await using (var command = new NpgsqlCommand("select sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2 and journal_entry_id=$3 order by sequence_no", context.Connection, context.Transaction))
        {
            command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(originalEntryId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                lines.Add((reader.GetInt32(0), reader.GetGuid(1), reader.GetString(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? reader.GetDecimal(2) : reader.GetDecimal(9),
                    reader.IsDBNull(10) ? reader.GetDecimal(3) : reader.GetDecimal(10), reader.IsDBNull(11) ? reader.GetDecimal(2) : reader.GetDecimal(11),
                    reader.IsDBNull(12) ? reader.GetDecimal(3) : reader.GetDecimal(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetString(14)));
        }
        if (lines.Count == 0) throw new LedgerDomainException("ledger.inventory_original_fact_missing", "The original Inventory Ledger entry has no journal lines.");
        foreach (var line in lines)
            await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line(tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)", ct,
                fact.TenantId.Value, fact.LegalEntityId.Value, Guid.CreateVersion7(), reversalEntryId, line.Sequence, line.AccountId, line.BaseCredit, line.BaseDebit,
                line.Currency, line.Amount, (object?)line.Rate ?? DBNull.Value, line.Role, (object?)line.Description ?? DBNull.Value,
                line.TxCredit, line.TxDebit, line.BaseCredit, line.BaseDebit,
                new NpgsqlParameter { Value = (object?)line.Snapshot ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Jsonb }, (object?)line.Reference ?? DBNull.Value);

        await ExecuteAsync(context, "insert into finance_ledger.journal_entry_reversal(tenant_id,legal_entity_id,id,original_entry_id,reversal_entry_id,reason,actor_id,reversal_date,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", ct,
            fact.TenantId.Value, fact.LegalEntityId.Value, Guid.CreateVersion7(), originalEntryId, reversalEntryId, payload.Reason.Trim(), fact.ActorId, fact.PostingDate, now);
        await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", ct,
            fact.TenantId.Value, fact.FactId, reversalEntryId, now);
        await RefreshAccountPeriodBalancesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, reversalEntryId, ct);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryReversedV1,
            JsonSerializer.Serialize(new JournalEntryReversedEvent(originalEntryId, reversalEntryId, fact.LegalEntityId.Value, payload.Reason)),
            fact.TenantId, now, fact.CorrelationId, originalEntryId, "ledger"));
        return new LedgerPostingResult(fact.FactId, reversalEntryId, entryNumber, "Posted", false);
    }

    public async Task<string?> ValidateInventoryPostingDateInTransactionAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity,
        DateOnly postingDate, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select p.id from finance_ledger.accounting_period p where p.tenant_id=$1 and p.legal_entity_id=$2 and p.start_date <= $3 and p.end_date >= $3 and p.status='Open' and not exists (select 1 from finance_ledger.financial_lock l where l.tenant_id=p.tenant_id and l.legal_entity_id=p.legal_entity_id and l.status='Active' and l.start_date <= $3 and (l.end_date is null or l.end_date >= $3)) for update", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(postingDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var count = 0; while (await reader.ReadAsync(ct)) count++;
        if (count != 1) return null;
        return await financialRead.GetBaseCurrencyCodeAsync(tenant, entity, ct);
    }

    private async Task<LedgerPostingResult> PostExplicitCoreInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        ExplicitLedgerPostingPayload payload, Guid? configurationVersionOverride, IAccountingFactPayload? factPayloadOverride, CancellationToken cancellationToken)
    {
        if (payload.Lines.Count == 0) throw new LedgerDomainException("ledger.entry_unbalanced", "At least one journal line is required."); LedgerDomainRules.EnsureBalanced(payload.Lines.Sum(line => line.BaseDebit), payload.Lines.Sum(line => line.BaseCredit));
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, cancellationToken) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable."); if (!string.Equals(baseCurrency, payload.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.currency_mismatch", "The posting base currency does not match the legal entity.");
        var storedFactPayload = factPayloadOverride ?? payload;
        var payloadJson = JsonSerializer.Serialize(storedFactPayload, storedFactPayload.GetType(), JsonOptions); var payloadHash = AccountingFactFingerprint.Compute(storedFactPayload); var existing = await FindFactAsync(context, fact, cancellationToken); if (existing is not null) { if (!string.Equals(existing.Value.Hash, payloadHash, StringComparison.Ordinal)) throw new LedgerDomainException("ledger.fact_duplicate_conflict", "The source fact identity was already used with a different payload."); if (existing.Value.Entry is { } already) return new LedgerPostingResult(fact.FactId, already, existing.Value.EntryNumber!, "Posted", true); } else await ExecuteAsync(context, "insert into finance_ledger.accounting_fact (tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Received',$13,$14,$15)", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, fact.FactId, fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate, new NpgsqlParameter { Value = payloadJson, NpgsqlDbType = NpgsqlDbType.Jsonb }, payloadHash, fact.CorrelationId, fact.ActorId, clock.GetUtcNow());
        var period = await ResolvePeriodAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, fact.PostingDate, cancellationToken); var rule = await ResolveRuleAsync(context, fact.FactType, fact.FactVersion, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, cancellationToken); var config = configurationVersionOverride ?? await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, cancellationToken); if (configurationVersionOverride is not null && !await ConfigurationExistsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, config, cancellationToken)) throw new LedgerDomainException("ledger.configuration_missing", "The historical accounting configuration does not belong to this legal entity."); var entryId = Guid.CreateVersion7(); var entryNumber = $"JE-{fact.FactId:N}"; var now = clock.GetUtcNow(); await ExecuteAsync(context, "insert into finance_ledger.journal_entry (tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, payload.JournalId, fact.FactId, fact.SourceModule, fact.SourceReference, entryNumber, fact.PostingDate, period, fact.FactType, fact.FactVersion, payload.Lines[0].TransactionCurrencyCode, payload.BaseCurrencyCode, rule, config, payload.Description, fact.CorrelationId, fact.ActorId, now);
        await EnsureExplicitAccountsAsync(context, fact, payload, cancellationToken);
        var dimensionRules = await LoadDimensionRulesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, payload.Lines.Select(line => line.AccountId).Distinct().ToArray(), payload.JournalId, fact.FactType, cancellationToken);
        var sequence = 1; foreach (var line in payload.Lines) { if (line.Debit < 0 || line.Credit < 0 || line.Debit > 0 && line.Credit > 0 || line.BaseDebit < 0 || line.BaseCredit < 0 || line.BaseDebit > 0 && line.BaseCredit > 0) throw new LedgerDomainException("ledger.fact_invalid", "Journal lines contain invalid amounts."); var lineId = Guid.CreateVersion7(); var amount = line.TransactionDebit > 0 ? line.TransactionDebit : line.TransactionCredit; var snapshot = new NpgsqlParameter { Value = JsonSerializer.Serialize(new { rate = line.ExchangeRate, from = line.TransactionCurrencyCode, to = payload.BaseCurrencyCode }, JsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb }; await ExecuteAsync(context, "insert into finance_ledger.journal_entry_line (tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,exchange_rate,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate_snapshot,line_reference) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", cancellationToken, fact.TenantId.Value, fact.LegalEntityId.Value, lineId, entryId, sequence++, line.AccountId, line.BaseDebit, line.BaseCredit, line.TransactionCurrencyCode, amount, line.ExchangeRate, fact.FactType, line.Description ?? payload.Description, line.Description ?? fact.FactType, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, snapshot, line.LegacyReference); await ValidateAndPersistDimensionsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, lineId, line.Dimensions, dimensionRules.GetValueOrDefault(line.AccountId, []), cancellationToken); }
        await ExecuteAsync(context, "update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$3,posted_at=$4 where tenant_id=$1 and id=$2", cancellationToken, fact.TenantId.Value, fact.FactId, entryId, now); await RefreshAccountPeriodBalancesAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, entryId, cancellationToken); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.JournalEntryPostedV1, JsonSerializer.Serialize(new { fact.FactId, entryId, entryNumber, fact.FactType }, JsonOptions), fact.TenantId, now, fact.CorrelationId, fact.FactId, "ledger")); return new LedgerPostingResult(fact.FactId, entryId, entryNumber, "Posted", false);
    }

    public async Task<LedgerPostingResult> PostPaymentInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact,
        PaymentFactPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "payments" || fact.FactType is not (AccountingFactTypes.PaymentReceived or AccountingFactTypes.PaymentMade or AccountingFactTypes.RefundPaid)
            || payload.Amount <= 0m || string.IsNullOrWhiteSpace(payload.PaymentAccountReference)
            || payload.Direction is not ("Incoming" or "Outgoing") || payload.SettlementRole is not ("Receivable" or "Payable" or "PayrollPayable" or "EmployeeReimbursementPayable" or "EmployeeReceivable")
            || (fact.FactType == AccountingFactTypes.PaymentReceived) != (payload.Direction == "Incoming")
            || (fact.FactType == AccountingFactTypes.RefundPaid && (payload.Direction != "Outgoing" || payload.SettlementRole != "Receivable")))
            throw new LedgerDomainException("ledger.fact_invalid", "The payment fact does not contain valid semantic movement details.");
        if (!await IsRegisteredProducerAsync(context, fact, ct)) throw new LedgerDomainException("ledger.fact_producer_unregistered", "Payments is not registered for this accounting fact type.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var transactionCurrency = payload.CurrencyCode.Trim().ToUpperInvariant();
        var factBaseCurrency = payload.BaseCurrencyCode?.Trim().ToUpperInvariant() ?? transactionCurrency;
        var baseAmount = payload.BaseAmount ?? payload.Amount;
        var feeAmount = payload.FeeAmount ?? 0m; var baseFeeAmount = payload.BaseFeeAmount ?? 0m;
        if (transactionCurrency.Length != 3 || factBaseCurrency != baseCurrency || baseAmount <= 0m
            || feeAmount < 0m || baseFeeAmount < 0m || (feeAmount == 0m) != (baseFeeAmount == 0m)
            || payload.Direction == "Incoming" && (feeAmount >= payload.Amount || baseFeeAmount >= baseAmount)
            || (transactionCurrency == baseCurrency && baseAmount != payload.Amount)
            || (transactionCurrency != baseCurrency && (payload.BaseAmount is null || string.IsNullOrWhiteSpace(payload.ExchangeRateReference))))
            throw new LedgerDomainException("ledger.currency_mismatch", "The payment fact is missing a valid immutable base-currency snapshot.");
        var configuration = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        var cashAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "Cash", ct);
        var partyAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, payload.SettlementRole, ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = baseAmount / payload.Amount;
        var money = new LedgerPostingLinePayload(cashAccount, payload.Direction == "Incoming" ? baseAmount : 0m, payload.Direction == "Outgoing" ? baseAmount : 0m,
            transactionCurrency, payload.Direction == "Incoming" ? payload.Amount : 0m, payload.Direction == "Outgoing" ? payload.Amount : 0m,
            payload.Direction == "Incoming" ? baseAmount : 0m, payload.Direction == "Outgoing" ? baseAmount : 0m, rate, [], "Operational payment account movement", null);
        var party = new LedgerPostingLinePayload(partyAccount, payload.Direction == "Outgoing" ? baseAmount : 0m, payload.Direction == "Incoming" ? baseAmount : 0m,
            transactionCurrency, payload.Direction == "Outgoing" ? payload.Amount : 0m, payload.Direction == "Incoming" ? payload.Amount : 0m,
            payload.Direction == "Outgoing" ? baseAmount : 0m, payload.Direction == "Incoming" ? baseAmount : 0m, rate, [], "Party settlement movement", payload.PartyId?.ToString("D"));
        var description = payload.Direction == "Incoming" ? "Incoming payment received" : "Outgoing payment made";
        var lines = new List<LedgerPostingLinePayload> { money };
        lines.Add(party);
        var explicitPayload = new ExplicitLedgerPostingPayload(journal, description, baseCurrency, lines);
        return await PostExplicitInTransactionAsync(context, fact, explicitPayload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostPosCashSaleInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, PosCashSalePostingPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "pos" || fact.FactType != AccountingFactTypes.PosCashSalePosted || fact.FactVersion != 1
            || payload.NetAmount <= 0m || payload.TaxAmount < 0m || payload.GrossAmount != payload.NetAmount + payload.TaxAmount
            || payload.Lines.Count == 0 || payload.Lines.Any(line => string.IsNullOrWhiteSpace(line.CategoryCode) || line.NetAmount < 0m)
            || payload.Lines.Sum(line => line.NetAmount) != payload.NetAmount
            || payload.Taxes.Any(tax => string.IsNullOrWhiteSpace(tax.Code) || tax.Amount < 0m)
            || payload.Taxes.Sum(tax => tax.Amount) != payload.TaxAmount)
            throw new LedgerDomainException("ledger.fact_invalid", "The anonymous POS cash-sale amounts are inconsistent.");
        if (!await IsRegisteredProducerAsync(context, fact, ct))
            throw new LedgerDomainException("ledger.fact_producer_unregistered", "POS is not registered for anonymous cash-sale facts.");

        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var currency = payload.CurrencyCode.Trim().ToUpperInvariant();
        var rate = string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase)
            ? 1m
            : payload.ExchangeRate ?? throw new LedgerDomainException("ledger.currency_mismatch", "A foreign-currency anonymous cash sale requires an approved exchange-rate snapshot.");
        if (rate <= 0m || !string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(payload.ExchangeRateReference))
            throw new LedgerDomainException("ledger.currency_mismatch", "The anonymous cash sale is missing an immutable exchange-rate snapshot.");

        var postingFact = new AccountingFactEnvelope<CustomerInvoicePostedPayload>(fact.FactId, fact.TenantId, fact.LegalEntityId,
            fact.FactType, fact.FactVersion, fact.SourceModule, fact.SourceReference, fact.IdempotencyKey, fact.BusinessDate, fact.PostingDate,
            new(payload.NetAmount, payload.TaxAmount, payload.GrossAmount, currency, "AnonymousWalkIn", payload.Lines[0].CategoryCode, payload.Taxes,
                payload.ExchangeRate, payload.ExchangeRateReference), fact.CorrelationId, fact.ActorId);
        var configuration = await ResolveConfigurationAsync(context, postingFact, ct);
        var cash = await ResolveSemanticAccountIdAsync(context, fact, configuration, "Cash", ct);
        var revenueGroups = payload.Lines.GroupBy(line => line.CategoryCode, StringComparer.Ordinal)
            .Select(group => (CategoryCode: group.Key, NetAmount: group.Sum(line => line.NetAmount))).ToArray();
        var revenueAccounts = await ResolveInvoiceAccountsAsync(context, postingFact, configuration, "SalesRevenue",
            revenueGroups.Select(line => new InvoiceAccountLookup(line.CategoryCode, null)).ToArray(), ct);
        var taxGroups = payload.Taxes.GroupBy(tax => (Role: string.IsNullOrWhiteSpace(tax.AccountRole) ? "OutputTax" : tax.AccountRole, Code: tax.Code))
            .Select(group => (group.Key.Role, group.Key.Code, Amount: group.Sum(tax => tax.Amount))).ToArray();
        var taxAccounts = new Dictionary<(string Role, string Code), (Guid Id, AccountDeterminationExplanation Explanation)>();
        foreach (var taxGroup in taxGroups)
        {
            var resolved = await ResolveInvoiceAccountsAsync(context, postingFact, configuration, taxGroup.Role,
                [new InvoiceAccountLookup(null, taxGroup.Code)], ct);
            taxAccounts[(taxGroup.Role, taxGroup.Code)] = resolved[new InvoiceAccountLookup(null, taxGroup.Code)];
        }
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='SALES' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The Sales journal is not configured.");
        var lines = new List<LedgerPostingLinePayload>
        {
            new(cash, payload.GrossAmount * rate, 0m, currency, payload.GrossAmount, 0m, payload.GrossAmount * rate, 0m, rate, [], "Anonymous POS walk-in cash receipt", null)
        };
        foreach (var group in revenueGroups)
        {
            var account = revenueAccounts[new InvoiceAccountLookup(group.CategoryCode, null)].Id;
            lines.Add(new(account, 0m, group.NetAmount * rate, currency, 0m, group.NetAmount, 0m, group.NetAmount * rate, rate, [], "Anonymous POS sale revenue", null));
        }
        foreach (var taxGroup in taxGroups)
        {
            var account = taxAccounts[(taxGroup.Role, taxGroup.Code)].Id;
            lines.Add(new(account, 0m, taxGroup.Amount * rate, currency, 0m, taxGroup.Amount, 0m, taxGroup.Amount * rate, rate, [], "Anonymous POS output tax", null));
        }
        return await PostSemanticExplicitInTransactionAsync(context, fact,
            new ExplicitLedgerPostingPayload(journal, "Anonymous POS walk-in cash sale", baseCurrency, lines), payload, configuration, ct);
    }

    public async Task<LedgerPostingResult> PostPaymentFeeInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, PaymentFeeFactPayload payload, CancellationToken ct)
    {
        string[] feeTypes = ["BankFee", "ProviderFee", "CardProcessingFee", "FXFee", "TransferFee", "OtherApprovedFee"];
        if (fact.SourceModule != "payments" || fact.FactType != AccountingFactTypes.PaymentFeeRecognized
            || payload.Amount <= 0m || payload.BaseAmount <= 0m || string.IsNullOrWhiteSpace(payload.PaymentAccountReference)
            || payload.CurrencyCode.Trim().Length != 3 || payload.BaseCurrencyCode.Trim().Length != 3 || !feeTypes.Contains(payload.FeeType, StringComparer.Ordinal))
            throw new LedgerDomainException("ledger.fact_invalid", "The payment fee fact does not contain valid semantic fee details.");
        if (!await IsRegisteredProducerAsync(context, fact, ct)) throw new LedgerDomainException("ledger.fact_producer_unregistered", "Payments is not registered for this accounting fact type.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        if (!string.Equals(payload.BaseCurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase)
            || payload.CurrencyCode.Trim().Equals(baseCurrency, StringComparison.OrdinalIgnoreCase) && payload.Amount != payload.BaseAmount
            || !payload.CurrencyCode.Trim().Equals(baseCurrency, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(payload.ExchangeRateReference))
            throw new LedgerDomainException("ledger.currency_mismatch", "The payment fee fact is missing a valid immutable base-currency snapshot.");
        var configuration = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        var cashAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "Cash", ct);
        var expenseAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "PaymentFeeExpense", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var rate = payload.BaseAmount / payload.Amount;
        var lines = new[]
        {
            new LedgerPostingLinePayload(expenseAccount, payload.BaseAmount, 0m, payload.CurrencyCode, payload.Amount, 0m, payload.BaseAmount, 0m, rate, [], $"Recognized {payload.FeeType}", null),
            new LedgerPostingLinePayload(cashAccount, 0m, payload.BaseAmount, payload.CurrencyCode, 0m, payload.Amount, 0m, payload.BaseAmount, rate, [], "Payment account fee settlement", payload.PaymentAccountReference)
        };
        return await PostExplicitInTransactionAsync(context, fact,
            new ExplicitLedgerPostingPayload(journal, $"Payment fee recognized: {payload.FeeType}", baseCurrency, lines), configuration, ct);
    }

    public async Task<LedgerPostingResult> PostPaymentFeeReversalInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, Guid originalFeeFactId, string evidenceReference, CancellationToken ct)
    {
        if (fact.SourceModule != "payments" || fact.FactType != AccountingFactTypes.PaymentFeeReversed
            || originalFeeFactId == Guid.Empty || string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference.Trim().Length > 500)
            throw new LedgerDomainException("ledger.fact_invalid", "The payment fee correction requires its original fee and external evidence.");
        if (fact.Payload is not PaymentFeeReversalRequestPayload request
            || request.OriginalFeeFactId != originalFeeFactId
            || string.IsNullOrWhiteSpace(request.EvidenceReference)
            || !string.Equals(request.EvidenceReference.Trim(), evidenceReference.Trim(), StringComparison.Ordinal))
            throw new LedgerDomainException("ledger.fact_invalid", "The payment fee correction request does not match its source fee evidence.");
        if (!await IsRegisteredProducerAsync(context, fact, ct))
            throw new LedgerDomainException("ledger.fact_producer_unregistered", "Payments is not registered for this fee correction fact.");

        Guid configurationId;
        Guid journalId;
        string baseCurrency;
        string currency;
        decimal amount;
        decimal baseAmount;
        string feeType;
        string paymentAccountReference;
        Guid originalExpenseAccountId;
        Guid originalCashAccountId;
        await using (var original = new NpgsqlCommand("""
            select fact.source_module, fact.fact_type, fact.status, entry.accounting_configuration_version_id,
                   entry.journal_id, entry.base_currency_code,
                   expense.transaction_debit, expense.base_debit, expense.currency_code,
                   expense.description, cash.line_reference, cash.transaction_credit, cash.base_credit,
                   cash.currency_code, expense.account_id, cash.account_id
            from finance_ledger.accounting_fact fact
            join finance_ledger.journal_entry entry on entry.tenant_id=fact.tenant_id and entry.legal_entity_id=fact.legal_entity_id and entry.fact_id=fact.id
            join finance_ledger.journal_entry_line expense on expense.tenant_id=entry.tenant_id and expense.legal_entity_id=entry.legal_entity_id and expense.journal_entry_id=entry.id and expense.transaction_debit>0
            join finance_ledger.journal_entry_line cash on cash.tenant_id=entry.tenant_id and cash.legal_entity_id=entry.legal_entity_id and cash.journal_entry_id=entry.id and cash.transaction_credit>0
            where fact.tenant_id=$1 and fact.legal_entity_id=$2 and fact.id=$3
            """, context.Connection, context.Transaction))
        {
            original.Parameters.AddWithValue(fact.TenantId.Value);
            original.Parameters.AddWithValue(fact.LegalEntityId.Value);
            original.Parameters.AddWithValue(originalFeeFactId);
            await using var reader = await original.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != "payments"
                || reader.GetString(1) != AccountingFactTypes.PaymentFeeRecognized || reader.GetString(2) != "Posted")
                throw new LedgerDomainException("ledger.payment_fee_original_missing", "The original recognized fee fact is not posted.");
            configurationId = reader.GetGuid(3); journalId = reader.GetGuid(4); baseCurrency = reader.GetString(5).Trim();
            amount = reader.GetDecimal(6); baseAmount = reader.GetDecimal(7); currency = reader.GetString(8).Trim();
            feeType = reader.GetString(9).Replace("Recognized ", string.Empty, StringComparison.Ordinal);
            paymentAccountReference = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
            if (reader.GetDecimal(11) != amount || reader.GetDecimal(12) != baseAmount
                || !string.Equals(reader.GetString(13).Trim(), currency, StringComparison.OrdinalIgnoreCase))
                throw new LedgerDomainException("ledger.payment_fee_original_invalid", "The original fee journal lines do not balance to the same immutable amount.");
            originalExpenseAccountId = reader.GetGuid(14); originalCashAccountId = reader.GetGuid(15);
        }
        if (amount <= 0m || baseAmount <= 0m || string.IsNullOrWhiteSpace(paymentAccountReference))
            throw new LedgerDomainException("ledger.payment_fee_original_invalid", "The original fee journal does not contain a reversible payment fee.");
        string[] feeTypes = ["BankFee", "ProviderFee", "CardProcessingFee", "FXFee", "TransferFee", "OtherApprovedFee"];
        if (!feeTypes.Contains(feeType, StringComparer.Ordinal))
            throw new LedgerDomainException("ledger.payment_fee_original_invalid", "The original fee journal does not contain a supported fee type.");

        var cashAccount = await ResolveSemanticAccountIdAsync(context, fact, configurationId, "Cash", ct);
        var expenseAccount = await ResolveSemanticAccountIdAsync(context, fact, configurationId, "PaymentFeeExpense", ct);
        if (cashAccount != originalCashAccountId || expenseAccount != originalExpenseAccountId)
            throw new LedgerDomainException("ledger.payment_fee_original_invalid", "The original fee was not posted to its historical semantic accounts.");
        var rate = baseAmount / amount;
        var lines = new[]
        {
            new LedgerPostingLinePayload(cashAccount, baseAmount, 0m, currency, amount, 0m, baseAmount, 0m, rate, [],
                $"Reversed fee {originalFeeFactId:D}; evidence {evidenceReference.Trim()}", paymentAccountReference),
            new LedgerPostingLinePayload(expenseAccount, 0m, baseAmount, currency, 0m, amount, 0m, baseAmount, rate, [],
                $"Corrects recognized {feeType}", originalFeeFactId.ToString("D"))
        };
        var semanticPayload = new PaymentFeeReversalFactPayload(originalFeeFactId, amount, currency, baseAmount,
            baseCurrency, paymentAccountReference, feeType, evidenceReference.Trim());
        var semanticFact = fact with { Payload = semanticPayload };
        return await PostSemanticExplicitInTransactionAsync(context, semanticFact,
            new ExplicitLedgerPostingPayload(journalId, $"Payment fee reversed: {feeType}; evidence {evidenceReference.Trim()}", baseCurrency, lines),
            semanticPayload, configurationId, ct);
    }


    public async Task<LedgerPostingResult> PostInternalTransferInTransactionAsync(ITransactionContext context,
        AccountingFactEnvelope<IAccountingFactPayload> fact, InternalTransferFactPayload payload, CancellationToken ct)
    {
        if (fact.SourceModule != "payments" || fact.FactType != AccountingFactTypes.InternalTransferCompleted || payload.SourceAmount <= 0m
            || payload.DestinationAmount <= 0m || payload.SourceBaseAmount <= 0m || payload.DestinationBaseAmount <= 0m
            || string.IsNullOrWhiteSpace(payload.SourcePaymentAccountReference)
            || string.IsNullOrWhiteSpace(payload.DestinationPaymentAccountReference)
            || payload.SourcePaymentAccountReference == payload.DestinationPaymentAccountReference
            || payload.SourceCurrencyCode.Length != 3 || payload.DestinationCurrencyCode.Length != 3)
            throw new LedgerDomainException("ledger.fact_invalid", "The internal transfer fact does not contain valid account movement details.");
        if (!await IsRegisteredProducerAsync(context, fact, ct)) throw new LedgerDomainException("ledger.fact_producer_unregistered", "Payments is not registered for this accounting fact type.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        var sourceCurrency = payload.SourceCurrencyCode.Trim().ToUpperInvariant();
        var destinationCurrency = payload.DestinationCurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(payload.BaseCurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase)
            || sourceCurrency == baseCurrency && payload.SourceBaseAmount != payload.SourceAmount
            || destinationCurrency == baseCurrency && payload.DestinationBaseAmount != payload.DestinationAmount
            || sourceCurrency != baseCurrency && string.IsNullOrWhiteSpace(payload.SourceExchangeRateReference)
            || destinationCurrency != baseCurrency && string.IsNullOrWhiteSpace(payload.DestinationExchangeRateReference)
            || sourceCurrency != baseCurrency && string.IsNullOrWhiteSpace(payload.SourceExchangeRateSnapshot)
            || destinationCurrency != baseCurrency && string.IsNullOrWhiteSpace(payload.DestinationExchangeRateSnapshot)
            || sourceCurrency == destinationCurrency && (payload.SourceAmount != payload.DestinationAmount || payload.SourceBaseAmount != payload.DestinationBaseAmount))
            throw new LedgerDomainException("ledger.currency_mismatch", "The internal transfer fact has an invalid immutable base-currency snapshot.");
        try
        {
            if (payload.SourceExchangeRateSnapshot is { } sourceSnapshot) using (JsonDocument.Parse(sourceSnapshot)) { }
            if (payload.DestinationExchangeRateSnapshot is { } destinationSnapshot) using (JsonDocument.Parse(destinationSnapshot)) { }
        }
        catch (JsonException) { throw new LedgerDomainException("ledger.currency_mismatch", "The internal transfer exchange-rate snapshot is invalid."); }
        var configuration = await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        var cashAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "Cash", ct);
        var journal = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The General journal is not configured.");
        var sourceRate = payload.SourceBaseAmount / payload.SourceAmount;
        var destinationRate = payload.DestinationBaseAmount / payload.DestinationAmount;
        var lines = new List<LedgerPostingLinePayload>
        {
            new(cashAccount, payload.DestinationBaseAmount, 0m, destinationCurrency, payload.DestinationAmount, 0m, payload.DestinationBaseAmount, 0m, destinationRate, [], "Internal transfer destination", payload.DestinationPaymentAccountReference),
            new(cashAccount, 0m, payload.SourceBaseAmount, sourceCurrency, 0m, payload.SourceAmount, 0m, payload.SourceBaseAmount, sourceRate, [], "Internal transfer source", payload.SourcePaymentAccountReference)
        };
        var fxDifference = payload.DestinationBaseAmount - payload.SourceBaseAmount;
        if (fxDifference > 0m)
        {
            var gainAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FXGain", ct);
            lines.Add(new(gainAccount, 0m, fxDifference, baseCurrency, 0m, fxDifference, 0m, fxDifference, 1m, [], "Internal transfer exchange gain", null));
        }
        else if (fxDifference < 0m)
        {
            var lossAccount = await ResolveSemanticAccountIdAsync(context, fact, configuration, "FXLoss", ct);
            var loss = decimal.Abs(fxDifference);
            lines.Add(new(lossAccount, loss, 0m, baseCurrency, loss, 0m, loss, 0m, 1m, [], "Internal transfer exchange loss", null));
        }
        return await PostExplicitInTransactionAsync(context, fact,
            new ExplicitLedgerPostingPayload(journal, "Internal transfer between operational payment accounts", baseCurrency, lines), configuration, ct);
    }

    public async Task<LedgerPostingResult> PostSettlementFxDifferenceInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementFxDifferencePayload payload, CancellationToken ct)
    {
        if (fact.FactType != AccountingFactTypes.SettlementFxDifference || fact.SourceModule != "subledger" || payload.Amount <= 0m
            || payload.DifferenceRole is not ("FXGain" or "FXLoss") || payload.ControlAccountRole is not ("Receivable" or "Payable") || payload.ApplicationId == Guid.Empty)
            throw new LedgerDomainException("ledger.fact_invalid", "The settlement FX fact has invalid semantic details.");
        if (!await IsRegisteredProducerAsync(context, fact, ct)) throw new LedgerDomainException("ledger.fact_producer_unregistered", "The producer is not registered for this accounting fact type and version.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        if (!string.Equals(baseCurrency, payload.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.currency_mismatch", "Settlement FX differences must be stated in the legal entity base currency.");
        var journalId = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='FX' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The foreign-exchange journal is not configured.");
        var configurationId = payload.HistoricalConfigurationVersionId ?? await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        if (!await ConfigurationExistsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, configurationId, ct)) throw new LedgerDomainException("ledger.configuration_missing", "The historical accounting configuration does not belong to this legal entity.");
        var controlId = await ResolveSemanticAccountIdAsync(context, fact, configurationId, payload.ControlAccountRole, ct);
        var differenceId = await ResolveSemanticAccountIdAsync(context, fact, configurationId, payload.DifferenceRole, ct);
        var amount = payload.Amount;
        var lines = payload.DifferenceRole == "FXLoss"
            ? new[]
            {
                new LedgerPostingLinePayload(differenceId, amount, 0m, baseCurrency, amount, 0m, amount, 0m, 1m, [], "Settlement FX loss", null),
                new LedgerPostingLinePayload(controlId, 0m, amount, baseCurrency, 0m, amount, 0m, amount, 1m, [], "Subledger control settlement", null)
            }
            : new[]
            {
                new LedgerPostingLinePayload(controlId, amount, 0m, baseCurrency, amount, 0m, amount, 0m, 1m, [], "Subledger control settlement", null),
                new LedgerPostingLinePayload(differenceId, 0m, amount, baseCurrency, 0m, amount, 0m, amount, 1m, [], "Settlement FX gain", null)
            };
        var payloadForPosting = new ExplicitLedgerPostingPayload(journalId, $"Settlement FX difference for application {payload.ApplicationId:N}", baseCurrency, lines);
        return await PostExplicitInTransactionAsync(context, fact, payloadForPosting, configurationId, ct);
    }

    public async Task<LedgerPostingResult> PostUnrealizedFxRevaluationAsync(AccountingFactEnvelope<IAccountingFactPayload> fact, UnrealizedFxRevaluationPayload payload, CancellationToken cancellationToken = default)
    {
        if (fact.FactType != AccountingFactTypes.UnrealizedFxRevaluation || fact.SourceModule != "accounting" || payload.RevaluationRunId == Guid.Empty || payload.OpenItemId == Guid.Empty
            || payload.Direction is not ("Receivable" or "Payable") || payload.ControlAccountRole is not ("Receivable" or "Payable")
            || payload.Direction != payload.ControlAccountRole || payload.DifferenceRole is not ("FXGain" or "FXLoss") || payload.DifferenceAmount == 0m
            || payload.OpenTransactionAmount <= 0m || payload.CarryingBaseAmount < 0m || payload.RevaluedBaseAmount < 0m || payload.ExchangeRate <= 0m
            || string.IsNullOrWhiteSpace(payload.ExchangeRateSnapshot))
            throw new LedgerDomainException("ledger.fact_invalid", "The unrealized FX revaluation fact has invalid semantic details.");

        return await transactions.ExecuteAsync(fact.TenantId, async (context, ct) =>
        {
            if (!await IsRegisteredProducerAsync(context, fact, ct))
                throw new LedgerDomainException("ledger.fact_producer_unregistered", "Accounting is not registered for unrealized FX revaluation facts.");
            var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
                ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
            if (!string.Equals(baseCurrency, payload.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new LedgerDomainException("ledger.currency_mismatch", "FX revaluation must use the legal entity base currency.");
            try { using var _ = JsonDocument.Parse(payload.ExchangeRateSnapshot); }
            catch (JsonException) { throw new LedgerDomainException("ledger.currency_mismatch", "FX revaluation requires a valid immutable exchange-rate snapshot."); }

            var configurationId = payload.HistoricalConfigurationVersionId ?? await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
            if (!await ConfigurationExistsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, configurationId, ct))
                throw new LedgerDomainException("ledger.configuration_missing", "The accounting configuration does not belong to this legal entity.");
            var journalId = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='FX' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
                ?? throw new LedgerDomainException("ledger.journal_missing", "The foreign-exchange journal is not configured.");
            var controlId = await ResolveSemanticAccountIdAsync(context, fact, configurationId, payload.ControlAccountRole, ct);
            var differenceId = await ResolveSemanticAccountIdAsync(context, fact, configurationId, payload.DifferenceRole, ct);
            var amount = decimal.Abs(payload.DifferenceAmount);
            var lines = payload.DifferenceRole == "FXLoss"
                ? new[]
                {
                    new LedgerPostingLinePayload(differenceId, amount, 0m, baseCurrency, amount, 0m, amount, 0m, 1m, [], "Unrealized FX loss", payload.OpenItemId.ToString("D")),
                    new LedgerPostingLinePayload(controlId, 0m, amount, baseCurrency, 0m, amount, 0m, amount, 1m, [], "Subledger control remeasurement", payload.OpenItemId.ToString("D"))
                }
                : new[]
                {
                    new LedgerPostingLinePayload(controlId, amount, 0m, baseCurrency, amount, 0m, amount, 0m, 1m, [], "Subledger control remeasurement", payload.OpenItemId.ToString("D")),
                    new LedgerPostingLinePayload(differenceId, 0m, amount, baseCurrency, 0m, amount, 0m, amount, 1m, [], "Unrealized FX gain", payload.OpenItemId.ToString("D"))
                };
            var semanticFact = fact with { Payload = payload };
            return await PostSemanticExplicitInTransactionAsync(context, semanticFact,
                new ExplicitLedgerPostingPayload(journalId, $"Unrealized FX revaluation for open item {payload.OpenItemId:N}", baseCurrency, lines), payload, configurationId, ct);
        }, cancellationToken);
    }

    public async Task<LedgerPostingResult> PostSettlementWriteOffInTransactionAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, SettlementWriteOffPayload payload, CancellationToken ct)
    {
        if (fact.FactType != AccountingFactTypes.SettlementWriteOff || fact.SourceModule is not ("subledger" or "expenses") || payload.WriteOffId == Guid.Empty
            || payload.PartyId == Guid.Empty || payload.OpenItemId == Guid.Empty || payload.Direction is not ("Receivable" or "Payable")
            || payload.BaseAmount <= 0m || string.IsNullOrWhiteSpace(payload.ReasonCode) || string.IsNullOrWhiteSpace(payload.ReasonText))
            throw new LedgerDomainException("ledger.fact_invalid", "The settlement write-off fact has invalid semantic details.");
        if (!await IsRegisteredProducerAsync(context, fact, ct)) throw new LedgerDomainException("ledger.fact_producer_unregistered", "The producer is not registered for this accounting fact type and version.");
        var baseCurrency = await financialRead.GetBaseCurrencyCodeAsync(fact.TenantId, fact.LegalEntityId, ct)
            ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable.");
        if (!string.Equals(baseCurrency, payload.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)) throw new LedgerDomainException("ledger.currency_mismatch", "Write-off carrying amounts must use the legal entity base currency.");
        var journalId = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='ADJUSTMENT' and status='Active'", ct, fact.TenantId.Value, fact.LegalEntityId.Value)
            ?? throw new LedgerDomainException("ledger.journal_missing", "The adjustment journal is not configured.");
        var controlRole = payload.Direction == "Receivable" ? "Receivable" : "Payable";
        var offsetRole = payload.Direction == "Receivable" ? "BadDebtExpense" : "WriteOffIncome";
        var configurationId = payload.HistoricalConfigurationVersionId ?? await ResolveConfigurationAsync(context, fact.PostingDate, fact.TenantId.Value, fact.LegalEntityId.Value, ct);
        if (!await ConfigurationExistsAsync(context, fact.TenantId.Value, fact.LegalEntityId.Value, configurationId, ct)) throw new LedgerDomainException("ledger.configuration_missing", "The historical accounting configuration does not belong to this legal entity.");
        var controlId = await ResolveSemanticAccountIdAsync(context, fact, configurationId, controlRole, ct);
        var offsetId = await ResolveSemanticAccountIdAsync(context, fact, configurationId, offsetRole, ct);
        var amount = payload.BaseAmount;
        var lines = payload.Direction == "Receivable"
            ? new[]
            {
                new LedgerPostingLinePayload(offsetId, amount, 0m, baseCurrency, amount, 0m, amount, 0m, 1m, [], $"AR write-off {payload.ReasonCode}", payload.OpenItemId.ToString("D")),
                new LedgerPostingLinePayload(controlId, 0m, amount, baseCurrency, 0m, amount, 0m, amount, 1m, [], "Subledger receivable control", payload.OpenItemId.ToString("D"))
            }
            : new[]
            {
                new LedgerPostingLinePayload(controlId, amount, 0m, baseCurrency, amount, 0m, amount, 0m, 1m, [], "Subledger payable control", payload.OpenItemId.ToString("D")),
                new LedgerPostingLinePayload(offsetId, 0m, amount, baseCurrency, 0m, amount, 0m, amount, 1m, [], $"AP write-off {payload.ReasonCode}", payload.OpenItemId.ToString("D"))
            };
        var posting = new ExplicitLedgerPostingPayload(journalId, $"Settlement write-off {payload.WriteOffId:N}: {payload.ReasonText}", baseCurrency, lines);
        return await PostExplicitInTransactionAsync(context, fact, posting, configurationId, ct);
    }

    private static async Task<Guid> ResolveSemanticAccountIdAsync(ITransactionContext context, AccountingFactEnvelope<IAccountingFactPayload> fact, Guid configurationId, string role, CancellationToken ct)
    {
        var aliases = role switch
        {
            "Receivable" => new[] { "Receivable", "AccountsReceivableControl" },
            "Payable" => new[] { "Payable", "AccountsPayableControl" },
            _ => new[] { role }
        };
        await using var command = new NpgsqlCommand("with candidates as (select v.target_account_id,v.specificity_rank,(r.code=$3) as exact_role,count(*) over(partition by (r.code=$3),v.specificity_rank) as rank_count from finance_ledger.account_role r join finance_ledger.account_mapping m on m.tenant_id=r.tenant_id and m.legal_entity_id=r.legal_entity_id and m.role_id=r.id join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id where r.tenant_id=$1 and r.legal_entity_id=$2 and v.accounting_configuration_version_id=$6 and r.code=any($4) and r.status='Active' and m.status='Active' and v.status='Active' and a.status='Active' and a.allow_direct_posting=true and v.effective_from <= $5 and (v.effective_to is null or v.effective_to >= $5)), best_role as (select coalesce(bool_or(exact_role),false) as exact_role from candidates) select target_account_id,rank_count from candidates where exact_role=(select exact_role from best_role) and specificity_rank=(select max(specificity_rank) from candidates where exact_role=(select exact_role from best_role))", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(role); command.Parameters.AddWithValue(aliases); command.Parameters.AddWithValue(fact.PostingDate); command.Parameters.AddWithValue(configurationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var matches = new List<(Guid AccountId, int Count)>();
        while (await reader.ReadAsync(ct)) matches.Add((reader.GetGuid(0), reader.GetInt32(1)));
        if (matches.Count == 0) throw new LedgerDomainException("ledger.account_mapping_missing", $"No active account mapping exists for semantic role {role}.");
        if (matches.Any(match => match.Count > 1) || matches.Select(match => match.AccountId).Distinct().Count() != 1) throw new LedgerDomainException("ledger.account_mapping_conflict", $"More than one account mapping matches semantic role {role}.");
        return matches[0].AccountId;
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

    private static async Task<bool> ConfigurationExistsAsync(ITransactionContext context, Guid tenantId, Guid entityId, Guid configurationId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status in ('Active','Retired'))", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(configurationId);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
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
        await using var command = new NpgsqlCommand("select p.id from finance_ledger.accounting_period p where p.tenant_id=$1 and p.legal_entity_id=$2 and p.start_date <= $3 and p.end_date >= $3 and p.status='Open' and not exists (select 1 from finance_ledger.financial_lock l where l.tenant_id=p.tenant_id and l.legal_entity_id=p.legal_entity_id and l.status='Active' and l.start_date <= $3 and (l.end_date is null or l.end_date >= $3)) for update", context.Connection, context.Transaction);
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
        await using var command = new NpgsqlCommand("select c.active_configuration_version_id from finance_ledger.ledger_entity_configuration c join finance_ledger.accounting_configuration_version v on v.tenant_id=c.tenant_id and v.legal_entity_id=c.legal_entity_id and v.id=c.active_configuration_version_id and v.status='Active' where c.tenant_id=$1 and c.legal_entity_id=$2 for share of c", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.configuration_missing", "No active accounting configuration exists for this legal entity.");
    }

    private sealed record InvoiceAccountLookup(string? CategoryCode, string? TaxCode);

    private static async Task<Dictionary<InvoiceAccountLookup, (Guid Id, AccountDeterminationExplanation Explanation)>> ResolveInvoiceAccountsAsync(
        ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, Guid configurationId, string role,
        IReadOnlyList<InvoiceAccountLookup> lookups, CancellationToken ct)
    {
        if (lookups.Count == 0) return [];
        const string sql = "with requested as (select * from unnest($5::text[],$6::text[]) as k(category_code,tax_code)), candidates as (select k.category_code,k.tax_code,m.id as mapping_id,v.id as mapping_version_id,v.target_account_id,v.specificity_rank,count(*) over(partition by k.category_code,k.tax_code,v.specificity_rank) as rank_count,max(v.specificity_rank) over(partition by k.category_code,k.tax_code) as max_rank,m.code,v.version_number,a.code as account_code from requested k join finance_ledger.account_mapping m on m.tenant_id=$1 and m.legal_entity_id=$2 join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id and r.code=$3 join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id where v.accounting_configuration_version_id=$4 and m.status='Active' and v.status='Active' and a.status='Active' and a.allow_direct_posting=true and v.effective_from <= $7 and (v.effective_to is null or v.effective_to >= $7) and (v.product_category_code is null or v.product_category_code=k.category_code) and v.party_group_code is null and (v.tax_code is null or v.tax_code=k.tax_code)) select category_code,tax_code,mapping_id,mapping_version_id,target_account_id,rank_count,code,version_number,account_code,specificity_rank from candidates where specificity_rank=max_rank order by category_code,tax_code";
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value);
        command.Parameters.AddWithValue(fact.LegalEntityId.Value);
        command.Parameters.AddWithValue(role);
        command.Parameters.AddWithValue(configurationId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, Value = lookups.Select(item => item.CategoryCode).ToArray() });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, Value = lookups.Select(item => item.TaxCode).ToArray() });
        command.Parameters.AddWithValue(fact.PostingDate);
        var candidates = new Dictionary<InvoiceAccountLookup, List<(Guid MappingId, Guid MappingVersionId, Guid Id, int Count, string Mapping, long Version, string AccountCode, int Rank)>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var lookup = new InvoiceAccountLookup(reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
            if (!candidates.TryGetValue(lookup, out var rows)) candidates[lookup] = rows = [];
            rows.Add((reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetInt32(5), reader.GetString(6), reader.GetInt64(7), reader.GetString(8), reader.GetInt32(9)));
        }
        var result = new Dictionary<InvoiceAccountLookup, (Guid Id, AccountDeterminationExplanation Explanation)>();
        foreach (var lookup in lookups.Distinct())
        {
            if (!candidates.TryGetValue(lookup, out var rows) || rows.Count == 0)
                throw new LedgerDomainException("ledger.account_mapping_missing", $"No active account mapping exists for role {role}.");
            if (rows.Any(row => row.Count > 1) || rows.Select(row => row.Id).Distinct().Count() != 1)
                throw new LedgerDomainException("ledger.account_mapping_conflict", $"More than one account mapping matches role {role}.");
            var selected = rows[0];
            result.Add(lookup, (selected.Id, new AccountDeterminationExplanation(role, selected.Mapping, selected.Version, selected.MappingId,
                selected.MappingVersionId, selected.AccountCode, selected.Id, fact.LegalEntityId.Value, selected.Rank,
                $"highest matching specificity rank {selected.Rank}; category={lookup.CategoryCode ?? "*"}; partyGroup=*; taxCode={lookup.TaxCode ?? "*"}")));
        }
        return result;
    }

    private static async Task<(Guid Id, AccountDeterminationExplanation Explanation)> ResolveAccountAsync(ITransactionContext context, AccountingFactEnvelope<CustomerInvoicePostedPayload> fact, Guid configurationId, string role, string? category, string? partyGroup, string? taxCode, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("with candidates as (select m.id as mapping_id,v.id as mapping_version_id,v.target_account_id,v.specificity_rank,count(*) over (partition by v.specificity_rank) as rank_count,max(v.specificity_rank) over () as max_rank,m.code,v.version_number,a.code as account_code from finance_ledger.account_mapping m join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id join finance_ledger.account a on a.tenant_id=v.tenant_id and a.legal_entity_id=v.legal_entity_id and a.id=v.target_account_id where m.tenant_id=$1 and m.legal_entity_id=$2 and r.code=$3 and v.accounting_configuration_version_id=$8 and m.status='Active' and v.status='Active' and a.status='Active' and a.allow_direct_posting=true and v.effective_from <= $4 and (v.effective_to is null or v.effective_to >= $4) and (v.product_category_code is null or v.product_category_code=$5) and (v.party_group_code is null or v.party_group_code=$6) and (v.tax_code is null or v.tax_code=$7)) select mapping_id,mapping_version_id,target_account_id,rank_count,code,version_number,account_code,specificity_rank from candidates where specificity_rank=max_rank", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(fact.TenantId.Value); command.Parameters.AddWithValue(fact.LegalEntityId.Value); command.Parameters.AddWithValue(role); command.Parameters.AddWithValue(fact.PostingDate); command.Parameters.AddWithValue((object?)category ?? DBNull.Value); command.Parameters.AddWithValue((object?)partyGroup ?? DBNull.Value); command.Parameters.AddWithValue((object?)taxCode ?? DBNull.Value); command.Parameters.AddWithValue(configurationId);
        await using var reader = await command.ExecuteReaderAsync(ct); var accounts = new List<(Guid MappingId, Guid MappingVersionId, Guid Id, int Count, string Mapping, long Version, string AccountCode, int Rank)>(); while (await reader.ReadAsync(ct)) accounts.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt32(3), reader.GetString(4), reader.GetInt64(5), reader.GetString(6), reader.GetInt32(7))); if (accounts.Count == 0) throw new LedgerDomainException("ledger.account_mapping_missing", $"No active account mapping exists for role {role}."); if (accounts.Any(x => x.Count > 1) || accounts.Select(x => x.Id).Distinct().Count() != 1) throw new LedgerDomainException("ledger.account_mapping_conflict", $"More than one account mapping matches role {role}."); var selected = accounts[0]; return (selected.Id, new AccountDeterminationExplanation(role, selected.Mapping, selected.Version, selected.MappingId, selected.MappingVersionId, selected.AccountCode, selected.Id, fact.LegalEntityId.Value, selected.Rank, $"highest matching specificity rank {selected.Rank}; category={category ?? "*"}; partyGroup={partyGroup ?? "*"}; taxCode={taxCode ?? "*"}"));
    }

    private static async Task<Guid> ResolvePeriodAsync(ITransactionContext context, Guid tenantId, Guid entityId, DateOnly postingDate, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select p.id from finance_ledger.accounting_period p where p.tenant_id=$1 and p.legal_entity_id=$2 and p.start_date <= $3 and p.end_date >= $3 and p.status='Open' and not exists (select 1 from finance_ledger.financial_lock l where l.tenant_id=p.tenant_id and l.legal_entity_id=p.legal_entity_id and l.status='Active' and l.start_date <= $3 and (l.end_date is null or l.end_date >= $3)) for update", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(postingDate); var ids = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0)); if (ids.Count == 0) throw new LedgerDomainException("ledger.period_locked", "Posting date has no open, unlocked accounting period."); if (ids.Count > 1) throw new LedgerDomainException("ledger.period_ambiguous", "Posting date resolves to more than one accounting period."); return ids[0];
    }

    private static async Task<Guid> ResolveRuleAsync(ITransactionContext context, string factType, int factVersion, DateOnly postingDate, Guid tenantId, Guid entityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select v.id from finance_ledger.posting_rule r join finance_ledger.posting_rule_version v on v.tenant_id=r.tenant_id and v.legal_entity_id=r.legal_entity_id and v.posting_rule_id=r.id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.fact_type=$3 and r.fact_version=$4 and r.status='Active' and v.status='Active' and v.effective_from <= $5 and (v.effective_to is null or v.effective_to >= $5) order by v.version_number desc limit 1", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(factType); command.Parameters.AddWithValue(factVersion); command.Parameters.AddWithValue(postingDate); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.posting_rule_missing", "No active posting rule matches this accounting fact.");
    }

    private static async Task<Guid> ResolveConfigurationAsync(ITransactionContext context, DateOnly postingDate, Guid tenantId, Guid entityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select c.active_configuration_version_id from finance_ledger.ledger_entity_configuration c join finance_ledger.accounting_configuration_version v on v.tenant_id=c.tenant_id and v.legal_entity_id=c.legal_entity_id and v.id=c.active_configuration_version_id and v.status='Active' where c.tenant_id=$1 and c.legal_entity_id=$2 for share of c", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : throw new LedgerDomainException("ledger.configuration_missing", "No active accounting configuration exists for this legal entity.");
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
        await using var policyReader = await policyCommand.ExecuteReaderAsync(ct); var rules = new Dictionary<Guid, List<DimensionRule>>(); while (await policyReader.ReadAsync(ct)) { var accountId = policyReader.GetGuid(0); if (!rules.TryGetValue(accountId, out var list)) rules[accountId] = list = []; list.Add(new(policyReader.GetGuid(1), policyReader.GetString(2), policyReader.IsDBNull(3) ? null : policyReader.GetString(3))); }
        await policyReader.CloseAsync(); return rules;
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
        const string sql = "insert into finance_ledger.account_period_balance (tenant_id,legal_entity_id,account_id,fiscal_year_id,accounting_period_id,dimension_key,currency_code,opening_debit,opening_credit,period_debit,period_credit,closing_balance,updated_at) select e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,'',e.base_currency_code,sum(case when e.fact_type='OpeningBalancePosted' then l.base_debit else 0 end),sum(case when e.fact_type='OpeningBalancePosted' then l.base_credit else 0 end),sum(case when e.fact_type='OpeningBalancePosted' then 0 else l.base_debit end),sum(case when e.fact_type='OpeningBalancePosted' then 0 else l.base_credit end),sum(l.base_debit-l.base_credit),now() from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 group by e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,e.base_currency_code order by l.account_id,e.accounting_period_id,e.base_currency_code on conflict (tenant_id,legal_entity_id,account_id,accounting_period_id,dimension_key,currency_code) do update set opening_debit=finance_ledger.account_period_balance.opening_debit+excluded.opening_debit,opening_credit=finance_ledger.account_period_balance.opening_credit+excluded.opening_credit,period_debit=finance_ledger.account_period_balance.period_debit+excluded.period_debit,period_credit=finance_ledger.account_period_balance.period_credit+excluded.period_credit,closing_balance=finance_ledger.account_period_balance.closing_balance+excluded.closing_balance,updated_at=now()";
        await ExecuteAsync(context, sql, ct, tenantId, entityId, entryId);
    }
}
