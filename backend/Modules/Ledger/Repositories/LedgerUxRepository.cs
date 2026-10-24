using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Services;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Repositories;

public sealed class LedgerUxRepository(NpgsqlDataSource dataSource, LedgerSetupRepository setup)
{
    public async Task<AccountSearchPageDto> SearchAccountsAsync(TenantId tenant, LegalEntityId entity, string? query, string? cursor, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var connection = await OpenAsync(tenant, ct);
        const string sql = "select id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,status,version from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or code ilike '%'||$3||'%' or name_ar ilike '%'||$3||'%' or name_en ilike '%'||$3||'%') and ($4::text is null or code>$4) order by code limit $5";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue((object?)query ?? DBNull.Value); command.Parameters.AddWithValue((object?)cursor ?? DBNull.Value); command.Parameters.AddWithValue(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<AccountDto>();
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), entity.Value, reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<Entities.AccountType>(reader.GetString(4)), reader.GetString(5), Enum.Parse<Entities.NormalBalance>(reader.GetString(6)), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetBoolean(8), Enum.Parse<Entities.AccountStatus>(reader.GetString(9)), reader.GetInt64(10)));
        var next = rows.Count > limit ? rows[limit - 1].Code : null; if (rows.Count > limit) rows.RemoveAt(limit); return new(rows, next);
    }

    public async Task<AccountWorkspaceDto> GetAccountWorkspaceAsync(TenantId tenant, LegalEntityId entity, Guid accountId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        AccountDto? account = null;
        await using (var command = new NpgsqlCommand("select id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,status,version from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(accountId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) account = new(reader.GetGuid(0), entity.Value, reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<Entities.AccountType>(reader.GetString(4)), reader.GetString(5), Enum.Parse<Entities.NormalBalance>(reader.GetString(6)), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetBoolean(8), Enum.Parse<Entities.AccountStatus>(reader.GetString(9)), reader.GetInt64(10));
        }
        if (account is null) throw new LedgerDomainException("ledger.account_not_found", "The account was not found.");
        var activity = new List<GeneralLedgerRowDto>();
        await using (var command = new NpgsqlCommand("select e.id,l.id,e.entry_number,e.posting_date,a.id,a.code,a.name_en,l.debit,l.credit,l.base_debit,l.base_credit,l.currency_code,e.source_module,e.source_reference,l.sequence_no from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where e.tenant_id=$1 and e.legal_entity_id=$2 and l.account_id=$3 order by e.posting_date desc,e.entry_number desc,l.sequence_no limit 50", connection))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(accountId); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) activity.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), DateOnly.FromDateTime(reader.GetDateTime(3)), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetInt32(14)));
        }
        decimal debit = 0m, credit = 0m, closing = 0m;
        string currency = activity.FirstOrDefault()?.CurrencyCode ?? "";
        await using (var command = new NpgsqlCommand("select coalesce(sum(period_debit),0),coalesce(sum(period_credit),0),coalesce(sum(closing_balance),0),max(currency_code) from finance_ledger.account_period_balance where tenant_id=$1 and legal_entity_id=$2 and account_id=$3", connection))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(accountId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { debit = reader.GetDecimal(0); credit = reader.GetDecimal(1); closing = reader.GetDecimal(2); if (!reader.IsDBNull(3)) currency = reader.GetString(3); }
        }
        var balance = new AccountBalanceDto(accountId, debit, credit, closing, currency, null, null);
        var dimensions = new List<EntryExplanationDimensionDto>();
        await using (var command = new NpgsqlCommand("select distinct d.code,v.id,v.code from finance_ledger.journal_entry_line l join finance_ledger.journal_line_dimension ld on ld.tenant_id=l.tenant_id and ld.legal_entity_id=l.legal_entity_id and ld.journal_entry_line_id=l.id join finance_ledger.dimension_definition d on d.tenant_id=ld.tenant_id and d.legal_entity_id=ld.legal_entity_id and d.id=ld.dimension_definition_id join finance_ledger.dimension_value v on v.tenant_id=ld.tenant_id and v.legal_entity_id=ld.legal_entity_id and v.id=ld.dimension_value_id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.account_id=$3 order by d.code,v.code limit 200", connection))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(accountId); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) dimensions.Add(new(reader.GetString(0), reader.GetGuid(1), reader.GetString(2)));
        }
        var audit = new List<AuditSummaryDto>();
        await using (var command = new NpgsqlCommand("select action,entity_id,actor_id,occurred_at,reason from platform.audit_entry where tenant_id=$1 and legal_entity_id=$2 and entity_id=$3 order by occurred_at desc limit 50", connection))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(accountId); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) audit.Add(new(reader.GetString(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        var configuration = new List<LedgerReadinessReason>();
        return new(account, activity, balance, dimensions, configuration, audit);
    }

    public async Task<PeriodCloseCheckDto> GetPeriodCloseReadinessAsync(TenantId tenant, LegalEntityId entity, Guid periodId, CancellationToken ct)
    {
        var readiness = await setup.GetReadinessAsync(tenant, entity, ct);
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select status from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(periodId); var status = (string?)await command.ExecuteScalarAsync(ct);
        var blockers = new List<LedgerReadinessReason>(); var warnings = new List<LedgerReadinessReason>(); var checks = new List<LedgerReadinessReason> { new("ledger.readiness", "Ledger readiness is evaluated before close.", readiness.State == LedgerReadinessState.Ready ? "Ready" : "Attention") };
        if (status is null) blockers.Add(new("ledger.period_not_found", "The accounting period was not found."));
        if (status is "Closed") warnings.Add(new("ledger.period_already_closed", "The accounting period is already closed."));
        if (readiness.State == LedgerReadinessState.Blocked) blockers.AddRange(readiness.Capabilities.SelectMany(x => x.Reasons));
        return new(periodId, blockers.Count == 0, checks, warnings, blockers);
    }

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct) { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
}
