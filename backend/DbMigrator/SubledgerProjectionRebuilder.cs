using System.Text.Json;
using Core.Services;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace DbMigrator;

internal static class SubledgerProjectionRebuilder
{
    public static async Task<int> RunAsync(NpgsqlConnection connection, Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        await using (var privilege = new NpgsqlCommand("select r.rolsuper or r.rolbypassrls from pg_roles r where r.rolname=current_user", connection))
            if (await privilege.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("Subledger cache rebuild requires a privileged maintenance database role.");

        var correlationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var validate = new NpgsqlCommand("select exists(select 1 from platform.legal_entity where tenant_id=$1 and id=$2)", connection, transaction))
        {
            validate.Parameters.AddWithValue(tenantId);
            validate.Parameters.AddWithValue(legalEntityId);
            if (await validate.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("The requested legal entity was not found for this tenant.");
        }

        await using (var locks = new NpgsqlCommand("select id from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 order by id for update", connection, transaction))
        {
            locks.Parameters.AddWithValue(tenantId);
            locks.Parameters.AddWithValue(legalEntityId);
            await using (var reader = await locks.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken)) { }
        }

        const string stateSql = """
            create temporary table subledger_rebuild_state on commit drop as
            with movements as (
                select a.debit_item_id as item_id, a.debit_applied_amount as amount, a.debit_base_carrying_amount_closed as base_amount, a.created_at
                from finance_subledger.open_item_application a
                where a.tenant_id=$1 and a.legal_entity_id=$2
                  and not exists (select 1 from finance_subledger.application_reversal r where r.tenant_id=a.tenant_id and r.legal_entity_id=a.legal_entity_id and r.original_application_id=a.id)
                union all
                select a.credit_item_id, a.credit_applied_amount, a.credit_base_carrying_amount_closed, a.created_at
                from finance_subledger.open_item_application a
                where a.tenant_id=$1 and a.legal_entity_id=$2
                  and not exists (select 1 from finance_subledger.application_reversal r where r.tenant_id=a.tenant_id and r.legal_entity_id=a.legal_entity_id and r.original_application_id=a.id)
                union all
                select w.open_item_id, w.amount, w.base_amount, w.created_at
                from finance_subledger.write_off w
                where w.tenant_id=$1 and w.legal_entity_id=$2
            ), totals as (
                select item_id, sum(amount) as settled_amount, sum(base_amount) as settled_base, max(created_at) as last_activity
                from movements group by item_id
            )
            select i.id,
                   coalesce(t.settled_amount,0)::numeric(20,6) as settled_amount,
                   (i.original_transaction_amount-coalesce(t.settled_amount,0))::numeric(20,6) as open_amount,
                   coalesce(t.settled_base,0)::numeric(20,6) as settled_base,
                   (i.original_base_amount-coalesce(t.settled_base,0))::numeric(20,6) as open_base,
                   case when i.original_transaction_amount-coalesce(t.settled_amount,0)=0 then 'Settled'
                        when coalesce(t.settled_amount,0)=0 then 'Open' else 'PartiallySettled' end as status,
                   case when i.original_transaction_amount-coalesce(t.settled_amount,0)=0
                        then coalesce(t.last_activity,i.closed_at,i.created_at) else null end as closed_at
            from finance_subledger.party_open_item i
            left join totals t on t.item_id=i.id
            where i.tenant_id=$1 and i.legal_entity_id=$2
            """;
        await using (var createState = new NpgsqlCommand(stateSql, connection, transaction))
        {
            createState.Parameters.AddWithValue(tenantId);
            createState.Parameters.AddWithValue(legalEntityId);
            await createState.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var invalid = new NpgsqlCommand("select count(*) from subledger_rebuild_state where open_amount<0 or open_base<0", connection, transaction))
        {
            if (Convert.ToInt64(await invalid.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) != 0)
                throw new InvalidOperationException("Canonical application and write-off history exceeds an open item's original amount; no cache values were changed.");
        }

        int changed;
        await using (var update = new NpgsqlCommand("""
            update finance_subledger.party_open_item i
            set settled_transaction_amount=s.settled_amount,
                open_transaction_amount=s.open_amount,
                settled_base_carrying_amount=s.settled_base,
                open_base_carrying_amount=s.open_base,
                status=s.status,
                is_open=(s.status<>'Settled'),
                closed_at=s.closed_at,
                row_version=i.row_version+1
            from subledger_rebuild_state s
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.id=s.id
              and row(i.settled_transaction_amount,i.open_transaction_amount,i.settled_base_carrying_amount,i.open_base_carrying_amount,i.status,i.is_open,i.closed_at)
                  is distinct from row(s.settled_amount,s.open_amount,s.settled_base,s.open_base,s.status,(s.status<>'Settled'),s.closed_at)
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(tenantId);
            update.Parameters.AddWithValue(legalEntityId);
            changed = await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await NpgsqlAuditWriter.WriteAsync(connection, transaction,
            new AuditEntry(Guid.CreateVersion7(), new TenantId(tenantId), legalEntityId, null, "System", null, null, "subledger",
                "open_item_cache_rebuilt", "legal_entity", legalEntityId, null,
                JsonSerializer.Serialize(new { changedRows = changed }), null, "internal-maintenance", correlationId, null, now, null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }
}
