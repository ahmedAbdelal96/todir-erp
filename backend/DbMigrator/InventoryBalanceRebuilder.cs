using Npgsql;

namespace DbMigrator;

internal static class InventoryBalanceRebuilder
{
    public static async Task<int> RunAsync(NpgsqlConnection connection, Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        await using (var privilege = new NpgsqlCommand("select r.rolsuper or r.rolbypassrls from pg_roles r where r.rolname=current_user", connection))
            if (await privilege.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("Inventory balance rebuild requires a privileged maintenance database role.");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var validate = new NpgsqlCommand("select exists(select 1 from platform.legal_entity where tenant_id=$1 and id=$2)", connection, transaction))
        {
            validate.Parameters.AddWithValue(tenantId); validate.Parameters.AddWithValue(legalEntityId);
            if (await validate.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("The requested legal entity was not found for this tenant.");
        }

        await using (var tableLock = new NpgsqlCommand("lock table business_inventory.stock_movement_entry, business_inventory.stock_balance in exclusive mode", connection, transaction))
            await tableLock.ExecuteNonQueryAsync(cancellationToken);
        await using (var create = new NpgsqlCommand("""
            create temporary table inventory_balance_rebuild on commit drop as
            select product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,
                   sum(signed_base_quantity)::numeric(20,6) as on_hand,
                   max(sequence)::bigint as last_sequence
            from business_inventory.stock_movement_entry
            where tenant_id=$1 and legal_entity_id=$2
            group by product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id
            """, connection, transaction))
        {
            create.Parameters.AddWithValue(tenantId); create.Parameters.AddWithValue(legalEntityId);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var invalid = new NpgsqlCommand("select count(*) from inventory_balance_rebuild r left join business_inventory.stock_balance b on b.tenant_id=$1 and b.legal_entity_id=$2 and b.product_variant_id=r.product_variant_id and b.warehouse_id=r.warehouse_id and b.location_id=r.location_id and b.stock_status=r.stock_status and b.lot_id is not distinct from r.lot_id and b.serial_id is not distinct from r.serial_id where coalesce(b.reserved_base_quantity,0)>r.on_hand", connection, transaction))
        {
            invalid.Parameters.AddWithValue(tenantId); invalid.Parameters.AddWithValue(legalEntityId);
            if (Convert.ToInt64(await invalid.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) != 0)
                throw new InvalidOperationException("A canonical movement total is below the active reservation projection; reconcile reservations before rebuilding balances.");
        }
        var missing = new List<(Guid Product, Guid Warehouse, Guid Location, string Status, Guid? Lot, Guid? Serial, decimal OnHand, long Sequence)>();
        await using (var missingRows = new NpgsqlCommand("select r.product_variant_id,r.warehouse_id,r.location_id,r.stock_status,r.lot_id,r.serial_id,r.on_hand,r.last_sequence from inventory_balance_rebuild r where not exists(select 1 from business_inventory.stock_balance b where b.tenant_id=$1 and b.legal_entity_id=$2 and b.product_variant_id=r.product_variant_id and b.warehouse_id=r.warehouse_id and b.location_id=r.location_id and b.stock_status=r.stock_status and b.lot_id is not distinct from r.lot_id and b.serial_id is not distinct from r.serial_id)", connection, transaction))
        {
            missingRows.Parameters.AddWithValue(tenantId); missingRows.Parameters.AddWithValue(legalEntityId);
            await using var reader = await missingRows.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) missing.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetDecimal(6), reader.GetInt64(7)));
        }
        foreach (var row in missing)
        {
            await using var insert = new NpgsqlCommand("insert into business_inventory.stock_balance(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,on_hand_base_quantity,reserved_base_quantity,version,last_movement_sequence) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,0,1,$11)", connection, transaction);
            insert.Parameters.AddWithValue(tenantId); insert.Parameters.AddWithValue(Guid.CreateVersion7()); insert.Parameters.AddWithValue(legalEntityId);
            insert.Parameters.AddWithValue(row.Product); insert.Parameters.AddWithValue(row.Warehouse); insert.Parameters.AddWithValue(row.Location); insert.Parameters.AddWithValue(row.Status);
            insert.Parameters.AddWithValue(row.Lot ?? (object)DBNull.Value); insert.Parameters.AddWithValue(row.Serial ?? (object)DBNull.Value); insert.Parameters.AddWithValue(row.OnHand); insert.Parameters.AddWithValue(row.Sequence);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        int changed;
        await using (var update = new NpgsqlCommand("""
            update business_inventory.stock_balance b set on_hand_base_quantity=r.on_hand,last_movement_sequence=r.last_sequence,version=b.version+1
            from inventory_balance_rebuild r where b.tenant_id=$1 and b.legal_entity_id=$2 and b.product_variant_id=r.product_variant_id
              and b.warehouse_id=r.warehouse_id and b.location_id=r.location_id and b.stock_status=r.stock_status
              and b.lot_id is not distinct from r.lot_id and b.serial_id is not distinct from r.serial_id
              and (b.on_hand_base_quantity is distinct from r.on_hand or b.last_movement_sequence is distinct from r.last_sequence)
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(tenantId); update.Parameters.AddWithValue(legalEntityId);
            changed = await update.ExecuteNonQueryAsync(cancellationToken) + missing.Count;
        }
        await using (var clear = new NpgsqlCommand("update business_inventory.stock_balance b set on_hand_base_quantity=0,last_movement_sequence=0,version=version+1 where b.tenant_id=$1 and b.legal_entity_id=$2 and not exists(select 1 from inventory_balance_rebuild r where r.product_variant_id=b.product_variant_id and r.warehouse_id=b.warehouse_id and r.location_id=b.location_id and r.stock_status=b.stock_status and r.lot_id is not distinct from b.lot_id and r.serial_id is not distinct from b.serial_id) and (b.on_hand_base_quantity<>0 or b.last_movement_sequence<>0)", connection, transaction))
        {
            clear.Parameters.AddWithValue(tenantId); clear.Parameters.AddWithValue(legalEntityId);
            changed += await clear.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }
}
