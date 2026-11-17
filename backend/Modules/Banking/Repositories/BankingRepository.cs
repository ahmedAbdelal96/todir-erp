using System.Text.Json;
using System.Globalization;
using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Npgsql;
using Shared.Contracts.Banking;
using Shared.Primitives.Identifiers;

namespace Modules.Banking.Repositories;

public sealed class BankingRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TreasuryAccountPage> ListAccountsAsync(TenantId tenant, Guid legalEntityId, string? search, TreasuryAccountType? type,
        TreasuryAccountStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or code ilike '%' || $3::text || '%' or name ilike '%' || $3::text || '%') and ($4::text is null or account_type=$4::text) and ($5::text is null or status=$5::text)";
        await using var count = new NpgsqlCommand($"select count(*) from finance_banking.treasury_account where {where}", connection);
        Add(count, tenant.Value, legalEntityId, Db(search), Db(type?.ToString()), Db(status?.ToString()));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select id,legal_entity_id,branch_id,code,name,account_type,status,currency_policy,currency_code,purpose,liquidity_class,masked_identifier,version,created_at,updated_at from finance_banking.treasury_account where {where} order by code,id offset $6 limit $7", connection);
        Add(command, tenant.Value, legalEntityId, Db(search), Db(type?.ToString()), Db(status?.ToString()), offset, pageSize);
        var items = new List<TreasuryAccountDto>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(ReadAccount(reader));
        return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<TreasuryAccountDto?> GetAccountAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,branch_id,code,name,account_type,status,currency_policy,currency_code,purpose,liquidity_class,masked_identifier,version,created_at,updated_at from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadAccount(reader) : null;
    }

    public async Task<TreasuryAccountDto> CreateAccountAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, TreasuryAccountCreateRequest request, CancellationToken ct)
    {
        ValidateAccount(request);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var allowedUses = JsonSerializer.Serialize(request.AllowedUses ?? [], JsonOptions);
        await using (var command = new NpgsqlCommand("insert into finance_banking.treasury_account(tenant_id,legal_entity_id,id,branch_id,code,name,account_type,status,currency_policy,currency_code,purpose,liquidity_class,allowed_uses,masked_identifier,secret_ref,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,'Draft',$8,$9,$10,$11,$12::jsonb,$13,$14,1,$15,$15)", connection, transaction))
        { Add(command, tenant.Value, request.LegalEntityId, id, Db(request.BranchId), request.Code.Trim(), request.Name.Trim(), request.Type.ToString(), request.CurrencyPolicy.ToString(), Db(NormalizeCurrency(request.CurrencyCode, request.CurrencyPolicy)), request.Purpose.Trim(), request.LiquidityClass.Trim(), allowedUses, Db(request.MaskedIdentifier), Db(request.SecretRef), now); await command.ExecuteNonQueryAsync(ct); }
        var currency = NormalizeCurrency(request.CurrencyCode, request.CurrencyPolicy);
        if (currency is not null)
        {
            await using var pocket = new NpgsqlCommand("insert into finance_banking.treasury_account_balance_pocket(tenant_id,legal_entity_id,id,treasury_account_id,currency_code,is_primary,version,created_at,updated_at) values($1,$2,$3,$4,$5,true,1,$6,$6)", connection, transaction);
            Add(pocket, tenant.Value, request.LegalEntityId, Guid.CreateVersion7(), id, currency, now); await pocket.ExecuteNonQueryAsync(ct);
        }
        await InsertProfileAsync(connection, transaction, tenant, request, id, now, ct);
        await transaction.CommitAsync(ct); return (await GetAccountAsync(tenant, request.LegalEntityId, id, ct))!;
    }

    public async Task<TreasuryAccountDto> UpdateAccountAsync(TenantId tenant, Guid legalEntityId, Guid id, UpdateTreasuryAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Purpose) || string.IsNullOrWhiteSpace(request.LiquidityClass))
            throw new BankingException("banking.account_invalid", "Account name, purpose and liquidity class are required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var command = new NpgsqlCommand("update finance_banking.treasury_account set name=$4,purpose=$5,liquidity_class=$6,version=version+1,updated_at=$7 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status not in ('Closed','Closing') and version=$8 returning id", connection, transaction);
        Add(command, tenant.Value, legalEntityId, id, request.Name.Trim(), request.Purpose.Trim(), request.LiquidityClass.Trim(), clock.GetUtcNow(), request.Version);
        if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.account_concurrency", "The treasury account was not found, is closed, or has changed.");
        await transaction.CommitAsync(ct); return (await GetAccountAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<TreasuryAccountDto> ChangeStatusAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid id, string target, long? expectedVersion, CancellationToken ct)
    {
        if (target is not ("Active" or "Suspended" or "Closed")) throw new BankingException("banking.account_status_invalid", "The requested treasury account status is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var command = new NpgsqlCommand("update finance_banking.treasury_account set status=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and ($6::bigint is null or version=$6::bigint) and status <> 'Closed' returning id", connection, transaction);
        Add(command, tenant.Value, legalEntityId, id, target, clock.GetUtcNow(), Db(expectedVersion));
        if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException(expectedVersion is null ? "banking.account_not_found" : "banking.account_concurrency", "The treasury account was not found or has changed.");
        await transaction.CommitAsync(ct); return (await GetAccountAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<StatementDto> ImportStatementAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid correlationId, Guid legalEntityId, Guid accountId, string idempotencyKey, ImportStatementRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new BankingException("banking.import_idempotency_required", "A retry key is required for statement import.");
        if (request.Lines.Count > 10000) throw new BankingException("banking.statement_too_large", "The statement exceeds the supported import size.");
        var currency = NormalizeCurrency(request.CurrencyCode, TreasuryCurrencyPolicy.SingleCurrency)!;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await EnsureAccountAsync(connection, transaction, tenant, legalEntityId, accountId, ct);
        await using (var existing = new NpgsqlCommand("select s.id,s.treasury_account_id,s.statement_number,s.opening_date,s.closing_date,s.currency_code,s.processing_state,s.reconciliation_state,s.opening_balance,s.closing_balance,(select count(*) from finance_banking.external_statement_line l where l.statement_id=s.id),s.version from finance_banking.external_statement s join finance_banking.statement_import_batch b on b.external_statement_id=s.id where s.tenant_id=$1 and b.treasury_account_id=$2 and b.idempotency_key=$3", connection, transaction))
        { Add(existing, tenant.Value, accountId, idempotencyKey.Trim()); await using var reader = await existing.ExecuteReaderAsync(ct); if (await reader.ReadAsync(ct)) { var result = ReadStatement(reader); await reader.CloseAsync(); await transaction.CommitAsync(ct); return result; } }
        var now = clock.GetUtcNow(); var statementId = Guid.CreateVersion7(); var batchId = Guid.CreateVersion7();
        await using (var statement = new NpgsqlCommand("insert into finance_banking.external_statement(tenant_id,legal_entity_id,id,treasury_account_id,statement_number,opening_date,closing_date,currency_code,opening_balance,closing_balance,processing_state,reconciliation_state,source_type,source_reference,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Imported','Unreconciled',$11,$12,1,$13,$13)", connection, transaction))
        { Add(statement, tenant.Value, legalEntityId, statementId, accountId, request.StatementNumber.Trim(), Db(request.OpeningDate), Db(request.ClosingDate), currency, Db(request.OpeningBalance), Db(request.ClosingBalance), request.SourceType.Trim(), Db(request.SourceReference), now); await statement.ExecuteNonQueryAsync(ct); }
        await using (var batch = new NpgsqlCommand("insert into finance_banking.statement_import_batch(tenant_id,legal_entity_id,id,treasury_account_id,external_statement_id,idempotency_key,source_type,status,line_count,created_at,completed_at) values($1,$2,$3,$4,$5,$6,$7,'Completed',$8,$9,$9)", connection, transaction))
        { Add(batch, tenant.Value, legalEntityId, batchId, accountId, statementId, idempotencyKey.Trim(), request.SourceType.Trim(), request.Lines.Count, now); await batch.ExecuteNonQueryAsync(ct); }
        foreach (var line in request.Lines)
        {
            if (line.Amount <= 0 || !string.Equals(NormalizeCurrency(line.CurrencyCode, TreasuryCurrencyPolicy.SingleCurrency), currency, StringComparison.Ordinal)) throw new BankingException("banking.statement_line_invalid", "Statement lines must have a positive amount in the statement currency.");
            await using var lineCommand = new NpgsqlCommand("insert into finance_banking.external_statement_line(tenant_id,legal_entity_id,id,statement_id,treasury_account_id,external_line_id,booking_date,value_date,direction,amount,currency_code,reference,transaction_code,counterparty_name,raw_payload,content_hash,classification,claimed_amount,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15::jsonb,$16,'Unclassified',0,1,$17,$17) on conflict(tenant_id,treasury_account_id,external_line_id) do nothing", connection, transaction);
            Add(lineCommand, tenant.Value, legalEntityId, Guid.CreateVersion7(), statementId, accountId, line.ExternalLineId.Trim(), line.BookingDate, Db(line.ValueDate), line.Direction, line.Amount, currency, Db(line.Reference), Db(line.TransactionCode), Db(line.CounterpartyName), line.RawPayload ?? "{}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(line, JsonOptions)))), now); await lineCommand.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct); return (await GetStatementAsync(tenant, legalEntityId, statementId, ct))!;
    }

    public async Task<StatementPage> ListStatementsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.external_statement where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3", connection); Add(count, tenant.Value, legalEntityId, accountId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select s.id,s.treasury_account_id,s.statement_number,s.opening_date,s.closing_date,s.currency_code,s.processing_state,s.reconciliation_state,s.opening_balance,s.closing_balance,(select count(*) from finance_banking.external_statement_line l where l.statement_id=s.id),s.version from finance_banking.external_statement s where s.tenant_id=$1 and s.legal_entity_id=$2 and s.treasury_account_id=$3 order by s.closing_date desc nulls last,s.id desc offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, accountId, offset, pageSize);
        var items = new List<StatementDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadStatement(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<StatementDto?> GetStatementAsync(TenantId tenant, Guid legalEntityId, Guid statementId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select s.id,s.treasury_account_id,s.statement_number,s.opening_date,s.closing_date,s.currency_code,s.processing_state,s.reconciliation_state,s.opening_balance,s.closing_balance,(select count(*) from finance_banking.external_statement_line l where l.statement_id=s.id),s.version from finance_banking.external_statement s where s.tenant_id=$1 and s.legal_entity_id=$2 and s.id=$3", connection); Add(command, tenant.Value, legalEntityId, statementId); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadStatement(reader) : null;
    }

    public async Task<StatementLinePage> ListStatementLinesAsync(TenantId tenant, Guid legalEntityId, Guid statementId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        const string scope = "tenant_id=$1 and legal_entity_id=$2 and statement_id=$3";
        await using var count = new NpgsqlCommand($"select count(*) from finance_banking.external_statement_line where {scope}", connection); Add(count, tenant.Value, legalEntityId, statementId);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select id,statement_id,external_line_id,booking_date,value_date,direction,amount,currency_code,reference,transaction_code,classification,claimed_amount,version from finance_banking.external_statement_line where {scope} order by booking_date,id offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, statementId, offset, pageSize);
        var items = new List<StatementLineDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadStatementLine(reader));
        return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<StatementLineDto?> GetStatementLineAsync(TenantId tenant, Guid legalEntityId, Guid lineId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,statement_id,external_line_id,booking_date,value_date,direction,amount,currency_code,reference,transaction_code,classification,claimed_amount,version from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, lineId);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadStatementLine(reader) : null;
    }

    public async Task<ReconciliationDto> CreateReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var statement = new NpgsqlCommand("select treasury_account_id,closing_balance from finance_banking.external_statement where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(statement, tenant.Value, legalEntityId, request.StatementId);
        await using var reader = await statement.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.statement_not_found", "The statement was not found.");
        var accountId = reader.GetGuid(0); var externalBalance = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1); await reader.CloseAsync();
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var difference = externalBalance - request.BookBalance;
        await using var command = new NpgsqlCommand("insert into finance_banking.reconciliation_session(tenant_id,legal_entity_id,id,treasury_account_id,statement_id,as_of_date,state,external_balance,book_balance,difference,notes,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Open',$7,$8,$9,$10,1,$11,$11)", connection, transaction);
        Add(command, tenant.Value, legalEntityId, id, accountId, request.StatementId, request.AsOfDate, externalBalance, request.BookBalance, difference, Db(request.Notes), now); await command.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
        return (await GetReconciliationAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3", connection); Add(count, tenant.Value, legalEntityId, accountId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.session_id=r.id and m.state='Accepted'),version from finance_banking.reconciliation_session r where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3 order by as_of_date desc,id desc offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, accountId, offset, pageSize);
        var items = new List<ReconciliationDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadReconciliation(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2", connection); Add(count, tenant.Value, legalEntityId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.session_id=r.id and m.state='Accepted'),version from finance_banking.reconciliation_session r where tenant_id=$1 and legal_entity_id=$2 order by as_of_date desc,id desc offset $3 limit $4", connection); Add(command, tenant.Value, legalEntityId, offset, pageSize);
        var items = new List<ReconciliationDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadReconciliation(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<ReconciliationDto?> GetReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.session_id=r.id and m.state='Accepted'),version from finance_banking.reconciliation_session r where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadReconciliation(reader) : null;
    }

    public async Task<ReconciliationDto> AddMatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid sessionId, CreateMatchRequest request, CancellationToken ct)
    {
        if (request.StatementLineIds.Count == 0 || request.ExternalAmount <= 0 || request.SystemAmount < 0 || request.Difference != request.ExternalAmount - request.SystemAmount)
            throw new BankingException("banking.match_conservation_invalid", "A match must contain statement lines and conserve the external amount.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var session = new NpgsqlCommand("select statement_id from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Open' for update", connection, transaction); Add(session, tenant.Value, legalEntityId, sessionId); var sessionValue = await session.ExecuteScalarAsync(ct); var sessionStatementId = sessionValue is Guid value ? value : (Guid?)null; if (sessionStatementId is null) throw new BankingException("banking.reconciliation_not_open", "The reconciliation session is not open.");
        var lineIds = request.StatementLineIds.Distinct().ToArray(); var lineParams = string.Join(',', lineIds.Select((_, i) => $"${i + 4}"));
        await using var lines = new NpgsqlCommand($"select id,amount,claimed_amount from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and statement_id=$3 and id in ({lineParams}) for update", connection, transaction); Add(lines, tenant.Value, legalEntityId, sessionStatementId.Value); Add(lines, lineIds.Cast<object?>().ToArray());
        var lineRows = new List<(Guid Id, decimal Amount, decimal Claimed)>(); await using (var reader = await lines.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) lineRows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2)));
        if (lineRows.Count != lineIds.Length || lineRows.Sum(x => x.Amount - x.Claimed) < request.ExternalAmount) throw new BankingException("banking.match_exceeds_unclaimed", "The match exceeds the remaining statement amount.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var match = new NpgsqlCommand("insert into finance_banking.reconciliation_match(tenant_id,legal_entity_id,id,session_id,source,state,external_amount,system_amount,difference,explanation,created_by) values($1,$2,$3,$4,$5,'Accepted',$6,$7,$8,$9,$10)", connection, transaction); Add(match, tenant.Value, legalEntityId, id, sessionId, request.Source.Trim(), request.ExternalAmount, request.SystemAmount, request.Difference, Db(request.Explanation), actorId); await match.ExecuteNonQueryAsync(ct);
        foreach (var line in lineRows)
        {
            var allocation = Math.Min(line.Amount - line.Claimed, request.ExternalAmount);
            if (allocation <= 0) continue;
            await using var item = new NpgsqlCommand("insert into finance_banking.reconciliation_match_item(tenant_id,legal_entity_id,id,match_id,statement_line_id,external_amount,system_reference) values($1,$2,$3,$4,$5,$6,$7)", connection, transaction); Add(item, tenant.Value, legalEntityId, Guid.CreateVersion7(), id, line.Id, allocation, Db(request.Payments.Count > 0 ? request.Payments[0].PaymentId.ToString("D") : null)); await item.ExecuteNonQueryAsync(ct);
            await using var claim = new NpgsqlCommand("update finance_banking.external_statement_line set claimed_amount=claimed_amount+$4,classification=case when claimed_amount+$4>=amount then 'Other' else classification end,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(claim, tenant.Value, legalEntityId, line.Id, allocation, now); await claim.ExecuteNonQueryAsync(ct);
            request = request with { ExternalAmount = request.ExternalAmount - allocation }; if (request.ExternalAmount <= 0) break;
        }
        await transaction.CommitAsync(ct); return (await GetReconciliationAsync(tenant, legalEntityId, sessionId, ct))!;
    }

    public async Task<ReconciliationDto> ChangeReconciliationStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct)
    {
        if (target is not ("Finalized" or "Reopened")) throw new BankingException("banking.reconciliation_state_invalid", "The requested reconciliation state is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var command = new NpgsqlCommand("update finance_banking.reconciliation_session set state=$4,version=version+1,finalized_by=case when $4='Finalized' then $5 else null end,finalized_at=case when $4='Finalized' then $6 else null end,updated_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$7 and ((state <> 'Finalized') or (state='Finalized' and $4='Reopened')) returning id", connection, transaction); Add(command, tenant.Value, legalEntityId, id, target, actorId, clock.GetUtcNow(), version); if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.reconciliation_concurrency", "The reconciliation has changed."); await transaction.CommitAsync(ct); return (await GetReconciliationAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<IReadOnlyList<ReconciliationSuggestionDto>> GetReconciliationSuggestionsAsync(TenantId tenant, Guid legalEntityId, Guid reconciliationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select l.id,l.amount,l.currency_code,l.reference,l.transaction_code,case when l.classification<>'Unclassified' then 'Classified bank activity' when l.reference is not null then 'Reference available for matching' else 'Unclaimed statement line' end from finance_banking.reconciliation_session s join finance_banking.external_statement_line l on l.tenant_id=s.tenant_id and l.legal_entity_id=s.legal_entity_id and l.statement_id=s.statement_id where s.tenant_id=$1 and s.legal_entity_id=$2 and s.id=$3 and l.claimed_amount<l.amount order by l.booking_date,l.id", connection); Add(command, tenant.Value, legalEntityId, reconciliationId);
        var result = new List<ReconciliationSuggestionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2).Trim(), StringValue(reader, 3), StringValue(reader, 4), reader.GetString(5))); return result;
    }

    public async Task<ReconciliationDto> UnmatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid reconciliationId, UnmatchRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var lockSession = new NpgsqlCommand("select version from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4 for update", connection, transaction); Add(lockSession, tenant.Value, legalEntityId, reconciliationId, request.Version); if (await lockSession.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.reconciliation_concurrency", "The reconciliation no longer exists or has changed.");
        await using var update = new NpgsqlCommand("update finance_banking.reconciliation_match set state='Removed' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and session_id=$4", connection, transaction); Add(update, tenant.Value, legalEntityId, request.MatchId, reconciliationId); if (await update.ExecuteNonQueryAsync(ct) == 0) throw new BankingException("banking.match_not_found", "The reconciliation match was not found.");
        await using var bump = new NpgsqlCommand("update finance_banking.reconciliation_session set version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 returning id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.tenant_id=$1 and m.legal_entity_id=$2 and m.session_id=$3 and m.state<>'Removed'),version", connection, transaction); Add(bump, tenant.Value, legalEntityId, reconciliationId, clock.GetUtcNow()); await using var reader = await bump.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.reconciliation_not_found", "The reconciliation was not found."); var result = ReadReconciliation(reader); await transaction.CommitAsync(ct); return result;
    }

    public async Task<CashCountDto> CreateCashCountAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateCashCountRequest request, CancellationToken ct)
    {
        if (request.Denominations.Count == 0 || request.Denominations.Any(x => x.UnitValue <= 0 || x.Quantity < 0)) throw new BankingException("banking.cash_count_invalid", "Cash denominations must be valid.");
        var counted = request.Denominations.Sum(x => x.UnitValue * x.Quantity);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); await EnsureCashAccountAsync(connection, transaction, tenant, legalEntityId, accountId, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); await using var count = new NpgsqlCommand("insert into finance_banking.cash_count(tenant_id,legal_entity_id,id,treasury_account_id,cutoff_date,custodian_id,state,expected_amount,counted_amount,difference,version,created_at) values($1,$2,$3,$4,$5,$6,'Submitted',$7,$8,$8-$7,1,$9)", connection, transaction); Add(count, tenant.Value, legalEntityId, id, accountId, request.CutoffDate, request.CustodianId, request.ExpectedAmount, counted, now); await count.ExecuteNonQueryAsync(ct);
        foreach (var denomination in request.Denominations) { await using var line = new NpgsqlCommand("insert into finance_banking.cash_count_denomination(tenant_id,id,cash_count_id,denomination,unit_value,quantity) values($1,$2,$3,$4,$5,$6)", connection, transaction); Add(line, tenant.Value, Guid.CreateVersion7(), id, denomination.Denomination.Trim(), denomination.UnitValue, denomination.Quantity); await line.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct); return (await GetCashCountAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<CashCountDto?> GetCashCountAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,treasury_account_id,cutoff_date,state,expected_amount,counted_amount,difference,custodian_id,version from finance_banking.cash_count where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadCashCount(reader) : null;
    }

    public async Task<CashCountPage> ListCashCountsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.cash_count where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3", connection); Add(count, tenant.Value, legalEntityId, accountId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,cutoff_date,state,expected_amount,counted_amount,difference,custodian_id,version from finance_banking.cash_count where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3 order by cutoff_date desc,id desc offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, accountId, offset, pageSize);
        var items = new List<CashCountDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadCashCount(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<CashCountDto> ChangeCashCountStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct)
    {
        if (target is not ("Submitted" or "Approved" or "Finalized" or "Rejected")) throw new BankingException("banking.cash_count_state_invalid", "The requested cash count state is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); await using var command = new NpgsqlCommand("update finance_banking.cash_count set state=$4,version=version+1,submitted_at=case when $4='Submitted' then $5 else submitted_at end,finalized_at=case when $4='Finalized' then $5 else finalized_at end where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$6 and ((state='Draft' and $4='Submitted') or (state='Submitted' and $4 in ('Approved','Rejected')) or (state='Approved' and $4='Finalized')) returning id", connection, transaction); Add(command, tenant.Value, legalEntityId, id, target, clock.GetUtcNow(), version); if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.cash_count_concurrency", "The cash count is not in a valid state or has changed."); await transaction.CommitAsync(ct); return (await GetCashCountAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<IReadOnlyList<CashPositionDto>> GetCashPositionAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        const string sql = "select a.legal_entity_id,a.id,a.code,a.name,a.account_type,coalesce(p.currency_code,a.currency_code),coalesce(p.book_balance,0),coalesce(p.external_balance,0),coalesce(p.available_balance,p.book_balance,0),coalesce(p.in_transit_amount,0),coalesce(p.balance_basis,'No projection'),coalesce(p.as_of,a.updated_at),p.external_as_of,coalesce(p.as_of,a.updated_at) < now()-interval '24 hours' from finance_banking.treasury_account a left join finance_banking.cash_position_projection p on p.tenant_id=a.tenant_id and p.legal_entity_id=a.legal_entity_id and p.treasury_account_id=a.id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.status='Active' order by a.code";
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value, legalEntityId); var items = new List<CashPositionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), Enum.Parse<TreasuryAccountType>(reader.GetString(4)), reader.IsDBNull(5) ? "" : reader.GetString(5).Trim(), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12), reader.GetBoolean(13))); return items;
    }

    public async Task<StatementDto> ValidateStatementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid statementId, ValidateStatementRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tenant, transaction, ct);
        Guid accountId;
        string currency;
        DateOnly? openingDate;
        decimal? openingBalance;
        decimal? closingBalance;
        string state;
        await using (var statement = new NpgsqlCommand("select treasury_account_id,currency_code,opening_date,opening_balance,closing_balance,processing_state from finance_banking.external_statement where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction))
        {
            Add(statement, tenant.Value, legalEntityId, statementId);
            await using var reader = await statement.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new BankingException("banking.statement_not_found", "The bank statement was not found.");
            accountId = reader.GetGuid(0); currency = reader.GetString(1).Trim(); openingDate = DateValue(reader, 2); openingBalance = DecimalValue(reader, 3); closingBalance = DecimalValue(reader, 4); state = reader.GetString(5);
        }
        if (state is "Superseded" or "Rejected") throw new BankingException("banking.statement_not_validatable", "This statement cannot be validated in its current state.");

        await using (var invalidCurrency = new NpgsqlCommand("select count(*) from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and statement_id=$3 and currency_code<>$4", connection, transaction))
        {
            Add(invalidCurrency, tenant.Value, legalEntityId, statementId, currency);
            if (Convert.ToInt64(await invalidCurrency.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) > 0) throw new BankingException("banking.statement_currency_mismatch", "Statement lines must use the statement currency.");
        }
        if (openingBalance.HasValue && closingBalance.HasValue)
        {
            await using var movement = new NpgsqlCommand("select coalesce(sum(case when direction='Credit' then amount else -amount end),0) from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and statement_id=$3", connection, transaction);
            Add(movement, tenant.Value, legalEntityId, statementId);
            var calculated = Convert.ToDecimal(await movement.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            if (calculated != closingBalance.Value - openingBalance.Value) throw new BankingException("banking.statement_balance_mismatch", "The statement opening, movements, and closing balance do not reconcile.");
        }
        if (request.RequireBalanceContinuity && openingDate.HasValue && openingBalance.HasValue)
        {
            await using var prior = new NpgsqlCommand("select closing_balance from finance_banking.external_statement where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3 and closing_date < $4 and processing_state='Validated' order by closing_date desc,id desc limit 1", connection, transaction);
            Add(prior, tenant.Value, legalEntityId, accountId, openingDate.Value);
            var value = await prior.ExecuteScalarAsync(ct);
            if (value is not null and not DBNull && Convert.ToDecimal(value, CultureInfo.InvariantCulture) != openingBalance.Value) throw new BankingException("banking.statement_continuity_mismatch", "The statement opening balance does not continue from the previous validated statement.");
        }
        var now = clock.GetUtcNow();
        await using (var update = new NpgsqlCommand("update finance_banking.external_statement set processing_state='Validated',version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction))
        { Add(update, tenant.Value, legalEntityId, statementId, now); await update.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct);
        return (await GetStatementAsync(tenant, legalEntityId, statementId, ct))!;
    }

    public async Task<StatementLineDto> ClassifyStatementLineAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid lineId, ClassifyStatementLineRequest request, CancellationToken ct)
    {
        var allowed = new[] { "Unclassified", "Receipt", "Payment", "BankFee", "Interest", "Transfer", "DirectDebit", "ReturnedCheque", "Other" };
        if (!allowed.Contains(request.Classification, StringComparer.Ordinal)) throw new BankingException("banking.statement_classification_invalid", "The statement classification is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("update finance_banking.external_statement_line set classification=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 returning id,statement_id,external_line_id,booking_date,value_date,direction,amount,currency_code,reference,transaction_code,classification,claimed_amount,version", connection);
        Add(command, tenant.Value, legalEntityId, lineId, request.Classification, now);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new BankingException("banking.statement_line_not_found", "The statement line was not found.");
        return ReadStatementLine(reader);
    }

    public async Task<ForecastVersionDto> CreateForecastVersionAsync(TenantId tenant, Guid actorId, CreateForecastVersionRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.HorizonDays is < 1 or > 366) throw new BankingException("banking.forecast_invalid", "Forecast horizon must be between 1 and 366 days.");
        var status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status;
        if (status is not ("Draft" or "Published")) throw new BankingException("banking.forecast_state_invalid", "A forecast can only start as Draft or Published.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("insert into finance_banking.cash_forecast_version(tenant_id,legal_entity_id,id,as_of_date,horizon_days,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8) returning id", connection, transaction);
        Add(command, tenant.Value, request.LegalEntityId, id, request.AsOfDate, request.HorizonDays, status, actorId, now);
        try { await command.ExecuteNonQueryAsync(ct); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new BankingException("banking.forecast_duplicate", "A forecast with the same date and horizon already exists.", ex); }
        await transaction.CommitAsync(ct); return new(id, request.AsOfDate, request.HorizonDays, status, now);
    }

    public async Task<ForecastItemDto> CreateForecastItemAsync(TenantId tenant, Guid actorId, CreateForecastItemRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.ForecastVersionId == Guid.Empty || request.Amount <= 0 || request.Direction is not ("Inflow" or "Outflow") || string.IsNullOrWhiteSpace(request.CurrencyCode) || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.Description)) throw new BankingException("banking.forecast_item_invalid", "Forecast items require a forecast version, positive amount, direction, currency, source, and description.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using (var version = new NpgsqlCommand("select 1 from finance_banking.cash_forecast_version where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft'", connection, transaction))
        {
            Add(version, tenant.Value, request.LegalEntityId, request.ForecastVersionId);
            if (await version.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.forecast_version_not_found", "The draft forecast version was not found.");
        }
        if (request.TreasuryAccountId.HasValue)
        {
            await using var account = new NpgsqlCommand("select 1 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", connection, transaction);
            Add(account, tenant.Value, request.LegalEntityId, request.TreasuryAccountId.Value);
            if (await account.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.forecast_account_not_found", "The forecast account was not found or is inactive.");
        }
        var sourceReference = request.SourceReference is { } source && source.ValueKind != JsonValueKind.Undefined ? JsonSerializer.Serialize(source, JsonOptions) : "{}";
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var insert = new NpgsqlCommand("insert into finance_banking.cash_forecast_item(tenant_id,legal_entity_id,id,forecast_version_id,treasury_account_id,due_date,direction,amount,currency_code,source_type,description,confidence,source_reference) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Estimated',$12::jsonb)", connection, transaction);
        Add(insert, tenant.Value, request.LegalEntityId, id, request.ForecastVersionId, Db(request.TreasuryAccountId), request.DueDate, request.Direction, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.SourceType.Trim(), request.Description.Trim(), sourceReference);
        await insert.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
        return new(id, request.DueDate, request.Direction, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.SourceType.Trim(), request.Description.Trim(), "Estimated", request.TreasuryAccountId);
    }

    public async Task<ForecastPage?> GetForecastAsync(TenantId tenant, Guid legalEntityId, DateOnly? asOfDate, int? horizonDays, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var version = new NpgsqlCommand("select id,as_of_date,horizon_days,status from finance_banking.cash_forecast_version where tenant_id=$1 and legal_entity_id=$2 and ($3::date is null or as_of_date=$3::date) and ($4::integer is null or horizon_days=$4::integer) order by as_of_date desc,id desc limit 1", connection);
        Add(version, tenant.Value, legalEntityId, asOfDate.HasValue ? asOfDate.Value : DBNull.Value, horizonDays.HasValue ? horizonDays.Value : DBNull.Value);
        await using var vr = await version.ExecuteReaderAsync(ct); if (!await vr.ReadAsync(ct)) return null; var id = vr.GetGuid(0); var date = DateOnly.FromDateTime(vr.GetDateTime(1)); var horizon = vr.GetInt32(2); var status = vr.GetString(3); await vr.CloseAsync();
        await using var itemsCommand = new NpgsqlCommand("select id,due_date,direction,amount,currency_code,source_type,description,confidence,treasury_account_id from finance_banking.cash_forecast_item where tenant_id=$1 and legal_entity_id=$2 and forecast_version_id=$3 order by due_date,id", connection); Add(itemsCommand, tenant.Value, legalEntityId, id);
        var items = new List<ForecastItemDto>(); await using var reader = await itemsCommand.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), DateOnly.FromDateTime(reader.GetDateTime(1)), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetString(5), reader.GetString(6), reader.GetString(7), GuidValue(reader, 8))); return new ForecastPage(id, date, horizon, status, items);
    }

    public async Task<AccountChangeRequestDto> CreateAccountChangeRequestAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateAccountChangeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Payload.ValueKind != JsonValueKind.Object) throw new BankingException("banking.account_change_invalid", "A change reason and structured change details are required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var payload = JsonSerializer.Serialize(request.Payload, JsonOptions);
        await using var command = new NpgsqlCommand("insert into finance_banking.treasury_account_change_request(tenant_id,legal_entity_id,id,treasury_account_id,requested_by,status,request_payload,reason,version,created_at) select $1,$2,$3,id,$4,'Pending',$5::jsonb,$6,$7,$8 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$9 and version=$10 returning id", connection);
        Add(command, tenant.Value, legalEntityId, id, actorId, payload, request.Reason.Trim(), 1L, now, accountId, request.Version);
        if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.account_change_concurrency", "The account changed or does not exist.");
        return (await GetAccountChangeRequestAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<AccountChangeRequestDto> DecideAccountChangeRequestAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, ChangeRequestDecisionRequest request, CancellationToken ct)
    {
        if (request.Decision is not ("Approved" or "Rejected")) throw new BankingException("banking.account_change_decision_invalid", "The change request decision is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        Guid accountId; string payload;
        await using (var read = new NpgsqlCommand("select treasury_account_id,request_payload::text from finance_banking.treasury_account_change_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Pending' and version=$4 for update", connection, transaction)) { Add(read, tenant.Value, legalEntityId, id, request.Version); await using var r = await read.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new BankingException("banking.account_change_concurrency", "The change request is no longer pending."); accountId = r.GetGuid(0); payload = r.GetString(1); }
        var now = clock.GetUtcNow();
        if (request.Decision == "Approved")
        {
            using var document = JsonDocument.Parse(payload); var root = document.RootElement; var masked = root.TryGetProperty("maskedIdentifier", out var maskedValue) && maskedValue.ValueKind == JsonValueKind.String ? maskedValue.GetString() : null;
            if (masked is not null && masked.Count(char.IsAsciiDigit) > 4) throw new BankingException("banking.identifier_must_be_masked", "Only a masked bank identifier may be stored.");
            await using var updateAccount = new NpgsqlCommand("update finance_banking.treasury_account set masked_identifier=coalesce($4,masked_identifier),version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(updateAccount, tenant.Value, legalEntityId, accountId, Db(masked), now); await updateAccount.ExecuteNonQueryAsync(ct);
        }
        await using var decision = new NpgsqlCommand("update finance_banking.treasury_account_change_request set status=$4,approved_by=$5,decided_at=$6,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(decision, tenant.Value, legalEntityId, id, request.Decision, actorId, now); await decision.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); return (await GetAccountChangeRequestAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<AccountChangeRequestDto?> GetAccountChangeRequestAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,treasury_account_id,status,request_payload,reason,requested_by,approved_by,version,created_at,decided_at from finance_banking.treasury_account_change_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; using var payload = reader.GetFieldValue<JsonDocument>(3); return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), payload.RootElement.Clone(), reader.GetString(4), reader.GetGuid(5), GuidValue(reader, 6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9));
    }

    public async Task<CashDifferenceResolutionDto> ResolveCashDifferenceAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid cashCountId, ResolveCashDifferenceRequest request, CancellationToken ct)
    {
        if (request.Amount == 0 || string.IsNullOrWhiteSpace(request.ReasonCode) || string.IsNullOrWhiteSpace(request.ResolutionType)) throw new BankingException("banking.cash_difference_invalid", "A non-zero difference resolution with reason is required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var count = new NpgsqlCommand("select difference from finance_banking.cash_count where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4 and state in ('Approved','Finalized') for update", connection, transaction); Add(count, tenant.Value, legalEntityId, cashCountId, request.Version); var difference = await count.ExecuteScalarAsync(ct); if (difference is null) throw new BankingException("banking.cash_difference_concurrency", "The cash count is not approved or has changed."); if (Convert.ToDecimal(difference, CultureInfo.InvariantCulture) != request.Amount) throw new BankingException("banking.cash_difference_amount_mismatch", "The resolution amount must equal the counted difference.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); await using var insert = new NpgsqlCommand("insert into finance_banking.cash_difference_resolution(tenant_id,legal_entity_id,id,cash_count_id,reason_code,resolution_type,amount,evidence_reference,approved_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", connection, transaction); Add(insert, tenant.Value, legalEntityId, id, cashCountId, request.ReasonCode.Trim(), request.ResolutionType.Trim(), request.Amount, Db(request.EvidenceReference), actorId, now); await insert.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); return new(id, cashCountId, request.ReasonCode.Trim(), request.ResolutionType.Trim(), request.Amount, request.EvidenceReference, actorId, now);
    }

    public async Task<CustodyTransferDto> TransferCashCustodyAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, TransferCustodyRequest request, CancellationToken ct)
    {
        if (request.ToCustodianId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason)) throw new BankingException("banking.custody_transfer_invalid", "A custodian and transfer reason are required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); await EnsureCashAccountAsync(connection, transaction, tenant, legalEntityId, accountId, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); await using var command = new NpgsqlCommand("insert into finance_banking.cash_custodian_history(tenant_id,legal_entity_id,id,treasury_account_id,from_custodian_id,to_custodian_id,effective_at,reason,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", connection, transaction); Add(command, tenant.Value, legalEntityId, id, accountId, Db(request.FromCustodianId), request.ToCustodianId, now, request.Reason.Trim(), actorId); await command.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); return new(id, accountId, request.FromCustodianId, request.ToCustodianId, now, request.Reason.Trim(), actorId);
    }

    public async Task<IReadOnlyList<ConnectionDto>> ListConnectionsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,provider_code,connection_ref,status,last_success_at,last_failure_at,failure_code,version from finance_banking.treasury_account_connection where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3 order by provider_code,id", connection); Add(command, tenant.Value, legalEntityId, accountId);
        var result = new List<ConnectionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(ReadConnection(reader)); return result;
    }

    public async Task<ConnectionDto> CreateConnectionAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid accountId, CreateConnectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderCode) || string.IsNullOrWhiteSpace(request.ConnectionRef)) throw new BankingException("banking.connection_invalid", "A provider and connector reference are required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); await EnsureAccountAsync(connection, transaction, tenant, legalEntityId, accountId, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); await using var command = new NpgsqlCommand("insert into finance_banking.treasury_account_connection(tenant_id,legal_entity_id,id,treasury_account_id,provider_code,connection_ref,status,last_success_at,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Connected',$7,1,$7,$7) returning id", connection, transaction); Add(command, tenant.Value, legalEntityId, id, accountId, request.ProviderCode.Trim(), request.ConnectionRef.Trim(), now);
        try { await command.ExecuteNonQueryAsync(ct); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new BankingException("banking.connection_duplicate", "This provider is already connected to the account.", ex); }
        await transaction.CommitAsync(ct); return new(id, accountId, request.ProviderCode.Trim(), request.ConnectionRef.Trim(), "Connected", now, null, null, 1);
    }

    public async Task<ConnectionDto> ChangeConnectionStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct)
    {
        if (target is not ("Connected" or "Disconnected")) throw new BankingException("banking.connection_state_invalid", "The connection state is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("update finance_banking.treasury_account_connection set status=$4,last_success_at=case when $4='Connected' then $5 else last_success_at end,last_failure_at=case when $4='Disconnected' then $5 else last_failure_at end,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$6 returning id,treasury_account_id,provider_code,connection_ref,status,last_success_at,last_failure_at,failure_code,version", connection); Add(command, tenant.Value, legalEntityId, id, target, now, version); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.connection_concurrency", "The connection no longer exists or has changed."); return ReadConnection(reader);
    }

    public async Task<BankOriginatedCandidateDto> CreatePaymentProposalAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid lineId, CreatePaymentProposalRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var evidence = request.Evidence is { } value && value.ValueKind != JsonValueKind.Undefined ? JsonSerializer.Serialize(value, JsonOptions) : "{}";
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("insert into finance_banking.bank_originated_event_candidate(tenant_id,legal_entity_id,id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at) select tenant_id,legal_entity_id,$4,id,'UnknownReceipt','Proposed',amount,currency_code,$5::jsonb,$6 from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and id=$3 and claimed_amount<amount on conflict (tenant_id,statement_line_id,event_type) do nothing returning id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at,reviewed_by,reviewed_at", connection); Add(command, tenant.Value, legalEntityId, lineId, id, evidence, now);
        await using (var reader = await command.ExecuteReaderAsync(ct)) if (await reader.ReadAsync(ct)) return ReadCandidate(reader);
        await using var existing = new NpgsqlCommand("select id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at,reviewed_by,reviewed_at from finance_banking.bank_originated_event_candidate where tenant_id=$1 and legal_entity_id=$2 and statement_line_id=$3 and event_type='UnknownReceipt'", connection); Add(existing, tenant.Value, legalEntityId, lineId); await using var existingReader = await existing.ExecuteReaderAsync(ct); if (!await existingReader.ReadAsync(ct)) throw new BankingException("banking.statement_line_not_found", "The statement line was not found or has already been claimed."); return ReadCandidate(existingReader);
    }

    public async Task RecordTransferProjectionAsync(TenantId tenant, Guid legalEntityId, Guid transferId, Guid sourceAccountId, Guid destinationAccountId, decimal sourceAmount, decimal destinationAmount, string sourceCurrency, string destinationCurrency, string state, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("insert into finance_banking.transfer_reconciliation_projection(tenant_id,legal_entity_id,id,source_account_id,destination_account_id,transfer_reference,state,source_amount,destination_amount,currency_code,in_transit_amount,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,case when $7='InTransit' then $9 else 0 end,$11) on conflict(tenant_id,legal_entity_id,transfer_reference) do update set state=excluded.state,source_amount=excluded.source_amount,destination_amount=excluded.destination_amount,currency_code=excluded.currency_code,in_transit_amount=excluded.in_transit_amount,updated_at=excluded.updated_at", connection); Add(command, tenant.Value, legalEntityId, Guid.CreateVersion7(), sourceAccountId, destinationAccountId, transferId.ToString("D"), state is "Completed" ? "Matched" : "InTransit", sourceAmount, destinationAmount, sourceCurrency == destinationCurrency ? sourceCurrency : null, now); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<BankOriginatedCandidateDto> DecideBankOriginatedCandidateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, BankOriginatedDecisionRequest request, CancellationToken ct)
    {
        if (request.Decision is not ("Approved" or "Rejected")) throw new BankingException("banking.bank_originated_decision_invalid", "The candidate decision is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("update finance_banking.bank_originated_event_candidate set status=$4,reviewed_by=$5,reviewed_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Proposed' returning id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at,reviewed_by,reviewed_at", connection); Add(command, tenant.Value, legalEntityId, id, request.Decision, actorId, now); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.bank_originated_concurrency", "The candidate is no longer pending."); return ReadCandidate(reader);
    }

    public async Task<TransactionCodeMappingDto> CreateTransactionCodeMappingAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateTransactionCodeMappingRequest request, CancellationToken ct)
    {
        var allowed = new[] { "Unclassified", "Receipt", "Payment", "BankFee", "Interest", "Transfer", "DirectDebit", "ReturnedCheque", "Other" }; if (string.IsNullOrWhiteSpace(request.TransactionCode) || !allowed.Contains(request.Classification, StringComparer.Ordinal) || request.EffectiveTo < request.EffectiveFrom) throw new BankingException("banking.mapping_invalid", "The transaction mapping is invalid.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("insert into finance_banking.statement_transaction_code_mapping(tenant_id,legal_entity_id,id,transaction_code,classification,effective_from,effective_to,created_at,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9) returning id,transaction_code,classification,effective_from,effective_to,version", connection); Add(command, tenant.Value, legalEntityId, id, request.TransactionCode.Trim(), request.Classification, request.EffectiveFrom, Db(request.EffectiveTo), now, actorId); try { await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.mapping_invalid", "The mapping could not be created."); return ReadMapping(reader); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new BankingException("banking.mapping_duplicate", "A transaction code mapping already exists for that effective date.", ex); }
    }

    public async Task<IReadOnlyList<TransactionCodeMappingDto>> ListTransactionCodeMappingsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,transaction_code,classification,effective_from,effective_to,version from finance_banking.statement_transaction_code_mapping where tenant_id=$1 and legal_entity_id=$2 order by transaction_code,effective_from desc,id", connection); Add(command, tenant.Value, legalEntityId); var result = new List<TransactionCodeMappingDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(ReadMapping(reader)); return result;
    }

    public async Task<ReconciliationRuleSetDto> CreateReconciliationRuleSetAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRuleSetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Definition.ValueKind != JsonValueKind.Object) throw new BankingException("banking.rule_set_invalid", "A rule set name and structured definition are required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); var id = Guid.CreateVersion7(); var ruleId = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var definition = JsonSerializer.Serialize(request.Definition, JsonOptions);
        await using var set = new NpgsqlCommand("insert into finance_banking.reconciliation_rule_set(tenant_id,legal_entity_id,id,name,status,created_at,updated_at) values($1,$2,$3,$4,'Draft',$5,$5)", connection, transaction); Add(set, tenant.Value, legalEntityId, id, request.Name.Trim(), now); try { await set.ExecuteNonQueryAsync(ct); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new BankingException("banking.rule_set_duplicate", "A rule set with this name already exists.", ex); }
        await using var version = new NpgsqlCommand("insert into finance_banking.reconciliation_rule_version(tenant_id,legal_entity_id,id,rule_set_id,rule_version,priority,definition,created_at,created_by) values($1,$2,$3,$4,1,1,$5::jsonb,$6,$7)", connection, transaction); Add(version, tenant.Value, legalEntityId, ruleId, id, definition, now, actorId); await version.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); return new(id, request.Name.Trim(), "Draft", 1, 1);
    }

    private static async Task EnsureCashAccountAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenant, Guid entity, Guid account, CancellationToken ct) { await using var cmd = new NpgsqlCommand("select 1 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and account_type in ('Cash','PettyCash') and status='Active'", c, t); Add(cmd, tenant.Value, entity, account); if (await cmd.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.cash_account_not_found", "An active cash account is required."); }
    private static async Task InsertProfileAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenant, TreasuryAccountCreateRequest request, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        string sql = request.Type switch { TreasuryAccountType.Bank => "insert into finance_banking.bank_account_profile(tenant_id,legal_entity_id,treasury_account_id,bank_name,account_number_masked,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6)", TreasuryAccountType.Cash or TreasuryAccountType.PettyCash => "insert into finance_banking.cash_account_profile(tenant_id,legal_entity_id,treasury_account_id,location_description,created_at,updated_at) values($1,$2,$3,$4,$5,$5)", TreasuryAccountType.DigitalWallet => "insert into finance_banking.digital_wallet_profile(tenant_id,legal_entity_id,treasury_account_id,provider_name,wallet_reference_masked,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6)", _ => "insert into finance_banking.settlement_account_profile(tenant_id,legal_entity_id,treasury_account_id,provider_code,merchant_reference_masked,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6)" };
        await using var command = new NpgsqlCommand(sql, c, t); Add(command, tenant.Value, request.LegalEntityId, id, request.Name.Trim(), Db(request.MaskedIdentifier), now); await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureAccountAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenant, Guid entity, Guid account, CancellationToken ct)
    { await using var cmd = new NpgsqlCommand("select 1 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status in ('Active','Suspended')", c, t); Add(cmd, tenant.Value, entity, account); if (await cmd.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.account_not_found", "The treasury account is unavailable."); }
    private static async Task SetTenantAsync(NpgsqlConnection c, TenantId tenant, CancellationToken ct) { await using var cmd = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", c); Add(cmd, tenant.Value.ToString("D")); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task SetTenantAsync(NpgsqlConnection c, TenantId tenant, NpgsqlTransaction t, CancellationToken ct) { await using var cmd = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", c, t); Add(cmd, tenant.Value.ToString("D")); await cmd.ExecuteNonQueryAsync(ct); }
    private static void ValidateAccount(TreasuryAccountCreateRequest request) { if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new BankingException("banking.account_invalid", "Account code and name are required."); if (request.Type == TreasuryAccountType.Bank && string.IsNullOrWhiteSpace(request.MaskedIdentifier)) throw new BankingException("banking.bank_identifier_required", "A masked bank identifier is required."); if (request.MaskedIdentifier?.Count(char.IsAsciiDigit) > 4) throw new BankingException("banking.identifier_must_be_masked", "Only a masked bank identifier may be stored."); _ = NormalizeCurrency(request.CurrencyCode, request.CurrencyPolicy); }
    private static string? NormalizeCurrency(string? value, TreasuryCurrencyPolicy policy) { if (policy == TreasuryCurrencyPolicy.MultiCurrency) return null; var currency = value?.Trim().ToUpperInvariant(); if (currency is null || currency.Length != 3 || currency.Any(ch => !char.IsAsciiLetter(ch))) throw new BankingException("banking.currency_invalid", "Select a valid currency."); return currency; }
    private static TreasuryAccountDto ReadAccount(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), GuidValue(r, 2), r.GetString(3).Trim(), r.GetString(4), Enum.Parse<TreasuryAccountType>(r.GetString(5)), Enum.Parse<TreasuryAccountStatus>(r.GetString(6)), Enum.Parse<TreasuryCurrencyPolicy>(r.GetString(7)), StringValue(r, 8), r.GetString(9), r.GetString(10), StringValue(r, 11), !r.IsDBNull(11), r.GetInt64(12), r.GetFieldValue<DateTimeOffset>(13), r.GetFieldValue<DateTimeOffset>(14));
    private static StatementDto ReadStatement(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), DateValue(r, 3), DateValue(r, 4), r.GetString(5).Trim(), r.GetString(6), r.GetString(7), DecimalValue(r, 8), DecimalValue(r, 9), Convert.ToInt32(r.GetInt64(10)), r.GetInt64(11));
    private static StatementLineDto ReadStatementLine(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), DateOnly.FromDateTime(r.GetDateTime(3)), DateValue(r, 4), r.GetString(5), r.GetDecimal(6), r.GetString(7).Trim(), StringValue(r, 8), StringValue(r, 9), r.GetString(10), r.GetDecimal(11), r.GetInt64(12));
    private static ReconciliationDto ReadReconciliation(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), Convert.ToInt32(r.GetInt64(7)), r.GetInt64(8));
    private static ConnectionDto ReadConnection(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6), StringValue(r, 7), r.GetInt64(8));
    private static BankOriginatedCandidateDto ReadCandidate(NpgsqlDataReader r) { using var evidence = r.GetFieldValue<JsonDocument>(6); return new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetString(5).Trim(), evidence.RootElement.Clone(), r.GetFieldValue<DateTimeOffset>(7), GuidValue(r, 8), r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9)); }
    private static TransactionCodeMappingDto ReadMapping(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), DateOnly.FromDateTime(r.GetDateTime(3)), r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)), r.GetInt32(5));
    private static CashCountDto ReadCashCount(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetString(3), r.GetDecimal(4), DecimalValue(r, 5) ?? 0m, DecimalValue(r, 6) ?? 0m, r.GetGuid(7), r.GetInt64(8));
    private static Guid? GuidValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i); private static string? StringValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim(); private static DateOnly? DateValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i)); private static decimal? DecimalValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i); private static object Db(object? value) => value ?? DBNull.Value;
    private static void Add(NpgsqlCommand c, params object?[] values) { foreach (var value in values) c.Parameters.AddWithValue(value ?? DBNull.Value); }
}
