using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace DbMigrator.SeedData;

public static class InventorySeed
{
    public static async Task<SeedInventorySummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var tenant = context.Tenant.Id;
        var entity = SeedIds.TenantEntity(tenant);
        var actor = SeedIds.Identity(tenant, "admin");
        var simpleWarehouse = Id(tenant, "warehouse-main");
        var simpleDefault = Id(tenant, "location-main-default");
        var advancedWarehouse = Id(tenant, "warehouse-structured");
        var advancedDefault = Id(tenant, "location-structured-default");
        var zone = Id(tenant, "location-zone-a");
        var bin = Id(tenant, "location-bin-a-01");
        var now = context.AsOf;

        await context.ExecuteAsync("insert into business_inventory.inventory_policy(tenant_id,legal_entity_id,adjustment_approval_mode,separation_of_duties,operational_cutoff_date,version,created_at,updated_at) values($1,$2,'DirectConfirm',true,null,1,$3,$3) on conflict(tenant_id,legal_entity_id) do update set adjustment_approval_mode='DirectConfirm',separation_of_duties=true,updated_at=excluded.updated_at", ct, tenant, entity, now);

        await context.ExecuteAsync("insert into business_inventory.warehouse(tenant_id,id,legal_entity_id,branch_id,code,name,location_mode,default_location_id,status,version,created_at,updated_at) values($1,$2,$3,$4,'MAIN','المخزن الرئيسي','WarehouseOnly',$5,'Active',1,$6,$6) on conflict(tenant_id,id) do update set name=excluded.name,location_mode=excluded.location_mode,default_location_id=excluded.default_location_id,status='Active',updated_at=excluded.updated_at", ct, tenant, simpleWarehouse, entity, SeedIds.TenantBranch(tenant), simpleDefault, now);
        await context.ExecuteAsync("insert into business_inventory.location(tenant_id,id,legal_entity_id,warehouse_id,parent_location_id,code,name,location_type,hidden,can_hold_stock,can_receive_into,can_pick_from,status,created_at) values($1,$2,$3,$4,null,'DEFAULT','موقع افتراضي','Default',true,true,true,true,'Active',$5) on conflict(tenant_id,id) do update set warehouse_id=excluded.warehouse_id,name=excluded.name,status='Active'", ct, tenant, simpleDefault, entity, simpleWarehouse, now);

        await context.ExecuteAsync("insert into business_inventory.warehouse(tenant_id,id,legal_entity_id,branch_id,code,name,location_mode,default_location_id,status,version,created_at,updated_at) values($1,$2,$3,$4,'CAIRO-DC','مركز توزيع القاهرة','HierarchicalLocations',$5,'Active',1,$6,$6) on conflict(tenant_id,id) do update set name=excluded.name,location_mode=excluded.location_mode,default_location_id=excluded.default_location_id,status='Active',updated_at=excluded.updated_at", ct, tenant, advancedWarehouse, entity, SeedIds.TenantBranch(tenant), advancedDefault, now);
        await context.ExecuteAsync("insert into business_inventory.location(tenant_id,id,legal_entity_id,warehouse_id,parent_location_id,code,name,location_type,hidden,can_hold_stock,can_receive_into,can_pick_from,status,created_at) values($1,$2,$3,$4,null,'DEFAULT','موقع افتراضي','Default',true,true,true,true,'Active',$5) on conflict(tenant_id,id) do update set warehouse_id=excluded.warehouse_id,name=excluded.name,status='Active'", ct, tenant, advancedDefault, entity, advancedWarehouse, now);
        await context.ExecuteAsync("insert into business_inventory.location(tenant_id,id,legal_entity_id,warehouse_id,parent_location_id,code,name,location_type,hidden,can_hold_stock,can_receive_into,can_pick_from,status,created_at) values($1,$2,$3,$4,null,'ZONE-A','منطقة التخزين أ','Zone',false,true,true,true,'Active',$5) on conflict(tenant_id,id) do update set name=excluded.name,status='Active'", ct, tenant, zone, entity, advancedWarehouse, now);
        await context.ExecuteAsync("insert into business_inventory.location(tenant_id,id,legal_entity_id,warehouse_id,parent_location_id,code,name,location_type,barcode,hidden,can_hold_stock,can_receive_into,can_pick_from,status,created_at) values($1,$2,$3,$4,$5,'A-01','رف أ-01','Bin','6220000000001',false,true,true,true,'Active',$6) on conflict(tenant_id,id) do update set parent_location_id=excluded.parent_location_id,name=excluded.name,barcode=excluded.barcode,status='Active'", ct, tenant, bin, entity, advancedWarehouse, zone, now);

