using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Core.Transactions;
using Core.Services;
using Shared.Contracts.Ledger;
using Modules.Subledger.Entities;
using Modules.Subledger.Events;
using Npgsql;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Modules.Subledger.Repositories;

public sealed class SubledgerRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, TimeProvider clock, ITransactionalLedgerPostingCapability? ledgerPosting = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(Guid PartyId, string Currency, string Direction)?> GetOpenItemWriteOffContextAsync(TenantId tenant, LegalEntityId entity, Guid openItemId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select party_id,transaction_currency,direction from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        Add(command, tenant.Value, entity.Value, openItemId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetString(1).Trim(), reader.GetString(2)) : null;
    }

    public async Task<PartyFinancialProfileResult?> GetProfileAsync(TenantId tenant, LegalEntityId entity, Guid partyId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,party_id,legal_entity_id,default_receivable_payment_term_id,default_payable_payment_term_id,credit_limit_amount,credit_limit_currency,receivable_policy_code,payable_policy_code,version,updated_at from finance_subledger.party_financial_profile where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and status='Active'", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(partyId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadProfile(reader);
    }

    public Task<PartyFinancialProfileResult> SaveProfileAsync(TenantId tenant, PartyFinancialProfileCommand request, decimal? creditLimit, string? creditCurrency, CancellationToken ct, bool creditLimitChanged = false) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var now = clock.GetUtcNow();
            if (request.ExpectedVersion == 0)
            {
                var id = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into finance_subledger.party_financial_profile(tenant_id,legal_entity_id,id,party_id,default_receivable_payment_term_id,default_payable_payment_term_id,credit_limit_amount,credit_limit_currency,receivable_policy_code,payable_policy_code,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Active',1,$11,$11) on conflict(tenant_id,legal_entity_id,party_id) do nothing", context.Connection, context.Transaction);
                Add(insert, tenant.Value, request.LegalEntityId.Value, id, request.PartyId, Db(request.DefaultReceivablePaymentTermId), Db(request.DefaultPayablePaymentTermId), Db(creditLimit), Db(creditCurrency), Db(Clean(request.ReceivablePolicyCode)), Db(Clean(request.PayablePolicyCode)), now);
                if (await insert.ExecuteNonQueryAsync(token) == 0) throw new SubledgerException("subledger.profile_changed", "The financial profile already exists or changed. Refresh and try again.");
            }
            else
            {
                await using var update = new NpgsqlCommand("update finance_subledger.party_financial_profile set default_receivable_payment_term_id=$4,default_payable_payment_term_id=$5,credit_limit_amount=$6,credit_limit_currency=$7,receivable_policy_code=$8,payable_policy_code=$9,version=version+1,updated_at=$10 where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and version=$11 and status='Active' returning id", context.Connection, context.Transaction);
                Add(update, tenant.Value, request.LegalEntityId.Value, request.PartyId, Db(request.DefaultReceivablePaymentTermId), Db(request.DefaultPayablePaymentTermId), Db(creditLimit), Db(creditCurrency), Db(Clean(request.ReceivablePolicyCode)), Db(Clean(request.PayablePolicyCode)), now, request.ExpectedVersion);
                if (await update.ExecuteScalarAsync(token) is null) throw new SubledgerException("subledger.profile_changed", "The financial profile changed. Refresh and try again.");
            }
            await using var query = new NpgsqlCommand("select id,party_id,legal_entity_id,default_receivable_payment_term_id,default_payable_payment_term_id,credit_limit_amount,credit_limit_currency,receivable_policy_code,payable_policy_code,version,updated_at from finance_subledger.party_financial_profile where tenant_id=$1 and legal_entity_id=$2 and party_id=$3", context.Connection, context.Transaction);
            Add(query, tenant.Value, request.LegalEntityId.Value, request.PartyId);
            PartyFinancialProfileResult result;
            await using (var reader = await query.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.profile_changed", "The financial profile could not be loaded after update.");
                result = ReadProfile(reader);
            }
            var correlationId = request.CorrelationId == Guid.Empty ? Guid.CreateVersion7() : request.CorrelationId;
            EnqueueEvent(context, tenant, SubledgerEventTypes.PartyFinancialProfileChangedV1,
                new PartyFinancialProfileChangedEventV1(result.Id, request.LegalEntityId.Value, request.PartyId, result.Version), result.Id, correlationId, result.UpdatedAt);
            if (creditLimitChanged)
                EnqueueEvent(context, tenant, SubledgerEventTypes.CreditLimitChangedV1,
                    new CreditLimitChangedEventV1(result.Id, request.LegalEntityId.Value, request.PartyId, result.Version), result.Id, correlationId, result.UpdatedAt);
            if (request.ActorId != Guid.Empty)
                await AuditAsync(context, tenant, request.LegalEntityId, request.ActorId, null, "party_financial_profile_changed", "party_financial_profile", result.Id,
                    JsonSerializer.Serialize(new { expectedVersion = request.ExpectedVersion }), JsonSerializer.Serialize(new { version = result.Version, creditLimitChanged }), null, correlationId, result.UpdatedAt, token);
            return result;
        }, ct);

    public Task<WriteOffPolicyResult> SaveWriteOffPolicyAsync(TenantId tenant, WriteOffPolicyCommand command, decimal maximumAmount, decimal approvalThreshold, string currency, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var now = clock.GetUtcNow();
            if (command.ExpectedVersion == 0)
            {
                await using var insert = new NpgsqlCommand("insert into finance_subledger.write_off_policy(tenant_id,legal_entity_id,id,direction,currency_code,maximum_amount,approval_threshold,automatic_write_off_enabled,version,updated_at) values($1,$2,$3,$4,$5,$6,$7,false,1,$8) on conflict(tenant_id,legal_entity_id,direction,currency_code) do nothing", context.Connection, context.Transaction);
                Add(insert, tenant.Value, command.LegalEntityId.Value, Guid.CreateVersion7(), command.Direction.ToString(), currency, maximumAmount, approvalThreshold, now);
                if (await insert.ExecuteNonQueryAsync(token) == 0) throw new SubledgerException("subledger.writeoff_policy_changed", "A write-off policy already exists. Refresh and try again.");
            }
            else
            {
                await using var update = new NpgsqlCommand("update finance_subledger.write_off_policy set maximum_amount=$5,approval_threshold=$6,version=version+1,updated_at=$7 where tenant_id=$1 and legal_entity_id=$2 and direction=$3 and currency_code=$4 and version=$8 returning id", context.Connection, context.Transaction);
                Add(update, tenant.Value, command.LegalEntityId.Value, command.Direction.ToString(), currency, maximumAmount, approvalThreshold, now, command.ExpectedVersion);
                if (await update.ExecuteScalarAsync(token) is null) throw new SubledgerException("subledger.writeoff_policy_changed", "The write-off policy changed. Refresh and try again.");
            }
            await using var query = new NpgsqlCommand("select id,legal_entity_id,direction,currency_code,maximum_amount,approval_threshold,automatic_write_off_enabled,version,updated_at from finance_subledger.write_off_policy where tenant_id=$1 and legal_entity_id=$2 and direction=$3 and currency_code=$4", context.Connection, context.Transaction);
            Add(query, tenant.Value, command.LegalEntityId.Value, command.Direction.ToString(), currency);
            WriteOffPolicyResult result;
            await using (var reader = await query.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.writeoff_policy_changed", "The write-off policy could not be loaded.");
                result = ReadWriteOffPolicy(reader);
            }
            if (command.ActorId != Guid.Empty)
                await AuditAsync(context, tenant, command.LegalEntityId, command.ActorId, null, "writeoff_policy_changed", "write_off_policy", result.Id,
                    JsonSerializer.Serialize(new { expectedVersion = command.ExpectedVersion }), JsonSerializer.Serialize(new { result.Version, result.CurrencyCode }), null,
                    command.CorrelationId == Guid.Empty ? Guid.CreateVersion7() : command.CorrelationId, now, token);
            return result;
        }, ct);

    public Task<Guid> CreateWriteOffRequestAsync(TenantId tenant, CreateWriteOffCommand request, decimal amount, string currency, short roundingScale, string roundingMode, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var hash = Hash(new { request.OpenItemId, Amount = amount, Currency = currency, ReasonCode = request.ReasonCode.Trim(), ReasonText = request.ReasonText.Trim(), request.EffectiveDate, roundingScale, roundingMode });
            await using (var existing = new NpgsqlCommand("select id,payload_hash from finance_subledger.write_off_request where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, request.LegalEntityId.Value, request.IdempotencyKey.Trim());
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (!string.Equals(reader.GetString(1).Trim(), hash, StringComparison.Ordinal)) throw new SubledgerException("subledger.writeoff_idempotency_conflict", "This write-off request key was already used with different financial details.");
                    return reader.GetGuid(0);
                }
            }
            var items = await LockItemsAsync(context, tenant, request.LegalEntityId, [request.OpenItemId], token);
            if (!items.TryGetValue(request.OpenItemId, out var item)) throw new SubledgerException("subledger.open_item_not_found", "The open item was not found in this legal entity.");
            if (item.Status is not ("Open" or "PartiallySettled") || amount > item.OpenAmount) throw new SubledgerException("subledger.writeoff_amount_exceeds_open", "The write-off amount exceeds the current open balance.");
            await using var policy = new NpgsqlCommand("select id,version,maximum_amount,approval_threshold from finance_subledger.write_off_policy where tenant_id=$1 and legal_entity_id=$2 and direction=$3 and currency_code=$4 for share", context.Connection, context.Transaction);
            Add(policy, tenant.Value, request.LegalEntityId.Value, item.Direction, currency);
            Guid policyId; long policyVersion; decimal maximum; decimal approvalThreshold;
            await using (var reader = await policy.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.writeoff_policy_missing", "A write-off policy must be configured for this direction and currency.");
                policyId = reader.GetGuid(0); policyVersion = reader.GetInt64(1); maximum = reader.GetDecimal(2); approvalThreshold = reader.GetDecimal(3);
            }
            if (amount > maximum) throw new SubledgerException("subledger.writeoff_policy_limit_exceeded", "The requested write-off exceeds the configured policy limit.");
            var requestId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await using var insert = new NpgsqlCommand("insert into finance_subledger.write_off_request(tenant_id,legal_entity_id,id,policy_id,policy_version,approval_threshold,rounding_scale,rounding_mode,open_item_id,party_id,direction,amount,currency_code,reason_code,reason_text,effective_date,idempotency_key,payload_hash,created_by,status,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,'PendingApproval',$20,$20)", context.Connection, context.Transaction);
            Add(insert, tenant.Value, request.LegalEntityId.Value, requestId, policyId, policyVersion, approvalThreshold, roundingScale, roundingMode,
                item.Id, item.PartyId, item.Direction, amount, currency, request.ReasonCode.Trim(), request.ReasonText.Trim(), request.EffectiveDate,
                request.IdempotencyKey.Trim(), hash, request.ActorId, now);
            await insert.ExecuteNonQueryAsync(token);
            await WriteWriteOffAuditAsync(context, tenant, request.LegalEntityId, requestId, "Requested", request.ActorId,
                JsonSerializer.Serialize(new { request.OpenItemId, amount, currency, request.ReasonCode, request.EffectiveDate, policyId, policyVersion }), now, token);
            return requestId;
        }, ct);

    public Task<WriteOffResult> ApproveWriteOffAsync(TenantId tenant, ApproveWriteOffCommand request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var load = new NpgsqlCommand("select id,open_item_id,party_id,direction,amount,currency_code,reason_code,reason_text,effective_date,created_by,status,rounding_scale,rounding_mode from finance_subledger.write_off_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
            Add(load, tenant.Value, request.LegalEntityId.Value, request.RequestId);
            Guid requestId; Guid itemId; Guid partyId; string direction; decimal amount; string currency; string reasonCode; string reasonText; DateOnly effectiveDate; Guid createdBy; string status; short roundingScale; string roundingMode;
            await using (var reader = await load.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.writeoff_request_not_found", "The write-off request was not found.");
                requestId = reader.GetGuid(0); itemId = reader.GetGuid(1); partyId = reader.GetGuid(2); direction = reader.GetString(3); amount = reader.GetDecimal(4);
                currency = reader.GetString(5).Trim(); reasonCode = reader.GetString(6); reasonText = reader.GetString(7); effectiveDate = reader.GetFieldValue<DateOnly>(8);
                createdBy = reader.GetGuid(9); status = reader.GetString(10); roundingScale = reader.GetInt16(11); roundingMode = reader.GetString(12);
            }
            if (status == "Approved") return await ReadWriteOffResultAsync(context, tenant, request.LegalEntityId, requestId, true, token);
            if (status != "PendingApproval") throw new SubledgerException("subledger.writeoff_transition_invalid", "Only a pending write-off request can be approved.");
            if (createdBy == request.ActorId) throw new SubledgerException("subledger.writeoff_sod_violation", "A write-off creator cannot approve their own request.");
            var items = await LockItemsAsync(context, tenant, request.LegalEntityId, [itemId], token);
            if (!items.TryGetValue(itemId, out var item) || item.PartyId != partyId || item.Direction != direction || item.Currency != currency)
                throw new SubledgerException("subledger.writeoff_context_changed", "The open item no longer matches the write-off request.");
            if (item.Status is not ("Open" or "PartiallySettled") || amount > item.OpenAmount) throw new SubledgerException("subledger.writeoff_amount_exceeds_open", "The open balance changed and no longer covers this write-off.");
            if (!Enum.TryParse<Shared.Primitives.Financial.FinancialRoundingModeValue>(roundingMode, out var mode)) throw new SubledgerException("subledger.financial_policy_unavailable", "The captured carrying-value rounding rule is invalid.");
            var baseAmount = BasePortion(item.OpenAmount, item.OpenBase, amount, roundingScale, mode);
            if (baseAmount <= 0m) throw new SubledgerException("subledger.writeoff_base_amount_invalid", "A posted write-off must have a positive base-currency carrying amount.");
            if (ledgerPosting is null) throw new SubledgerException("subledger.ledger_capability_unavailable", "Ledger write-off posting is unavailable; the request remains pending.");
            var writeOffId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            var payload = new SettlementWriteOffPayload(writeOffId, partyId, itemId, direction, amount.ToString(CultureInfo.InvariantCulture), currency,
                baseAmount, item.BaseCurrency, reasonCode, reasonText, ParseConfigurationId(item.ControlConfiguration));
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(writeOffId, tenant, request.LegalEntityId, AccountingFactTypes.SettlementWriteOff, 1,
                "subledger", $"writeoff:{requestId:N}", $"subledger-writeoff:{requestId:N}", effectiveDate, effectiveDate, payload, request.CorrelationId, request.ActorId);
            LedgerPostingResult posting;
            try { posting = await ledgerPosting.PostSettlementWriteOffAsync(context, fact, payload, token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { throw new SubledgerException("subledger.ledger_writeoff_post_failed", "The write-off accounting fact could not be posted; no Subledger balance was changed.", exception); }
            await ReduceItemAsync(context, tenant, request.LegalEntityId, itemId, amount, baseAmount, item, token);
            await using var writeOff = new NpgsqlCommand("insert into finance_subledger.write_off(tenant_id,legal_entity_id,id,request_id,open_item_id,party_id,direction,amount,base_currency,base_amount,effective_date,created_by,approved_by,ledger_fact_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", context.Connection, context.Transaction);
            Add(writeOff, tenant.Value, request.LegalEntityId.Value, writeOffId, requestId, itemId, partyId, direction, amount, item.BaseCurrency, baseAmount,
                effectiveDate, createdBy, request.ActorId, posting.FactId, now);
            await writeOff.ExecuteNonQueryAsync(token);
            await using var updateRequest = new NpgsqlCommand("update finance_subledger.write_off_request set status='Approved',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='PendingApproval'", context.Connection, context.Transaction);
            Add(updateRequest, tenant.Value, request.LegalEntityId.Value, requestId, now);
            if (await updateRequest.ExecuteNonQueryAsync(token) != 1) throw new SubledgerException("subledger.writeoff_transition_invalid", "The write-off request changed while it was being approved.");
            await WriteWriteOffAuditAsync(context, tenant, request.LegalEntityId, requestId, "Approved", request.ActorId,
                JsonSerializer.Serialize(new { writeOffId, amount, currency, baseAmount, item.BaseCurrency, posting.FactId, posting.JournalEntryId, effectiveDate }), now, token);
            EnqueueEvent(context, tenant, SubledgerEventTypes.WriteOffApprovedV1,
                new WriteOffApprovedEventV1(writeOffId, requestId, request.LegalEntityId.Value, itemId, posting.FactId), writeOffId, request.CorrelationId, now);
            return new WriteOffResult(writeOffId, requestId, itemId, amount.ToString(CultureInfo.InvariantCulture), currency, baseAmount.ToString(CultureInfo.InvariantCulture), item.BaseCurrency, effectiveDate, posting.FactId, request.ActorId, false);
        }, ct);

    public Task<Guid> CreateOpeningSubledgerBatchAsync(TenantId tenant, CreateOpeningSubledgerBatchCommand request, string baseCurrency, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var payloadHash = Hash(new { request.LegalEntityId, request.BatchIdentity, request.GoLiveDate, BaseCurrency = baseCurrency, request.ControlConfigurationReference });
            await using (var existing = new NpgsqlCommand("select id,payload_hash,status from finance_subledger.opening_batch where tenant_id=$1 and legal_entity_id=$2 and (idempotency_key=$3 or batch_identity=$4) for update", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, request.LegalEntityId.Value, request.IdempotencyKey.Trim(), request.BatchIdentity.Trim());
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (reader.GetString(1).Trim() != payloadHash) throw new SubledgerException("subledger.opening_idempotency_conflict", "This opening batch identity was already used with different header details.");
                    return reader.GetGuid(0);
                }
            }
            var id = Guid.CreateVersion7();
            var now = clock.GetUtcNow();
            await using var insert = new NpgsqlCommand("insert into finance_subledger.opening_batch(tenant_id,legal_entity_id,id,batch_identity,go_live_date,base_currency,control_configuration_reference,idempotency_key,payload_hash,status,created_by,correlation_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',$10,$11,$12)", context.Connection, context.Transaction);
            Add(insert, tenant.Value, request.LegalEntityId.Value, id, request.BatchIdentity.Trim(), request.GoLiveDate, baseCurrency, request.ControlConfigurationReference.Trim(), request.IdempotencyKey.Trim(), payloadHash, request.ActorId, request.CorrelationId, now);
            await insert.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenant, request.LegalEntityId, request.ActorId, null, "opening_batch_created", "opening_batch", id, null,
                JsonSerializer.Serialize(new { request.BatchIdentity, request.GoLiveDate, BaseCurrency = baseCurrency }), null, request.CorrelationId, now, token);
            return id;
        }, ct);

    public Task AddOpeningSubledgerLinesAsync(TenantId tenant, LegalEntityId entity, Guid batchId, IReadOnlyList<OpeningSubledgerLineCommand> lines, Guid actorId, Guid correlationId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var batch = new NpgsqlCommand("select status from finance_subledger.opening_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(batch, tenant.Value, entity.Value, batchId);
                var status = await batch.ExecuteScalarAsync(token) as string;
                if (status is null) throw new SubledgerException("subledger.opening_batch_not_found", "The opening batch was not found in this legal entity.");
                if (status != "Draft") throw new SubledgerException("subledger.opening_batch_immutable", "Finalized opening batches cannot be changed.");
            }
            foreach (var line in lines)
            {
                var hash = Hash(line);
                var id = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into finance_subledger.opening_batch_item(tenant_id,legal_entity_id,id,batch_id,row_key,payload_hash,party_id,direction,polarity,source_reference,document_date,due_date,transaction_currency,transaction_amount,base_currency,base_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20::jsonb) on conflict do nothing", context.Connection, context.Transaction);
                Add(insert, tenant.Value, entity.Value, id, batchId, line.RowKey.Trim(), hash, line.PartyId, line.Direction.ToString(), line.Polarity.ToString(), line.SourceReference.Trim(), line.DocumentDate, line.DueDate,
                    NormalizeCurrency(line.TransactionCurrency), ParseAmount(line.TransactionAmount), NormalizeCurrency(line.BaseCurrency), ParseAmount(line.BaseAmount), line.ControlAccountRole,
                    line.ControlAccountReference.Trim(), line.ControlConfigurationReference.Trim(), Db(line.HistoricFxSnapshot));
                if (await insert.ExecuteNonQueryAsync(token) == 1) continue;
                await using var existing = new NpgsqlCommand("select payload_hash from finance_subledger.opening_batch_item where tenant_id=$1 and legal_entity_id=$2 and batch_id=$3 and row_key=$4", context.Connection, context.Transaction);
                Add(existing, tenant.Value, entity.Value, batchId, line.RowKey.Trim());
                var existingHash = (string?)await existing.ExecuteScalarAsync(token);
                if (existingHash?.Trim() != hash) throw new SubledgerException("subledger.opening_row_conflict", "An opening row key was replayed with different financial details.");
            }
            await AuditAsync(context, tenant, entity, actorId, null, "opening_batch_lines_added", "opening_batch", batchId, null,
                JsonSerializer.Serialize(new { LineCount = lines.Count }), null, correlationId, clock.GetUtcNow(), token);
            return true;
        }, ct);

    public async Task<OpeningSubledgerBatchContext?> GetOpeningSubledgerBatchAsync(TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,batch_identity,go_live_date,base_currency,control_configuration_reference,status from finance_subledger.opening_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        Add(command, tenant.Value, entity.Value, batchId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3).Trim(), reader.GetString(4), reader.GetString(5)) : null;
    }

    public async Task<IReadOnlyList<OpeningSubledgerStoredLine>> GetOpeningSubledgerLinesAsync(TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,row_key,payload_hash,created_open_item_id,party_id,direction,polarity,source_reference,document_date,due_date,transaction_currency,transaction_amount,base_currency,base_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot from finance_subledger.opening_batch_item where tenant_id=$1 and legal_entity_id=$2 and batch_id=$3 order by row_key", connection, transaction);
        Add(command, tenant.Value, entity.Value, batchId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<OpeningSubledgerStoredLine>();
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0); var rowKey = reader.GetString(1); var hash = reader.GetString(2).Trim(); Guid? openItemId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
            var historic = reader.IsDBNull(17) ? null : reader.GetString(17);
            var line = new OpeningSubledgerLineCommand(rowKey, reader.GetGuid(4), Enum.Parse<SubledgerDirectionContract>(reader.GetString(5)), Enum.Parse<SubledgerPolarityContract>(reader.GetString(6)),
                reader.GetString(7), reader.GetFieldValue<DateOnly>(8), reader.GetFieldValue<DateOnly>(9), reader.GetString(10).Trim(), reader.GetDecimal(11).ToString(CultureInfo.InvariantCulture),
                reader.GetString(12).Trim(), reader.GetDecimal(13).ToString(CultureInfo.InvariantCulture), reader.GetString(14), reader.GetString(15), reader.GetString(16), historic);
            rows.Add(new(id, rowKey, hash, openItemId, line));
        }
        return rows;
    }

    public Task<OpeningSubledgerBatchResult> FinalizeOpeningSubledgerBatchAsync(TenantId tenant, LegalEntityId entity, Guid batchId, Guid actorId, Guid correlationId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var batch = new NpgsqlCommand("select batch_identity,go_live_date,base_currency,status from finance_subledger.opening_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
            Add(batch, tenant.Value, entity.Value, batchId);
            string identity; DateOnly goLiveDate; string baseCurrency; string status;
            await using (var reader = await batch.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.opening_batch_not_found", "The opening batch was not found in this legal entity.");
                identity = reader.GetString(0); goLiveDate = reader.GetFieldValue<DateOnly>(1); baseCurrency = reader.GetString(2).Trim(); status = reader.GetString(3);
            }
            var lines = await LoadOpeningLinesInTransactionAsync(context, tenant, entity, batchId, token);
            if (status == "Finalized") return new OpeningSubledgerBatchResult(batchId, identity, goLiveDate, status, lines.Count, lines.Where(line => line.OpenItemId.HasValue).Select(line => line.OpenItemId!.Value).ToArray(), true);
            if (lines.Count == 0) throw new SubledgerException("subledger.opening_batch_empty", "An opening batch must contain at least one outstanding item or credit.");
            var now = clock.GetUtcNow();
            var openItemIds = new List<Guid>(lines.Count);
            foreach (var stored in lines)
            {
                var line = stored.Command;
                var reference = $"OB-{batchId:N}-{line.RowKey}";
                var itemId = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into finance_subledger.party_open_item(tenant_id,legal_entity_id,id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,open_transaction_amount,base_currency,original_base_amount,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot,source_version,source_payload_hash,status,is_open,row_version,created_at) values($1,$2,$3,$4,$5,$6,'OpeningBalance',$7,null,$8,$9,$10,$11,$12,$13,$13,$14,$15,$15,$16,$17,$18,$19::jsonb,1,$20,'Open',true,1,$21)", context.Connection, context.Transaction);
                Add(insert, tenant.Value, entity.Value, itemId, line.PartyId, line.Direction.ToString(), line.Polarity.ToString(), reference, line.SourceReference.Trim(), line.DocumentDate, goLiveDate, line.DueDate,
                    line.TransactionCurrency, ParseAmount(line.TransactionAmount), baseCurrency, ParseAmount(line.BaseAmount), line.ControlAccountRole, line.ControlAccountReference,
                    line.ControlConfigurationReference, Db(line.HistoricFxSnapshot), stored.PayloadHash, now);
                await insert.ExecuteNonQueryAsync(token);
                openItemIds.Add(itemId);
                await using var link = new NpgsqlCommand("update finance_subledger.opening_batch_item set created_open_item_id=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
                Add(link, tenant.Value, entity.Value, stored.Id, itemId);
                await link.ExecuteNonQueryAsync(token);
            }
            await using var finalize = new NpgsqlCommand("update finance_subledger.opening_batch set status='Finalized',finalized_by=$4,finalization_correlation_id=$5,finalized_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft'", context.Connection, context.Transaction);
            Add(finalize, tenant.Value, entity.Value, batchId, actorId, correlationId, now);
            if (await finalize.ExecuteNonQueryAsync(token) != 1) throw new SubledgerException("subledger.opening_batch_changed", "The opening batch changed while it was being finalized.");
            EnqueueEvent(context, tenant, SubledgerEventTypes.OpeningBatchFinalizedV1,
                new OpeningBatchFinalizedEventV1(batchId, entity.Value, openItemIds.Count), batchId, correlationId, now);
            await AuditAsync(context, tenant, entity, actorId, null, "opening_batch_finalized", "opening_batch", batchId,
                JsonSerializer.Serialize(new { Status = "Draft", LineCount = lines.Count }),
                JsonSerializer.Serialize(new { Status = "Finalized", OpenItemCount = openItemIds.Count }), null, correlationId, now, token);
            return new OpeningSubledgerBatchResult(batchId, identity, goLiveDate, "Finalized", lines.Count, openItemIds, false);
        }, ct);

    public Task<IReadOnlyList<Guid>> CreateOpenItemsAsync(TenantId tenant, LegalEntityId entity, Guid partyId, SubledgerDirection direction, OpenItemPolarity polarity, string sourceType, IReadOnlyList<OpenItemSnapshotContract> items, CancellationToken ct, Guid? correlationId = null, Guid? actorId = null) =>
        transactions.ExecuteAsync(tenant, (context, token) => CreateOpenItemsInTransactionAsync(context, tenant, entity, partyId, direction, polarity, sourceType, items, token, correlationId, actorId), ct);

    public async Task<IReadOnlyList<Guid>> CreateOpenItemsInTransactionAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid partyId, SubledgerDirection direction, OpenItemPolarity polarity, string sourceType, IReadOnlyList<OpenItemSnapshotContract> items, CancellationToken token, Guid? correlationId = null, Guid? actorId = null)
    {
        var ids = new List<Guid>(items.Count);
        foreach (var item in items)
        {
            var payloadHash = Hash(new { PartyId = partyId, Direction = direction, Polarity = polarity, SourceType = sourceType, Item = item });
            var id = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into finance_subledger.party_open_item(tenant_id,legal_entity_id,id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,open_transaction_amount,base_currency,original_base_amount,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot,source_version,source_payload_hash,status,is_open,row_version,created_at,payment_term_snapshot) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$15,$16,$17,$17,$18,$19,$20,$21::jsonb,$22,$23,'Open',true,1,$24,$25::jsonb) on conflict do nothing", context.Connection, context.Transaction);
            Add(insert, tenant.Value, entity.Value, id, partyId, direction.ToString(), polarity.ToString(), sourceType, item.SourceReference.Trim(), Db(Clean(item.SourceScheduleReference)), Db(Clean(item.SourceDisplayNumber)), item.DocumentDate, item.PostingDate, item.DueDate, NormalizeCurrency(item.TransactionCurrency), ParseAmount(item.OriginalTransactionAmount), NormalizeCurrency(item.BaseCurrency), ParseAmount(item.OriginalBaseAmount), item.ControlAccountRole, item.ControlAccountReference.Trim(), item.ControlConfigurationReference.Trim(), Db(item.HistoricFxSnapshot), item.SourceVersion, payloadHash, clock.GetUtcNow(), Db(item.PaymentTermSnapshot));
            if (await insert.ExecuteNonQueryAsync(token) == 1)
            {
                ids.Add(id);
                var occurredAt = clock.GetUtcNow();
                EnqueueEvent(context, tenant, SubledgerEventTypes.OpenItemCreatedV1, new OpenItemCreatedEventV1(id, entity.Value, partyId, direction.ToString(), sourceType), id, correlationId ?? id, occurredAt);
                if (actorId is { } actor && actor != Guid.Empty)
                    await AuditAsync(context, tenant, entity, actor, null, "open_item_created", "party_open_item", id, null,
                        JsonSerializer.Serialize(new { partyId, direction, polarity, sourceType }), null,
                        correlationId ?? id, occurredAt, token);
                continue;
            }
            await using var existing = new NpgsqlCommand("select id,source_payload_hash from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type=$3 and source_reference=$4 and source_schedule_reference is not distinct from $5::text", context.Connection, context.Transaction);
            Add(existing, tenant.Value, entity.Value, sourceType, item.SourceReference.Trim(), Db(Clean(item.SourceScheduleReference)));
            await using var reader = await existing.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.open_item_conflict", "The source open item could not be created.");
            var existingId = reader.GetGuid(0); var existingHash = reader.GetString(1).Trim(); await reader.CloseAsync();
            if (!string.Equals(existingHash, payloadHash, StringComparison.Ordinal)) throw new SubledgerException("subledger.source_conflict", "The source identity already exists with different financial facts.");
            ids.Add(existingId);
        }
        return ids;
    }

    public async Task<IReadOnlyDictionary<Guid, (string TransactionCurrency, string BaseCurrency)>> GetApplicationCurrenciesAsync(TenantId tenant, LegalEntityId entity, IReadOnlyCollection<Guid> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0) return new Dictionary<Guid, (string, string)>();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,transaction_currency,base_currency from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=any($3)", connection, transaction);
        Add(command, tenant.Value, entity.Value, itemIds.Distinct().ToArray());
        var result = new Dictionary<Guid, (string, string)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0), (reader.GetString(1).Trim(), reader.GetString(2).Trim()));
        return result;
    }

    public Task<SubledgerApplicationResult> ApplyAsync(TenantId tenant, ApplyOpenItemsCommand request, IReadOnlyList<decimal> amounts, int moneyScale, Shared.Primitives.Financial.FinancialRoundingModeValue roundingMode, CancellationToken ct) =>
        ApplyAsync(tenant, request, request.Lines.Select((line, index) => new ResolvedApplicationLineContract(
            line.DebitItemId, line.CreditItemId, amounts[index].ToString(CultureInfo.InvariantCulture),
            amounts[index].ToString(CultureInfo.InvariantCulture), null, null, "0")).ToArray(), moneyScale, roundingMode, ct);

    public Task<SubledgerApplicationResult> ApplyAsync(TenantId tenant, ApplyOpenItemsCommand request, IReadOnlyList<ResolvedApplicationLineContract> resolvedLines, int moneyScale, Shared.Primitives.Financial.FinancialRoundingModeValue roundingMode, CancellationToken ct) =>
        transactions.ExecuteAsync<SubledgerApplicationResult>(tenant, async (context, token) =>
        {
            if (resolvedLines.Count != request.Lines.Count) throw new SubledgerException("subledger.application_lines_invalid", "Resolved settlement amounts do not match the requested application lines.");
            var lines = resolvedLines.Select(line => new NormalizedLine(line.DebitItemId, line.CreditItemId,
                    ParseAmount(line.DebitAmount), ParseAmount(line.CreditAmount), line.DebitFxSnapshot, line.CreditFxSnapshot, ParseAmount(line.SettlementBaseAmount)))
                .OrderBy(line => line.DebitItemId).ThenBy(line => line.CreditItemId).ThenBy(line => line.Amount).ToArray();
            var hash = Hash(new { request.PartyId, request.Direction, request.ApplicationDate, request.SettlementDate, Lines = lines });
            var batchId = Guid.CreateVersion7();
            await using var createBatch = new NpgsqlCommand("insert into finance_subledger.application_batch(tenant_id,legal_entity_id,id,party_id,direction,operation,idempotency_key,payload_hash,application_date,settlement_date,actor_id,correlation_id,created_at,confirmed_at) values($1,$2,$3,$4,$5,'Apply',$6,$7,$8,$9,$10,$11,$12,$12) on conflict do nothing returning id", context.Connection, context.Transaction);
            Add(createBatch, tenant.Value, request.LegalEntityId.Value, batchId, request.PartyId, request.Direction.ToString(), request.IdempotencyKey, hash, request.ApplicationDate, request.SettlementDate, request.ActorId, request.CorrelationId, clock.GetUtcNow());
            if (await createBatch.ExecuteScalarAsync(token) is null)
            {
                var prior = await FindBatchAsync(context, tenant, request.LegalEntityId, "Apply", request.IdempotencyKey, token);
                if (prior is null || !string.Equals(prior.Value.Hash, hash, StringComparison.Ordinal)) throw new SubledgerException("subledger.application_conflict", "This idempotency key was already used for a different application.");
                return new(prior.Value.Id, true, await ApplicationIdsAsync(context, tenant, request.LegalEntityId, prior.Value.Id, token));
            }

            var ids = lines.SelectMany(line => new[] { line.DebitItemId, line.CreditItemId }).Distinct().Order().ToArray();
            var items = await LockItemsAsync(context, tenant, request.LegalEntityId, ids, token);
            if (items.Count != ids.Length) throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found.");
            Activity.Current?.AddEvent(new ActivityEvent("subledger.open_item_locks_acquired"));
            foreach (var requested in request.Lines)
            {
                if (!items.TryGetValue(requested.DebitItemId, out var debitItem) || !items.TryGetValue(requested.CreditItemId, out var creditItem))
                    throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found.");
                if (requested.ExpectedDebitVersion is { } debitVersion && debitItem.RowVersion != debitVersion
                    || requested.ExpectedCreditVersion is { } creditVersion && creditItem.RowVersion != creditVersion)
                    throw new SubledgerException("subledger.item_changed", "An open-item balance changed after preview. Refresh the suggestions and try again.");
            }
            var debitTotals = new Dictionary<Guid, decimal>(); var creditTotals = new Dictionary<Guid, decimal>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]; var amount = line.Amount;
                if (!items.TryGetValue(line.DebitItemId, out var debit) || !items.TryGetValue(line.CreditItemId, out var credit)) throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found.");
                ValidatePair(debit, credit, request.PartyId, request.Direction, amount, line.CreditAmount, line.DebitFxSnapshot, line.CreditFxSnapshot, line.SettlementBaseAmount);
                debitTotals[line.DebitItemId] = debitTotals.GetValueOrDefault(line.DebitItemId) + amount;
                creditTotals[line.CreditItemId] = creditTotals.GetValueOrDefault(line.CreditItemId) + line.CreditAmount;
            }
            foreach (var pair in debitTotals) if (pair.Value > items[pair.Key].OpenAmount) throw new SubledgerException("subledger.application_over_target", "The application exceeds the remaining amount.");
            foreach (var pair in creditTotals) if (pair.Value > items[pair.Key].OpenAmount) throw new SubledgerException("subledger.application_over_source", "The application exceeds the available credit.");
            Activity.Current?.AddEvent(new ActivityEvent("subledger.applications_calculated"));

            var closedBases = new List<(decimal Debit, decimal Credit)>();
            var remainingAmounts = items.ToDictionary(pair => pair.Key, pair => pair.Value.OpenAmount);
            var remainingBase = items.ToDictionary(pair => pair.Key, pair => pair.Value.OpenBase);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]; var amount = line.Amount;
                var debit = items[line.DebitItemId]; var credit = items[line.CreditItemId];
                var debitBase = BasePortion(remainingAmounts[line.DebitItemId], remainingBase[line.DebitItemId], amount, moneyScale, roundingMode);
                var creditBase = BasePortion(remainingAmounts[line.CreditItemId], remainingBase[line.CreditItemId], line.CreditAmount, moneyScale, roundingMode);
                remainingAmounts[line.DebitItemId] -= amount; remainingBase[line.DebitItemId] -= debitBase;
                remainingAmounts[line.CreditItemId] -= line.CreditAmount; remainingBase[line.CreditItemId] -= creditBase;
                closedBases.Add((debitBase, creditBase));
                var appId = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into finance_subledger.open_item_application(tenant_id,legal_entity_id,id,batch_id,debit_item_id,credit_item_id,debit_applied_amount,credit_applied_amount,base_currency,debit_base_carrying_amount_closed,credit_base_carrying_amount_closed,application_date,settlement_date,sequence_no,created_at,debit_fx_snapshot,credit_fx_snapshot,settlement_base_amount) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16::jsonb,$17::jsonb,$18)", context.Connection, context.Transaction);
                Add(insert, tenant.Value, request.LegalEntityId.Value, appId, batchId, line.DebitItemId, line.CreditItemId, amount, line.CreditAmount, debit.BaseCurrency, debitBase, creditBase, request.ApplicationDate, request.SettlementDate, (long)i + 1, clock.GetUtcNow(), Db(line.DebitFxSnapshot), Db(line.CreditFxSnapshot), line.SettlementBaseAmount);
                await insert.ExecuteNonQueryAsync(token);
                await PostSettlementFxDifferenceAsync(context, tenant, request.LegalEntityId, appId, debit.BaseCurrency, debit.ControlRole, debit.ControlConfiguration, debitBase - creditBase,
                    request.SettlementDate, request.ActorId, request.CorrelationId, $"application:{appId:N}", token);
            }
            Activity.Current?.AddEvent(new ActivityEvent("subledger.accounting_facts_generated"));
            foreach (var id in debitTotals.Keys.Union(creditTotals.Keys).Order())
            {
                var item = items[id]; var applied = debitTotals.GetValueOrDefault(id) + creditTotals.GetValueOrDefault(id);
                var baseApplied = 0m;
                for (var i = 0; i < lines.Length; i++) if (lines[i].DebitItemId == id) baseApplied += closedBases[i].Debit; else if (lines[i].CreditItemId == id) baseApplied += closedBases[i].Credit;
                await ReduceItemAsync(context, tenant, request.LegalEntityId, id, applied, baseApplied, item, token);
            }
            var applicationIds = await ApplicationIdsAsync(context, tenant, request.LegalEntityId, batchId, token);
            EnqueueEvent(context, tenant, SubledgerEventTypes.ApplicationConfirmedV1,
                new ApplicationConfirmedEventV1(batchId, request.LegalEntityId.Value, request.PartyId, request.Direction.ToString(), applicationIds.Count),
                batchId, request.CorrelationId, clock.GetUtcNow());
            await AuditAsync(context, tenant, request.LegalEntityId, request.ActorId, null, "open_item_application_confirmed", "application_batch", batchId,
                null, JsonSerializer.Serialize(new { request.PartyId, request.Direction, LineCount = applicationIds.Count }), null, request.CorrelationId, clock.GetUtcNow(), token);
            return new(batchId, false, applicationIds);
        }, ct);

    public Task<ApplicationPreviewResult> PreviewApplicationAsync(TenantId tenant, ApplyOpenItemsCommand request, IReadOnlyList<ResolvedApplicationLineContract> resolvedLines,
        int moneyScale, Shared.Primitives.Financial.FinancialRoundingModeValue roundingMode, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (resolvedLines.Count != request.Lines.Count) throw new SubledgerException("subledger.application_lines_invalid", "Resolved settlement amounts do not match the requested application lines.");
            var ids = resolvedLines.SelectMany(line => new[] { line.DebitItemId, line.CreditItemId }).Distinct().Order().ToArray();
            var items = new Dictionary<Guid, LockedItem>();
            await using (var load = new NpgsqlCommand("select id,party_id,direction,polarity,transaction_currency,open_transaction_amount,settled_transaction_amount,base_currency,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=any($3)", context.Connection, context.Transaction))
            {
                Add(load, tenant.Value, request.LegalEntityId.Value, ids);
                await using var reader = await load.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) items.Add(reader.GetGuid(0), new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4).Trim(), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetString(7).Trim(), reader.GetDecimal(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetInt64(13)));
            }
            if (items.Count != ids.Length) throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found.");
            var debitTotals = new Dictionary<Guid, decimal>(); var creditTotals = new Dictionary<Guid, decimal>();
            var transactionRemaining = items.ToDictionary(pair => pair.Key, pair => pair.Value.OpenAmount);
            var baseRemaining = items.ToDictionary(pair => pair.Key, pair => pair.Value.OpenBase);
            var result = new List<ApplicationPreviewLine>(resolvedLines.Count);
            decimal totalDebit = 0m, totalCredit = 0m, totalFx = 0m;
            for (var index = 0; index < resolvedLines.Count; index++)
            {
                var line = resolvedLines[index]; var requested = request.Lines[index];
                if (!items.TryGetValue(line.DebitItemId, out var debit) || !items.TryGetValue(line.CreditItemId, out var credit))
                    throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found.");
                if (requested.ExpectedDebitVersion is { } debitVersion && debit.RowVersion != debitVersion
                    || requested.ExpectedCreditVersion is { } creditVersion && credit.RowVersion != creditVersion)
                    throw new SubledgerException("subledger.item_changed", "An open-item balance changed after preview. Refresh and try again.");
                var debitAmount = ParseAmount(line.DebitAmount); var creditAmount = ParseAmount(line.CreditAmount);
                ValidatePair(debit, credit, request.PartyId, request.Direction, debitAmount, creditAmount, line.DebitFxSnapshot, line.CreditFxSnapshot, ParseAmount(line.SettlementBaseAmount));
                debitTotals[line.DebitItemId] = debitTotals.GetValueOrDefault(line.DebitItemId) + debitAmount;
                creditTotals[line.CreditItemId] = creditTotals.GetValueOrDefault(line.CreditItemId) + creditAmount;
                totalDebit += debitAmount; totalCredit += creditAmount;
                var debitBase = BasePortion(transactionRemaining[line.DebitItemId], baseRemaining[line.DebitItemId], debitAmount, moneyScale, roundingMode);
                var creditBase = BasePortion(transactionRemaining[line.CreditItemId], baseRemaining[line.CreditItemId], creditAmount, moneyScale, roundingMode);
                transactionRemaining[line.DebitItemId] -= debitAmount; transactionRemaining[line.CreditItemId] -= creditAmount;
                baseRemaining[line.DebitItemId] -= debitBase; baseRemaining[line.CreditItemId] -= creditBase;
                var fx = debitBase - creditBase; totalFx += fx;
                result.Add(new(line.DebitItemId, line.CreditItemId, line.DebitAmount, line.CreditAmount, debit.RowVersion, credit.RowVersion,
                    (debit.OpenAmount - debitTotals[line.DebitItemId]).ToString(CultureInfo.InvariantCulture),
                    (credit.OpenAmount - creditTotals[line.CreditItemId]).ToString(CultureInfo.InvariantCulture), fx.ToString(CultureInfo.InvariantCulture), debit.BaseCurrency));
            }
            foreach (var pair in debitTotals) if (pair.Value > items[pair.Key].OpenAmount) throw new SubledgerException("subledger.application_over_target", "The application exceeds the remaining amount.");
            foreach (var pair in creditTotals) if (pair.Value > items[pair.Key].OpenAmount) throw new SubledgerException("subledger.application_over_source", "The application exceeds the available credit.");
            return new ApplicationPreviewResult(request.PartyId, request.LegalEntityId.Value, request.Direction, result,
                totalDebit.ToString(CultureInfo.InvariantCulture), totalCredit.ToString(CultureInfo.InvariantCulture), totalFx.ToString(CultureInfo.InvariantCulture));
        }, ct);

    public Task<SubledgerApplicationResult> ReverseAsync(TenantId tenant, ReverseApplicationCommand request, CancellationToken ct) =>
        transactions.ExecuteAsync<SubledgerApplicationResult>(tenant, async (context, token) =>
        {
            var hash = Hash(new { request.ApplicationId, request.EffectiveDate, Reason = request.Reason.Trim() });
            var priorBeforeLock = await FindBatchAsync(context, tenant, request.LegalEntityId, "Reverse", request.IdempotencyKey, token);
            if (priorBeforeLock is not null)
            {
                if (!string.Equals(priorBeforeLock.Value.Hash, hash, StringComparison.Ordinal)) throw new SubledgerException("subledger.application_conflict", "This idempotency key was already used for a different reversal.");
                return new(priorBeforeLock.Value.Id, true, await ReversedApplicationIdsAsync(context, tenant, request.LegalEntityId, priorBeforeLock.Value.Id, token));
            }
            var original = await LockApplicationAsync(context, tenant, request.LegalEntityId, request.ApplicationId, token);
            if (original is null)
            {
                var priorAfterLock = await FindBatchAsync(context, tenant, request.LegalEntityId, "Reverse", request.IdempotencyKey, token);
                if (priorAfterLock is not null && string.Equals(priorAfterLock.Value.Hash, hash, StringComparison.Ordinal)) return new(priorAfterLock.Value.Id, true, await ReversedApplicationIdsAsync(context, tenant, request.LegalEntityId, priorAfterLock.Value.Id, token));
                await using var reversed = new NpgsqlCommand("select exists(select 1 from finance_subledger.application_reversal where tenant_id=$1 and legal_entity_id=$2 and original_application_id=$3)", context.Connection, context.Transaction);
                Add(reversed, tenant.Value, request.LegalEntityId.Value, request.ApplicationId);
                if ((bool)(await reversed.ExecuteScalarAsync(token) ?? false)) throw new SubledgerException("subledger.application_already_reversed", "This application has already been reversed.");
                throw new SubledgerException("subledger.application_not_found", "The application was not found.");
            }
            if (request.EffectiveDate < original.ApplicationDate) throw new SubledgerException("subledger.reversal_date_invalid", "An application cannot be reversed before its effective application date.");
            var batchId = Guid.CreateVersion7();
            await using var batch = new NpgsqlCommand("insert into finance_subledger.application_batch(tenant_id,legal_entity_id,id,party_id,direction,operation,idempotency_key,payload_hash,application_date,settlement_date,actor_id,correlation_id,created_at,confirmed_at) values($1,$2,$3,$4,$5,'Reverse',$6,$7,$8,$8,$9,$10,$11,$11) on conflict do nothing returning id", context.Connection, context.Transaction);
            Add(batch, tenant.Value, request.LegalEntityId.Value, batchId, original.PartyId, original.Direction, request.IdempotencyKey, hash, request.EffectiveDate, request.ActorId, request.CorrelationId, clock.GetUtcNow());
            if (await batch.ExecuteScalarAsync(token) is null)
            {
                var prior = await FindBatchAsync(context, tenant, request.LegalEntityId, "Reverse", request.IdempotencyKey, token);
                if (prior is null || !string.Equals(prior.Value.Hash, hash, StringComparison.Ordinal)) throw new SubledgerException("subledger.application_conflict", "This idempotency key was already used for a different reversal.");
                return new(prior.Value.Id, true, await ReversedApplicationIdsAsync(context, tenant, request.LegalEntityId, prior.Value.Id, token));
            }
            var itemIds = new[] { original.DebitId, original.CreditId }.Order().ToArray();
            var items = await LockItemsAsync(context, tenant, request.LegalEntityId, itemIds, token);
            if (items.Count != 2) throw new SubledgerException("subledger.open_item_not_found", "The original open items are no longer available.");
            await using var reversal = new NpgsqlCommand("insert into finance_subledger.application_reversal(tenant_id,legal_entity_id,id,original_application_id,reversal_batch_id,effective_date,reason,actor_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict(tenant_id,legal_entity_id,original_application_id) do nothing", context.Connection, context.Transaction);
            Add(reversal, tenant.Value, request.LegalEntityId.Value, Guid.CreateVersion7(), request.ApplicationId, batchId, request.EffectiveDate, request.Reason.Trim(), request.ActorId, clock.GetUtcNow());
            if (await reversal.ExecuteNonQueryAsync(token) == 0) throw new SubledgerException("subledger.application_already_reversed", "This application has already been reversed.");
            await RestoreItemAsync(context, tenant, request.LegalEntityId, items[original.DebitId], original.DebitAmount, original.DebitBaseClosed, token);
            await RestoreItemAsync(context, tenant, request.LegalEntityId, items[original.CreditId], original.CreditAmount, original.CreditBaseClosed, token);
            await PostSettlementFxDifferenceAsync(context, tenant, request.LegalEntityId, original.Id, original.BaseCurrency, items[original.DebitId].ControlRole, items[original.DebitId].ControlConfiguration,
                original.DebitBaseClosed - original.CreditBaseClosed, request.EffectiveDate, request.ActorId, request.CorrelationId, $"application-reversal:{original.Id:N}", token, reverse: true);
            EnqueueEvent(context, tenant, SubledgerEventTypes.ApplicationReversedV1,
                new ApplicationReversedEventV1(batchId, request.ApplicationId, request.LegalEntityId.Value, original.PartyId, original.Direction),
                batchId, request.CorrelationId, clock.GetUtcNow());
            await AuditAsync(context, tenant, request.LegalEntityId, request.ActorId, null, "open_item_application_reversed", "application_reversal", batchId,
                null, JsonSerializer.Serialize(new { request.ApplicationId, request.EffectiveDate }), request.Reason.Trim(), request.CorrelationId, clock.GetUtcNow(), token);
            return new(batchId, false, [request.ApplicationId]);
        }, ct);

    public async Task<OpenItemPage> ListAsync(TenantId tenant, OpenItemListQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 200);
        var sort = query.Sort ?? "idAsc";
        if (query.Page is not null)
        {
            var pageNumber = Math.Max(1, query.Page.Value); var offset = (pageNumber - 1) * limit;
            var pageOrder = sort switch { "dueDateAsc" => "due_date asc,id asc", "dueDateDesc" => "due_date desc,id desc", _ => "id asc" };
            await using var pageConnection = await dataSource.OpenConnectionAsync(ct);
            await using var pageTransaction = await pageConnection.BeginTransactionAsync(ct); await SetTenantAsync(pageConnection, pageTransaction, tenant, ct);
            var pageSql = $"select id,legal_entity_id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,settled_transaction_amount,open_transaction_amount,base_currency,original_base_amount,settled_base_carrying_amount,open_base_carrying_amount,control_account_role,control_account_reference,status,row_version,count(*) over() from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or party_id=$3) and ($4::text is null or direction=$4) and (not $5 or is_open) and ($6::text is null or (source_display_number is not null and lower(source_display_number) like lower($6)||'%') or lower(source_reference) like lower($6)||'%') and ($7::date is null or due_date >= $7) and ($8::date is null or due_date <= $8) and ($9::text is null or transaction_currency=$9) and (not $10 or (is_open and due_date < $11)) and ($12::text is null or status=$12) order by {pageOrder} offset $13 limit $14";
            await using var pageCommand = new NpgsqlCommand(pageSql, pageConnection, pageTransaction);
            Add(pageCommand, tenant.Value, query.LegalEntityId.Value, (object?)query.PartyId ?? DBNull.Value, Db(query.Direction?.ToString()), query.OpenOnly, Db(query.Search), Db(query.DueFrom), Db(query.DueTo), Db(query.CurrencyCode), query.OverdueOnly, Db(query.AsOfDate), Db(query.Status), offset, limit);
            var rows = new List<PartyOpenItemResult>(); var totalCount = 0; await using var reader = await pageCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) { totalCount = reader.GetInt32(24); rows.Add(ReadItem(reader)); }
            await pageTransaction.CommitAsync(ct);
            return new(rows, null, totalCount, pageNumber, limit);
        }
        if (sort is "idAsc")
        {
            await using var idConnection = await dataSource.OpenConnectionAsync(ct);
            await using var idTransaction = await idConnection.BeginTransactionAsync(ct); await SetTenantAsync(idConnection, idTransaction, tenant, ct);
            await using var idCommand = new NpgsqlCommand("select id,legal_entity_id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,settled_transaction_amount,open_transaction_amount,base_currency,original_base_amount,settled_base_carrying_amount,open_base_carrying_amount,control_account_role,control_account_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or party_id=$3) and ($4::text is null or direction=$4) and (not $5 or is_open) and ($6::uuid is null or id>$6) and ($8::text is null or (source_display_number is not null and lower(source_display_number) like lower($8)||'%') or lower(source_reference) like lower($8)||'%') and ($9::date is null or due_date >= $9) and ($10::date is null or due_date <= $10) and ($11::text is null or transaction_currency=$11) and (not $12 or (is_open and due_date < $13)) and ($14::text is null or status=$14) order by id limit $7", idConnection, idTransaction);
            Add(idCommand, tenant.Value, query.LegalEntityId.Value, (object?)query.PartyId ?? DBNull.Value, Db(query.Direction?.ToString()), query.OpenOnly, Db(ParseCursor(query.Cursor)), limit + 1,
                Db(query.Search), Db(query.DueFrom), Db(query.DueTo), Db(query.CurrencyCode), query.OverdueOnly, Db(query.AsOfDate), Db(query.Status));
            return await ReadPageAsync(idCommand, limit, sort, ct);
        }

        var descending = sort == "dueDateDesc";
        var (cursorDate, cursorId) = ParseDateCursor(query.Cursor, sort);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var comparison = descending ? "<" : ">";
        var order = descending ? "desc" : "asc";
        await using var command = new NpgsqlCommand($"select id,legal_entity_id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,settled_transaction_amount,open_transaction_amount,base_currency,original_base_amount,settled_base_carrying_amount,open_base_carrying_amount,control_account_role,control_account_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or party_id=$3) and ($4::text is null or direction=$4) and (not $5 or is_open) and ($6::date is null or ($7::uuid is null) or (due_date,id) {comparison} ($6,$7)) and ($9::text is null or (source_display_number is not null and lower(source_display_number) like lower($9)||'%') or lower(source_reference) like lower($9)||'%') and ($10::date is null or due_date >= $10) and ($11::date is null or due_date <= $11) and ($12::text is null or transaction_currency=$12) and (not $13 or (is_open and due_date < $14)) and ($15::text is null or status=$15) order by due_date {order}, id {order} limit $8", connection, transaction);
        Add(command, tenant.Value, query.LegalEntityId.Value, (object?)query.PartyId ?? DBNull.Value, Db(query.Direction?.ToString()), query.OpenOnly, Db(cursorDate), Db(cursorId), limit + 1,
            Db(query.Search), Db(query.DueFrom), Db(query.DueTo), Db(query.CurrencyCode), query.OverdueOnly, Db(query.AsOfDate), Db(query.Status));
        return await ReadPageAsync(command, limit, sort, ct);
    }

    public static bool ValidateCursor(string cursor, string sort) => sort == "idAsc"
        ? Guid.TryParse(cursor, out _)
        : TryDecodeDateCursor(cursor, sort, out _, out _);

    private static async Task<OpenItemPage> ReadPageAsync(NpgsqlCommand command, int limit, string sort, CancellationToken ct)
    {
        var rows = new List<PartyOpenItemResult>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ReadItem(reader));
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1);
        if (!hasMore || rows.Count == 0) return new(rows, null);
        var last = rows[^1];
        if (sort == "idAsc") return new(rows, last.Id.ToString("D"));
        var value = $"{sort}|{last.DueDate:yyyy-MM-dd}|{last.Id:D}";
        return new(rows, Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
    }

    public async Task<PartyOpenItemResult?> GetOpenItemAsync(TenantId tenant, LegalEntityId entity, Guid openItemId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,settled_transaction_amount,open_transaction_amount,base_currency,original_base_amount,settled_base_carrying_amount,open_base_carrying_amount,control_account_role,control_account_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        Add(command, tenant.Value, entity.Value, openItemId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadItem(reader) : null;
    }

    public async Task<IReadOnlyList<ForeignCurrencyOpenItemSnapshot>> ListForeignCurrencyOpenItemsForRevaluationAsync(TenantId tenant, LegalEntityId entity, DateOnly asOfDate, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string sql = "select id,direction,transaction_currency,open_transaction_amount,base_currency,open_base_carrying_amount,control_account_role,control_configuration_reference,posting_date,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and is_open and posting_date <= $3 and transaction_currency <> base_currency and open_transaction_amount <> 0 order by id";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        Add(command, tenant.Value, entity.Value, asOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<ForeignCurrencyOpenItemSnapshot>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(reader.GetGuid(0), Enum.Parse<SubledgerDirectionContract>(reader.GetString(1), true), reader.GetString(2).Trim(),
                reader.GetDecimal(3).ToString(CultureInfo.InvariantCulture), reader.GetString(4).Trim(), reader.GetDecimal(5).ToString(CultureInfo.InvariantCulture),
                reader.GetString(6), reader.GetString(7), DateOnly.FromDateTime(reader.GetDateTime(8)), reader.GetInt64(9)));
        return rows;
    }

    public async Task<SourceSettlementSummaryContract?> GetSourceSettlementSummaryAsync(TenantId tenant, LegalEntityId entity, string sourceType,
        Guid sourceDocumentId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select min(transaction_currency),count(distinct transaction_currency),coalesce(sum(original_transaction_amount),0),coalesce(sum(settled_transaction_amount),0),coalesce(sum(open_transaction_amount),0),count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type=$3 and source_reference=$4", connection, transaction);
        Add(command, tenant.Value, entity.Value, sourceType, sourceDocumentId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var count = reader.GetInt64(5);
        if (count == 0) return null;
        if (reader.GetInt64(1) != 1) throw new SubledgerException("subledger.source_currency_mismatch", "One source document cannot have open items in multiple transaction currencies.");
        var original = reader.GetDecimal(2);
        var settled = reader.GetDecimal(3);
        var open = reader.GetDecimal(4);
        var status = open == 0m ? "Settled" : settled == 0m ? "Open" : "PartiallySettled";
        return new(status, reader.GetString(0).Trim(), original.ToString(CultureInfo.InvariantCulture), settled.ToString(CultureInfo.InvariantCulture),
            open.ToString(CultureInfo.InvariantCulture), checked((int)count));
    }

    public async Task<AutoApplyContext> GetAutoApplyContextAsync(TenantId tenant, LegalEntityId entity, Guid sourceItemId, string? reference, int candidateLimit, bool prioritizeReference, bool oldestDocumentFirst, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string columns = "id,legal_entity_id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,settled_transaction_amount,open_transaction_amount,base_currency,original_base_amount,settled_base_carrying_amount,open_base_carrying_amount,control_account_role,control_account_reference,status,row_version";
        PartyOpenItemResult source;
        string sourceConfiguration;
        await using (var loadSource = new NpgsqlCommand($"select {columns},control_configuration_reference from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction))
        {
            Add(loadSource, tenant.Value, entity.Value, sourceItemId);
            await using var reader = await loadSource.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new SubledgerException("subledger.open_item_not_found", "The source open item was not found in this legal entity.");
            if (reader.GetString(22) is not ("Open" or "PartiallySettled")) throw new SubledgerException("subledger.item_closed", "The source open item has no available balance.");
            source = ReadItem(reader);
            sourceConfiguration = reader.GetString(24);
        }

        var opposite = source.Polarity == SubledgerPolarityContract.Debit ? "Credit" : "Debit";
        var matchReference = string.IsNullOrWhiteSpace(reference) ? source.SourceDisplayNumber ?? source.SourceReference : reference.Trim();
        var candidates = new List<AutoApplyCandidateContext>();
        var candidateIds = new HashSet<Guid>();
        // SuggestApplicationsAsync can return at most 50 lines, so loading more candidates cannot affect its result.
        var boundedCandidateLimit = Math.Clamp(candidateLimit, 0, 50);
        var exactReference = NormalizeMatchReference(matchReference);
        const string exactDisplaySql = """
            select __COLUMNS__
            from finance_subledger.party_open_item i
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$4 and i.polarity=$5
              and i.is_open and i.id<>$6 and i.transaction_currency=$7 and i.control_account_role=$8
              and i.control_account_reference=$9 and i.control_configuration_reference=$10 and i.base_currency=$11
              and i.source_display_number_normalized=$12
            limit $13
            """;
        const string exactSourceSql = """
            select __COLUMNS__
            from finance_subledger.party_open_item i
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$4 and i.polarity=$5
              and i.is_open and i.id<>$6 and i.transaction_currency=$7 and i.control_account_role=$8
              and i.control_account_reference=$9 and i.control_configuration_reference=$10 and i.base_currency=$11
              and i.source_reference_normalized=$12
            limit $13
            """;
        var fallbackOrder = oldestDocumentFirst ? "i.document_date,i.id" : "i.due_date,i.document_date,i.id";
        var fallbackSql = $"""
            select __COLUMNS__
            from finance_subledger.party_open_item i
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$4 and i.polarity=$5
              and i.is_open and i.id<>$6 and i.transaction_currency=$7 and i.control_account_role=$8
              and i.control_account_reference=$9 and i.control_configuration_reference=$10 and i.base_currency=$11
            order by {fallbackOrder} limit $13
            """;

        async Task LoadCandidatesAsync(string sql, int limit, string? normalizedExactReference)
        {
            var sqlWithColumns = sql.Replace("__COLUMNS__", string.Join(',', columns.Split(',').Select(column => "i." + column)) + ",i.control_configuration_reference", StringComparison.Ordinal);
            await using var loadCandidates = new NpgsqlCommand(sqlWithColumns, connection, transaction);
            Add(loadCandidates, tenant.Value, entity.Value, source.PartyId, source.Direction.ToString(), opposite, source.Id, source.TransactionCurrency,
                source.ControlAccountRole, source.ControlAccountReference, sourceConfiguration, source.BaseCurrency,
                normalizedExactReference ?? string.Empty, limit);
            await using var reader = await loadCandidates.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var item = ReadItem(reader);
                if (candidateIds.Add(item.Id)) candidates.Add(new(item, reader.GetString(24)));
            }
        }

        if (prioritizeReference && exactReference.Length > 0 && boundedCandidateLimit > 0)
        {
            await LoadCandidatesAsync(exactDisplaySql, boundedCandidateLimit, exactReference);
            if (candidates.Count < boundedCandidateLimit)
                await LoadCandidatesAsync(exactSourceSql, boundedCandidateLimit - candidates.Count, exactReference);
            if (candidates.Count == 0)
            {
                const string normalizedDisplaySql = """
                    select __COLUMNS__ from finance_subledger.party_open_item i
                    where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$4 and i.polarity=$5
                      and i.is_open and i.id<>$6 and i.transaction_currency=$7 and i.control_account_role=$8
                      and i.control_account_reference=$9 and i.control_configuration_reference=$10 and i.base_currency=$11
                      and i.source_display_number_normalized=$12
                    limit $13
                    """;
                const string normalizedSourceSql = """
                    select __COLUMNS__ from finance_subledger.party_open_item i
                    where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$4 and i.polarity=$5
                      and i.is_open and i.id<>$6 and i.transaction_currency=$7 and i.control_account_role=$8
                      and i.control_account_reference=$9 and i.control_configuration_reference=$10 and i.base_currency=$11
                      and i.source_reference_normalized=$12
                    limit $13
                    """;
                await LoadCandidatesAsync(normalizedDisplaySql, boundedCandidateLimit, exactReference);
                if (candidates.Count < boundedCandidateLimit)
                    await LoadCandidatesAsync(normalizedSourceSql, boundedCandidateLimit - candidates.Count, exactReference);
            }
        }
        var exactCandidatesCoverSource = prioritizeReference && candidates.Sum(candidate => ParseAmount(candidate.Item.OpenTransactionAmount)) >= ParseAmount(source.OpenTransactionAmount);
        if (!exactCandidatesCoverSource && candidates.Count < boundedCandidateLimit)
            await LoadCandidatesAsync(fallbackSql, Math.Min(1000, boundedCandidateLimit + candidates.Count), null);
        if (candidates.Count > boundedCandidateLimit) candidates.RemoveRange(boundedCandidateLimit, candidates.Count - boundedCandidateLimit);
        await transaction.CommitAsync(ct);
        return new(source, sourceConfiguration, candidates);
    }

    public async Task<PartyBalanceResult> GetPartyBalanceAsync(TenantId tenant, PartyBalanceQuery query, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string currentStateSql = """
            select not exists (
                select 1 from finance_subledger.application_batch b
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and b.application_date > $4
            ) and not exists (
                select 1 from finance_subledger.write_off w
                where w.tenant_id=$1 and w.legal_entity_id=$2 and w.party_id=$3 and w.direction=$5 and w.effective_date > $4
            ) and not exists (
                select 1
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch b
                  on b.tenant_id=r.tenant_id and b.legal_entity_id=r.legal_entity_id and b.id=r.reversal_batch_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and r.effective_date > $4
            )
            """;
        var useCurrentState = false;
        if (query.AsOfDate == DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
        {
            await using var currentState = new NpgsqlCommand(currentStateSql, connection, transaction);
            Add(currentState, tenant.Value, query.LegalEntityId.Value, query.PartyId, query.AsOfDate, query.Direction.ToString());
            useCurrentState = await currentState.ExecuteScalarAsync(ct) is true;
        }

        const string currentSql = """
            select i.transaction_currency,
              coalesce(sum(case when i.polarity=$6 then i.open_transaction_amount else 0 end),0),
              coalesce(sum(case when i.polarity<>$6 then i.open_transaction_amount else 0 end),0),
              coalesce(sum(case when i.polarity=$6 and i.due_date<$4 then i.open_transaction_amount else 0 end),0),
              coalesce(sum(i.row_version),0)
            from finance_subledger.party_open_item i
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$5 and i.posting_date <= $4
            group by i.transaction_currency order by i.transaction_currency
            """;
        const string historicalSql = """
            with party_items as materialized (
              select i.id,i.transaction_currency,i.polarity,i.due_date,i.row_version,
                i.original_transaction_amount as original_amount
              from finance_subledger.party_open_item i
              where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$5 and i.posting_date <= $4
            ), application_effects as (
              select item_id,sum(amount) as amount from (
                select a.debit_item_id as item_id,a.debit_applied_amount as amount
                from finance_subledger.application_batch b
                join finance_subledger.open_item_application a
                  on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
                join party_items i on i.id=a.debit_item_id
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and a.application_date <= $4
                union all
                select a.credit_item_id as item_id,a.credit_applied_amount as amount
                from finance_subledger.application_batch b
                join finance_subledger.open_item_application a
                  on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
                join party_items i on i.id=a.credit_item_id
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and a.application_date <= $4
              ) lines group by item_id
            ), reversal_effects as (
              select item_id,sum(amount) as amount from (
                select a.debit_item_id as item_id,a.debit_applied_amount as amount
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch rb
                  on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                join party_items i on i.id=a.debit_item_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$5 and r.effective_date <= $4
                union all
                select a.credit_item_id as item_id,a.credit_applied_amount as amount
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch rb
                  on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                join party_items i on i.id=a.credit_item_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$5 and r.effective_date <= $4
              ) lines group by item_id
            ), write_off_effects as (
              select w.open_item_id as item_id,sum(w.amount) as amount
              from finance_subledger.write_off w
              join party_items i on i.id=w.open_item_id
              where w.tenant_id=$1 and w.legal_entity_id=$2 and w.effective_date <= $4
              group by w.open_item_id
            ), item_balance as (
              select i.transaction_currency,i.polarity,i.due_date,i.row_version,
                i.original_amount - coalesce(a.amount,0) + coalesce(r.amount,0) - coalesce(w.amount,0) as remaining
              from party_items i
              left join application_effects a on a.item_id=i.id
              left join reversal_effects r on r.item_id=i.id
              left join write_off_effects w on w.item_id=i.id
            )
            select transaction_currency,
              coalesce(sum(case when polarity=$6 then remaining else 0 end),0),
              coalesce(sum(case when polarity<>$6 then remaining else 0 end),0),
              coalesce(sum(case when polarity=$6 and due_date<$4 then remaining else 0 end),0),
              coalesce(sum(row_version),0)
            from item_balance
            group by transaction_currency order by transaction_currency
            """;
        await using var command = new NpgsqlCommand(useCurrentState ? currentSql : historicalSql, connection, transaction);
        Add(command, tenant.Value, query.LegalEntityId.Value, query.PartyId, query.AsOfDate, query.Direction.ToString(),
            query.Direction == SubledgerDirectionContract.Receivable ? "Debit" : "Credit");
        var currencies = new List<PartyCurrencyBalance>(); long version = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var due = reader.GetDecimal(1); var credits = reader.GetDecimal(2); var overdue = reader.GetDecimal(3); version = unchecked(version + reader.GetInt64(4));
                currencies.Add(new(reader.GetString(0).Trim(), due.ToString(CultureInfo.InvariantCulture), credits.ToString(CultureInfo.InvariantCulture), (due - credits).ToString(CultureInfo.InvariantCulture), overdue.ToString(CultureInfo.InvariantCulture)));
            }
        }
        await using var profile = new NpgsqlCommand("select credit_limit_amount,credit_limit_currency,version from finance_subledger.party_financial_profile where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and status='Active'", connection, transaction);
        Add(profile, tenant.Value, query.LegalEntityId.Value, query.PartyId);
        string? limit = null, limitCurrency = null;
        await using (var reader = await profile.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct)) { limit = reader.IsDBNull(0) ? null : reader.GetDecimal(0).ToString(CultureInfo.InvariantCulture); limitCurrency = reader.IsDBNull(1) ? null : reader.GetString(1).Trim(); version = unchecked(version + reader.GetInt64(2)); }
        }
        string? availableCredit = null;
        if (limit is not null && limitCurrency is not null && currencies.All(item => item.CurrencyCode == limitCurrency))
        {
            var net = currencies.Count == 0 ? 0m : ParseAmount(currencies[0].NetBalance);
            availableCredit = (ParseAmount(limit) - net).ToString(CultureInfo.InvariantCulture);
        }
        await using var instantQuery = new NpgsqlCommand("select transaction_timestamp()", connection, transaction);
        var instantValue = Convert.ToDateTime(await instantQuery.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        var asOfInstant = new DateTimeOffset(DateTime.SpecifyKind(instantValue, DateTimeKind.Utc));
        return new(query.PartyId, query.LegalEntityId.Value, query.Direction, query.AsOfDate, currencies, limit, limitCurrency, availableCredit, version, asOfInstant);
    }

    public async Task<IReadOnlyList<AgingBucketResult>> GetAgingAsync(TenantId tenant, AgingQuery query, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string currentStateSql = """
            select not exists (
                select 1 from finance_subledger.application_batch b
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and b.application_date>$4
            ) and not exists (
                select 1 from finance_subledger.write_off w
                where w.tenant_id=$1 and w.legal_entity_id=$2 and w.party_id=$3 and w.direction=$5 and w.effective_date>$4
            ) and not exists (
                select 1
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch b
                  on b.tenant_id=r.tenant_id and b.legal_entity_id=r.legal_entity_id and b.id=r.reversal_batch_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and r.effective_date>$4
            )
            """;
        var useCurrentState = false;
        if (query.AsOfDate == DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
        {
            await using var currentState = new NpgsqlCommand(currentStateSql, connection, transaction);
            Add(currentState, tenant.Value, query.LegalEntityId.Value, query.PartyId, query.AsOfDate, query.Direction.ToString());
            useCurrentState = await currentState.ExecuteScalarAsync(ct) is true;
        }

        const string currentSql = """
            select i.transaction_currency,
              coalesce(sum(i.open_transaction_amount) filter(where i.due_date >= $4),0),
              coalesce(sum(i.open_transaction_amount) filter(where i.due_date < $4 and i.due_date >= $4-30),0),
              coalesce(sum(i.open_transaction_amount) filter(where i.due_date < $4-30 and i.due_date >= $4-60),0),
              coalesce(sum(i.open_transaction_amount) filter(where i.due_date < $4-60 and i.due_date >= $4-90),0),
              coalesce(sum(i.open_transaction_amount) filter(where i.due_date < $4-90),0)
            from finance_subledger.party_open_item i
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$5 and i.polarity=$6
              and i.posting_date<=$4 and i.is_open and i.open_transaction_amount>0
              and ($7::text is null or i.transaction_currency=$7)
            group by i.transaction_currency order by i.transaction_currency
            """;
        const string historicalSql = """
            with party_items as materialized (
              select i.id,i.transaction_currency,i.due_date,
                i.original_transaction_amount as original_amount
              from finance_subledger.party_open_item i
              where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$5 and i.polarity=$6 and i.posting_date <= $4
            ), application_effects as (
              select item_id,sum(amount) as amount from (
                select a.debit_item_id as item_id,a.debit_applied_amount as amount
                from finance_subledger.application_batch b
                join finance_subledger.open_item_application a
                  on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
                join party_items i on i.id=a.debit_item_id
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and a.application_date <= $4
                union all
                select a.credit_item_id as item_id,a.credit_applied_amount as amount
                from finance_subledger.application_batch b
                join finance_subledger.open_item_application a
                  on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
                join party_items i on i.id=a.credit_item_id
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$5 and a.application_date <= $4
              ) lines group by item_id
            ), reversal_effects as (
              select item_id,sum(amount) as amount from (
                select a.debit_item_id as item_id,a.debit_applied_amount as amount
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch rb
                  on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                join party_items i on i.id=a.debit_item_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$5 and r.effective_date <= $4
                union all
                select a.credit_item_id as item_id,a.credit_applied_amount as amount
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch rb
                  on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                join party_items i on i.id=a.credit_item_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$5 and r.effective_date <= $4
              ) lines group by item_id
            ), write_off_effects as (
              select w.open_item_id as item_id,sum(w.amount) as amount
              from finance_subledger.write_off w
              join party_items i on i.id=w.open_item_id
              where w.tenant_id=$1 and w.legal_entity_id=$2 and w.effective_date <= $4
              group by w.open_item_id
            ), remaining as (
              select i.id,i.transaction_currency,i.due_date,
                i.original_amount - coalesce(a.amount,0) + coalesce(r.amount,0) - coalesce(w.amount,0) as amount
              from party_items i
              left join application_effects a on a.item_id=i.id
              left join reversal_effects r on r.item_id=i.id
              left join write_off_effects w on w.item_id=i.id
            )
            select transaction_currency,
              coalesce(sum(amount) filter(where due_date >= $4),0),
              coalesce(sum(amount) filter(where due_date < $4 and due_date >= $4-30),0),
              coalesce(sum(amount) filter(where due_date < $4-30 and due_date >= $4-60),0),
              coalesce(sum(amount) filter(where due_date < $4-60 and due_date >= $4-90),0),
              coalesce(sum(amount) filter(where due_date < $4-90),0)
            from remaining where amount > 0 and ($7::text is null or transaction_currency=$7) group by transaction_currency order by transaction_currency
            """;
        await using var command = new NpgsqlCommand(useCurrentState ? currentSql : historicalSql, connection, transaction);
        Add(command, tenant.Value, query.LegalEntityId.Value, query.PartyId, query.AsOfDate, query.Direction.ToString(),
            query.Direction == SubledgerDirectionContract.Receivable ? "Debit" : "Credit", Db(query.CurrencyCode));
        var result = new List<AgingBucketResult>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0).Trim(), D(reader, 1), D(reader, 2), D(reader, 3), D(reader, 4), D(reader, 5)));
        return result;
    }

    public async Task<PartyStatementResult> GetStatementAsync(TenantId tenant, PartyStatementQuery query, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string sql = """
            with party_items as materialized (
              select i.id,i.transaction_currency,i.polarity,i.original_transaction_amount
              from finance_subledger.party_open_item i
              where i.tenant_id=$1 and i.legal_entity_id=$2 and i.party_id=$3 and i.direction=$4 and i.posting_date < $5
            ), application_effects as (
              select item_id,sum(amount) as amount from (
                select a.debit_item_id as item_id,a.debit_applied_amount as amount
                from finance_subledger.application_batch b
                join finance_subledger.open_item_application a
                  on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
                join party_items i on i.id=a.debit_item_id
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$4 and a.application_date < $5
                union all
                select a.credit_item_id as item_id,a.credit_applied_amount as amount
                from finance_subledger.application_batch b
                join finance_subledger.open_item_application a
                  on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
                join party_items i on i.id=a.credit_item_id
                where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$4 and a.application_date < $5
              ) lines group by item_id
            ), reversal_effects as (
              select item_id,sum(amount) as amount from (
                select a.debit_item_id as item_id,a.debit_applied_amount as amount
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch rb
                  on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                join party_items i on i.id=a.debit_item_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$4 and r.effective_date < $5
                union all
                select a.credit_item_id as item_id,a.credit_applied_amount as amount
                from finance_subledger.application_reversal r
                join finance_subledger.application_batch rb
                  on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                join party_items i on i.id=a.credit_item_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$4 and r.effective_date < $5
              ) lines group by item_id
            ), write_off_effects as (
              select w.open_item_id as item_id,sum(w.amount) as amount
              from finance_subledger.write_off w
              join party_items i on i.id=w.open_item_id
              where w.tenant_id=$1 and w.legal_entity_id=$2 and w.effective_date < $5
              group by w.open_item_id
            ), opening as (
              select i.transaction_currency,
                sum(case when i.polarity=$6 then i.original_transaction_amount - coalesce(a.amount,0) + coalesce(r.amount,0) - coalesce(w.amount,0)
                         else -(i.original_transaction_amount - coalesce(a.amount,0) + coalesce(r.amount,0) - coalesce(w.amount,0)) end) as balance
              from party_items i
              left join application_effects a on a.item_id=i.id
              left join reversal_effects r on r.item_id=i.id
              left join write_off_effects w on w.item_id=i.id
              group by i.transaction_currency
            ), events as (
              select posting_date as effective_date,posting_date,10::bigint as source_sequence,id,source_type,source_reference,
                transaction_currency as currency,
                case when polarity=$6 then original_transaction_amount else -original_transaction_amount end as amount
              from finance_subledger.party_open_item
              where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and direction=$4 and posting_date between $5 and $7
              union all
              select a.application_date,a.settlement_date,20::bigint,a.id,'ApplicationDebit',a.id::text,d.transaction_currency,
                case when d.polarity=$6 then -a.debit_applied_amount else a.debit_applied_amount end
              from finance_subledger.application_batch b
              join finance_subledger.open_item_application a on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
              join finance_subledger.party_open_item d on d.tenant_id=a.tenant_id and d.legal_entity_id=a.legal_entity_id and d.id=a.debit_item_id
              where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$4 and b.operation='Apply' and b.application_date between $5 and $7
              union all
              select a.application_date,a.settlement_date,21::bigint,a.id,'ApplicationCredit',a.id::text,c.transaction_currency,
                case when c.polarity=$6 then -a.credit_applied_amount else a.credit_applied_amount end
              from finance_subledger.application_batch b
              join finance_subledger.open_item_application a on a.tenant_id=b.tenant_id and a.legal_entity_id=b.legal_entity_id and a.batch_id=b.id
              join finance_subledger.party_open_item c on c.tenant_id=a.tenant_id and c.legal_entity_id=a.legal_entity_id and c.id=a.credit_item_id
              where b.tenant_id=$1 and b.legal_entity_id=$2 and b.party_id=$3 and b.direction=$4 and b.operation='Apply' and b.application_date between $5 and $7
              union all
              select r.effective_date,r.effective_date,30::bigint,r.id,'ApplicationReversalDebit',r.original_application_id::text,d.transaction_currency,
                case when d.polarity=$6 then a.debit_applied_amount else -a.debit_applied_amount end
              from finance_subledger.application_reversal r
              join finance_subledger.application_batch rb on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
              join finance_subledger.open_item_application a on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
              join finance_subledger.party_open_item d on d.tenant_id=a.tenant_id and d.legal_entity_id=a.legal_entity_id and d.id=a.debit_item_id
              where rb.tenant_id=$1 and rb.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$4 and r.effective_date between $5 and $7
              union all
              select r.effective_date,r.effective_date,31::bigint,r.id,'ApplicationReversalCredit',r.original_application_id::text,c.transaction_currency,
                case when c.polarity=$6 then a.credit_applied_amount else -a.credit_applied_amount end
              from finance_subledger.application_reversal r
              join finance_subledger.application_batch rb on rb.tenant_id=r.tenant_id and rb.legal_entity_id=r.legal_entity_id and rb.id=r.reversal_batch_id
              join finance_subledger.open_item_application a on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
              join finance_subledger.party_open_item c on c.tenant_id=a.tenant_id and c.legal_entity_id=a.legal_entity_id and c.id=a.credit_item_id
              where rb.tenant_id=$1 and rb.legal_entity_id=$2 and rb.party_id=$3 and rb.direction=$4 and r.effective_date between $5 and $7
              union all
              select w.effective_date,w.effective_date,25::bigint,w.id,'WriteOff',wr.reason_code,i.transaction_currency,
                case when i.polarity=$6 then -w.amount else w.amount end
              from finance_subledger.write_off w
              join finance_subledger.write_off_request wr on wr.tenant_id=w.tenant_id and wr.legal_entity_id=w.legal_entity_id and wr.id=w.request_id
              join finance_subledger.party_open_item i on i.tenant_id=w.tenant_id and i.legal_entity_id=w.legal_entity_id and i.id=w.open_item_id
              where w.tenant_id=$1 and w.legal_entity_id=$2 and w.party_id=$3 and w.direction=$4 and w.effective_date between $5 and $7
            ), rows as (
              select $5::date as effective_date,$5::date as posting_date,0::bigint as source_sequence,
                md5(transaction_currency)::uuid as id,'OpeningBalance'::text as source_type,'Opening balance'::text as source_reference,
                transaction_currency as currency,0::numeric as amount,balance as opening_balance
              from opening where balance <> 0
              union all
              select effective_date,posting_date,source_sequence,id,source_type,source_reference,currency,amount,0::numeric
              from events
            )
            select effective_date,posting_date,source_sequence,id,source_type,source_reference,currency,amount,
              sum(opening_balance + amount) over(partition by currency order by effective_date,posting_date,source_sequence,id rows between unbounded preceding and current row) as running_balance
            from rows
            where ($8::text is null or currency=$8)
            order by currency,effective_date,posting_date,source_sequence,id
            limit $9 offset $10
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        Add(command, tenant.Value, query.LegalEntityId.Value, query.PartyId, query.Direction.ToString(), query.FromDate,
            query.Direction == SubledgerDirectionContract.Receivable ? "Debit" : "Credit", query.ToDate, Db(query.CurrencyCode), query.Limit + 1, (query.Page - 1) * query.Limit);
        var lines = new List<PartyStatementLine>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetFieldValue<DateOnly>(0), reader.GetFieldValue<DateOnly>(1), reader.GetInt64(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetString(6).Trim(), D(reader, 7), D(reader, 8)));
        var hasMore = lines.Count > query.Limit;
        if (hasMore) lines.RemoveAt(lines.Count - 1);
        var closing = lines.GroupBy(x => x.CurrencyCode, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().RunningBalance, StringComparer.Ordinal);
        return new(query.PartyId, query.LegalEntityId, query.Direction, query.FromDate, query.ToDate, lines, closing, hasMore, query.Page);
    }

    public async Task<IReadOnlyList<ReconciliationSourceContext>> GetReconciliationSourceContextsAsync(TenantId tenant, LegalEntityId entity, DateOnly asOfDate, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string sql = """
            with item_balance as (
              select i.id,i.direction,i.polarity,i.control_account_role,i.control_account_reference,i.control_configuration_reference,i.base_currency,
                i.original_base_amount as original_amount
              from finance_subledger.party_open_item i
              where i.tenant_id=$1 and i.legal_entity_id=$2 and i.posting_date<=$3
            ), application_effects as (
              select item_id,sum(amount) amount from (
                select a.debit_item_id item_id,a.debit_base_carrying_amount_closed amount
                from finance_subledger.open_item_application a
                where a.tenant_id=$1 and a.legal_entity_id=$2 and a.application_date<=$3
                union all
                select a.credit_item_id item_id,a.credit_base_carrying_amount_closed amount
                from finance_subledger.open_item_application a
                where a.tenant_id=$1 and a.legal_entity_id=$2 and a.application_date<=$3
              ) lines group by item_id
            ), reversal_effects as (
              select item_id,sum(amount) amount from (
                select a.debit_item_id item_id,a.debit_base_carrying_amount_closed amount
                from finance_subledger.application_reversal r
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and r.effective_date<=$3
                union all
                select a.credit_item_id item_id,a.credit_base_carrying_amount_closed amount
                from finance_subledger.application_reversal r
                join finance_subledger.open_item_application a
                  on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
                where r.tenant_id=$1 and r.legal_entity_id=$2 and r.effective_date<=$3
              ) lines group by item_id
            ), write_off_effects as (
              select w.open_item_id item_id,sum(w.base_amount) amount
              from finance_subledger.write_off w
              where w.tenant_id=$1 and w.legal_entity_id=$2 and w.effective_date<=$3
              group by w.open_item_id
            ), balances as (
              select i.*,a.amount applied_amount,r.amount reversed_amount,w.amount written_off_amount
              from item_balance i
              left join application_effects a on a.item_id=i.id
              left join reversal_effects r on r.item_id=i.id
              left join write_off_effects w on w.item_id=i.id
            )
            select direction,control_account_role,control_account_reference,control_configuration_reference,base_currency,
              sum(case when (direction='Receivable' and polarity='Debit') or (direction='Payable' and polarity='Credit')
                then original_amount-coalesce(applied_amount,0)+coalesce(reversed_amount,0)-coalesce(written_off_amount,0)
                else -(original_amount-coalesce(applied_amount,0)+coalesce(reversed_amount,0)-coalesce(written_off_amount,0)) end)
            from balances group by direction,control_account_role,control_configuration_reference,control_account_reference,base_currency
            order by direction,control_account_role,control_configuration_reference,control_account_reference
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        Add(command, tenant.Value, entity.Value, asOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ReconciliationSourceContext>();
        while (await reader.ReadAsync(ct)) result.Add(new(Enum.Parse<SubledgerDirectionContract>(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4).Trim(), reader.GetDecimal(5)));
        return result;
    }

    public Task<ReconciliationResult> SaveReconciliationRunAsync(TenantId tenant, LegalEntityId entity, DateOnly asOfDate, string baseCurrency,
        IReadOnlyList<ReconciliationDetail> details, Guid actorId, Guid correlationId, DateTimeOffset createdAt, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var runId = Guid.CreateVersion7();
            var subledgerTotal = details.Sum(detail => ParseAmount(detail.SubledgerBaseBalance));
            var ledgerTotal = details.Sum(detail => ParseAmount(detail.LedgerBaseBalance));
            var variance = subledgerTotal - ledgerTotal;
            var balanced = details.Count > 0 && details.All(detail => detail.ContextResolved) && variance == 0m;
            await using var insert = new NpgsqlCommand("insert into finance_subledger.control_reconciliation_run(tenant_id,legal_entity_id,id,as_of_date,base_currency,subledger_base_balance,ledger_base_balance,variance,is_balanced,created_by,correlation_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", context.Connection, context.Transaction);
            Add(insert, tenant.Value, entity.Value, runId, asOfDate, baseCurrency, subledgerTotal, ledgerTotal, variance, balanced, actorId, correlationId, createdAt);
            await insert.ExecuteNonQueryAsync(token);
            foreach (var detail in details)
            {
                await using var row = new NpgsqlCommand("insert into finance_subledger.control_reconciliation_detail(tenant_id,legal_entity_id,id,run_id,direction,control_role,control_account_reference,control_configuration_reference,ledger_account_id,subledger_base_balance,ledger_base_balance,variance,context_resolved) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction);
                Add(row, tenant.Value, entity.Value, detail.Id, runId, detail.Direction.ToString(), detail.ControlRole, detail.ControlAccountReference, detail.ControlConfigurationReference,
                    Db(detail.LedgerAccountId), ParseAmount(detail.SubledgerBaseBalance), ParseAmount(detail.LedgerBaseBalance), ParseAmount(detail.Variance), detail.ContextResolved);
                await row.ExecuteNonQueryAsync(token);
            }
            await AuditAsync(context, tenant, entity, actorId, null, "control_reconciliation_completed", "control_reconciliation_run", runId,
                null, JsonSerializer.Serialize(new { asOfDate, IsBalanced = balanced, Variance = variance }), null, correlationId, createdAt, token);
            return new ReconciliationResult(runId, entity, asOfDate, baseCurrency, subledgerTotal.ToString(CultureInfo.InvariantCulture), ledgerTotal.ToString(CultureInfo.InvariantCulture), variance.ToString(CultureInfo.InvariantCulture), balanced, createdAt, details);
        }, ct);

    public async Task<ReconciliationResult?> GetReconciliationRunAsync(TenantId tenant, LegalEntityId entity, Guid runId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        DateOnly asOfDate;
        string baseCurrency;
        string subledgerBalance;
        string ledgerBalance;
        string variance;
        bool balanced;
        DateTimeOffset createdAt;
        await using (var command = new NpgsqlCommand("select as_of_date,base_currency,subledger_base_balance,ledger_base_balance,variance,is_balanced,created_at from finance_subledger.control_reconciliation_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction))
        {
            Add(command, tenant.Value, entity.Value, runId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            asOfDate = reader.GetFieldValue<DateOnly>(0);
            baseCurrency = reader.GetString(1).Trim();
            subledgerBalance = D(reader, 2);
            ledgerBalance = D(reader, 3);
            variance = D(reader, 4);
            balanced = reader.GetBoolean(5);
            createdAt = reader.GetFieldValue<DateTimeOffset>(6);
        }
        var details = new List<ReconciliationDetail>();
        await using (var command = new NpgsqlCommand("select id,direction,control_role,control_account_reference,control_configuration_reference,ledger_account_id,subledger_base_balance,ledger_base_balance,variance,context_resolved from finance_subledger.control_reconciliation_detail where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 order by direction,control_role,control_account_reference,id", connection, transaction))
        {
            Add(command, tenant.Value, entity.Value, runId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) details.Add(new(reader.GetGuid(0), Enum.Parse<SubledgerDirectionContract>(reader.GetString(1)), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), D(reader, 6), D(reader, 7), D(reader, 8), reader.GetBoolean(9)));
        }
        return new(runId, entity, asOfDate, baseCurrency, subledgerBalance, ledgerBalance, variance, balanced, createdAt, details);
    }

    private static async Task<IReadOnlyList<OpeningSubledgerStoredLine>> LoadOpeningLinesInTransactionAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,row_key,payload_hash,created_open_item_id,party_id,direction,polarity,source_reference,document_date,due_date,transaction_currency,transaction_amount,base_currency,base_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot from finance_subledger.opening_batch_item where tenant_id=$1 and legal_entity_id=$2 and batch_id=$3 order by row_key", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, batchId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<OpeningSubledgerStoredLine>();
        while (await reader.ReadAsync(ct))
        {
            var line = new OpeningSubledgerLineCommand(reader.GetString(1), reader.GetGuid(4), Enum.Parse<SubledgerDirectionContract>(reader.GetString(5)), Enum.Parse<SubledgerPolarityContract>(reader.GetString(6)),
                reader.GetString(7), reader.GetFieldValue<DateOnly>(8), reader.GetFieldValue<DateOnly>(9), reader.GetString(10).Trim(), reader.GetDecimal(11).ToString(CultureInfo.InvariantCulture),
                reader.GetString(12).Trim(), reader.GetDecimal(13).ToString(CultureInfo.InvariantCulture), reader.GetString(14), reader.GetString(15), reader.GetString(16), reader.IsDBNull(17) ? null : reader.GetString(17));
            rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2).Trim(), reader.IsDBNull(3) ? null : reader.GetGuid(3), line));
        }
        return rows;
    }

    private static async Task<Dictionary<Guid, LockedItem>> LockItemsAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid[] sortedIds, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,party_id,direction,polarity,transaction_currency,open_transaction_amount,settled_transaction_amount,base_currency,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=any($3) order by id for update", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, sortedIds);
        var result = new Dictionary<Guid, LockedItem>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0), new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4).Trim(), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetString(7).Trim(), reader.GetDecimal(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetInt64(13)));
        return result;
    }

    private static void ValidatePair(LockedItem debit, LockedItem credit, Guid partyId, SubledgerDirectionContract direction, decimal debitAmount, decimal creditAmount, string? debitFxSnapshot, string? creditFxSnapshot, decimal settlementBaseAmount)
    {
        if (debit.Status is not ("Open" or "PartiallySettled") || credit.Status is not ("Open" or "PartiallySettled")) throw new SubledgerException("subledger.item_closed", "Both open items must have an available balance.");
        if (debit.PartyId != partyId || credit.PartyId != partyId) throw new SubledgerException("subledger.party_mismatch", "Open items from different parties cannot be applied.");
        if (debit.Direction != direction.ToString() || credit.Direction != direction.ToString()) throw new SubledgerException("subledger.direction_mismatch", "Receivable and payable items cannot be applied together.");
        if (debit.Polarity != "Debit" || credit.Polarity != "Credit") throw new SubledgerException("subledger.polarity_mismatch", "An application must pair a debit item with a credit item.");
        if (debit.ControlRole != credit.ControlRole || debit.ControlReference != credit.ControlReference || debit.ControlConfiguration != credit.ControlConfiguration || debit.BaseCurrency != credit.BaseCurrency) throw new SubledgerException("subledger.control_context_mismatch", "Open items with different accounting control context cannot be applied.");
        if (debitAmount <= 0m || creditAmount <= 0m) throw new SubledgerException("subledger.amount_invalid", "Both applied amounts must be greater than zero.");
        if (settlementBaseAmount < 0m) throw new SubledgerException("subledger.amount_invalid", "The base-currency settlement amount cannot be negative.");
        if (string.Equals(debit.Currency, credit.Currency, StringComparison.Ordinal))
        {
            if (debitAmount != creditAmount) throw new SubledgerException("subledger.same_currency_amount_mismatch", "Same-currency applications must use equal transaction amounts.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(debitFxSnapshot) || string.IsNullOrWhiteSpace(creditFxSnapshot)) throw new SubledgerException("subledger.fx_snapshot_required", "Cross-currency applications require approved FinBase exchange-rate snapshots for both items.");
            if (settlementBaseAmount == 0m) throw new SubledgerException("subledger.fx_equivalent_mismatch", "A cross-currency application must have a positive base-currency settlement amount.");
            try { using var _ = JsonDocument.Parse(debitFxSnapshot); using var __ = JsonDocument.Parse(creditFxSnapshot); }
            catch (JsonException) { throw new SubledgerException("subledger.fx_snapshot_invalid", "Settlement exchange-rate snapshots must contain valid JSON."); }
        }
    }

    private static decimal BasePortion(decimal remainingTransactionAmount, decimal remainingBaseAmount, decimal transactionAmount, int scale, Shared.Primitives.Financial.FinancialRoundingModeValue roundingMode)
    {
        if (transactionAmount == remainingTransactionAmount) return remainingBaseAmount;
        var ratio = remainingTransactionAmount == 0m ? 0m : transactionAmount / remainingTransactionAmount;
        return Shared.Primitives.Financial.FinancialRounding.Round(remainingBaseAmount * ratio, scale, roundingMode);
    }

    private async Task ReduceItemAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid id, decimal amount, decimal baseAmount, LockedItem item, CancellationToken ct)
    {
        var nextOpen = item.OpenAmount - amount; var nextBase = item.OpenBase - baseAmount;
        if (nextOpen < 0m || nextBase < 0m) throw new SubledgerException("subledger.application_over_target", "The application exceeds the remaining balance.");
        var settled = item.OpenAmount - nextOpen;
        var status = nextOpen == 0m ? "Settled" : settled > 0m ? "PartiallySettled" : "Open";
        await using var update = new NpgsqlCommand("update finance_subledger.party_open_item set settled_transaction_amount=settled_transaction_amount+$4,open_transaction_amount=$5,settled_base_carrying_amount=settled_base_carrying_amount+$6,open_base_carrying_amount=$7,status=$8,is_open=$9,closed_at=case when $9 then null else $10 end,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$11", context.Connection, context.Transaction);
        Add(update, tenant.Value, entity.Value, id, amount, nextOpen, baseAmount, nextBase, status, nextOpen > 0m, clock.GetUtcNow(), item.RowVersion);
        if (await update.ExecuteNonQueryAsync(ct) != 1) throw new SubledgerException("subledger.item_changed", "An open-item balance changed during application. Refresh and try again.");
    }

    private static async Task RestoreItemAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, LockedItem item, decimal amount, decimal baseAmount, CancellationToken ct)
    {
        var open = item.OpenAmount + amount; var openBase = item.OpenBase + baseAmount;
        if (open < 0m || openBase < 0m) throw new SubledgerException("subledger.reversal_invalid", "The application reversal would create an invalid balance.");
        var settled = item.SettledAmount - amount;
        var status = SubledgerRules.StatusFor(settled, open).ToString();
        await using var update = new NpgsqlCommand("update finance_subledger.party_open_item set settled_transaction_amount=settled_transaction_amount-$4,open_transaction_amount=$5,settled_base_carrying_amount=settled_base_carrying_amount-$6,open_base_carrying_amount=$7,status=$8,is_open=true,closed_at=null,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$9", context.Connection, context.Transaction);
        Add(update, tenant.Value, entity.Value, item.Id, amount, open, baseAmount, openBase, status, item.RowVersion);
        if (await update.ExecuteNonQueryAsync(ct) != 1) throw new SubledgerException("subledger.item_changed", "An open-item balance changed during reversal. Refresh and try again.");
    }

    private async Task PostSettlementFxDifferenceAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid applicationId, string baseCurrency,
        string controlRole, string historicalConfigurationReference, decimal signedDifference, DateOnly postingDate, Guid actorId, Guid correlationId, string sourceReference, CancellationToken ct, bool reverse = false)
    {
        if (signedDifference == 0m) return;
        if (ledgerPosting is null) throw new SubledgerException("subledger.ledger_capability_unavailable", "Ledger settlement FX posting is unavailable; no application state can be committed.");
        if (reverse) signedDifference = -signedDifference;
        var factId = Guid.CreateVersion7();
        var historicalConfigurationId = ParseConfigurationId(historicalConfigurationReference);
        var fxPayload = new SettlementFxDifferencePayload(Math.Abs(signedDifference), baseCurrency, controlRole, signedDifference > 0m ? "FXLoss" : "FXGain", applicationId, historicalConfigurationId);
        IAccountingFactPayload payload = fxPayload;
        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, entity, AccountingFactTypes.SettlementFxDifference, 1, "subledger", sourceReference,
            $"subledger-settlement-fx:{sourceReference}", postingDate, postingDate, payload, correlationId, actorId);
        try { await ledgerPosting.PostSettlementFxDifferenceAsync(context, fact, fxPayload, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            throw new SubledgerException("subledger.ledger_fx_post_failed",
                "The settlement FX accounting fact could not be posted. The Subledger operation was rolled back.", exception);
        }
    }

    private static async Task<OriginalApplication?> LockApplicationAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid appId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select a.id,a.debit_item_id,a.credit_item_id,a.debit_applied_amount,a.credit_applied_amount,a.debit_base_carrying_amount_closed,a.credit_base_carrying_amount_closed,d.party_id,d.direction,a.base_currency,a.application_date from finance_subledger.open_item_application a join finance_subledger.party_open_item d on d.tenant_id=a.tenant_id and d.legal_entity_id=a.legal_entity_id and d.id=a.debit_item_id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.id=$3 and not exists(select 1 from finance_subledger.application_reversal r where r.tenant_id=a.tenant_id and r.legal_entity_id=a.legal_entity_id and r.original_application_id=a.id) for update of a", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, appId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetGuid(7), reader.GetString(8), reader.GetString(9).Trim(), reader.GetFieldValue<DateOnly>(10)) : null;
    }

    private static async Task<(Guid Id, string Hash)?> FindBatchAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, string operation, string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,payload_hash from finance_subledger.application_batch where tenant_id=$1 and legal_entity_id=$2 and operation=$3 and idempotency_key=$4", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, operation, key); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetString(1).Trim()) : null;
    }

    private static async Task<IReadOnlyList<Guid>> ApplicationIdsAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id from finance_subledger.open_item_application where tenant_id=$1 and legal_entity_id=$2 and batch_id=$3 order by sequence_no", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, batchId); var result = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0)); return result;
    }

    private static async Task<IReadOnlyList<Guid>> ReversedApplicationIdsAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select original_application_id from finance_subledger.application_reversal where tenant_id=$1 and legal_entity_id=$2 and reversal_batch_id=$3", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, batchId); var result = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0)); return result;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct);
    }

    private static void EnqueueEvent(ITransactionContext context, TenantId tenant, string messageType, object payload, Guid causationId, Guid correlationId, DateTimeOffset occurredAt) =>
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), messageType, JsonSerializer.Serialize(payload, JsonOptions), tenant, occurredAt,
            correlationId, causationId, "subledger", Activity.Current?.Id));

    private static Task AuditAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid actorId, Guid? membershipId, string action,
        string entityType, Guid entityId, string? before, string? after, string? reason, Guid correlationId, DateTimeOffset occurredAt, CancellationToken ct) =>
        NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
            new AuditEntry(Guid.CreateVersion7(), tenant, entity.Value, null, "Human", actorId, membershipId, "subledger", action, entityType, entityId,
                before, after, reason, "api", correlationId, Activity.Current?.TraceId.ToString(), occurredAt, null), ct);

    private static async Task WriteWriteOffAuditAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid requestId, string action, Guid actorId, string evidence, DateTimeOffset createdAt, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into finance_subledger.write_off_request_audit(tenant_id,legal_entity_id,id,request_id,action,actor_id,evidence,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8)", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, Guid.CreateVersion7(), requestId, action, actorId, evidence, createdAt);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<WriteOffResult> ReadWriteOffResultAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid requestId, bool alreadyCompleted, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select w.id,w.request_id,w.open_item_id,w.amount,i.transaction_currency,w.base_amount,w.base_currency,w.effective_date,w.ledger_fact_id,w.approved_by from finance_subledger.write_off w join finance_subledger.party_open_item i on i.tenant_id=w.tenant_id and i.legal_entity_id=w.legal_entity_id and i.id=w.open_item_id where w.tenant_id=$1 and w.legal_entity_id=$2 and w.request_id=$3", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, requestId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new SubledgerException("subledger.writeoff_result_missing", "The approved write-off record could not be loaded.");
        var result = new WriteOffResult(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3).ToString(CultureInfo.InvariantCulture),
            reader.GetString(4).Trim(), reader.GetDecimal(5).ToString(CultureInfo.InvariantCulture), reader.GetString(6).Trim(), reader.GetFieldValue<DateOnly>(7),
            reader.GetGuid(8), reader.GetGuid(9), alreadyCompleted);
        return result;
    }

    private static PartyFinancialProfileResult ReadProfile(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5).ToString(CultureInfo.InvariantCulture), reader.IsDBNull(6) ? null : reader.GetString(6).Trim(), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt64(9), reader.GetFieldValue<DateTimeOffset>(10));

    private static WriteOffPolicyResult ReadWriteOffPolicy(NpgsqlDataReader reader) => new(reader.GetGuid(0), new LegalEntityId(reader.GetGuid(1)), Enum.Parse<SubledgerDirectionContract>(reader.GetString(2)), reader.GetString(3).Trim(), reader.GetDecimal(4).ToString(CultureInfo.InvariantCulture), reader.GetDecimal(5).ToString(CultureInfo.InvariantCulture), reader.GetBoolean(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8));

    private static PartyOpenItemResult ReadItem(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), Enum.Parse<SubledgerDirectionContract>(r.GetString(3)), Enum.Parse<SubledgerPolarityContract>(r.GetString(4)), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetFieldValue<DateOnly>(9), r.GetFieldValue<DateOnly>(10), r.GetFieldValue<DateOnly>(11), r.GetString(12).Trim(), D(r, 13), D(r, 14), D(r, 15), r.GetString(16).Trim(), D(r, 17), D(r, 18), D(r, 19), r.GetString(20), r.GetString(21), r.GetString(22), r.GetInt64(23));
    private static string D(NpgsqlDataReader r, int index) => r.GetDecimal(index).ToString(CultureInfo.InvariantCulture);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions))).ToLowerInvariant();
    private static decimal ParseAmount(string text) => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : throw new SubledgerException("subledger.amount_invalid", "Financial amounts must be invariant decimal values.");
    private static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant() is { Length: 3 } currency && currency.All(char.IsAsciiLetter) ? currency : throw new SubledgerException("subledger.currency_invalid", "A valid three-letter currency code is required.");
    private static string NormalizeMatchReference(string value) => string.Concat(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid ParseConfigurationId(string reference) => reference.StartsWith("configuration:", StringComparison.Ordinal)
        && Guid.TryParse(reference["configuration:".Length..], out var id)
            ? id
            : throw new SubledgerException("subledger.ledger_configuration_reference_invalid", "The open item has no valid historical Ledger configuration reference.");
    private static object Db(object? value) => value ?? DBNull.Value;
    private static Guid? ParseCursor(string? value) => Guid.TryParse(value, out var id) ? id : null;
    private static (DateOnly? Date, Guid? Id) ParseDateCursor(string? value, string sort) =>
        TryDecodeDateCursor(value, sort, out var date, out var id) ? (date, id) : (null, null);
    private static bool TryDecodeDateCursor(string? value, string sort, out DateOnly date, out Guid id)
    {
        date = default; id = default;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|', StringSplitOptions.None);
            return parts.Length == 3 && parts[0] == sort && DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && Guid.TryParse(parts[2], out id);
        }
        catch (FormatException) { return false; }
    }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }

    private sealed record LockedItem(Guid Id, Guid PartyId, string Direction, string Polarity, string Currency, decimal OpenAmount, decimal SettledAmount, string BaseCurrency, decimal OpenBase, string ControlRole, string ControlReference, string ControlConfiguration, string Status, long RowVersion);
    private sealed record NormalizedLine(Guid DebitItemId, Guid CreditItemId, decimal Amount, decimal CreditAmount, string? DebitFxSnapshot, string? CreditFxSnapshot, decimal SettlementBaseAmount);
    private sealed record OriginalApplication(Guid Id, Guid DebitId, Guid CreditId, decimal DebitAmount, decimal CreditAmount, decimal DebitBaseClosed, decimal CreditBaseClosed, Guid PartyId, string Direction, string BaseCurrency, DateOnly ApplicationDate);
}
