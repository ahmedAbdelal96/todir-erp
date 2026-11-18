using System.Text.Json;
using System.Globalization;
using Modules.Banking.DTOs;
using Modules.Banking.Entities;
using Npgsql;
using Core.Transactions;
using Shared.Contracts.Banking;
using Shared.Contracts.POS;
using Shared.Contracts.Ledger;
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

    public async Task<TreasuryAccountReference> CreateCanonicalAccountInTransactionAsync(ITransactionContext context, TenantId tenant,
        TreasuryAccountCreateRequest request, CancellationToken ct)
    {
        ValidateAccount(request);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var allowedUses = JsonSerializer.Serialize(request.AllowedUses ?? [], JsonOptions);
        await using (var command = new NpgsqlCommand("insert into finance_banking.treasury_account(tenant_id,legal_entity_id,id,branch_id,code,name,account_type,status,currency_policy,currency_code,purpose,liquidity_class,allowed_uses,masked_identifier,secret_ref,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,'Draft',$8,$9,$10,$11,$12::jsonb,$13,$14,1,$15,$15)", context.Connection, context.Transaction))
        { Add(command, tenant.Value, request.LegalEntityId, id, Db(request.BranchId), request.Code.Trim(), request.Name.Trim(), request.Type.ToString(), request.CurrencyPolicy.ToString(), Db(NormalizeCurrency(request.CurrencyCode, request.CurrencyPolicy)), request.Purpose.Trim(), request.LiquidityClass.Trim(), allowedUses, Db(request.MaskedIdentifier), Db(request.SecretRef), now); await command.ExecuteNonQueryAsync(ct); }
        var currency = NormalizeCurrency(request.CurrencyCode, request.CurrencyPolicy);
        if (currency is not null)
        {
            await using var pocket = new NpgsqlCommand("insert into finance_banking.treasury_account_balance_pocket(tenant_id,legal_entity_id,id,treasury_account_id,currency_code,is_primary,version,created_at,updated_at) values($1,$2,$3,$4,$5,true,1,$6,$6)", context.Connection, context.Transaction);
            Add(pocket, tenant.Value, request.LegalEntityId, Guid.CreateVersion7(), id, currency, now); await pocket.ExecuteNonQueryAsync(ct);
        }
        await InsertProfileAsync(context.Connection, context.Transaction, tenant, request, id, now, ct);
        return new(id, tenant, request.LegalEntityId, request.BranchId, request.Code.Trim(), request.Name.Trim(), request.Type,
            TreasuryAccountStatus.Draft, request.CurrencyPolicy.ToString(), currency);
    }

    public async Task<TreasuryAccountReference> ChangeStatusInTransactionAsync(ITransactionContext context, TenantId tenant,
        Guid legalEntityId, Guid id, TreasuryAccountStatus target, long? expectedVersion, CancellationToken ct)
    {
        if (target is not (TreasuryAccountStatus.Active or TreasuryAccountStatus.Suspended or TreasuryAccountStatus.Closed)) throw new BankingException("banking.account_status_invalid", "The requested treasury account status is not supported.");
        await using var command = new NpgsqlCommand("update finance_banking.treasury_account set status=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and ($6::bigint is null or version=$6::bigint) and status <> 'Closed' returning id,branch_id,code,name,account_type,status,currency_policy,currency_code", context.Connection, context.Transaction);
        Add(command, tenant.Value, legalEntityId, id, target.ToString(), clock.GetUtcNow(), Db(expectedVersion));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new BankingException(expectedVersion is null ? "banking.account_not_found" : "banking.account_concurrency", "The treasury account was not found or has changed.");
        return new(reader.GetGuid(0), tenant, legalEntityId, GuidValue(reader, 1), reader.GetString(2).Trim(), reader.GetString(3),
            Enum.Parse<TreasuryAccountType>(reader.GetString(4)), Enum.Parse<TreasuryAccountStatus>(reader.GetString(5)), reader.GetString(6), StringValue(reader, 7));
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

    public Task<ReconciliationDto> CreateReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, CreateReconciliationRequest request, CancellationToken ct)
    {
        _ = clock;
        ct.ThrowIfCancellationRequested();
        throw new BankingException("banking.ledger_balance_required", "A reconciliation must be created from an authoritative posted Ledger balance.");
    }

    public async Task<ReconciliationDto> CreateReconciliationFromLedgerAsync(TenantId tenant, Guid actorId, Guid legalEntityId,
        CreateReconciliationRequest request, LedgerTreasuryBalanceSnapshot ledgerSnapshot, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var statement = new NpgsqlCommand("select treasury_account_id,currency_code,closing_balance from finance_banking.external_statement where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction); Add(statement, tenant.Value, legalEntityId, request.StatementId);
        await using var reader = await statement.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.statement_not_found", "The statement was not found.");
        var accountId = reader.GetGuid(0); var currency = reader.GetString(1).Trim(); var externalBalance = reader.IsDBNull(2) ? throw new BankingException("banking.statement_closing_balance_required", "A closing statement balance is required for reconciliation.") : reader.GetDecimal(2); await reader.CloseAsync();
        if (accountId != ledgerSnapshot.TreasuryAccountId || !string.Equals(currency, ledgerSnapshot.CurrencyCode, StringComparison.OrdinalIgnoreCase) || ledgerSnapshot.PostedLineCount == 0)
            throw new BankingException("banking.ledger_balance_scope_invalid", "The Ledger balance does not belong to the statement account, currency, or posted cutoff.");
        if (request.BookBalance is { } clientBookBalance && clientBookBalance != ledgerSnapshot.SignedCurrencyBalance)
            throw new BankingException("banking.book_balance_client_supplied", "Book balance is derived from the Ledger and cannot be overridden by the client.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var difference = externalBalance - ledgerSnapshot.SignedCurrencyBalance;
        var snapshotJson = JsonSerializer.Serialize(ledgerSnapshot, JsonOptions);
        await using var command = new NpgsqlCommand("insert into finance_banking.reconciliation_session(tenant_id,legal_entity_id,id,treasury_account_id,statement_id,as_of_date,state,external_balance,book_balance,difference,notes,ledger_source_snapshot,ledger_last_posted_at,ledger_posted_line_count,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Open',$7,$8,$9,$10,$11::jsonb,$12,$13,1,$14,$14)", connection, transaction);
        Add(command, tenant.Value, legalEntityId, id, accountId, request.StatementId, request.AsOfDate, externalBalance, ledgerSnapshot.SignedCurrencyBalance, difference, Db(request.Notes), snapshotJson, Db(ledgerSnapshot.LastPostedAt), ledgerSnapshot.PostedLineCount, now); await command.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
        return (await GetReconciliationAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid accountId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3", connection); Add(count, tenant.Value, legalEntityId, accountId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.session_id=r.id and m.state='Accepted'),version,ledger_source_snapshot->>'sourceReference',ledger_last_posted_at,ledger_posted_line_count from finance_banking.reconciliation_session r where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3 order by as_of_date desc,id desc offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, accountId, offset, pageSize);
        var items = new List<ReconciliationDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadReconciliation(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<ReconciliationPage> ListReconciliationsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2", connection); Add(count, tenant.Value, legalEntityId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.session_id=r.id and m.state='Accepted'),version,ledger_source_snapshot->>'sourceReference',ledger_last_posted_at,ledger_posted_line_count from finance_banking.reconciliation_session r where tenant_id=$1 and legal_entity_id=$2 order by as_of_date desc,id desc offset $3 limit $4", connection); Add(command, tenant.Value, legalEntityId, offset, pageSize);
        var items = new List<ReconciliationDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadReconciliation(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<ReconciliationDto?> GetReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.session_id=r.id and m.state='Accepted'),version,ledger_source_snapshot->>'sourceReference',ledger_last_posted_at,ledger_posted_line_count from finance_banking.reconciliation_session r where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadReconciliation(reader) : null;
    }

    public async Task<(Guid TreasuryAccountId, Guid StatementId, DateOnly AsOfDate, string CurrencyCode)?> GetReconciliationScopeAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select s.treasury_account_id,s.statement_id,s.as_of_date,st.currency_code from finance_banking.reconciliation_session s join finance_banking.external_statement st on st.tenant_id=s.tenant_id and st.legal_entity_id=s.legal_entity_id and st.id=s.statement_id where s.tenant_id=$1 and s.legal_entity_id=$2 and s.id=$3", connection); Add(command, tenant.Value, legalEntityId, id);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetGuid(1), DateOnly.FromDateTime(reader.GetDateTime(2)), reader.GetString(3).Trim()) : null;
    }

    public async Task<ReconciliationDto> AddMatchAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid sessionId, CreateMatchRequest request, CancellationToken ct, ITransactionalPaymentReconciliationReadCapability? paymentReader = null)
    {
        if (request.StatementLineIds.Count == 0 || request.ExternalAmount <= 0 || request.SystemAmount < 0 || request.Difference != request.ExternalAmount - request.SystemAmount || request.SystemAmount > 0 && request.Payments.Count == 0 || request.Payments.Any(payment => payment.PaymentId == Guid.Empty || payment.Amount <= 0) || request.Payments.Select(payment => payment.PaymentId).Distinct().Count() != request.Payments.Count || request.Payments.Sum(payment => payment.Amount) != request.SystemAmount)
            throw new BankingException("banking.match_conservation_invalid", "A match must contain statement lines and conserve the external amount.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        if (paymentReader is null && request.Payments.Count > 0) throw new BankingException("banking.payment_read_capability_missing", "Payment ownership evidence is required for reconciliation.");
        await using var session = new NpgsqlCommand("select statement_id,treasury_account_id from finance_banking.reconciliation_session where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Open' for update", connection, transaction); Add(session, tenant.Value, legalEntityId, sessionId); Guid? sessionStatementId = null; Guid accountId; await using (var sessionReader = await session.ExecuteReaderAsync(ct)) { if (!await sessionReader.ReadAsync(ct)) throw new BankingException("banking.reconciliation_not_open", "The reconciliation session is not open."); sessionStatementId = sessionReader.GetGuid(0); accountId = sessionReader.GetGuid(1); }
        var lineIds = request.StatementLineIds.Distinct().ToArray(); var lineParams = string.Join(',', lineIds.Select((_, i) => $"${i + 4}"));
        await using var lines = new NpgsqlCommand($"select id,amount,claimed_amount,currency_code,direction from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and statement_id=$3 and id in ({lineParams}) for update", connection, transaction); Add(lines, tenant.Value, legalEntityId, sessionStatementId!.Value); Add(lines, lineIds.Cast<object?>().ToArray());
        var lineRows = new List<(Guid Id, decimal Amount, decimal Claimed, string Currency, string Direction)>(); await using (var reader = await lines.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) lineRows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetString(4)));
        if (lineRows.Count != lineIds.Length || lineRows.Sum(x => x.Amount - x.Claimed) < request.ExternalAmount) throw new BankingException("banking.match_exceeds_unclaimed", "The match exceeds the remaining statement amount.");
        if (lineRows.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) throw new BankingException("banking.match_currency_invalid", "A match must use one statement currency.");
        if (lineRows.Select(x => x.Direction).Distinct(StringComparer.Ordinal).Count() != 1) throw new BankingException("banking.match_direction_invalid", "A match must use statement lines with one direction.");
        foreach (var payment in request.Payments)
        {
            var movement = await paymentReader!.GetForReconciliationAsync(new TransactionContextAdapter(connection, transaction), tenant, legalEntityId, payment.PaymentId, ct)
                ?? throw new BankingException("banking.payment_not_found", "The selected payment does not belong to this legal entity.");
            if (movement.BusinessState is not "Completed") throw new BankingException("banking.payment_not_reconcilable", "Only completed, unreversed payments can be matched.");
            if (movement.PaymentAccountId != accountId || !string.Equals(movement.CurrencyCode, lineRows[0].Currency, StringComparison.OrdinalIgnoreCase)) throw new BankingException("banking.payment_scope_invalid", "The payment account or currency does not match the statement.");
            var expectedDirection = lineRows[0].Direction == "Credit" ? "Incoming" : "Outgoing";
            if (movement.Direction != expectedDirection) throw new BankingException("banking.payment_direction_invalid", "The payment direction does not match the statement line.");
            if (payment.Amount > movement.Amount) throw new BankingException("banking.payment_amount_exceeds", "The matched amount exceeds the payment amount.");
            await using var claim = new NpgsqlCommand("insert into finance_banking.reconciliation_system_claim(tenant_id,legal_entity_id,system_reference,claimed_amount,updated_at) values($1,$2,$3,$4,$5) on conflict(tenant_id,legal_entity_id,system_reference) do nothing", connection, transaction); Add(claim, tenant.Value, legalEntityId, payment.PaymentId.ToString("D"), 0m, clock.GetUtcNow()); await claim.ExecuteNonQueryAsync(ct);
            await using var claimLock = new NpgsqlCommand("select claimed_amount from finance_banking.reconciliation_system_claim where tenant_id=$1 and legal_entity_id=$2 and system_reference=$3 for update", connection, transaction); Add(claimLock, tenant.Value, legalEntityId, payment.PaymentId.ToString("D")); var claimed = Convert.ToDecimal(await claimLock.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); if (claimed + payment.Amount > movement.Amount) throw new BankingException("banking.payment_already_claimed", "The payment has already been claimed by another reconciliation.");
            await using var claimUpdate = new NpgsqlCommand("update finance_banking.reconciliation_system_claim set claimed_amount=claimed_amount+$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and system_reference=$3", connection, transaction); Add(claimUpdate, tenant.Value, legalEntityId, payment.PaymentId.ToString("D"), payment.Amount, clock.GetUtcNow()); await claimUpdate.ExecuteNonQueryAsync(ct);
        }
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using var match = new NpgsqlCommand("insert into finance_banking.reconciliation_match(tenant_id,legal_entity_id,id,session_id,source,state,external_amount,system_amount,difference,explanation,created_by) values($1,$2,$3,$4,$5,'Accepted',$6,$7,$8,$9,$10)", connection, transaction); Add(match, tenant.Value, legalEntityId, id, sessionId, request.Source.Trim(), request.ExternalAmount, request.SystemAmount, request.Difference, Db(request.Explanation), actorId); await match.ExecuteNonQueryAsync(ct);
        foreach (var payment in request.Payments)
        {
            await using var systemItem = new NpgsqlCommand("insert into finance_banking.reconciliation_match_system_item(tenant_id,legal_entity_id,id,match_id,system_reference,system_amount) values($1,$2,$3,$4,$5,$6)", connection, transaction);
            Add(systemItem, tenant.Value, legalEntityId, Guid.CreateVersion7(), id, payment.PaymentId.ToString("D"), payment.Amount);
            await systemItem.ExecuteNonQueryAsync(ct);
        }
        var remainingExternal = request.ExternalAmount;
        foreach (var line in lineRows)
        {
            var allocation = Math.Min(line.Amount - line.Claimed, remainingExternal);
            if (allocation <= 0) continue;
            await using var item = new NpgsqlCommand("insert into finance_banking.reconciliation_match_item(tenant_id,legal_entity_id,id,match_id,statement_line_id,external_amount,system_reference) values($1,$2,$3,$4,$5,$6,$7)", connection, transaction); Add(item, tenant.Value, legalEntityId, Guid.CreateVersion7(), id, line.Id, allocation, Db(request.Payments.Count > 0 ? request.Payments[0].PaymentId.ToString("D") : null)); await item.ExecuteNonQueryAsync(ct);
            await using var claim = new NpgsqlCommand("update finance_banking.external_statement_line set claimed_amount=claimed_amount+$4,classification=case when claimed_amount+$4>=amount then 'Other' else classification end,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(claim, tenant.Value, legalEntityId, line.Id, allocation, now); await claim.ExecuteNonQueryAsync(ct);
            remainingExternal -= allocation; if (remainingExternal <= 0) break;
        }
        await transaction.CommitAsync(ct); return (await GetReconciliationAsync(tenant, legalEntityId, sessionId, ct))!;
    }

    public async Task<ReconciliationDto> ChangeReconciliationStateAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, string target, long version, CancellationToken ct)
    {
        if (target is not ("Finalized" or "Reopened")) throw new BankingException("banking.reconciliation_state_invalid", "The requested reconciliation state is not supported.");
        if (target == "Finalized") throw new BankingException("banking.finalization_requires_ledger_validation", "Finalization requires a fresh authoritative Ledger validation.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var command = new NpgsqlCommand("update finance_banking.reconciliation_session set state=$4,version=version+1,finalized_by=case when $4='Finalized' then $5 else null end,finalized_at=case when $4='Finalized' then $6 else null end,updated_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$7 and ((state <> 'Finalized') or (state='Finalized' and $4='Reopened')) returning id", connection, transaction); Add(command, tenant.Value, legalEntityId, id, target, actorId, clock.GetUtcNow(), version); if (await command.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.reconciliation_concurrency", "The reconciliation has changed."); await transaction.CommitAsync(ct); return (await GetReconciliationAsync(tenant, legalEntityId, id, ct))!;
    }

    public async Task<ReconciliationDto> FinalizeReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id,
        long version, LedgerTreasuryBalanceSnapshot ledgerSnapshot, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        Guid accountId; string currency; decimal external; DateOnly asOf; string state;
        await using (var session = new NpgsqlCommand("select s.treasury_account_id,s.as_of_date,s.state,st.currency_code,st.closing_balance from finance_banking.reconciliation_session s join finance_banking.external_statement st on st.tenant_id=s.tenant_id and st.legal_entity_id=s.legal_entity_id and st.id=s.statement_id where s.tenant_id=$1 and s.legal_entity_id=$2 and s.id=$3 and s.version=$4 for update", connection, transaction))
        {
            Add(session, tenant.Value, legalEntityId, id, version); await using var reader = await session.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new BankingException("banking.reconciliation_concurrency", "The reconciliation has changed or does not exist.");
            accountId = reader.GetGuid(0); asOf = DateOnly.FromDateTime(reader.GetDateTime(1)); state = reader.GetString(2); currency = reader.GetString(3).Trim();
            if (reader.IsDBNull(4)) throw new BankingException("banking.statement_closing_balance_required", "A closing statement balance is required before finalization.");
            external = reader.GetDecimal(4);
        }
        if (state is not ("Open" or "Reopened")) throw new BankingException("banking.reconciliation_state_invalid", "Only an open reconciliation can be finalized.");
        if (accountId != ledgerSnapshot.TreasuryAccountId || !string.Equals(currency, ledgerSnapshot.CurrencyCode, StringComparison.OrdinalIgnoreCase) || ledgerSnapshot.AsOfDate != asOf || ledgerSnapshot.PostedLineCount == 0)
            throw new BankingException("banking.ledger_balance_scope_invalid", "The Ledger balance no longer matches the reconciliation scope.");
        await using (var outstanding = new NpgsqlCommand("select coalesce(sum(amount-claimed_amount),0) from finance_banking.external_statement_line l join finance_banking.reconciliation_session s on s.tenant_id=l.tenant_id and s.legal_entity_id=l.legal_entity_id and s.statement_id=l.statement_id where s.tenant_id=$1 and s.legal_entity_id=$2 and s.id=$3", connection, transaction))
        { Add(outstanding, tenant.Value, legalEntityId, id); if (Convert.ToDecimal(await outstanding.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) != 0m) throw new BankingException("banking.reconciliation_outstanding_items", "All statement lines must be claimed before finalization."); }
        await using (var pending = new NpgsqlCommand("select exists(select 1 from finance_banking.reconciliation_adjustment a join finance_banking.reconciliation_session s on s.tenant_id=a.tenant_id and s.legal_entity_id=a.legal_entity_id and s.id=a.session_id where s.tenant_id=$1 and s.legal_entity_id=$2 and s.id=$3 and a.status='Pending')", connection, transaction))
        { Add(pending, tenant.Value, legalEntityId, id); if ((bool?)await pending.ExecuteScalarAsync(ct) == true) throw new BankingException("banking.reconciliation_pending_adjustment", "Pending adjustments must be resolved before finalization."); }
        var difference = external - ledgerSnapshot.SignedCurrencyBalance;
        if (difference != 0m) throw new BankingException("banking.reconciliation_unbalanced", "The external statement does not reconcile to the posted Ledger balance.");
        var now = clock.GetUtcNow(); var snapshotJson = JsonSerializer.Serialize(ledgerSnapshot, JsonOptions);
        await using var update = new NpgsqlCommand("update finance_banking.reconciliation_session set state='Finalized',book_balance=$5,difference=$6,ledger_source_snapshot=$7::jsonb,ledger_last_posted_at=$8,ledger_posted_line_count=$9,finalized_by=$4,finalized_at=$10,version=version+1,updated_at=$10 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$11", connection, transaction);
        Add(update, tenant.Value, legalEntityId, id, actorId, ledgerSnapshot.SignedCurrencyBalance, difference, snapshotJson, Db(ledgerSnapshot.LastPostedAt), ledgerSnapshot.PostedLineCount, now, version); if (await update.ExecuteNonQueryAsync(ct) != 1) throw new BankingException("banking.reconciliation_concurrency", "The reconciliation changed before finalization.");
        await transaction.CommitAsync(ct); return (await GetReconciliationAsync(tenant, legalEntityId, id, ct))!;
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
        var lineClaims = new List<(Guid LineId, decimal Amount)>();
        await using (var matchLines = new NpgsqlCommand("select statement_line_id,external_amount from finance_banking.reconciliation_match_item where tenant_id=$1 and legal_entity_id=$2 and match_id=$3 and statement_line_id is not null", connection, transaction))
        { Add(matchLines, tenant.Value, legalEntityId, request.MatchId); await using var matchReader = await matchLines.ExecuteReaderAsync(ct); while (await matchReader.ReadAsync(ct)) lineClaims.Add((matchReader.GetGuid(0), matchReader.GetDecimal(1))); }
        foreach (var claim in lineClaims)
        {
            await using var restore = new NpgsqlCommand("update finance_banking.external_statement_line set claimed_amount=claimed_amount-$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and claimed_amount >= $4", connection, transaction); Add(restore, tenant.Value, legalEntityId, claim.LineId, claim.Amount, clock.GetUtcNow()); if (await restore.ExecuteNonQueryAsync(ct) != 1) throw new BankingException("banking.match_claim_integrity", "The statement claim could not be safely released.");
        }
        var systemClaims = new List<(string Reference, decimal Amount)>();
        await using (var matchSystems = new NpgsqlCommand("select system_reference,system_amount from finance_banking.reconciliation_match_system_item where tenant_id=$1 and legal_entity_id=$2 and match_id=$3", connection, transaction))
        { Add(matchSystems, tenant.Value, legalEntityId, request.MatchId); await using var systemReader = await matchSystems.ExecuteReaderAsync(ct); while (await systemReader.ReadAsync(ct)) systemClaims.Add((systemReader.GetString(0), systemReader.GetDecimal(1))); }
        foreach (var claim in systemClaims)
        {
            await using var restore = new NpgsqlCommand("update finance_banking.reconciliation_system_claim set claimed_amount=claimed_amount-$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and system_reference=$3 and claimed_amount >= $4", connection, transaction); Add(restore, tenant.Value, legalEntityId, claim.Reference, claim.Amount, clock.GetUtcNow()); if (await restore.ExecuteNonQueryAsync(ct) != 1) throw new BankingException("banking.payment_claim_integrity", "The payment claim could not be safely released.");
        }
        await using var update = new NpgsqlCommand("update finance_banking.reconciliation_match set state='Removed' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and session_id=$4", connection, transaction); Add(update, tenant.Value, legalEntityId, request.MatchId, reconciliationId); if (await update.ExecuteNonQueryAsync(ct) == 0) throw new BankingException("banking.match_not_found", "The reconciliation match was not found.");
        await using var bump = new NpgsqlCommand("update finance_banking.reconciliation_session set version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 returning id,treasury_account_id,as_of_date,state,external_balance,book_balance,difference,(select count(*) from finance_banking.reconciliation_match m where m.tenant_id=$1 and m.legal_entity_id=$2 and m.session_id=$3 and m.state<>'Removed'),version,ledger_source_snapshot->>'sourceReference',ledger_last_posted_at,ledger_posted_line_count", connection, transaction); Add(bump, tenant.Value, legalEntityId, reconciliationId, clock.GetUtcNow()); await using var reader = await bump.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.reconciliation_not_found", "The reconciliation was not found."); var result = ReadReconciliation(reader); await transaction.CommitAsync(ct); return result;
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
        const string sql = "select a.legal_entity_id,a.id,a.code,a.name,a.account_type,coalesce(p.currency_code,a.currency_code),p.book_balance,p.external_balance,p.available_balance,coalesce(p.in_transit_amount,0),coalesce(p.balance_basis,'No projection'),p.as_of,p.external_as_of,(p.as_of is null or p.as_of < now()-interval '24 hours') from finance_banking.treasury_account a left join finance_banking.cash_position_projection p on p.tenant_id=a.tenant_id and p.legal_entity_id=a.legal_entity_id and p.treasury_account_id=a.id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.status='Active' order by a.code";
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value, legalEntityId); var items = new List<CashPositionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), Enum.Parse<TreasuryAccountType>(reader.GetString(4)), reader.IsDBNull(5) ? "" : reader.GetString(5).Trim(), DecimalValue(reader, 6), DecimalValue(reader, 7), DecimalValue(reader, 8), reader.GetDecimal(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12), reader.GetBoolean(13))); return items;
    }

    public async Task<BankActivityPage> ListBankActivityAsync(TenantId tenant, Guid legalEntityId, Guid? treasuryAccountId, string? classification, string? direction, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        const string scope = "l.tenant_id=$1 and l.legal_entity_id=$2 and ($3::uuid is null or l.treasury_account_id=$3) and ($4::text is null or l.classification=$4) and ($5::text is null or l.direction=$5) and ($6::date is null or l.booking_date >= $6) and ($7::date is null or l.booking_date <= $7)";
        await using var count = new NpgsqlCommand($"select count(*) from finance_banking.external_statement_line l where {scope}", connection); Add(count, tenant.Value, legalEntityId, Db(treasuryAccountId), Db(classification), Db(direction), Db(fromDate), Db(toDate)); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select l.id,l.statement_id,l.treasury_account_id,l.booking_date,l.direction,l.amount,l.currency_code,l.reference,l.transaction_code,l.classification,l.claimed_amount,l.amount-l.claimed_amount,l.version from finance_banking.external_statement_line l where {scope} order by l.booking_date desc,l.id desc offset $8 limit $9", connection); Add(command, tenant.Value, legalEntityId, Db(treasuryAccountId), Db(classification), Db(direction), Db(fromDate), Db(toDate), (page - 1) * pageSize, pageSize);
        var items = new List<BankActivityDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6).Trim(), StringValue(reader, 7), StringValue(reader, 8), reader.GetString(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetInt64(12))); return new(items, page, pageSize, (page - 1) * pageSize + items.Count < total, total);
    }

    public async Task<(decimal? BookBalance, decimal? ExternalBalance, decimal InTransitAmount, DateTimeOffset? AsOf, string BalanceBasis)> GetCashToLedgerBaseBalanceAsync(TenantId tenant, Guid legalEntityId, string baseCurrencyCode, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select sum(book_balance),sum(external_balance),coalesce(sum(in_transit_amount),0),max(as_of),coalesce(max(balance_basis),'No projection') from finance_banking.cash_position_projection where tenant_id=$1 and legal_entity_id=$2 and currency_code=$3", connection); Add(command, tenant.Value, legalEntityId, baseCurrencyCode.Trim().ToUpperInvariant()); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return (null, null, 0m, null, "No projection"); return (DecimalValue(reader, 0), DecimalValue(reader, 1), reader.GetDecimal(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4));
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

    public async Task<ForecastPage?> GetForecastVersionAsync(TenantId tenant, Guid legalEntityId, Guid versionId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select as_of_date,horizon_days from finance_banking.cash_forecast_version where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, versionId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; var asOf = DateOnly.FromDateTime(reader.GetDateTime(0)); var horizon = reader.GetInt32(1); await reader.CloseAsync(); return await GetForecastAsync(tenant, legalEntityId, asOf, horizon, ct);
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
        await using var command = new NpgsqlCommand("insert into finance_banking.bank_originated_event_candidate(tenant_id,legal_entity_id,id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at) select tenant_id,legal_entity_id,$4,id,case when classification='BankFee' then 'BankFee' when classification='Interest' then case when direction='Credit' then 'InterestIncome' else 'InterestCharge' end when classification='DirectDebit' then 'DirectDebit' else 'UnknownReceipt' end,'Proposed',amount,currency_code,$5::jsonb,$6 from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and id=$3 and claimed_amount<amount on conflict (tenant_id,statement_line_id,event_type) do nothing returning id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at,reviewed_by,reviewed_at", connection); Add(command, tenant.Value, legalEntityId, lineId, id, evidence, now);
        await using (var reader = await command.ExecuteReaderAsync(ct)) if (await reader.ReadAsync(ct)) return ReadCandidate(reader);
        await using var existing = new NpgsqlCommand("select id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at,reviewed_by,reviewed_at from finance_banking.bank_originated_event_candidate where tenant_id=$1 and legal_entity_id=$2 and statement_line_id=$3 order by proposed_at desc limit 1", connection); Add(existing, tenant.Value, legalEntityId, lineId); await using var existingReader = await existing.ExecuteReaderAsync(ct); if (!await existingReader.ReadAsync(ct)) throw new BankingException("banking.statement_line_not_found", "The statement line was not found or has already been claimed."); return ReadCandidate(existingReader);
    }

    public async Task<ExternalFeedTransactionDto> RecordExternalFeedTransactionAsync(TenantId tenant, ExternalFeedTransactionReceivedV1 request, CancellationToken ct)
    {
        var sourceType = request.SourceType?.Trim();
        var externalId = request.ExternalTransactionId?.Trim();
        var contentHash = request.ContentHash?.Trim().ToLowerInvariant();
        var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
        var direction = request.Direction?.Trim();
        var idempotencyKey = request.IdempotencyKey?.Trim();
        if (request.LegalEntityId == Guid.Empty || request.TreasuryAccountId == Guid.Empty || string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 64 || contentHash.Any(ch => !char.IsAsciiHexDigit(ch)) || request.BookingDate == default || direction is not ("Credit" or "Debit") || request.Amount <= 0 || currency is null || currency.Length != 3 || currency.Any(ch => !char.IsAsciiLetter(ch)) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new BankingException("banking.feed_invalid", "The external feed transaction is invalid.");
        if (request.RawPayload is null || request.RawPayload.TrimStart() is not ['{', ..])
            throw new BankingException("banking.feed_payload_invalid", "The external feed payload must be a JSON object.");

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tenant, transaction, ct);
        await EnsureAccountAsync(connection, transaction, tenant, request.LegalEntityId, request.TreasuryAccountId, ct);
        var now = clock.GetUtcNow();
        await using (var existing = new NpgsqlCommand("select id,treasury_account_id,source_type,external_transaction_id,content_hash,booking_date,direction,amount,currency_code,status,idempotency_key,created_at,updated_at from finance_banking.external_feed_transaction where tenant_id=$1 and legal_entity_id=$2 and treasury_account_id=$3 and source_type=$4 and external_transaction_id=$5", connection, transaction))
        {
            Add(existing, tenant.Value, request.LegalEntityId, request.TreasuryAccountId, sourceType, externalId);
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var existingHash = reader.GetString(4).Trim();
                if (!string.Equals(existingHash, contentHash, StringComparison.Ordinal)) throw new BankingException("banking.feed_conflict", "The external transaction id was already received with different evidence.");
                var result = ReadExternalFeedTransaction(reader);
                await reader.CloseAsync();
                await transaction.CommitAsync(ct);
                return result;
            }
        }

        var id = Guid.CreateVersion7();
        try
        {
            await using var insert = new NpgsqlCommand("insert into finance_banking.external_feed_transaction(tenant_id,legal_entity_id,id,treasury_account_id,source_type,external_transaction_id,content_hash,booking_date,direction,amount,currency_code,status,idempotency_key,raw_payload,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Projected',$12,$13::jsonb,$14,$14)", connection, transaction);
            Add(insert, tenant.Value, request.LegalEntityId, id, request.TreasuryAccountId, sourceType, externalId, contentHash, request.BookingDate, direction, request.Amount, currency, idempotencyKey, request.RawPayload, now);
            await insert.ExecuteNonQueryAsync(ct);
            await using var project = new NpgsqlCommand("insert into finance_banking.treasury_activity_projection(tenant_id,legal_entity_id,id,treasury_account_id,activity_type,source_reference,business_date,direction,amount,currency_code,status,source_watermark,created_at) values($1,$2,$3,$4,'ExternalFeed',$5,$6,$7,$8,$9,'Projected',$10,$10) on conflict(tenant_id,treasury_account_id,source_reference) do nothing", connection, transaction);
            Add(project, tenant.Value, request.LegalEntityId, Guid.CreateVersion7(), request.TreasuryAccountId, $"feed:{sourceType}:{externalId}", request.BookingDate, direction, request.Amount, currency, now);
            await project.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new BankingException("banking.feed_conflict", "The external feed transaction conflicts with an existing transaction.", ex);
        }
        await transaction.CommitAsync(ct);
        return new(id, request.TreasuryAccountId, sourceType, externalId, contentHash, request.BookingDate, direction, request.Amount, currency, "Projected", idempotencyKey, now, now);
    }

    public async Task ProjectPosSessionClosedAsync(TenantId tenant, PosSessionClosedV1 eventData, Guid sourceMessageId, CancellationToken ct)
    {
        if (eventData.LegalEntityId == Guid.Empty || eventData.RegisterId == Guid.Empty || eventData.SessionId == Guid.Empty || eventData.BranchId == Guid.Empty || eventData.ExpectedCash < 0 || eventData.CountedCash < 0 || eventData.CurrencyCode?.Trim().Length != 3)
            throw new BankingException("banking.pos_settlement_invalid", "The POS session close evidence is invalid.");
        var currency = eventData.CurrencyCode.Trim().ToUpperInvariant();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tenant, transaction, ct);
        await using (var existing = new NpgsqlCommand("select legal_entity_id,register_id,branch_id,business_date,currency_code,expected_cash,counted_cash,variance,source_message_id from finance_banking.pos_cash_settlement_projection where tenant_id=$1 and pos_session_id=$2", connection, transaction))
        {
            Add(existing, tenant.Value, eventData.SessionId);
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var same = reader.GetGuid(0) == eventData.LegalEntityId && reader.GetGuid(1) == eventData.RegisterId && reader.GetGuid(2) == eventData.BranchId && reader.GetFieldValue<DateOnly>(3) == eventData.BusinessDate && reader.GetString(4).Trim() == currency && reader.GetDecimal(5) == eventData.ExpectedCash && reader.GetDecimal(6) == eventData.CountedCash && reader.GetDecimal(7) == eventData.Variance;
                await reader.CloseAsync();
                if (!same) throw new BankingException("banking.pos_settlement_conflict", "The POS session already has different close evidence.");
                await transaction.CommitAsync(ct);
                return;
            }
        }
        var now = clock.GetUtcNow();
        await using var insert = new NpgsqlCommand("insert into finance_banking.pos_cash_settlement_projection(tenant_id,legal_entity_id,id,pos_session_id,register_id,branch_id,business_date,currency_code,expected_cash,counted_cash,variance,settlement_status,source_message_id,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Pending',$12,$13,$13)", connection, transaction);
        Add(insert, tenant.Value, eventData.LegalEntityId, Guid.CreateVersion7(), eventData.SessionId, eventData.RegisterId, eventData.BranchId!.Value, eventData.BusinessDate, currency, eventData.ExpectedCash, eventData.CountedCash, eventData.Variance, sourceMessageId, now);
        try { await insert.ExecuteNonQueryAsync(ct); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new BankingException("banking.pos_settlement_conflict", "The POS session close evidence conflicts with an existing settlement.", ex); }
        await transaction.CommitAsync(ct);
    }

    public async Task RecordProviderSettlementAsync(TenantId tenant, ProviderSettlementImportedV1 settlement, Guid sourceMessageId, CancellationToken ct)
    {
        var currency = settlement.CurrencyCode?.Trim().ToUpperInvariant();
        if (settlement.LegalEntityId == Guid.Empty || settlement.SettlementAccountId == Guid.Empty || string.IsNullOrWhiteSpace(settlement.ProviderCode) || string.IsNullOrWhiteSpace(settlement.ProviderReference) || string.IsNullOrWhiteSpace(settlement.ExternalReference) || settlement.PeriodFrom > settlement.PeriodTo || settlement.GrossAmount < 0 || settlement.FeesAmount < 0 || settlement.NetPayoutAmount < 0 || currency?.Length != 3 || currency.Any(ch => !char.IsAsciiLetter(ch)) || settlement.GrossAmount - settlement.FeesAmount + settlement.AdjustmentsAmount != settlement.NetPayoutAmount)
            throw new BankingException("banking.provider_settlement_invalid", "The provider settlement evidence is invalid.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tenant, transaction, ct);
        await EnsureAccountAsync(connection, transaction, tenant, settlement.LegalEntityId, settlement.SettlementAccountId, ct);
        await using (var accountType = new NpgsqlCommand("select 1 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and account_type='ProviderSettlement'", connection, transaction))
        {
            Add(accountType, tenant.Value, settlement.LegalEntityId, settlement.SettlementAccountId);
            if (await accountType.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.provider_settlement_account_invalid", "A provider settlement account is required.");
        }
        var now = clock.GetUtcNow();
        await using (var existing = new NpgsqlCommand("select legal_entity_id,settlement_account_id,provider_code,period_from,period_to,currency_code,gross_amount,fees_amount,adjustments_amount,net_payout_amount,external_reference,source_message_id from finance_banking.provider_settlement_projection where tenant_id=$1 and provider_reference=$2", connection, transaction))
        {
            Add(existing, tenant.Value, settlement.ProviderReference.Trim());
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var same = reader.GetGuid(0) == settlement.LegalEntityId && reader.GetGuid(1) == settlement.SettlementAccountId && reader.GetString(2) == settlement.ProviderCode.Trim() && reader.GetFieldValue<DateOnly>(3) == settlement.PeriodFrom && reader.GetFieldValue<DateOnly>(4) == settlement.PeriodTo && reader.GetString(5).Trim() == currency && reader.GetDecimal(6) == settlement.GrossAmount && reader.GetDecimal(7) == settlement.FeesAmount && reader.GetDecimal(8) == settlement.AdjustmentsAmount && reader.GetDecimal(9) == settlement.NetPayoutAmount && reader.GetString(10) == settlement.ExternalReference.Trim();
                await reader.CloseAsync();
                if (!same) throw new BankingException("banking.provider_settlement_conflict", "The provider settlement reference has different evidence.");
                await transaction.CommitAsync(ct);
                return;
            }
        }
        await using var insert = new NpgsqlCommand("insert into finance_banking.provider_settlement_projection(tenant_id,legal_entity_id,id,settlement_account_id,provider_code,provider_reference,period_from,period_to,currency_code,gross_amount,fees_amount,adjustments_amount,net_payout_amount,external_reference,source_message_id,status,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,'Imported',$16,$16)", connection, transaction);
        Add(insert, tenant.Value, settlement.LegalEntityId, Guid.CreateVersion7(), settlement.SettlementAccountId, settlement.ProviderCode.Trim(), settlement.ProviderReference.Trim(), settlement.PeriodFrom, settlement.PeriodTo, currency, settlement.GrossAmount, settlement.FeesAmount, settlement.AdjustmentsAmount, settlement.NetPayoutAmount, settlement.ExternalReference.Trim(), sourceMessageId, now);
        try { await insert.ExecuteNonQueryAsync(ct); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new BankingException("banking.provider_settlement_conflict", "The provider settlement evidence conflicts with an existing settlement.", ex); }
        await using var aggregate = new NpgsqlCommand("insert into finance_banking.settlement_account_projection(tenant_id,legal_entity_id,treasury_account_id,currency_code,gross_pending,fees_pending,net_expected,as_of) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict(tenant_id,treasury_account_id,currency_code) do update set gross_pending=finance_banking.settlement_account_projection.gross_pending+$5,fees_pending=finance_banking.settlement_account_projection.fees_pending+$6,net_expected=finance_banking.settlement_account_projection.net_expected+$7,as_of=$8", connection, transaction);
        Add(aggregate, tenant.Value, settlement.LegalEntityId, settlement.SettlementAccountId, currency, settlement.GrossAmount, settlement.FeesAmount, settlement.NetPayoutAmount, now);
        await aggregate.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PosCashSettlementPage> ListPosCashSettlementsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, string? status, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        var offset = (page - 1) * pageSize;
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.pos_cash_settlement_projection where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or settlement_status=$3)", connection); Add(count, tenant.Value, legalEntityId, Db(status)); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,pos_session_id,register_id,branch_id,business_date,currency_code,expected_cash,counted_cash,variance,settlement_status,treasury_account_id,version,created_at,updated_at from finance_banking.pos_cash_settlement_projection where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or settlement_status=$3) order by business_date desc,id offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, Db(status), offset, pageSize); var items = new List<PosCashSettlementDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadPosSettlement(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<PosCashSettlementDto> ConfirmPosCashSettlementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, ConfirmPosCashSettlementRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var load = new NpgsqlCommand("select id,pos_session_id,register_id,branch_id,business_date,currency_code,expected_cash,counted_cash,variance,settlement_status,treasury_account_id,version,created_at,updated_at from finance_banking.pos_cash_settlement_projection where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction); Add(load, tenant.Value, legalEntityId, id); await using var reader = await load.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.pos_settlement_not_found", "The POS cash settlement was not found."); var current = ReadPosSettlement(reader); await reader.CloseAsync();
        if (current.Status != "Pending" || current.Version != request.Version) throw new BankingException("banking.pos_settlement_concurrency", "The POS settlement is no longer pending or has changed.");
        await using var account = new NpgsqlCommand("select 1 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and account_type in ('Cash','PettyCash') and status='Active' and (currency_policy='MultiCurrency' or currency_code=$4)", connection, transaction); Add(account, tenant.Value, legalEntityId, request.TreasuryAccountId, current.CurrencyCode); if (await account.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.pos_settlement_account_invalid", "An active cash account in the settlement currency is required.");
        var now = clock.GetUtcNow(); await using var update = new NpgsqlCommand("update finance_banking.pos_cash_settlement_projection set settlement_status='Confirmed',treasury_account_id=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$6 returning id,pos_session_id,register_id,branch_id,business_date,currency_code,expected_cash,counted_cash,variance,settlement_status,treasury_account_id,version,created_at,updated_at", connection, transaction); Add(update, tenant.Value, legalEntityId, id, request.TreasuryAccountId, now, request.Version); await using var updated = await update.ExecuteReaderAsync(ct); if (!await updated.ReadAsync(ct)) throw new BankingException("banking.pos_settlement_concurrency", "The POS settlement changed while it was being confirmed."); var result = ReadPosSettlement(updated); await updated.CloseAsync();
        await using var projection = new NpgsqlCommand("insert into finance_banking.treasury_activity_projection(tenant_id,legal_entity_id,id,treasury_account_id,activity_type,source_reference,business_date,direction,amount,currency_code,status,source_watermark,created_at) values($1,$2,$3,$4,'PosCashSettlement',$5,$6,'Credit',$7,$8,'Projected',$9,$9) on conflict(tenant_id,treasury_account_id,source_reference) do nothing", connection, transaction); Add(projection, tenant.Value, legalEntityId, Guid.CreateVersion7(), request.TreasuryAccountId, $"pos-session:{current.PosSessionId:D}", current.BusinessDate, current.CountedCash, current.CurrencyCode, now); await projection.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task<ProviderSettlementPage> ListProviderSettlementsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, string? status, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100); await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var offset = (page - 1) * pageSize;
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.provider_settlement_projection where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3)", connection); Add(count, tenant.Value, legalEntityId, Db(status)); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,settlement_account_id,provider_code,provider_reference,period_from,period_to,currency_code,gross_amount,fees_amount,adjustments_amount,net_payout_amount,external_reference,status,statement_line_id,version,created_at,updated_at from finance_banking.provider_settlement_projection where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) order by period_to desc,id offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, Db(status), offset, pageSize); var items = new List<ProviderSettlementDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadProviderSettlement(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<ProviderSettlementDto> MatchProviderSettlementAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, MatchProviderSettlementRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var load = new NpgsqlCommand("select id,settlement_account_id,provider_code,provider_reference,period_from,period_to,currency_code,gross_amount,fees_amount,adjustments_amount,net_payout_amount,external_reference,status,statement_line_id,version,created_at,updated_at from finance_banking.provider_settlement_projection where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction); Add(load, tenant.Value, legalEntityId, id); await using var reader = await load.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.provider_settlement_not_found", "The provider settlement was not found."); var current = ReadProviderSettlement(reader); await reader.CloseAsync(); if (current.Status == "Matched" && current.StatementLineId == request.StatementLineId) return current; if (current.Version != request.Version || current.Status is not ("Imported" or "Exception")) throw new BankingException("banking.provider_settlement_concurrency", "The provider settlement is no longer available for matching.");
        await using var line = new NpgsqlCommand("select treasury_account_id,direction,amount,currency_code,claimed_amount from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction); Add(line, tenant.Value, legalEntityId, request.StatementLineId); await using var lineReader = await line.ExecuteReaderAsync(ct); if (!await lineReader.ReadAsync(ct)) throw new BankingException("banking.statement_line_not_found", "The statement line was not found."); var lineAccount = lineReader.GetGuid(0); var direction = lineReader.GetString(1); var lineAmount = lineReader.GetDecimal(2); var lineCurrency = lineReader.GetString(3).Trim(); var claimed = lineReader.GetDecimal(4); await lineReader.CloseAsync(); if (lineCurrency != current.CurrencyCode || lineAmount - claimed < current.NetPayoutAmount) throw new BankingException("banking.provider_settlement_amount_mismatch", "The statement line cannot cover the provider payout in the same currency.");
        var now = clock.GetUtcNow(); await using var claim = new NpgsqlCommand("update finance_banking.external_statement_line set claimed_amount=claimed_amount+$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(claim, tenant.Value, legalEntityId, request.StatementLineId, current.NetPayoutAmount, now); await claim.ExecuteNonQueryAsync(ct);
        await using var update = new NpgsqlCommand("update finance_banking.provider_settlement_projection set status='Matched',statement_line_id=$4,matched_at=$5,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$6 returning id,settlement_account_id,provider_code,provider_reference,period_from,period_to,currency_code,gross_amount,fees_amount,adjustments_amount,net_payout_amount,external_reference,status,statement_line_id,version,created_at,updated_at", connection, transaction); Add(update, tenant.Value, legalEntityId, id, request.StatementLineId, now, request.Version); await using var updated = await update.ExecuteReaderAsync(ct); if (!await updated.ReadAsync(ct)) throw new BankingException("banking.provider_settlement_concurrency", "The provider settlement changed while it was being matched."); var result = ReadProviderSettlement(updated); await updated.CloseAsync();
        await using var projection = new NpgsqlCommand("insert into finance_banking.treasury_activity_projection(tenant_id,legal_entity_id,id,treasury_account_id,activity_type,source_reference,business_date,direction,amount,currency_code,status,source_watermark,created_at) select $1,$2,$3,l.treasury_account_id,'ProviderSettlement',$4,l.booking_date,l.direction,$5,l.currency_code,'Reconciled',$6,$6 from finance_banking.external_statement_line l where l.tenant_id=$1 and l.legal_entity_id=$2 and l.id=$7 on conflict(tenant_id,treasury_account_id,source_reference) do nothing", connection, transaction); Add(projection, tenant.Value, legalEntityId, Guid.CreateVersion7(), $"provider-settlement:{id:D}", current.NetPayoutAmount, now, request.StatementLineId); await projection.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task RecordTransferProjectionAsync(TenantId tenant, Guid legalEntityId, Guid transferId, Guid sourceAccountId, Guid destinationAccountId, decimal sourceAmount, decimal destinationAmount, string sourceCurrency, string destinationCurrency, string state, string? baseCurrencyCode, string? fxSnapshot, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("insert into finance_banking.transfer_reconciliation_projection(tenant_id,legal_entity_id,id,source_account_id,destination_account_id,transfer_reference,state,source_amount,destination_amount,currency_code,source_currency_code,destination_currency_code,base_currency_code,fx_snapshot,in_transit_amount,version,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14::jsonb,case when $7='InTransit' then $9 else 0 end,1,$15) on conflict(tenant_id,legal_entity_id,transfer_reference) do update set state=excluded.state,source_amount=excluded.source_amount,destination_amount=excluded.destination_amount,currency_code=excluded.currency_code,source_currency_code=excluded.source_currency_code,destination_currency_code=excluded.destination_currency_code,base_currency_code=excluded.base_currency_code,fx_snapshot=excluded.fx_snapshot,in_transit_amount=excluded.in_transit_amount,version=finance_banking.transfer_reconciliation_projection.version+1,updated_at=excluded.updated_at", connection); Add(command, tenant.Value, legalEntityId, Guid.CreateVersion7(), sourceAccountId, destinationAccountId, transferId.ToString("D"), state is "Completed" ? "Matched" : "InTransit", sourceAmount, destinationAmount, sourceCurrency == destinationCurrency ? sourceCurrency : null, sourceCurrency, destinationCurrency, Db(baseCurrencyCode), Db(fxSnapshot), now); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<TransferReconciliationPage> ListTransferReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid? accountId, string? state, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var count = new NpgsqlCommand("select count(*) from finance_banking.transfer_reconciliation_projection where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or source_account_id=$3 or destination_account_id=$3) and ($4::text is null or state=$4)", connection); Add(count, tenant.Value, legalEntityId, Db(accountId), Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,transfer_reference,source_account_id,destination_account_id,state,source_amount,coalesce(source_currency_code,currency_code),destination_amount,coalesce(destination_currency_code,currency_code),base_currency_code,fx_snapshot,in_transit_amount,source_evidence,destination_evidence,version,updated_at from finance_banking.transfer_reconciliation_projection where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or source_account_id=$3 or destination_account_id=$3) and ($4::text is null or state=$4) order by updated_at desc,id offset $5 limit $6", connection); Add(command, tenant.Value, legalEntityId, Db(accountId), Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), offset, pageSize); var items = new List<TransferReconciliationDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadTransferReconciliation(reader)); return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<TransferReconciliationDto> ReconcileTransferAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, ReconcileTransferRequest request, CancellationToken ct)
    {
        if (request.SourceStatementLineId is null && request.DestinationStatementLineId is null) throw new BankingException("banking.transfer_evidence_required", "At least one external transfer statement line is required.");
        if (request.SourceStatementLineId is { } sourceId && request.DestinationStatementLineId == sourceId) throw new BankingException("banking.transfer_evidence_duplicate", "Source and destination evidence must be different statement lines.");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct);
        await using var projection = new NpgsqlCommand("select id,transfer_reference,source_account_id,destination_account_id,state,source_amount,coalesce(source_currency_code,currency_code),destination_amount,coalesce(destination_currency_code,currency_code),base_currency_code,fx_snapshot,in_transit_amount,source_evidence,destination_evidence,version,updated_at from finance_banking.transfer_reconciliation_projection where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", connection, transaction); Add(projection, tenant.Value, legalEntityId, id); await using var projectionReader = await projection.ExecuteReaderAsync(ct); if (!await projectionReader.ReadAsync(ct)) throw new BankingException("banking.transfer_not_found", "The internal transfer was not found."); var current = ReadTransferReconciliation(projectionReader); await projectionReader.CloseAsync();
        if (current.Version != request.Version || current.State == "Matched") throw new BankingException("banking.transfer_concurrency", "The internal transfer has already changed or been reconciled.");
        var source = request.SourceStatementLineId is { } sourceLineId ? await LoadTransferEvidenceAsync(connection, transaction, tenant, legalEntityId, sourceLineId, "Debit", current.SourceAccountId, current.SourceAmount, current.SourceCurrencyCode, ct) : null;
        var destination = request.DestinationStatementLineId is { } destinationLineId ? await LoadTransferEvidenceAsync(connection, transaction, tenant, legalEntityId, destinationLineId, "Credit", current.DestinationAccountId, current.DestinationAmount, current.DestinationCurrencyCode, ct) : null;
        var now = clock.GetUtcNow();
        if (source is { } sourceEvidence) await ClaimTransferEvidenceAsync(connection, transaction, tenant, legalEntityId, sourceEvidence.Id, current.SourceAmount, now, ct);
        if (destination is { } destinationEvidence) await ClaimTransferEvidenceAsync(connection, transaction, tenant, legalEntityId, destinationEvidence.Id, current.DestinationAmount, now, ct);
        var matched = (source is not null || current.SourceEvidence is not null) && (destination is not null || current.DestinationEvidence is not null); var newState = matched ? "Matched" : "InTransit";
        var sourceText = source is null ? current.SourceEvidence : $"statement-line:{source.Id:D}"; var destinationText = destination is null ? current.DestinationEvidence : $"statement-line:{destination.Id:D}";
        await using var update = new NpgsqlCommand("update finance_banking.transfer_reconciliation_projection set state=$4,source_evidence=$5,destination_evidence=$6,in_transit_amount=$7,version=version+1,updated_at=$8 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$9", connection, transaction); Add(update, tenant.Value, legalEntityId, id, newState, Db(sourceText), Db(destinationText), matched ? 0m : current.DestinationAmount, now, current.Version); if (await update.ExecuteNonQueryAsync(ct) != 1) throw new BankingException("banking.transfer_concurrency", "The internal transfer changed during reconciliation."); await transaction.CommitAsync(ct);
        return current with { State = newState, InTransitAmount = matched ? 0m : current.DestinationAmount, SourceEvidence = sourceText, DestinationEvidence = destinationText, Version = current.Version + 1, UpdatedAt = now };
    }

    public async Task<BankOriginatedCandidateDto> DecideBankOriginatedCandidateAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, Guid id, BankOriginatedDecisionRequest request, CancellationToken ct)
    {
        if (request.Decision is not ("Approved" or "Rejected")) throw new BankingException("banking.bank_originated_decision_invalid", "The candidate decision is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tenant, transaction, ct);
        var now = clock.GetUtcNow();
        BankOriginatedCandidateDto result;
        await using (var command = new NpgsqlCommand("update finance_banking.bank_originated_event_candidate set status=$4,reviewed_by=$5,reviewed_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Proposed' returning id,statement_line_id,event_type,status,amount,currency_code,evidence,proposed_at,reviewed_by,reviewed_at", connection, transaction))
        {
            Add(command, tenant.Value, legalEntityId, id, request.Decision, actorId, now);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new BankingException("banking.bank_originated_concurrency", "The candidate is no longer pending.");
            result = ReadCandidate(reader);
        }
        if (request.Decision == "Approved")
        {
            await using var line = new NpgsqlCommand("select treasury_account_id,direction,booking_date from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(line, tenant.Value, legalEntityId, result.StatementLineId); await using var lineReader = await line.ExecuteReaderAsync(ct); if (!await lineReader.ReadAsync(ct)) throw new BankingException("banking.statement_line_not_found", "The source statement line is no longer available."); var treasuryAccountId = lineReader.GetGuid(0); var direction = lineReader.GetString(1); var bookingDate = lineReader.GetFieldValue<DateOnly>(2); await lineReader.CloseAsync();
            var approved = new BankOriginatedFinancialEventApprovedV1(legalEntityId, result.Id, result.StatementLineId, treasuryAccountId, actorId, result.EventType, direction, result.Amount, result.CurrencyCode, bookingDate, result.Evidence.GetRawText(), membershipId);
            var payload = JsonSerializer.Serialize(approved, JsonOptions);
            await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status) values($1,'BankOriginatedFinancialEventApprovedV1',$2::jsonb,$3,$4,$5,$6,'banking',null,'Pending')", connection, transaction);
            Add(outbox, Guid.CreateVersion7(), payload, tenant.Value, now, id, id);
            await outbox.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return result;
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

    public async Task<OpeningReconciliationDto> CreateOpeningReconciliationAsync(TenantId tenant, Guid actorId, CreateOpeningReconciliationRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.TreasuryAccountId == Guid.Empty || request.LedgerOpeningBatchId == Guid.Empty || request.OpeningAsOfDate == default || request.OutstandingInTransitAmount < 0 || string.IsNullOrWhiteSpace(request.SourceSystem) || string.IsNullOrWhiteSpace(request.BatchId))
            throw new BankingException("banking.opening_invalid", "Opening reconciliation requires an account, posted Ledger batch, balances, source, and batch identity.");
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(ch => !char.IsAsciiLetter(ch))) throw new BankingException("banking.currency_invalid", "Select a valid opening currency.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tenant, transaction, ct);
        await EnsureAccountAsync(connection, transaction, tenant, request.LegalEntityId, request.TreasuryAccountId, ct);
        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        const string sql = """
            insert into finance_banking.opening_reconciliation
                (tenant_id,legal_entity_id,id,treasury_account_id,ledger_opening_batch_id,opening_as_of_date,currency_code,book_opening_balance,external_opening_balance,outstanding_in_transit_amount,difference,source_system,legacy_account_id,batch_id,status,created_by,created_at,version)
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$9-$8-$10,$11,$12,$13,'Draft',$14,$15,1)
            on conflict (tenant_id,legal_entity_id,treasury_account_id,ledger_opening_batch_id) do update set
                opening_as_of_date=excluded.opening_as_of_date,currency_code=excluded.currency_code,book_opening_balance=excluded.book_opening_balance,
                external_opening_balance=excluded.external_opening_balance,outstanding_in_transit_amount=excluded.outstanding_in_transit_amount,
                difference=excluded.difference,source_system=excluded.source_system,legacy_account_id=excluded.legacy_account_id,batch_id=excluded.batch_id,
                updated_at=excluded.created_at,version=finance_banking.opening_reconciliation.version+1
            returning id,treasury_account_id,ledger_opening_batch_id,opening_as_of_date,currency_code,book_opening_balance,external_opening_balance,outstanding_in_transit_amount,difference,source_system,legacy_account_id,batch_id,status,version,created_at,finalized_at
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        Add(command, tenant.Value, request.LegalEntityId, id, request.TreasuryAccountId, request.LedgerOpeningBatchId, request.OpeningAsOfDate, currency, request.BookOpeningBalance, request.ExternalOpeningBalance, request.OutstandingInTransitAmount, request.SourceSystem.Trim(), Db(request.LegacyAccountId?.Trim()), request.BatchId.Trim(), actorId, now);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new BankingException("banking.opening_invalid", "The opening reconciliation could not be created.");
        var result = ReadOpeningReconciliation(reader);
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<OpeningReconciliationPage> ListOpeningReconciliationsAsync(TenantId tenant, Guid legalEntityId, Guid? treasuryAccountId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 10_000); pageSize = Math.Clamp(pageSize, 1, 200); var offset = (page - 1) * pageSize;
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        const string baseSql = " from finance_banking.opening_reconciliation where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or treasury_account_id=$3)";
        await using var count = new NpgsqlCommand("select count(*)" + baseSql, connection); Add(count, tenant.Value, legalEntityId, Db(treasuryAccountId)); var total = Convert.ToInt32((long)(await count.ExecuteScalarAsync(ct) ?? 0L));
        await using var command = new NpgsqlCommand("select id,treasury_account_id,ledger_opening_batch_id,opening_as_of_date,currency_code,book_opening_balance,external_opening_balance,outstanding_in_transit_amount,difference,source_system,legacy_account_id,batch_id,status,version,created_at,finalized_at" + baseSql + " order by opening_as_of_date desc,id desc offset $4 limit $5", connection); Add(command, tenant.Value, legalEntityId, Db(treasuryAccountId), offset, pageSize);
        var items = new List<OpeningReconciliationDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadOpeningReconciliation(reader));
        return new(items, page, pageSize, offset + items.Count < total, total);
    }

    public async Task<OpeningReconciliationDto?> GetOpeningReconciliationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,treasury_account_id,ledger_opening_batch_id,opening_as_of_date,currency_code,book_opening_balance,external_opening_balance,outstanding_in_transit_amount,difference,source_system,legacy_account_id,batch_id,status,version,created_at,finalized_at from finance_banking.opening_reconciliation where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadOpeningReconciliation(reader) : null;
    }

    public async Task<OpeningReconciliationDto> FinalizeOpeningReconciliationAsync(TenantId tenant, Guid actorId, Guid legalEntityId, Guid id, FinalizeOpeningReconciliationRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tenant, transaction, ct); var now = clock.GetUtcNow();
        await using var command = new NpgsqlCommand("update finance_banking.opening_reconciliation set status='Finalized',finalized_by=$4,finalized_at=$5,updated_at=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$6 and status='Draft' returning id,treasury_account_id,ledger_opening_batch_id,opening_as_of_date,currency_code,book_opening_balance,external_opening_balance,outstanding_in_transit_amount,difference,source_system,legacy_account_id,batch_id,status,version,created_at,finalized_at", connection, transaction); Add(command, tenant.Value, legalEntityId, id, actorId, now, request.Version);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new BankingException("banking.opening_concurrency", "The opening reconciliation changed or is already finalized."); var result = ReadOpeningReconciliation(reader); await reader.CloseAsync(); await transaction.CommitAsync(ct); return result;
    }

    private static async Task EnsureCashAccountAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenant, Guid entity, Guid account, CancellationToken ct) { await using var cmd = new NpgsqlCommand("select 1 from finance_banking.treasury_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and account_type in ('Cash','PettyCash') and status='Active'", c, t); Add(cmd, tenant.Value, entity, account); if (await cmd.ExecuteScalarAsync(ct) is null) throw new BankingException("banking.cash_account_not_found", "An active cash account is required."); }
    private static async Task InsertProfileAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenant, TreasuryAccountCreateRequest request, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        if (request.Type is TreasuryAccountType.Cash or TreasuryAccountType.PettyCash)
        {
            await using var cashCommand = new NpgsqlCommand("insert into finance_banking.cash_account_profile(tenant_id,legal_entity_id,treasury_account_id,location_description,created_at,updated_at) values($1,$2,$3,$4,$5,$5)", c, t);
            Add(cashCommand, tenant.Value, request.LegalEntityId, id, request.Name.Trim(), now);
            await cashCommand.ExecuteNonQueryAsync(ct);
            return;
        }

        var sql = request.Type switch
        {
            TreasuryAccountType.Bank => "insert into finance_banking.bank_account_profile(tenant_id,legal_entity_id,treasury_account_id,bank_name,account_number_masked,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6)",
            TreasuryAccountType.DigitalWallet => "insert into finance_banking.digital_wallet_profile(tenant_id,legal_entity_id,treasury_account_id,provider_name,wallet_reference_masked,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6)",
            _ => "insert into finance_banking.settlement_account_profile(tenant_id,legal_entity_id,treasury_account_id,provider_code,merchant_reference_masked,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$6)"
        };
        await using var command = new NpgsqlCommand(sql, c, t);
        Add(command, tenant.Value, request.LegalEntityId, id, request.Name.Trim(), Db(request.MaskedIdentifier), now);
        await command.ExecuteNonQueryAsync(ct);
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
    private static ReconciliationDto ReadReconciliation(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetString(3), r.GetDecimal(4), DecimalValue(r, 5), DecimalValue(r, 6), Convert.ToInt32(r.GetInt64(7)), r.GetInt64(8), StringValue(r, 9), r.IsDBNull(10) ? null : r.GetFieldValue<DateTimeOffset>(10), r.GetInt64(11));
    private static ConnectionDto ReadConnection(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6), StringValue(r, 7), r.GetInt64(8));
    private static BankOriginatedCandidateDto ReadCandidate(NpgsqlDataReader r) { using var evidence = r.GetFieldValue<JsonDocument>(6); return new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetString(5).Trim(), evidence.RootElement.Clone(), r.GetFieldValue<DateTimeOffset>(7), GuidValue(r, 8), r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9)); }
    private static ExternalFeedTransactionDto ReadExternalFeedTransaction(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4).Trim(), DateOnly.FromDateTime(r.GetDateTime(5)), r.GetString(6), r.GetDecimal(7), r.GetString(8).Trim(), r.GetString(9), r.GetString(10), r.GetFieldValue<DateTimeOffset>(11), r.GetFieldValue<DateTimeOffset>(12));
    private static TransactionCodeMappingDto ReadMapping(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), DateOnly.FromDateTime(r.GetDateTime(3)), r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)), r.GetInt32(5));
    private static CashCountDto ReadCashCount(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetString(3), r.GetDecimal(4), DecimalValue(r, 5) ?? 0m, DecimalValue(r, 6) ?? 0m, r.GetGuid(7), r.GetInt64(8));
    private static PosCashSettlementDto ReadPosSettlement(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), DateOnly.FromDateTime(r.GetDateTime(4)), r.GetString(5).Trim(), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetString(9), GuidValue(r, 10), r.GetInt64(11), r.GetFieldValue<DateTimeOffset>(12), r.GetFieldValue<DateTimeOffset>(13));
    private static ProviderSettlementDto ReadProviderSettlement(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), DateOnly.FromDateTime(r.GetDateTime(4)), DateOnly.FromDateTime(r.GetDateTime(5)), r.GetString(6).Trim(), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetString(11), r.GetString(12), GuidValue(r, 13), r.GetInt64(14), r.GetFieldValue<DateTimeOffset>(15), r.GetFieldValue<DateTimeOffset>(16));
    private static TransferReconciliationDto ReadTransferReconciliation(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetGuid(3), r.GetString(4), r.GetDecimal(5), StringValue(r, 6), r.GetDecimal(7), StringValue(r, 8), StringValue(r, 9), StringValue(r, 10), r.GetDecimal(11), StringValue(r, 12), StringValue(r, 13), r.GetInt64(14), r.GetFieldValue<DateTimeOffset>(15));
    private static OpeningReconciliationDto ReadOpeningReconciliation(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), DateOnly.FromDateTime(r.GetDateTime(3)), r.GetString(4).Trim(), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetString(9), StringValue(r, 10), r.GetString(11), r.GetString(12), r.GetInt64(13), r.GetFieldValue<DateTimeOffset>(14), r.IsDBNull(15) ? null : r.GetFieldValue<DateTimeOffset>(15));
    private sealed record TransferEvidence(Guid Id);
    private static async Task<TransferEvidence?> LoadTransferEvidenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, Guid lineId, string direction, Guid accountId, decimal requiredAmount, string? currencyCode, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id from finance_banking.external_statement_line where tenant_id=$1 and legal_entity_id=$2 and id=$3 and treasury_account_id=$4 and direction=$5 and currency_code=$6 and amount-claimed_amount >= $7 for update", connection, transaction); Add(command, tenant.Value, legalEntityId, lineId, accountId, direction, currencyCode, requiredAmount); return await command.ExecuteScalarAsync(ct) is Guid id ? new TransferEvidence(id) : throw new BankingException("banking.transfer_evidence_invalid", "The external statement line does not match the transfer account, direction, currency, or remaining amount.");
    }
    private static async Task ClaimTransferEvidenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, Guid lineId, decimal amount, DateTimeOffset now, CancellationToken ct)
    { await using var command = new NpgsqlCommand("update finance_banking.external_statement_line set claimed_amount=claimed_amount+$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and amount-claimed_amount >= $4", connection, transaction); Add(command, tenant.Value, legalEntityId, lineId, amount, now); if (await command.ExecuteNonQueryAsync(ct) != 1) throw new BankingException("banking.transfer_evidence_concurrency", "The external transfer evidence changed before it could be claimed."); }
    private static Guid? GuidValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i); private static string? StringValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim(); private static DateOnly? DateValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i)); private static decimal? DecimalValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i); private static object Db(object? value) => value ?? DBNull.Value;
    private static void Add(NpgsqlCommand c, params object?[] values) { foreach (var value in values) c.Parameters.AddWithValue(value ?? DBNull.Value); }

    private sealed class TransactionContextAdapter(NpgsqlConnection connection, NpgsqlTransaction transaction) : ITransactionContext
    {
        public NpgsqlConnection Connection => connection;
        public NpgsqlTransaction Transaction => transaction;
        public void Enlist(NpgsqlCommand command) => command.Transaction = transaction;
        public void AddOutbox(OutboxMessage message) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
