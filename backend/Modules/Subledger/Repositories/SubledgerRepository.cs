using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Modules.Subledger.Entities;
using Npgsql;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;

namespace Modules.Subledger.Repositories;

public sealed class SubledgerRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public Task<PartyFinancialProfileResult> SaveProfileAsync(TenantId tenant, PartyFinancialProfileCommand request, decimal? creditLimit, string? creditCurrency, CancellationToken ct) =>
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
            await using var reader = await query.ExecuteReaderAsync(token); await reader.ReadAsync(token);
            return ReadProfile(reader);
        }, ct);

    public Task<IReadOnlyList<Guid>> CreateOpenItemsAsync(TenantId tenant, LegalEntityId entity, Guid partyId, SubledgerDirection direction, OpenItemPolarity polarity, string sourceType, IReadOnlyList<OpenItemSnapshotContract> items, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var ids = new List<Guid>(items.Count);
            foreach (var item in items)
            {
                var payloadHash = Hash(new { PartyId = partyId, Direction = direction, Polarity = polarity, SourceType = sourceType, Item = item });
                var id = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into finance_subledger.party_open_item(tenant_id,legal_entity_id,id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,open_transaction_amount,base_currency,original_base_amount,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,historic_fx_snapshot,source_version,source_payload_hash,status,is_open,row_version,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$15,$16,$17,$17,$18,$19,$20,$21::jsonb,$22,$23,'Open',true,1,$24) on conflict do nothing", context.Connection, context.Transaction);
                Add(insert, tenant.Value, entity.Value, id, partyId, direction.ToString(), polarity.ToString(), sourceType, item.SourceReference.Trim(), Db(Clean(item.SourceScheduleReference)), Db(Clean(item.SourceDisplayNumber)), item.DocumentDate, item.PostingDate, item.DueDate, NormalizeCurrency(item.TransactionCurrency), ParseAmount(item.OriginalTransactionAmount), NormalizeCurrency(item.BaseCurrency), ParseAmount(item.OriginalBaseAmount), item.ControlAccountRole, item.ControlAccountReference.Trim(), item.ControlConfigurationReference.Trim(), Db(item.HistoricFxSnapshot), item.SourceVersion, payloadHash, clock.GetUtcNow());
                if (await insert.ExecuteNonQueryAsync(token) == 1) { ids.Add(id); continue; }
                await using var existing = new NpgsqlCommand("select id,source_payload_hash from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type=$3 and source_reference=$4 and source_schedule_reference is not distinct from $5::text", context.Connection, context.Transaction);
                Add(existing, tenant.Value, entity.Value, sourceType, item.SourceReference.Trim(), Db(Clean(item.SourceScheduleReference)));
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new SubledgerException("subledger.open_item_conflict", "The source open item could not be created.");
                var existingId = reader.GetGuid(0); var existingHash = reader.GetString(1).Trim(); await reader.CloseAsync();
                if (!string.Equals(existingHash, payloadHash, StringComparison.Ordinal)) throw new SubledgerException("subledger.source_conflict", "The source identity already exists with different financial facts.");
                ids.Add(existingId);
            }
            return (IReadOnlyList<Guid>)ids;
        }, ct);

    public Task<SubledgerApplicationResult> ApplyAsync(TenantId tenant, ApplyOpenItemsCommand request, IReadOnlyList<decimal> amounts, int moneyScale, Shared.Primitives.Financial.FinancialRoundingModeValue roundingMode, CancellationToken ct) =>
        transactions.ExecuteAsync<SubledgerApplicationResult>(tenant, async (context, token) =>
        {
            var lines = request.Lines.Select((line, index) => new NormalizedLine(line.DebitItemId, line.CreditItemId, amounts[index]))
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
            var debitTotals = new Dictionary<Guid, decimal>(); var creditTotals = new Dictionary<Guid, decimal>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]; var amount = line.Amount;
                if (!items.TryGetValue(line.DebitItemId, out var debit) || !items.TryGetValue(line.CreditItemId, out var credit)) throw new SubledgerException("subledger.open_item_not_found", "One or more open items were not found.");
                ValidatePair(debit, credit, request.PartyId, request.Direction, amount);
                debitTotals[line.DebitItemId] = debitTotals.GetValueOrDefault(line.DebitItemId) + amount;
                creditTotals[line.CreditItemId] = creditTotals.GetValueOrDefault(line.CreditItemId) + amount;
            }
            foreach (var pair in debitTotals) if (pair.Value > items[pair.Key].OpenAmount) throw new SubledgerException("subledger.application_over_target", "The application exceeds the remaining amount.");
            foreach (var pair in creditTotals) if (pair.Value > items[pair.Key].OpenAmount) throw new SubledgerException("subledger.application_over_source", "The application exceeds the available credit.");

            var closedBases = new List<(decimal Debit, decimal Credit)>();
            var remainingAmounts = items.ToDictionary(pair => pair.Key, pair => pair.Value.OpenAmount);
            var remainingBase = items.ToDictionary(pair => pair.Key, pair => pair.Value.OpenBase);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]; var amount = line.Amount;
                var debit = items[line.DebitItemId]; var credit = items[line.CreditItemId];
                var debitBase = BasePortion(remainingAmounts[line.DebitItemId], remainingBase[line.DebitItemId], amount, moneyScale, roundingMode);
                var creditBase = BasePortion(remainingAmounts[line.CreditItemId], remainingBase[line.CreditItemId], amount, moneyScale, roundingMode);
                remainingAmounts[line.DebitItemId] -= amount; remainingBase[line.DebitItemId] -= debitBase;
                remainingAmounts[line.CreditItemId] -= amount; remainingBase[line.CreditItemId] -= creditBase;
                closedBases.Add((debitBase, creditBase));
                var appId = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into finance_subledger.open_item_application(tenant_id,legal_entity_id,id,batch_id,debit_item_id,credit_item_id,debit_applied_amount,credit_applied_amount,base_currency,debit_base_carrying_amount_closed,credit_base_carrying_amount_closed,application_date,settlement_date,sequence_no,created_at) values($1,$2,$3,$4,$5,$6,$7,$7,$8,$9,$10,$11,$12,$13,$14)", context.Connection, context.Transaction);
                Add(insert, tenant.Value, request.LegalEntityId.Value, appId, batchId, line.DebitItemId, line.CreditItemId, amount, debit.BaseCurrency, debitBase, creditBase, request.ApplicationDate, request.SettlementDate, (long)i + 1, clock.GetUtcNow());
                await insert.ExecuteNonQueryAsync(token);
            }
            foreach (var id in debitTotals.Keys.Union(creditTotals.Keys).Order())
            {
                var item = items[id]; var applied = debitTotals.GetValueOrDefault(id) + creditTotals.GetValueOrDefault(id);
                var baseApplied = 0m;
                for (var i = 0; i < lines.Length; i++) if (lines[i].DebitItemId == id) baseApplied += closedBases[i].Debit; else if (lines[i].CreditItemId == id) baseApplied += closedBases[i].Credit;
                await ReduceItemAsync(context, tenant, request.LegalEntityId, id, applied, baseApplied, item, token);
            }
            return new(batchId, false, await ApplicationIdsAsync(context, tenant, request.LegalEntityId, batchId, token));
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
            await RestoreItemAsync(context, tenant, request.LegalEntityId, items[original.DebitId], original.Amount, original.DebitBaseClosed, token);
            await RestoreItemAsync(context, tenant, request.LegalEntityId, items[original.CreditId], original.Amount, original.CreditBaseClosed, token);
            return new(batchId, false, [request.ApplicationId]);
        }, ct);

    public async Task<OpenItemPage> ListAsync(TenantId tenant, OpenItemListQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,party_id,direction,polarity,source_type,source_reference,source_schedule_reference,source_display_number,document_date,posting_date,due_date,transaction_currency,original_transaction_amount,settled_transaction_amount,open_transaction_amount,base_currency,original_base_amount,settled_base_carrying_amount,open_base_carrying_amount,control_account_role,control_account_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and ($4::text is null or direction=$4) and (not $5 or is_open) and ($6::uuid is null or id>$6) order by id limit $7", connection, transaction);
        Add(command, tenant.Value, query.LegalEntityId.Value, query.PartyId, Db(query.Direction?.ToString()), query.OpenOnly, Db(ParseCursor(query.Cursor)), limit + 1);
        var rows = new List<PartyOpenItemResult>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ReadItem(reader));
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new(rows, hasMore && rows.Count > 0 ? rows[^1].Id.ToString("D") : null);
    }

    private static async Task<Dictionary<Guid, LockedItem>> LockItemsAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid[] sortedIds, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,party_id,direction,polarity,transaction_currency,open_transaction_amount,settled_transaction_amount,base_currency,open_base_carrying_amount,control_account_role,control_account_reference,control_configuration_reference,status,row_version from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and id=any($3) order by id for update", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, sortedIds);
        var result = new Dictionary<Guid, LockedItem>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0), new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4).Trim(), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetString(7).Trim(), reader.GetDecimal(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetInt64(13)));
        return result;
    }

    private static void ValidatePair(LockedItem debit, LockedItem credit, Guid partyId, SubledgerDirectionContract direction, decimal amount)
    {
        if (debit.Status is not ("Open" or "PartiallySettled") || credit.Status is not ("Open" or "PartiallySettled")) throw new SubledgerException("subledger.item_closed", "Both open items must have an available balance.");
        if (debit.PartyId != partyId || credit.PartyId != partyId) throw new SubledgerException("subledger.party_mismatch", "Open items from different parties cannot be applied.");
        if (debit.Direction != direction.ToString() || credit.Direction != direction.ToString()) throw new SubledgerException("subledger.direction_mismatch", "Receivable and payable items cannot be applied together.");
        if (debit.Polarity != "Debit" || credit.Polarity != "Credit") throw new SubledgerException("subledger.polarity_mismatch", "An application must pair a debit item with a credit item.");
        if (!string.Equals(debit.Currency, credit.Currency, StringComparison.Ordinal)) throw new SubledgerException("subledger.currency_not_allowed", "Pass 1 applications require the same transaction currency.");
        if (debit.ControlRole != credit.ControlRole || debit.ControlReference != credit.ControlReference || debit.ControlConfiguration != credit.ControlConfiguration || debit.BaseCurrency != credit.BaseCurrency) throw new SubledgerException("subledger.control_context_mismatch", "Open items with different accounting control context cannot be applied.");
        if (amount <= 0m) throw new SubledgerException("subledger.amount_invalid", "The application amount must be greater than zero.");
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

    private static async Task<OriginalApplication?> LockApplicationAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid appId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select a.debit_item_id,a.credit_item_id,a.debit_applied_amount,a.debit_base_carrying_amount_closed,a.credit_base_carrying_amount_closed,d.party_id,d.direction,a.base_currency from finance_subledger.open_item_application a join finance_subledger.party_open_item d on d.tenant_id=a.tenant_id and d.legal_entity_id=a.legal_entity_id and d.id=a.debit_item_id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.id=$3 and not exists(select 1 from finance_subledger.application_reversal r where r.tenant_id=a.tenant_id and r.legal_entity_id=a.legal_entity_id and r.original_application_id=a.id) for update of a", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity.Value, appId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetGuid(5), reader.GetString(6), reader.GetString(7).Trim()) : null;
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

    private static PartyFinancialProfileResult ReadProfile(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5).ToString(CultureInfo.InvariantCulture), reader.IsDBNull(6) ? null : reader.GetString(6).Trim(), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt64(9), reader.GetFieldValue<DateTimeOffset>(10));

    private static PartyOpenItemResult ReadItem(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), Enum.Parse<SubledgerDirectionContract>(r.GetString(3)), Enum.Parse<SubledgerPolarityContract>(r.GetString(4)), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetFieldValue<DateOnly>(9), r.GetFieldValue<DateOnly>(10), r.GetFieldValue<DateOnly>(11), r.GetString(12).Trim(), D(r,13), D(r,14), D(r,15), r.GetString(16).Trim(), D(r,17), D(r,18), D(r,19), r.GetString(20), r.GetString(21), r.GetString(22), r.GetInt64(23));
    private static string D(NpgsqlDataReader r, int index) => r.GetDecimal(index).ToString(CultureInfo.InvariantCulture);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions))).ToLowerInvariant();
    private static decimal ParseAmount(string text) => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : throw new SubledgerException("subledger.amount_invalid", "Financial amounts must be invariant decimal values.");
    private static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant() is { Length: 3 } currency && currency.All(char.IsAsciiLetter) ? currency : throw new SubledgerException("subledger.currency_invalid", "A valid three-letter currency code is required.");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object Db(object? value) => value ?? DBNull.Value;
    private static Guid? ParseCursor(string? value) => Guid.TryParse(value, out var id) ? id : null;
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }

    private sealed record LockedItem(Guid Id, Guid PartyId, string Direction, string Polarity, string Currency, decimal OpenAmount, decimal SettledAmount, string BaseCurrency, decimal OpenBase, string ControlRole, string ControlReference, string ControlConfiguration, string Status, long RowVersion);
    private sealed record NormalizedLine(Guid DebitItemId, Guid CreditItemId, decimal Amount);
    private sealed record OriginalApplication(Guid DebitId, Guid CreditId, decimal Amount, decimal DebitBaseClosed, decimal CreditBaseClosed, Guid PartyId, string Direction, string BaseCurrency);
}