        await EnsureProfileAsync(context, tenant, entity, actor, 1, simpleWarehouse, "StockTracked", "Quantity", "NonValuated", null, "Disallow", "Enforced", now, ct);
        await EnsureProfileAsync(context, tenant, entity, actor, 2, advancedWarehouse, "StockTracked", "Lot", "NonValuated", null, "Disallow", "Advisory", now, ct);
        await EnsureProfileAsync(context, tenant, entity, actor, 3, advancedWarehouse, "StockTracked", "Serial", "NonValuated", null, "Disallow", "Enforced", now, ct);
        await EnsureProfileAsync(context, tenant, entity, actor, 4, simpleWarehouse, "StockTracked", "Quantity", "Perpetual", "MovingAverage", "Disallow", "Advisory", now, ct);
        await EnsureProfileAsync(context, tenant, entity, actor, 5, advancedWarehouse, "StockTracked", "Quantity", "Perpetual", "FIFO", "Disallow", "Advisory", now, ct);
        await EnsureProfileAsync(context, tenant, entity, actor, 17, simpleWarehouse, "NonStocked", "Quantity", "NonValuated", null, "Disallow", "Disabled", now, ct);

        var product = SeedIds.Variant(tenant, 1, "default");
        var uom = SeedIds.Uom(tenant, "PACK");
        var openingOperation = Id(tenant, "inventory-opening-main");
        var adjustmentOperation = Id(tenant, "inventory-count-adjustment");
        var openingMovement = Id(tenant, "inventory-opening-main-movement");
        var adjustmentMovement = Id(tenant, "inventory-count-adjustment-movement");
        var openingNumber = "INV-SEED-OPEN-001";
        var adjustmentNumber = "INV-SEED-ADJ-001";
        var operationalDate = new DateOnly(2026, 9, 28);
        var postingDate = new DateOnly(2026, 10, 1);
        var idemOpening = "seed:inventory:opening-main";
        var idemAdjustment = "seed:inventory:count-adjustment";
        var openingHash = Hash(idemOpening);
        var adjustmentHash = Hash(idemAdjustment);

