using System.Text;
using System.Text.Json;
using Modules.Accounting.DTOs;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Accounting.Repositories;

public sealed class AccountingRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<AccountingSetupSnapshot?> GetSetupAsync(TenantId tenant, Guid entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select mode,go_live_status,configuration_version_id from business_accounting.accounting_setup where tenant_id=$1 and legal_entity_id=$2", connection);
        Add(command, tenant.Value, entity);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new AccountingSetupSnapshot(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2)) : null;
    }

    public async Task<GoLiveApprovalView> ApproveGoLiveAsync(TenantId tenant, Guid entity, Guid openingBatchId, Guid? subledgerOpeningBatchId, DateOnly goLiveDate,
        string idempotencyKey, string payloadHash, string reason, Guid actor, Guid correlation, IReadOnlyList<CloseCheckView> checks, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var connection = await OpenAsync(tenant, ct); await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var setupLock = new NpgsqlCommand("select 1 from business_accounting.accounting_setup where tenant_id=$1 and legal_entity_id=$2 for update", connection, transaction))
        {
            Add(setupLock, tenant.Value, entity);
            if (await setupLock.ExecuteScalarAsync(ct) is null) throw new AccountingRepositoryException("accounting.go_live_setup_not_ready");
        }
        await using (var existing = new NpgsqlCommand("select id,opening_batch_id,subledger_opening_batch_id,go_live_date,reason,checks,approved_at,payload_hash from business_accounting.go_live_approval where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3 for update", connection, transaction))
        {
            Add(existing, tenant.Value, entity, idempotencyKey);
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var persistedHash = reader.GetString(7).Trim();
                if (!string.Equals(persistedHash, payloadHash, StringComparison.Ordinal)) throw new AccountingRepositoryException("accounting.idempotency_conflict");
                var persistedChecks = JsonSerializer.Deserialize<CloseCheckView[]>(reader.GetString(5)) ?? [];
                var result = new GoLiveApprovalView(entity, reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), DateOnly.FromDateTime(reader.GetDateTime(3)), "Approved", persistedChecks, reader.GetFieldValue<DateTimeOffset>(6));
                await reader.DisposeAsync();
                await transaction.CommitAsync(ct);
                return result;
            }
        }
        await using (var command = new NpgsqlCommand("update business_accounting.accounting_setup set go_live_status='Approved',updated_by=$3,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and go_live_status in ('Ready','Approved')", connection, transaction))
        {
            Add(command, tenant.Value, entity, actor, now);
            if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AccountingRepositoryException("accounting.go_live_setup_not_ready");
        }
        var approvalId = Guid.CreateVersion7();
        var approval = new GoLiveApprovalView(entity, openingBatchId, subledgerOpeningBatchId, goLiveDate, "Approved", checks, now);
        await using (var approvalCommand = new NpgsqlCommand("insert into business_accounting.go_live_approval(tenant_id,legal_entity_id,id,opening_batch_id,subledger_opening_batch_id,go_live_date,idempotency_key,payload_hash,reason,checks,approved_by,approved_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10::jsonb,$11,$12)", connection, transaction))
        {
            Add(approvalCommand, tenant.Value, entity, approvalId, openingBatchId, subledgerOpeningBatchId, goLiveDate, idempotencyKey, payloadHash, reason, JsonSerializer.Serialize(checks), actor, now);
            await approvalCommand.ExecuteNonQueryAsync(ct);
        }
        await using (var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) values($1,'AccountingGoLiveApprovedV1',$2::jsonb,$3,$4,$5,$6,'accounting','Pending',0)", connection, transaction))
        {
            Add(outbox, Guid.CreateVersion7(), JsonSerializer.Serialize(new { approvalId, legalEntityId = entity, openingBatchId, subledgerOpeningBatchId, goLiveDate, reason, idempotencyKey }), tenant.Value, now, correlation, approvalId);
            await outbox.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return approval;
    }

    public async Task<FinancialStatementDefinitionView> SaveStatementDefinitionAsync(TenantId tenant, Guid entity, FinancialStatementDefinitionRequest request, Guid actor, CancellationToken ct)
    {
        if (request.Kind is not ("ProfitAndLoss" or "BalanceSheet") || string.IsNullOrWhiteSpace(request.Code) || request.Lines.Count == 0 || request.Lines.Select(line => line.Sequence).Distinct().Count() != request.Lines.Count || request.Lines.Select(line => line.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Lines.Count)
            throw new AccountingRepositoryException("accounting.statement_definition_invalid");
        if (request.Lines.Any(line => string.IsNullOrWhiteSpace(line.Code) || string.IsNullOrWhiteSpace(line.NameAr) || string.IsNullOrWhiteSpace(line.NameEn) || line.Sequence <= 0 || line.LineKind is not ("MappedAccounts" or "Subtotal" or "Arithmetic")))
            throw new AccountingRepositoryException("accounting.statement_definition_invalid");
        if (request.Lines.Any(line => line.AccountType is not null && line.AccountType is not ("Asset" or "Liability" or "Equity" or "Revenue" or "Expense")))
            throw new AccountingRepositoryException("accounting.statement_mapping_invalid");
        if (request.Lines.Any(line => line.LineKind == "MappedAccounts" && ((line.AccountIds?.Count ?? 0) > 0) == (line.AccountType is not null))
            || request.Lines.Any(line => line.LineKind != "MappedAccounts" && (line.AccountType is not null || (line.AccountIds?.Count ?? 0) > 0)))
            throw new AccountingRepositoryException("accounting.statement_mapping_invalid");
        if (request.Lines.Where(line => line.LineKind == "MappedAccounts" && (line.AccountIds?.Count ?? 0) == 0).GroupBy(line => line.AccountType, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new AccountingRepositoryException("accounting.statement_mapping_duplicate_type");
        if (request.Lines.Where(line => line.LineKind == "MappedAccounts").SelectMany(line => line.AccountIds ?? []).GroupBy(accountId => accountId).Any(group => group.Count() > 1))
            throw new AccountingRepositoryException("accounting.statement_mapping_duplicate_account");

        var codes = request.Lines.ToDictionary(line => line.Code.Trim(), StringComparer.OrdinalIgnoreCase);
        if (request.Lines.Any(line => line.ParentLineCode is not null && (!codes.ContainsKey(line.ParentLineCode.Trim()) || string.Equals(line.Code.Trim(), line.ParentLineCode.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw new AccountingRepositoryException("accounting.statement_formula_invalid");
        foreach (var line in request.Lines)
        {
            var refs = line.FormulaLineCodes ?? [];
            if (refs.Any(code => string.IsNullOrWhiteSpace(code) || !codes.ContainsKey(code.Trim()) || string.Equals(code.Trim(), line.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                || refs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != refs.Count)
                throw new AccountingRepositoryException("accounting.statement_formula_invalid");
            if (line.LineKind == "Arithmetic" && (refs.Count < 2 || line.ArithmeticOperator is not ("Add" or "Subtract")))
                throw new AccountingRepositoryException("accounting.statement_formula_invalid");
            if (line.LineKind != "Arithmetic" && line.ArithmeticOperator is not null)
                throw new AccountingRepositoryException("accounting.statement_formula_invalid");
        }
        ValidateFormulaGraph(request.Lines, codes);
        await using var connection = await OpenAsync(tenant, ct); await using var transaction = await connection.BeginTransactionAsync(ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using (var current = new NpgsqlCommand("select code from business_accounting.financial_statement_definition where tenant_id=$1 and legal_entity_id=$2 and statement_kind=$3 and status='Active' for update", connection, transaction))
        {
            Add(current, tenant.Value, entity, request.Kind);
            var activeCode = (string?)await current.ExecuteScalarAsync(ct);
            if (activeCode is not null && !string.Equals(activeCode, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new AccountingRepositoryException("accounting.statement_definition_code_immutable");
        }
        await using (var definition = new NpgsqlCommand("insert into business_accounting.financial_statement_definition(tenant_id,legal_entity_id,id,code,statement_kind,status,current_version,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,'Active',1,$6,$7,$7) on conflict(tenant_id,legal_entity_id,code) do update set status='Active',current_version=business_accounting.financial_statement_definition.current_version+1,updated_at=excluded.updated_at returning id,current_version", connection, transaction))
        {
            Add(definition, tenant.Value, entity, id, request.Code.Trim(), request.Kind, actor, now);
            await using var reader = await definition.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct); id = reader.GetGuid(0); var version = reader.GetInt32(1);
            await reader.DisposeAsync();
            await using var deactivate = new NpgsqlCommand("update business_accounting.financial_statement_version set status='Superseded' where tenant_id=$1 and definition_id=$2 and status='Active'", connection, transaction); Add(deactivate, tenant.Value, id); await deactivate.ExecuteNonQueryAsync(ct);
            await using var versionInsert = new NpgsqlCommand("insert into business_accounting.financial_statement_version(tenant_id,definition_id,version,status,effective_from,created_by,created_at) values($1,$2,$3,'Active',$4,$5,$6)", connection, transaction); Add(versionInsert, tenant.Value, id, version, request.EffectiveFrom, actor, now); await versionInsert.ExecuteNonQueryAsync(ct);
            var lineIds = request.Lines.ToDictionary(line => line.Code.Trim(), _ => Guid.CreateVersion7(), StringComparer.OrdinalIgnoreCase);
            foreach (var line in request.Lines.OrderBy(line => line.Sequence))
            {
                await using var insert = new NpgsqlCommand("insert into business_accounting.financial_statement_line(tenant_id,definition_id,version,id,parent_line_id,sequence,code,name_ar,name_en,line_kind,account_type,account_ids,arithmetic_operator,formula_line_codes,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", connection, transaction);
                Add(insert, tenant.Value, id, version, lineIds[line.Code.Trim()], line.ParentLineCode is null ? null : lineIds[line.ParentLineCode.Trim()], line.Sequence, line.Code.Trim(), line.NameAr.Trim(), line.NameEn.Trim(), line.LineKind, line.AccountType, line.AccountIds?.ToArray() ?? Array.Empty<Guid>(), line.ArithmeticOperator, (line.FormulaLineCodes ?? []).Select(code => code.Trim()).ToArray(), now); await insert.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return new FinancialStatementDefinitionView(id, request.Code.Trim(), request.Kind, "Active", version, request.Lines.OrderBy(line => line.Sequence).ToArray());
        }
    }

    public async Task<FinancialStatementDefinitionView?> GetActiveStatementDefinitionAsync(TenantId tenant, Guid entity, string kind, CancellationToken ct) => await GetStatementDefinitionAsync(tenant, entity, kind, null, ct);

    public async Task<FinancialStatementDefinitionView?> GetStatementDefinitionAsync(TenantId tenant, Guid entity, string kind, DateOnly? asOf, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var definition = new NpgsqlCommand("select d.id,d.code,v.version from business_accounting.financial_statement_definition d join lateral (select version from business_accounting.financial_statement_version where tenant_id=d.tenant_id and definition_id=d.id and ($4::date is null or effective_from is null or effective_from <= $4::date) order by effective_from desc nulls last,version desc limit 1) v on true where d.tenant_id=$1 and d.legal_entity_id=$2 and d.statement_kind=$3 and d.status='Active'", connection); Add(definition, tenant.Value, entity, kind, asOf);
        await using var reader = await definition.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; var id = reader.GetGuid(0); var code = reader.GetString(1); var version = reader.GetInt32(2); await reader.DisposeAsync();
        await using var lines = new NpgsqlCommand("select l.code,l.name_ar,l.name_en,l.sequence,l.account_type,l.account_ids,l.line_kind,p.code,l.arithmetic_operator,l.formula_line_codes from business_accounting.financial_statement_line l left join business_accounting.financial_statement_line p on p.tenant_id=l.tenant_id and p.definition_id=l.definition_id and p.version=l.version and p.id=l.parent_line_id where l.tenant_id=$1 and l.definition_id=$2 and l.version=$3 order by l.sequence", connection); Add(lines, tenant.Value, id, version);
        await using var lineReader = await lines.ExecuteReaderAsync(ct); var result = new List<FinancialStatementDefinitionLineRequest>();
        while (await lineReader.ReadAsync(ct)) result.Add(new(lineReader.GetString(0), lineReader.GetString(1), lineReader.GetString(2), lineReader.GetInt32(3), lineReader.IsDBNull(4) ? null : lineReader.GetString(4), lineReader.GetFieldValue<Guid[]>(5), lineReader.GetString(6), lineReader.IsDBNull(7) ? null : lineReader.GetString(7), lineReader.IsDBNull(8) ? null : lineReader.GetString(8), lineReader.GetFieldValue<string[]>(9)));
        return new FinancialStatementDefinitionView(id, code, kind, "Active", version, result);
    }

    private static void ValidateFormulaGraph(IReadOnlyList<FinancialStatementDefinitionLineRequest> lines, IReadOnlyDictionary<string, FinancialStatementDefinitionLineRequest> codes)
    {
        var states = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> Dependencies(FinancialStatementDefinitionLineRequest line) => (line.FormulaLineCodes is { Count: > 0 }
            ? line.FormulaLineCodes
            : line.LineKind == "Subtotal" ? lines.Where(child => string.Equals(child.ParentLineCode?.Trim(), line.Code.Trim(), StringComparison.OrdinalIgnoreCase)).Select(child => child.Code) : [])
            .Select(code => code.Trim());
        void Visit(string code)
        {
            if (states.TryGetValue(code, out var state)) { if (state == 1) throw new AccountingRepositoryException("accounting.statement_formula_cycle"); if (state == 2) return; }
            states[code] = 1;
            foreach (var dependency in Dependencies(codes[code])) Visit(dependency);
            states[code] = 2;
        }
        foreach (var code in codes.Keys) Visit(code);
    }
    public async Task<ReconciliationView> SaveReconciliationAsync(TenantId tenant, Guid entity, string type, DateOnly asOf, decimal? source, decimal? ledger, decimal? difference, string? currency, string status, IReadOnlyList<string> diagnostics, Guid actor, Guid correlation, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var connection = await OpenAsync(tenant, ct); await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("insert into business_accounting.reconciliation_run(tenant_id,legal_entity_id,id,reconciliation_type,as_of_date,source_balance,ledger_balance,difference,currency_code,status,source_version,diagnostics,created_by,correlation_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'live',$11::jsonb,$12,$13,$14)", connection, transaction))
        { Add(command, tenant.Value, entity, id, type, asOf, source, ledger, difference, currency, status, JsonSerializer.Serialize(diagnostics), actor, correlation, now); await command.ExecuteNonQueryAsync(ct); }
        foreach (var diagnostic in diagnostics.Distinct(StringComparer.Ordinal))
        {
            await using var issue = new NpgsqlCommand("insert into business_accounting.reconciliation_issue(tenant_id,legal_entity_id,id,reconciliation_run_id,code,severity,status,details,created_at) values($1,$2,$3,$4,$5,$6,'Open',$7::jsonb,$8)", connection, transaction);
            Add(issue, tenant.Value, entity, DeterministicGuid(id, diagnostic), id, diagnostic, status == "Difference" ? "Blocking" : "Attention", JsonSerializer.Serialize(new { reconciliationType = type, asOfDate = asOf }), now);
            await issue.ExecuteNonQueryAsync(ct);
        }
        await using (var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) values($1,'AccountingReconciliationCompletedV1',$2::jsonb,$3,$4,$5,$6,'accounting','Pending',0)", connection, transaction))
        { Add(outbox, Guid.CreateVersion7(), JsonSerializer.Serialize(new { reconciliationRunId = id, legalEntityId = entity, type, asOf, status, difference }), tenant.Value, now, correlation, id); await outbox.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct);
        return new(id, type, asOf, source, ledger, difference, currency, status, diagnostics, now);
    }

    public async Task<IReadOnlyList<ReconciliationView>> ListReconciliationsAsync(TenantId tenant, Guid entity, string? type, DateOnly? asOf, int limit, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,reconciliation_type,as_of_date,source_balance,ledger_balance,difference,currency_code,status,diagnostics::text,created_at from business_accounting.reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or reconciliation_type=$3) and ($4::date is null or as_of_date=$4) order by created_at desc,id desc limit $5", connection);
        Add(command, tenant.Value, entity, (object?)type ?? DBNull.Value, (object?)asOf ?? DBNull.Value, Math.Clamp(limit, 1, 200));
        return await ReadReconciliationsAsync(command, ct);
    }

    public async Task<ReconciliationPageDto> ListReconciliationsPageAsync(TenantId tenant, Guid entity, string? type, DateOnly? asOf, string? cursor, int limit, CancellationToken ct, int? page = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var (cursorCreatedAt, cursorId) = page is null ? DecodeCursor(cursor, "accounting.reconciliation_cursor_invalid") : (null, null);
        await using var connection = await OpenAsync(tenant, ct);
        if (page is not null)
        {
            var pageNumber = Math.Max(1, page.Value); var offset = (pageNumber - 1) * limit;
            await using var countCommand = new NpgsqlCommand("select count(*) from business_accounting.reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or reconciliation_type=$3) and ($4::date is null or as_of_date=$4)", connection); Add(countCommand, tenant.Value, entity, (object?)type ?? DBNull.Value, (object?)asOf ?? DBNull.Value);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            const string offsetSql = "select id,reconciliation_type,as_of_date,source_balance,ledger_balance,difference,currency_code,status,diagnostics::text,created_at from business_accounting.reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or reconciliation_type=$3) and ($4::date is null or as_of_date=$4) order by created_at desc,id desc offset $5 limit $6";
            await using var offsetCommand = new NpgsqlCommand(offsetSql, connection); Add(offsetCommand, tenant.Value, entity, (object?)type ?? DBNull.Value, (object?)asOf ?? DBNull.Value, offset, limit);
            await using var offsetReader = await offsetCommand.ExecuteReaderAsync(ct); var offsetRows = new List<ReconciliationView>();
            while (await offsetReader.ReadAsync(ct)) offsetRows.Add(ReadReconciliation(offsetReader));
            return new(offsetRows, null, totalCount, pageNumber, limit);
        }
        const string sql = "select id,reconciliation_type,as_of_date,source_balance,ledger_balance,difference,currency_code,status,diagnostics::text,created_at from business_accounting.reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or reconciliation_type=$3) and ($4::date is null or as_of_date=$4) and ($5::timestamptz is null or (created_at,id)<($5,$6)) order by created_at desc,id desc limit $7";
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, tenant.Value, entity, (object?)type ?? DBNull.Value, (object?)asOf ?? DBNull.Value, (object?)cursorCreatedAt ?? DBNull.Value, (object?)cursorId ?? DBNull.Value, limit + 1);
        await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<ReconciliationView>();
        while (await reader.ReadAsync(ct)) rows.Add(ReadReconciliation(reader));
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1);
        var last = rows.LastOrDefault();
        return new(rows, hasMore && last is not null ? EncodeCursor(last.CreatedAt, last.Id) : null);
    }

    public async Task<ReconciliationView?> GetReconciliationAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,reconciliation_type,as_of_date,source_balance,ledger_balance,difference,currency_code,status,diagnostics::text,created_at from business_accounting.reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, entity, runId);
        return (await ReadReconciliationsAsync(command, ct)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<ReconciliationIssueView>> ListReconciliationIssuesAsync(TenantId tenant, Guid entity, string? status, int limit, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,reconciliation_run_id,code,severity,status,created_at,resolved_at from business_accounting.reconciliation_issue where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) order by created_at desc,id desc limit $4", connection);
        Add(command, tenant.Value, entity, (object?)status ?? DBNull.Value, Math.Clamp(limit, 1, 200));
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<ReconciliationIssueView>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        return result;
    }

    public async Task<ReconciliationIssuePageDto> ListReconciliationIssuesPageAsync(TenantId tenant, Guid entity, string? status, string? cursor, int limit, CancellationToken ct, int? page = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var (cursorCreatedAt, cursorId) = page is null ? DecodeCursor(cursor, "accounting.reconciliation_issue_cursor_invalid") : (null, null);
        await using var connection = await OpenAsync(tenant, ct);
        if (page is not null)
        {
            var pageNumber = Math.Max(1, page.Value); var offset = (pageNumber - 1) * limit;
            await using var countCommand = new NpgsqlCommand("select count(*) from business_accounting.reconciliation_issue where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3)", connection); Add(countCommand, tenant.Value, entity, (object?)status ?? DBNull.Value);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            const string offsetSql = "select id,reconciliation_run_id,code,severity,status,created_at,resolved_at from business_accounting.reconciliation_issue where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) order by created_at desc,id desc offset $4 limit $5";
            await using var offsetCommand = new NpgsqlCommand(offsetSql, connection); Add(offsetCommand, tenant.Value, entity, (object?)status ?? DBNull.Value, offset, limit);
            await using var offsetReader = await offsetCommand.ExecuteReaderAsync(ct); var offsetRows = new List<ReconciliationIssueView>();
            while (await offsetReader.ReadAsync(ct)) offsetRows.Add(new(offsetReader.GetGuid(0), offsetReader.GetGuid(1), offsetReader.GetString(2), offsetReader.GetString(3), offsetReader.GetString(4), offsetReader.GetFieldValue<DateTimeOffset>(5), offsetReader.IsDBNull(6) ? null : offsetReader.GetFieldValue<DateTimeOffset>(6)));
            return new(offsetRows, null, totalCount, pageNumber, limit);
        }
        const string sql = "select id,reconciliation_run_id,code,severity,status,created_at,resolved_at from business_accounting.reconciliation_issue where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) and ($4::timestamptz is null or (created_at,id)<($4,$5)) order by created_at desc,id desc limit $6";
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, tenant.Value, entity, (object?)status ?? DBNull.Value, (object?)cursorCreatedAt ?? DBNull.Value, (object?)cursorId ?? DBNull.Value, limit + 1);
        await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<ReconciliationIssueView>();
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1);
        var last = rows.LastOrDefault();
        return new(rows, hasMore && last is not null ? EncodeCursor(last.CreatedAt, last.Id) : null);
    }

    private static async Task<IReadOnlyList<ReconciliationView>> ReadReconciliationsAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<ReconciliationView>();
        while (await reader.ReadAsync(ct))
        {
            var diagnostics = JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [];
            result.Add(ReadReconciliation(reader));
        }
        return result;
    }

    private static ReconciliationView ReadReconciliation(NpgsqlDataReader reader)
    {
        var diagnostics = JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [];
        return new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetString(6).Trim(), reader.GetString(7), diagnostics, reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static string EncodeCursor(DateTimeOffset createdAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt:O}|{id}"));
    private static (DateTimeOffset? CreatedAt, Guid? Id) DecodeCursor(string? cursor, string code)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return (null, null);
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 2 || !DateTimeOffset.TryParse(parts[0], out var createdAt) || !Guid.TryParse(parts[1], out var id)) throw new FormatException();
            return (createdAt, id);
        }
        catch (FormatException) { throw new AccountingRepositoryException(code); }
    }

    public async Task<CloseRunView> SaveCloseRunAsync(TenantId tenant, Guid entity, Guid periodId, string idempotencyKey, string payloadHash, string status, string? reason, Guid actor, IReadOnlyList<CloseCheckView> checks, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct); await using var transaction = await connection.BeginTransactionAsync(ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        var persistedStatus = status;
        var existingTerminalEvidence = false;
        await using (var command = new NpgsqlCommand("insert into business_accounting.close_run(tenant_id,legal_entity_id,id,period_id,idempotency_key,payload_hash,status,requested_by,reason,completed_at,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,case when $7='Closed' then $10 else null end,$10,$10) on conflict(tenant_id,legal_entity_id,period_id,idempotency_key) do update set status=case when business_accounting.close_run.status in ('Closed','Reopened') then business_accounting.close_run.status else excluded.status end,reason=case when business_accounting.close_run.status in ('Closed','Reopened') then business_accounting.close_run.reason else excluded.reason end,completed_at=case when business_accounting.close_run.status in ('Closed','Reopened') then business_accounting.close_run.completed_at when excluded.status='Closed' then excluded.updated_at else business_accounting.close_run.completed_at end,updated_at=excluded.updated_at,row_version=business_accounting.close_run.row_version+1 where business_accounting.close_run.payload_hash=excluded.payload_hash returning id,created_at,status", connection, transaction))
        {
            Add(command, tenant.Value, entity, id, periodId, idempotencyKey, payloadHash, status, actor, reason, now);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { id = reader.GetGuid(0); now = reader.GetFieldValue<DateTimeOffset>(1); persistedStatus = reader.GetString(2); }
            else
            {
                await reader.DisposeAsync();
                await using var existing = new NpgsqlCommand("select payload_hash from business_accounting.close_run where tenant_id=$1 and legal_entity_id=$2 and period_id=$3 and idempotency_key=$4", connection, transaction);
                Add(existing, tenant.Value, entity, periodId, idempotencyKey);
                var persistedHash = (string?)await existing.ExecuteScalarAsync(ct);
                if (persistedHash is not null && !string.Equals(persistedHash, payloadHash, StringComparison.Ordinal))
                    throw new AccountingRepositoryException("accounting.idempotency_conflict");
                throw new AccountingRepositoryException("accounting.close_run_retry_required");
            }
        }
        if (persistedStatus is "Closed" or "Reopened")
        {
            await using var evidence = new NpgsqlCommand("select exists(select 1 from business_accounting.close_check where tenant_id=$1 and close_run_id=$2)", connection, transaction);
            Add(evidence, tenant.Value, id);
            existingTerminalEvidence = (bool)(await evidence.ExecuteScalarAsync(ct) ?? false);
        }
        foreach (var check in existingTerminalEvidence ? Array.Empty<CloseCheckView>() : checks)
        {
            // A retry must preserve a single auditable result for each deterministic
            // check, rather than deleting historical evidence before replacing it.
            await using var command = new NpgsqlCommand("insert into business_accounting.close_check(tenant_id,close_run_id,id,check_code,severity,status,details,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8) on conflict(tenant_id,close_run_id,id) do update set severity=excluded.severity,status=excluded.status,details=excluded.details", connection, transaction);
            Add(command, tenant.Value, id, DeterministicGuid(id, check.Code), check.Code, check.Severity, check.Status, JsonSerializer.Serialize(new { check.Message }), now); await command.ExecuteNonQueryAsync(ct);
        }
        if (persistedStatus is "Closed" or "Reopened")
        {
            await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) select $1,$2,$3::jsonb,$4,$5,$6,$7,'accounting','Pending',0 where not exists (select 1 from platform.outbox_message where tenant_id=$4 and causation_id=$7 and message_type=$2)", connection, transaction);
            Add(outbox, Guid.CreateVersion7(), persistedStatus == "Closed" ? "AccountingCloseCompletedV1" : "AccountingPeriodReopenedV1", JsonSerializer.Serialize(new { closeRunId = id, periodId, legalEntityId = entity, status = persistedStatus }), tenant.Value, now, Guid.CreateVersion7(), id);
            await outbox.ExecuteNonQueryAsync(ct);
        }
        var returnedChecks = existingTerminalEvidence ? await LoadCloseChecksAsync(connection, transaction, tenant, id, ct) : checks;
        await transaction.CommitAsync(ct); return new(id, periodId, persistedStatus, returnedChecks, now);
    }

    private static async Task<IReadOnlyList<CloseCheckView>> LoadCloseChecksAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid closeRunId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select check_code,severity,status,details->>'Message' from business_accounting.close_check where tenant_id=$1 and close_run_id=$2 order by check_code", connection, transaction);
        Add(command, tenant.Value, closeRunId);
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<CloseCheckView>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        return result;
    }

    public async Task<CloseRunView?> GetCloseRunAsync(TenantId tenant, Guid entity, Guid periodId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        Guid id;
        string status;
        DateTimeOffset createdAt;
        await using (var command = new NpgsqlCommand("select id,status,created_at from business_accounting.close_run where tenant_id=$1 and legal_entity_id=$2 and period_id=$3 order by updated_at desc,id desc limit 1", connection))
        {
            Add(command, tenant.Value, entity, periodId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            id = reader.GetGuid(0);
            status = reader.GetString(1);
            createdAt = reader.GetFieldValue<DateTimeOffset>(2);
        }
        await using var checksCommand = new NpgsqlCommand("select check_code,severity,status,details->>'Message' from business_accounting.close_check where tenant_id=$1 and close_run_id=$2 order by check_code", connection);
        Add(checksCommand, tenant.Value, id);
        await using var checksReader = await checksCommand.ExecuteReaderAsync(ct);
        var checks = new List<CloseCheckView>();
        while (await checksReader.ReadAsync(ct)) checks.Add(new(checksReader.GetString(0), checksReader.GetString(1), checksReader.GetString(2), checksReader.IsDBNull(3) ? string.Empty : checksReader.GetString(3)));
        return new(id, periodId, status, checks, createdAt);
    }

    public async Task<IReadOnlyList<CloseCheckView>> ListCloseRunIssuesAsync(TenantId tenant, Guid entity, Guid periodId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select c.check_code,c.severity,c.status,c.details->>'Message' from business_accounting.close_check c join business_accounting.close_run r on r.tenant_id=c.tenant_id and r.id=c.close_run_id where c.tenant_id=$1 and r.legal_entity_id=$2 and r.period_id=$3 and c.status<>'Pass' order by r.updated_at desc,c.check_code", connection);
        Add(command, tenant.Value, entity, periodId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var issues = new List<CloseCheckView>();
        while (await reader.ReadAsync(ct)) issues.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        return issues;
    }

    public async Task<FxRevaluationRunView> SaveFxRevaluationPreviewAsync(TenantId tenant, Guid entity, FxRevaluationRequest request, string payloadHash, IReadOnlyList<FxRevaluationLineDraft> lines, Guid actor, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var existing = false;
        await using (var command = new NpgsqlCommand("insert into business_accounting.fx_revaluation_run(tenant_id,legal_entity_id,id,as_of_date,idempotency_key,payload_hash,status,requested_by,auto_reversal_date,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Previewed',$7,$8,$9,$9) on conflict(tenant_id,legal_entity_id,as_of_date,idempotency_key) do update set updated_at=excluded.updated_at where business_accounting.fx_revaluation_run.payload_hash=excluded.payload_hash returning id,status,auto_reversal_date,created_at", connection, transaction))
        {
            Add(command, tenant.Value, entity, id, request.AsOfDate, request.IdempotencyKey.Trim(), payloadHash, actor, request.AutoReversalDate, now);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.DisposeAsync();
                await using var conflict = new NpgsqlCommand("select payload_hash from business_accounting.fx_revaluation_run where tenant_id=$1 and legal_entity_id=$2 and as_of_date=$3 and idempotency_key=$4", connection, transaction);
                Add(conflict, tenant.Value, entity, request.AsOfDate, request.IdempotencyKey.Trim());
                if (await conflict.ExecuteScalarAsync(ct) is string persisted && !string.Equals(persisted, payloadHash, StringComparison.Ordinal)) throw new AccountingRepositoryException("accounting.idempotency_conflict");
                throw new AccountingRepositoryException("accounting.fx_revaluation_retry_required");
            }
            id = reader.GetGuid(0); var status = reader.GetString(1);
            await reader.DisposeAsync();
            existing = status != "Previewed" || await HasFxLinesAsync(connection, transaction, tenant, id, ct);
        }
        if (!existing)
        {
            foreach (var line in lines)
            {
                await using var insert = new NpgsqlCommand("insert into business_accounting.fx_revaluation_line(tenant_id,revaluation_run_id,id,open_item_id,source_row_version,direction,transaction_currency,open_transaction_amount,base_currency,carrying_base_amount,revalued_base_amount,difference_amount,difference_role,exchange_rate,exchange_rate_snapshot,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15::jsonb,$16)", connection, transaction);
                Add(insert, tenant.Value, id, line.Id, line.OpenItemId, line.SourceRowVersion, line.Direction, line.TransactionCurrency, line.OpenTransactionAmount, line.BaseCurrency, line.CarryingBaseAmount, line.RevaluedBaseAmount, line.DifferenceAmount, line.DifferenceRole, line.ExchangeRate, line.ExchangeRateSnapshot, now);
                await insert.ExecuteNonQueryAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
        return await GetFxRevaluationRunAsync(tenant, entity, id, ct) ?? throw new AccountingRepositoryException("accounting.fx_revaluation_not_persisted");
    }

    public async Task<FxRevaluationRunView?> GetFxRevaluationRunAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var run = new NpgsqlCommand("select as_of_date,status,auto_reversal_date,created_at from business_accounting.fx_revaluation_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(run, tenant.Value, entity, runId);
        await using var reader = await run.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var asOf = DateOnly.FromDateTime(reader.GetDateTime(0)); var status = reader.GetString(1); var reversal = reader.IsDBNull(2) ? (DateOnly?)null : DateOnly.FromDateTime(reader.GetDateTime(2)); var created = reader.GetFieldValue<DateTimeOffset>(3);
        await reader.DisposeAsync();
        return new FxRevaluationRunView(runId, asOf, status, reversal, await LoadFxLinesAsync(connection, tenant, runId, ct), created);
    }

    public async Task<FxRevaluationRunView> StartFxRevaluationPostAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        DateOnly asOf;
        await using (var command = new NpgsqlCommand("select as_of_date,status from business_accounting.fx_revaluation_run where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction))
        {
            Add(command, tenant.Value, entity, runId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new AccountingRepositoryException("accounting.fx_revaluation_not_found");
            asOf = DateOnly.FromDateTime(reader.GetDateTime(0)); var status = reader.GetString(1);
            if (status == "Posted")
            {
                await reader.DisposeAsync();
                await transaction.CommitAsync(ct);
                return await GetFxRevaluationRunAsync(tenant, entity, runId, ct) ?? throw new AccountingRepositoryException("accounting.fx_revaluation_not_found");
            }
            if (status == "Reversed") throw new AccountingRepositoryException("accounting.fx_revaluation_reversed");
            if (status == "Invalidated") throw new AccountingRepositoryException("accounting.fx_revaluation_invalidated");
        }
        await using (var clash = new NpgsqlCommand("select exists(select 1 from business_accounting.fx_revaluation_run where tenant_id=$1 and legal_entity_id=$2 and as_of_date=$3 and id<>$4 and status in ('Posting','Posted'))", connection, transaction))
        {
            Add(clash, tenant.Value, entity, asOf, runId);
            if ((bool)(await clash.ExecuteScalarAsync(ct) ?? false)) throw new AccountingRepositoryException("accounting.fx_revaluation_already_posted");
        }
        await using (var update = new NpgsqlCommand("update business_accounting.fx_revaluation_run set status='Posting',updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction)) { Add(update, tenant.Value, entity, runId, clock.GetUtcNow()); await update.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct);
        return await GetFxRevaluationRunAsync(tenant, entity, runId, ct) ?? throw new AccountingRepositoryException("accounting.fx_revaluation_not_found");
    }

    public async Task SaveFxRevaluationPostingAsync(TenantId tenant, Guid entity, Guid runId, Guid lineId, Guid factId, Guid entryId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_accounting.fx_revaluation_line set ledger_fact_id=$5,journal_entry_id=$6 where tenant_id=$1 and revaluation_run_id=$2 and id=$3 and not exists(select 1 from business_accounting.fx_revaluation_line x where x.tenant_id=$1 and x.revaluation_run_id=$2 and x.id=$3 and x.ledger_fact_id is not null and x.ledger_fact_id<>$5)", connection);
        Add(command, tenant.Value, runId, lineId, entity, factId, entryId);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AccountingRepositoryException("accounting.fx_revaluation_line_changed");
    }

    public async Task<long> GetFxSourceRowVersionAsync(TenantId tenant, Guid entity, Guid runId, Guid lineId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select l.source_row_version from business_accounting.fx_revaluation_line l join business_accounting.fx_revaluation_run r on r.tenant_id=l.tenant_id and r.id=l.revaluation_run_id where l.tenant_id=$1 and r.legal_entity_id=$2 and l.revaluation_run_id=$3 and l.id=$4", connection);
        Add(command, tenant.Value, entity, runId, lineId);
        return await command.ExecuteScalarAsync(ct) is long version ? version : throw new AccountingRepositoryException("accounting.fx_revaluation_line_missing");
    }

    public async Task CompleteFxRevaluationAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_accounting.fx_revaluation_run set status='Posted',posted_at=$4,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Posting' and not exists(select 1 from business_accounting.fx_revaluation_line where tenant_id=$1 and revaluation_run_id=$3 and journal_entry_id is null)", connection);
        Add(command, tenant.Value, entity, runId, clock.GetUtcNow());
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AccountingRepositoryException("accounting.fx_revaluation_incomplete");
    }

    public async Task InvalidateFxRevaluationAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_accounting.fx_revaluation_run set status='Invalidated',updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Posting'", connection);
        Add(command, tenant.Value, entity, runId, clock.GetUtcNow());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveFxRevaluationReversalAsync(TenantId tenant, Guid entity, Guid runId, Guid lineId, Guid reversalEntryId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_accounting.fx_revaluation_line set reversal_entry_id=$5 where tenant_id=$1 and revaluation_run_id=$2 and id=$3 and journal_entry_id is not null and (reversal_entry_id is null or reversal_entry_id=$5)", connection);
        Add(command, tenant.Value, runId, lineId, entity, reversalEntryId);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AccountingRepositoryException("accounting.fx_revaluation_line_changed");
    }

    public async Task CompleteFxRevaluationReversalAsync(TenantId tenant, Guid entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_accounting.fx_revaluation_run set status='Reversed',reversed_at=$4,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status in ('Posted','Invalidated') and not exists(select 1 from business_accounting.fx_revaluation_line where tenant_id=$1 and revaluation_run_id=$3 and journal_entry_id is not null and reversal_entry_id is null)", connection);
        Add(command, tenant.Value, entity, runId, clock.GetUtcNow());
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AccountingRepositoryException("accounting.fx_revaluation_reversal_incomplete");
    }

    private static async Task<bool> HasFxLinesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid runId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from business_accounting.fx_revaluation_line where tenant_id=$1 and revaluation_run_id=$2)", connection, transaction);
        Add(command, tenant.Value, runId); return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<IReadOnlyList<FxRevaluationLineView>> LoadFxLinesAsync(NpgsqlConnection connection, TenantId tenant, Guid runId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,open_item_id,direction,transaction_currency,open_transaction_amount,base_currency,carrying_base_amount,revalued_base_amount,difference_amount,difference_role,exchange_rate,journal_entry_id,reversal_entry_id,exchange_rate_snapshot::text from business_accounting.fx_revaluation_line where tenant_id=$1 and revaluation_run_id=$2 order by id", connection);
        Add(command, tenant.Value, runId); await using var reader = await command.ExecuteReaderAsync(ct); var lines = new List<FxRevaluationLineView>();
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetString(9), reader.GetDecimal(10), reader.IsDBNull(11) ? null : reader.GetGuid(11), reader.IsDBNull(12) ? null : reader.GetGuid(12), reader.GetString(13)));
        return lines;
    }

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct)
    { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static Guid DeterministicGuid(Guid scope, string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{scope:D}:{value}"));
        return new Guid(bytes[..16]);
    }
}

public sealed class AccountingRepositoryException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed record AccountingSetupSnapshot(string Mode, string GoLiveStatus, Guid? ConfigurationVersionId);
public sealed record FxRevaluationLineDraft(Guid Id, Guid OpenItemId, long SourceRowVersion, string Direction, string TransactionCurrency, decimal OpenTransactionAmount, string BaseCurrency, decimal CarryingBaseAmount, decimal RevaluedBaseAmount, decimal DifferenceAmount, string DifferenceRole, decimal ExchangeRate, string ExchangeRateSnapshot);
