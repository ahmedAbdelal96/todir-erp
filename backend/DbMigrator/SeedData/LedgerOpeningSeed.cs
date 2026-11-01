using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace DbMigrator.SeedData;

public static class LedgerOpeningSeed
{
    public static async Task<SeedLedgerOpeningSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var entityId = context.EntityId;
        var balances = await LoadControlBalancesAsync(context, entityId, cancellationToken);
        if (balances.Receivable == 0m && balances.Payable == 0m) return new(0m, 0m, false);

        var suffix = entityId.ToString("N");
        var factId = SeedIds.Child(context.Tenant.Id, $"ledger-opening-fact:{suffix}", 1);
        var entryId = SeedIds.Child(context.Tenant.Id, $"ledger-opening-entry:{suffix}", 1);
        var receivableLineId = SeedIds.Child(context.Tenant.Id, $"ledger-opening-line:{suffix}", 1);
        var payableLineId = SeedIds.Child(context.Tenant.Id, $"ledger-opening-line:{suffix}", 2);
        var retainedLineId = SeedIds.Child(context.Tenant.Id, $"ledger-opening-line:{suffix}", 3);
        var journalId = await context.ScalarAsync<Guid>("select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='OPENING' and status='Active'", cancellationToken, context.Tenant.Id, entityId);
        var periodId = await context.ScalarAsync<Guid>("select id from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and code='01'", cancellationToken, context.Tenant.Id, entityId);
        var configurationId = await context.ScalarAsync<Guid>("select active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", cancellationToken, context.Tenant.Id, entityId);
        var ruleVersionId = await context.ScalarAsync<Guid>("select v.id from finance_ledger.posting_rule r join finance_ledger.posting_rule_version v on v.tenant_id=r.tenant_id and v.legal_entity_id=r.legal_entity_id and v.posting_rule_id=r.id and v.version_number=1 and v.status='Active' where r.tenant_id=$1 and r.legal_entity_id=$2 and r.fact_type='OpeningBalancePosted' and r.status='Active'", cancellationToken, context.Tenant.Id, entityId);
        var receivableAccountId = await context.ScalarAsync<Guid>("select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code='110100' and status='Active'", cancellationToken, context.Tenant.Id, entityId);
        var payableAccountId = await context.ScalarAsync<Guid>("select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code='210200' and status='Active'", cancellationToken, context.Tenant.Id, entityId);
        var retainedEarningsAccountId = await context.ScalarAsync<Guid>("select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code='310000' and status='Active'", cancellationToken, context.Tenant.Id, entityId);
        var retainedEarnings = balances.Receivable - balances.Payable;
        var payload = JsonSerializer.Serialize(new
        {
            source = "canonical-development-seed",
            legalEntityId = entityId,
            receivableBaseBalance = balances.Receivable,
            payableBaseBalance = balances.Payable,
            retainedEarningsBaseBalance = retainedEarnings,
            asOfDate = "2026-10-01"
        });
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var correlationId = SeedIds.Child(context.Tenant.Id, $"ledger-opening-correlation:{suffix}", 1);

        await context.ExecuteAsync("insert into finance_ledger.accounting_fact(tenant_id,legal_entity_id,id,fact_type,fact_version,source_module,source_reference,idempotency_key,business_date,posting_date,payload_jsonb,payload_hash,status,correlation_id,actor_id,received_at) values($1,$2,$3,'OpeningBalancePosted',1,'accounting-seed',$4,$5,'2026-01-01','2026-01-01',$6::jsonb,$7,'Received',$8,$9,$10) on conflict (tenant_id,id) do nothing", cancellationToken,
            context.Tenant.Id, entityId, factId, $"opening-control-balances:{entityId:D}", $"seed:ledger:opening-control-balances:{entityId:D}", payload, payloadHash, correlationId, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into finance_ledger.journal_entry(tenant_id,legal_entity_id,id,journal_id,fact_id,source_module,source_reference,entry_number,posting_date,accounting_period_id,fact_type,fact_version,status,currency_code,base_currency_code,posting_rule_version_id,accounting_configuration_version_id,description,correlation_id,actor_id,posted_at) values($1,$2,$3,$4,$5,'accounting-seed',$6,$7,'2026-01-01',$8,'OpeningBalancePosted',1,'Posted','EGP','EGP',$9,$10,'Canonical development opening control balances',$11,$12,$13) on conflict (tenant_id,id) do nothing", cancellationToken,
            context.Tenant.Id, entityId, entryId, journalId, factId, $"opening-control-balances:{entityId:D}", $"OPEN-DEV-{entityId.ToString("N")[..8].ToUpperInvariant()}", periodId, ruleVersionId, configurationId, correlationId, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into finance_ledger.journal_entry_line(tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit) values($1,$2,$3,$4,1,$5,$6,0,'EGP',$6,'Receivable','Opening receivable control balance','Receivable',$6,0,$6,0) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, receivableLineId, entryId, receivableAccountId, balances.Receivable);
        await context.ExecuteAsync("insert into finance_ledger.journal_entry_line(tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit) values($1,$2,$3,$4,2,$5,0,$6,'EGP',$6,'Payable','Opening payable control balance','Payable',0,$6,0,$6) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, payableLineId, entryId, payableAccountId, balances.Payable);
        if (retainedEarnings >= 0m)
            await context.ExecuteAsync("insert into finance_ledger.journal_entry_line(tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit) values($1,$2,$3,$4,3,$5,0,$6,'EGP',$6,'RetainedEarnings','Opening retained earnings balancing line','RetainedEarnings',0,$6,0,$6) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, retainedLineId, entryId, retainedEarningsAccountId, retainedEarnings);
        else
            await context.ExecuteAsync("insert into finance_ledger.journal_entry_line(tenant_id,legal_entity_id,id,journal_entry_id,sequence_no,account_id,debit,credit,currency_code,amount,semantic_role,description,account_role,transaction_debit,transaction_credit,base_debit,base_credit) values($1,$2,$3,$4,3,$5,$6,0,'EGP',$6,'RetainedEarnings','Opening retained earnings balancing line','RetainedEarnings',$6,0,$6,0) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, retainedLineId, entryId, retainedEarningsAccountId, -retainedEarnings);
        await context.ExecuteAsync("update finance_ledger.accounting_fact set status='Posted',posted_entry_id=$4,posted_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", cancellationToken, context.Tenant.Id, entityId, factId, entryId, context.AsOf);
        return new(balances.Receivable, balances.Payable, true);
    }

    private static async Task<(decimal Receivable, decimal Payable)> LoadControlBalancesAsync(SeedContext context, Guid entityId, CancellationToken cancellationToken)
    {
        const string sql = """
            select
              coalesce(sum(case when direction='Receivable' and polarity='Debit' then original_base_amount when direction='Receivable' then -original_base_amount else 0 end),0),
              coalesce(sum(case when direction='Payable' and polarity='Credit' then original_base_amount when direction='Payable' then -original_base_amount else 0 end),0)
            from finance_subledger.party_open_item
            where tenant_id=$1 and legal_entity_id=$2 and posting_date <= date '2026-10-01'
            """;
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
        command.Parameters.AddWithValue(context.Tenant.Id);
        command.Parameters.AddWithValue(entityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return (0m, 0m);
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }
}

public sealed record SeedLedgerOpeningSummary(decimal Receivable, decimal Payable, bool Posted);