        await context.ExecuteAsync("insert into business_inventory.inventory_operation(tenant_id,id,legal_entity_id,operation_number,operation_type,status,source_module,idempotency_key,idempotency_hash,operational_date,posting_date,reason_code,reason,actor_id,correlation_id,created_at,confirmed_at) values($1,$2,$3,$4,'Opening','Confirmed','inventory',$5,$6,$7,$8,'HistoricalBoundary','Opening balance for canonical development data',$9,$10,$11,$11) on conflict(tenant_id,id) do nothing", ct, tenant, openingOperation, entity, openingNumber, idemOpening, openingHash, operationalDate, postingDate, actor, openingOperation, now);
        await context.ExecuteAsync("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,1,$5,$6,$7,'Available',10,$8,1,10,10,'inventory',$9,$10,$11,'Opening',$12,$11) on conflict(tenant_id,id) do nothing", ct, tenant, openingMovement, entity, openingOperation, product, simpleWarehouse, simpleDefault, uom, operationalDate, postingDate, now, actor);
        await context.ExecuteAsync("insert into business_inventory.inventory_operation(tenant_id,id,legal_entity_id,operation_number,operation_type,status,source_module,idempotency_key,idempotency_hash,operational_date,posting_date,reason_code,reason,actor_id,correlation_id,created_at,confirmed_at) values($1,$2,$3,$4,'Adjustment','Confirmed','inventory',$5,$6,$7,$8,'CountDifference','Seeded count variance: physical quantity is one kilogram below the opening snapshot',$9,$10,$11,$11) on conflict(tenant_id,id) do nothing", ct, tenant, adjustmentOperation, entity, adjustmentNumber, idemAdjustment, adjustmentHash, operationalDate, postingDate, actor, adjustmentOperation, now);
        await context.ExecuteAsync("insert into business_inventory.movement_sequence(tenant_id,legal_entity_id,last_value) values($1,$2,2) on conflict(tenant_id,legal_entity_id) do update set last_value=greatest(business_inventory.movement_sequence.last_value,excluded.last_value)", ct, tenant, entity);
        await context.ExecuteAsync("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,2,$5,$6,$7,'Available',1,$8,1,1,-1,'inventory',$9,$10,$11,'Adjustment',$12,$11) on conflict(tenant_id,id) do nothing", ct, tenant, adjustmentMovement, entity, adjustmentOperation, product, simpleWarehouse, simpleDefault, uom, operationalDate, postingDate, now, actor);
        await context.ExecuteAsync("insert into business_inventory.stock_balance(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,on_hand_base_quantity,reserved_base_quantity,version,last_movement_sequence) values($1,$2,$3,$4,$5,$6,'Available',9,2,1,2) on conflict(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id) do update set on_hand_base_quantity=9,reserved_base_quantity=2,version=greatest(business_inventory.stock_balance.version,1),last_movement_sequence=2", ct, tenant, Id(tenant, "balance-main-product-1"), entity, product, simpleWarehouse, simpleDefault);
        var reservationId = Id(tenant, "inventory-reservation-main");
        var reservationKey = "seed:inventory:reservation-main";
        await context.ExecuteAsync("insert into business_inventory.reservation(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,source_module,source_document_id,source_line_id,requested_quantity,reserved_quantity,idempotency_key,request_hash,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,'sales',$7,$8,2,2,$9,$10,'Active',$11,$12) on conflict(tenant_id,id) do update set requested_quantity=2,reserved_quantity=2,idempotency_key=excluded.idempotency_key,request_hash=excluded.request_hash,status='Active'", ct, tenant, reservationId, entity, product, simpleWarehouse, simpleDefault, Id(tenant, "inventory-source-order"), Id(tenant, "inventory-source-line"), reservationKey, Hash(reservationKey), actor, now);
        await context.ExecuteAsync("insert into business_inventory.reservation_allocation(tenant_id,id,legal_entity_id,reservation_id,warehouse_id,location_id,stock_status,quantity) values($1,$2,$3,$4,$5,$6,'Available',2) on conflict(tenant_id,id) do update set quantity=2", ct, tenant, Id(tenant, "inventory-reservation-allocation"), entity, reservationId, simpleWarehouse, simpleDefault);
        await context.ExecuteAsync("insert into business_inventory.reservation_event(tenant_id,id,legal_entity_id,reservation_id,event_type,quantity,actor_id,occurred_at,evidence) values($1,$2,$3,$4,'Created',2,$5,$6,'{\"fixture\":\"development\"}'::jsonb) on conflict(tenant_id,id) do nothing", ct, tenant, Id(tenant, "inventory-reservation-event"), entity, reservationId, actor, now);
        var countId = Id(tenant, "inventory-count-main");
        await context.ExecuteAsync("insert into business_inventory.physical_count(tenant_id,id,legal_entity_id,count_number,warehouse_id,status,count_mode,snapshot_at,created_by,created_at,version) values($1,$2,$3,'COUNT-SEED-001',$4,'Posted','Simple',$5,$6,$5,1) on conflict(tenant_id,id) do nothing", ct, tenant, countId, entity, simpleWarehouse, now, actor);
        await context.ExecuteAsync("insert into business_inventory.count_line(tenant_id,id,count_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,expected_quantity,counted_quantity,difference_quantity,entered_by,entered_at,posted_operation_id) values($1,$2,$3,$4,$5,$6,$7,'Available',10,9,-1,$8,$9,$10) on conflict(tenant_id,id) do nothing", ct, tenant, Id(tenant, "inventory-count-main-line"), countId, entity, product, simpleWarehouse, simpleDefault, actor, now, adjustmentOperation);
        await context.ExecuteAsync("insert into business_inventory.inventory_audit(tenant_id,id,legal_entity_id,actor_id,action,entity_type,entity_id,occurred_at,correlation_id,evidence) values($1,$2,$3,$4,'seed.opening_posted','InventoryOperation',$5,$6,$7,'{\"fixture\":\"development\"}'::jsonb) on conflict(tenant_id,id) do nothing", ct, tenant, Id(tenant, "inventory-audit-opening"), entity, actor, openingOperation, now, openingOperation);
        await context.ExecuteAsync("insert into business_inventory.inventory_audit(tenant_id,id,legal_entity_id,actor_id,action,entity_type,entity_id,occurred_at,correlation_id,evidence) values($1,$2,$3,$4,'seed.count_adjustment_posted','InventoryOperation',$5,$6,$7,'{\"fixture\":\"development\"}'::jsonb) on conflict(tenant_id,id) do nothing", ct, tenant, Id(tenant, "inventory-audit-adjustment"), entity, actor, adjustmentOperation, now, adjustmentOperation);

        return new(2, 6, 2, 1, 1, true);
    }

    private static async Task EnsureProfileAsync(SeedContext context, Guid tenant, Guid entity, Guid actor, int productIndex,
        Guid warehouse, string state, string tracking, string valuation, string? costing, string negative, string reservation, DateTimeOffset now, CancellationToken ct)
    {
        var variant = SeedIds.Variant(tenant, productIndex, "default");
        await context.ExecuteAsync("insert into business_inventory.product_inventory_profile(tenant_id,id,legal_entity_id,product_variant_id,inventory_state,tracking_mode,valuation_mode,costing_method,negative_stock_policy,default_warehouse_id,reservation_mode,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Active',1,$12,$12) on conflict(tenant_id,legal_entity_id,product_variant_id) do update set inventory_state=excluded.inventory_state,tracking_mode=excluded.tracking_mode,valuation_mode=excluded.valuation_mode,costing_method=excluded.costing_method,negative_stock_policy=excluded.negative_stock_policy,default_warehouse_id=excluded.default_warehouse_id,reservation_mode=excluded.reservation_mode,status='Active',updated_at=excluded.updated_at", ct, tenant, Id(tenant, $"inventory-profile-{productIndex}"), entity, variant, state, tracking, valuation, costing ?? (object)DBNull.Value, negative, warehouse, reservation, now);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static Guid Id(Guid tenant, string key) => SeedIds.Stable($"{tenant:D}:inventory:{key}");
}

public sealed record SeedInventorySummary(int Warehouses, int ProductProfiles, int ConfirmedMovements,
    int ActiveReservations, int PostedCountAdjustments, bool HasHierarchicalLocations);
