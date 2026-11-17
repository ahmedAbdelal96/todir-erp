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

    public async Task<CashCountDto> ChangeCashCountStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct)
    {
        if (target is not ("Approved" or "Finalized" or "Rejected")) throw new BankingException("banking.cash_count_state_invalid", "The requested cash count state is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); await using var command = new NpgsqlCommand("update finance_banking.cash_count set state=$4,version=version+1,finalized_at=case when $4='Finalized' then $5 else finalized_at end where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$6 and ((state='Submitted' and $4 in ('Approved','Rejected')) or (state='Approved' and $4='Finalized')) returning id", connection, transaction); Add(command, tenant.Value, legalEntityId, id, target, clock.GetUtcNow(), version); if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.cash_count_concurrency", "The cash count is not in a valid state or has changed."); await transaction.CommitAsync(ct); return (await GetCashCountAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<IReadOnlyList<CashPositionDto>> GetCashPositionAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        const string sql = "select a.legal_entity_id,a.id,a.code,a.name,a.account_type,coalesce(p.currency_code,a.currency_code),coalesce(p.book_balance,0),coalesce(p.external_balance,0),coalesce(p.available_balance,p.book_balance,0),coalesce(p.in_transit_amount,0),coalesce(p.balance_basis,'No projection'),coalesce(p.as_of,a.updated_at),p.external_as_of,coalesce(p.as_of,a.updated_at) < now()-interval '24 hours' from finance_banking.treasury_account a left join finance_banking.cash_position_projection p on p.tenant_id=a.tenant_id and p.legal_entity_id=a.legal_entity_id and p.treasury_account_id=a.id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.status='Active' order by a.code";
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value, legalEntityId); var items = new List<CashPositionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), Enum.Parse<TreasuryAccountType>(reader.GetString(4)), reader.IsDBNull(5) ? "" : reader.GetString(5).Trim(), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12), reader.GetBoolean(13))); return items;
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
    private static CashCountDto ReadCashCount(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetString(3), r.GetDecimal(4), DecimalValue(r, 5) ?? 0m, DecimalValue(r, 6) ?? 0m, r.GetGuid(7), r.GetInt64(8));
    private static Guid? GuidValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i); private static string? StringValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim(); private static DateOnly? DateValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i)); private static decimal? DecimalValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i); private static object Db(object? value) => value ?? DBNull.Value;
    private static void Add(NpgsqlCommand c, params object?[] values) { foreach (var value in values) c.Parameters.AddWithValue(value ?? DBNull.Value); }
}
