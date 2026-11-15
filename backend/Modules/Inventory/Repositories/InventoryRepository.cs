using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Modules.Inventory.DTOs;
using Modules.Inventory.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Ledger;
using Shared.Contracts.Catalog;
using Todir.Inventory.Contracts;

namespace Modules.Inventory.Repositories;

public sealed class InventoryRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, TimeProvider clock,
    ICatalogCapabilities catalog, IQuantityConversion quantityConversion, ITransactionalInventoryLedgerPostingCapability? ledgerPosting = null)
{
    public async Task<Warehouse> CreateWarehouseAsync(TenantId tenant, Guid actor, Guid entity, CreateWarehouseRequest request, Guid correlationId, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (entity == Guid.Empty || code.Length is < 1 or > 40 || name.Length is < 1 or > 200)
            throw new InventoryException("inventory.warehouse_request_invalid");
        var now = clock.GetUtcNow();
        var warehouseId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var command = new NpgsqlCommand("insert into business_inventory.warehouse(tenant_id,id,legal_entity_id,branch_id,code,name,location_mode,default_location_id,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Active',1,$9,$9)", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, warehouseId, entity, request.BranchId ?? (object)DBNull.Value, code, name, request.LocationMode.ToString(), locationId, now);
                await command.ExecuteNonQueryAsync(token);
            }
            await using (var command = new NpgsqlCommand("insert into business_inventory.location(tenant_id,id,legal_entity_id,warehouse_id,parent_location_id,code,name,location_type,hidden,can_hold_stock,can_receive_into,can_pick_from,status,created_at) values($1,$2,$3,$4,null,'DEFAULT','Default', 'Default',$5,true,true,true,'Active',$6)", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, locationId, entity, warehouseId, request.LocationMode == LocationTrackingMode.WarehouseOnly, now);
                await command.ExecuteNonQueryAsync(token);
            }
            await AuditAsync(context, tenant.Value, entity, actor, "warehouse.created", "Warehouse", warehouseId, correlationId, now, token);
            return new Warehouse(warehouseId, tenant.Value, entity, code, name, request.LocationMode, locationId, WarehouseStatus.Active, request.BranchId);
        }, ct);
    }

    public async Task<InventoryLocation> CreateLocationAsync(TenantId tenant, Guid actor, Guid entity, CreateLocationRequest request, Guid correlationId, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant(); var name = request.Name.Trim(); var type = request.LocationType.Trim();
        if (code.Length is < 1 or > 60 || name.Length is < 1 or > 200 || type.Length is < 1 or > 32 || request.Barcode?.Length > 120)
            throw new InventoryException("inventory.location_request_invalid");
        var now = clock.GetUtcNow(); var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            LocationTrackingMode mode;
            await using (var warehouse = new NpgsqlCommand("select location_mode from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active' for share", context.Connection, context.Transaction))
            {
                Add(warehouse, tenant.Value, entity, request.WarehouseId);
                mode = await warehouse.ExecuteScalarAsync(token) as string is { } text ? Enum.Parse<LocationTrackingMode>(text) : throw new InventoryException("inventory.warehouse_not_found_or_inactive");
            }
            if (mode == LocationTrackingMode.WarehouseOnly) throw new InventoryException("inventory.location_detail_not_enabled");
            if (request.ParentLocationId is { } parent)
            {
                await using var parentCheck = new NpgsqlCommand("select exists(select 1 from business_inventory.location where tenant_id=$1 and legal_entity_id=$2 and warehouse_id=$3 and id=$4 and status='Active')", context.Connection, context.Transaction);
                Add(parentCheck, tenant.Value, entity, request.WarehouseId, parent);
                if (!(bool)(await parentCheck.ExecuteScalarAsync(token) ?? false)) throw new InventoryException("inventory.parent_location_not_found");
            }
            await using var insert = new NpgsqlCommand("insert into business_inventory.location(tenant_id,id,legal_entity_id,warehouse_id,parent_location_id,code,name,location_type,barcode,hidden,can_hold_stock,can_receive_into,can_pick_from,status,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,false,$10,$11,$12,'Active',$13)", context.Connection, context.Transaction);
            Add(insert, tenant.Value, id, entity, request.WarehouseId, request.ParentLocationId ?? (object)DBNull.Value, code, name, type, request.Barcode?.Trim() ?? (object)DBNull.Value, request.CanHoldStock, request.CanReceiveInto, request.CanPickFrom, now);
            await insert.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenant.Value, entity, actor, "location.created", "InventoryLocation", id, correlationId, now, token);
            return new InventoryLocation(id, tenant.Value, entity, request.WarehouseId, request.ParentLocationId, code, name, false, request.CanHoldStock, request.CanReceiveInto, request.CanPickFrom, true);
        }, ct);
    }

    public async Task<InventoryLocation> MoveLocationAsync(TenantId tenant, Guid actor, Guid entity, Guid locationId,
        MoveLocationRequest request, Guid correlationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            Guid warehouseId = Guid.Empty; string code = string.Empty; string name = string.Empty; string type = string.Empty; string? barcode = null; bool hidden = false; bool canHold = false; bool canReceive = false; bool canPick = false;
            var parentByLocation = new Dictionary<Guid, Guid?>();
            await using (var locations = new NpgsqlCommand("select id,warehouse_id,parent_location_id,code,name,location_type,barcode,hidden,can_hold_stock,can_receive_into,can_pick_from from business_inventory.location where tenant_id=$1 and legal_entity_id=$2 and status='Active' for update", context.Connection, context.Transaction))
            {
                Add(locations, tenant.Value, entity); await using var reader = await locations.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    var id = reader.GetGuid(0); parentByLocation[id] = NullGuid(reader, 2);
                    if (id == locationId) { warehouseId = reader.GetGuid(1); code = reader.GetString(3); name = reader.GetString(4); type = reader.GetString(5); barcode = reader.IsDBNull(6) ? null : reader.GetString(6); hidden = reader.GetBoolean(7); canHold = reader.GetBoolean(8); canReceive = reader.GetBoolean(9); canPick = reader.GetBoolean(10); }
                }
            }
            if (!parentByLocation.ContainsKey(locationId)) throw new InventoryException("inventory.location_not_found");
            InventoryInvariants.EnsureLocationMoveIsAcyclic(locationId, request.ParentLocationId, parentByLocation);
            if (request.ParentLocationId is { } parent && parentByLocation.ContainsKey(parent) == false) throw new InventoryException("inventory.parent_location_not_found");
            await using (var update = new NpgsqlCommand("update business_inventory.location set parent_location_id=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, locationId, request.ParentLocationId ?? (object)DBNull.Value); await update.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenant.Value, entity, actor, "location.reparented", "InventoryLocation", locationId, correlationId, now, token);
            return new InventoryLocation(locationId, tenant.Value, entity, warehouseId, request.ParentLocationId, code, name, hidden, canHold, canReceive, canPick, true);
        }, ct);
    }

    public async Task<IReadOnlyList<InventoryLocation>> ListLocationsAsync(TenantId tenant, Guid entity, Guid warehouseId, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,warehouse_id,parent_location_id,code,name,hidden,can_hold_stock,can_receive_into,can_pick_from,status from business_inventory.location where tenant_id=$1 and legal_entity_id=$2 and warehouse_id=$3 order by code,id limit 1000", connection);
        Add(command, tenant.Value, entity, warehouseId); var locations = new List<InventoryLocation>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) locations.Add(new(reader.GetGuid(0), tenant.Value, entity, reader.GetGuid(1), NullGuid(reader, 2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8), reader.GetString(9) == "Active"));
        return locations;
    }

    public async Task<InventoryProfile> SaveProfileAsync(TenantId tenant, Guid actor, Guid entity, CreateInventoryProfileRequest request, Guid correlationId, CancellationToken ct)
    {
        var profile = new InventoryProfile(tenant.Value, entity, request.ProductVariantId, request.State, request.Tracking,
            request.Valuation, request.Costing, request.NegativeStock, request.DefaultWarehouseId, request.ReservationMode);
        profile.Validate();
        var catalogVariant = await catalog.FindVariantAsync(tenant, request.ProductVariantId, entity, ct)
            ?? throw new InventoryException("inventory.catalog_variant_not_found");
        if (catalogVariant.Status != "Active") throw new InventoryException("inventory.catalog_variant_inactive");
        if (request.State == InventoryState.StockTracked && catalogVariant.Nature == "Service")
            throw new InventoryException("inventory.service_cannot_be_stock_tracked");
        if (request.DefaultWarehouseId is { } defaultWarehouse)
        {
            await using var connection = await OpenTenantAsync(tenant, ct);
            await using var warehouseCheck = new NpgsqlCommand("select exists(select 1 from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active')", connection);
            Add(warehouseCheck, tenant.Value, entity, defaultWarehouse);
            if (!(bool)(await warehouseCheck.ExecuteScalarAsync(ct) ?? false)) throw new InventoryException("inventory.default_warehouse_not_found");
        }
        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("""
                insert into business_inventory.product_inventory_profile(tenant_id,id,legal_entity_id,product_variant_id,inventory_state,tracking_mode,valuation_mode,costing_method,negative_stock_policy,default_warehouse_id,reservation_mode,status,version,created_at,updated_at)
                values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Active',1,$12,$12)
                on conflict(tenant_id,legal_entity_id,product_variant_id) do update set inventory_state=excluded.inventory_state,tracking_mode=excluded.tracking_mode,valuation_mode=excluded.valuation_mode,costing_method=excluded.costing_method,negative_stock_policy=excluded.negative_stock_policy,default_warehouse_id=excluded.default_warehouse_id,reservation_mode=excluded.reservation_mode,version=business_inventory.product_inventory_profile.version+1,updated_at=excluded.updated_at
                where not exists(select 1 from business_inventory.stock_movement_entry m where m.tenant_id=$1 and m.legal_entity_id=$3 and m.product_variant_id=$4)
                """, context.Connection, context.Transaction);
            Add(command, tenant.Value, id, entity, request.ProductVariantId, request.State.ToString(), request.Tracking.ToString(), request.Valuation.ToString(), request.Costing?.ToString() ?? (object)DBNull.Value,
                request.NegativeStock.ToString(), request.DefaultWarehouseId ?? (object)DBNull.Value, request.ReservationMode.ToString(), now);
            if (await command.ExecuteNonQueryAsync(token) == 0) throw new InventoryException("inventory.profile_change_requires_migration");
            await AuditAsync(context, tenant.Value, entity, actor, "product_profile.saved", "ProductInventoryProfile", id, correlationId, now, token);
            return true;
        }, ct);
        return profile;
    }

    public async Task<TraceabilityRecord> CreateLotAsync(TenantId tenant, Guid actor, Guid entity, CreateLotRequest request, Guid correlationId, CancellationToken ct)
    {
        var number = request.LotNumber.Trim();
        if (number.Length is < 1 or > 120 || request.ManufacturedOn is { } made && request.ExpiresOn is { } expires && made > expires)
            throw new InventoryException("inventory.lot_request_invalid");
        var profile = await GetProfileAsync(tenant, entity, request.ProductVariantId, ct) ?? throw new InventoryException("inventory.profile_not_found");
        if (profile.State != InventoryState.StockTracked || profile.Tracking != TrackingMode.Lot) throw new InventoryException("inventory.lot_tracking_not_enabled");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_inventory.lot(tenant_id,id,legal_entity_id,product_variant_id,lot_number,manufactured_on,expires_on,supplier_reference,status,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Active',$9)", context.Connection, context.Transaction);
            Add(command, tenant.Value, id, entity, request.ProductVariantId, number, request.ManufacturedOn ?? (object)DBNull.Value, request.ExpiresOn ?? (object)DBNull.Value, string.IsNullOrWhiteSpace(request.SupplierReference) ? (object)DBNull.Value : request.SupplierReference.Trim(), now);
            try { await command.ExecuteNonQueryAsync(token); }
            catch (PostgresException ex) when (ex.SqlState == "23505") { throw new InventoryException("inventory.lot_duplicate"); }
            await AuditAsync(context, tenant.Value, entity, actor, "lot.created", "InventoryLot", id, correlationId, now, token); return true;
        }, ct);
        return new(id, request.ProductVariantId, number, "Active");
    }

    public async Task<TraceabilityRecord> CreateSerialAsync(TenantId tenant, Guid actor, Guid entity, CreateSerialRequest request, Guid correlationId, CancellationToken ct)
    {
        var number = request.SerialNumber.Trim();
        if (number.Length is < 1 or > 160) throw new InventoryException("inventory.serial_request_invalid");
        var profile = await GetProfileAsync(tenant, entity, request.ProductVariantId, ct) ?? throw new InventoryException("inventory.profile_not_found");
        if (profile.State != InventoryState.StockTracked || profile.Tracking != TrackingMode.Serial) throw new InventoryException("inventory.serial_tracking_not_enabled");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_inventory.serial(tenant_id,id,legal_entity_id,product_variant_id,serial_number,status,created_at) values($1,$2,$3,$4,$5,'PendingReceipt',$6)", context.Connection, context.Transaction);
            Add(command, tenant.Value, id, entity, request.ProductVariantId, number, now);
            try { await command.ExecuteNonQueryAsync(token); }
            catch (PostgresException ex) when (ex.SqlState == "23505") { throw new InventoryException("inventory.serial_duplicate"); }
            await AuditAsync(context, tenant.Value, entity, actor, "serial.created", "InventorySerial", id, correlationId, now, token); return true;
        }, ct);
        return new(id, request.ProductVariantId, number, "PendingReceipt");
    }

    public async Task<InventoryPolicyView> GetPolicyAsync(TenantId tenant, Guid entity, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select adjustment_approval_mode,separation_of_duties,operational_cutoff_date,version from business_inventory.inventory_policy where tenant_id=$1 and legal_entity_id=$2", connection);
        Add(command, tenant.Value, entity);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(entity, Enum.Parse<AdjustmentApprovalMode>(reader.GetString(0)), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetInt64(3))
            : new(entity, AdjustmentApprovalMode.DirectConfirm, true, null, 1);
    }

    public async Task<InventoryPolicyView> SavePolicyAsync(TenantId tenant, Guid actor, Guid entity, UpdateInventoryPolicyRequest request, Guid correlationId, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.AdjustmentApprovalMode)) throw new InventoryException("inventory.policy_invalid");
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_inventory.inventory_policy(tenant_id,legal_entity_id,adjustment_approval_mode,separation_of_duties,operational_cutoff_date,version,created_at,updated_at) values($1,$2,$3,$4,$5,1,$6,$6) on conflict(tenant_id,legal_entity_id) do update set adjustment_approval_mode=excluded.adjustment_approval_mode,separation_of_duties=excluded.separation_of_duties,operational_cutoff_date=excluded.operational_cutoff_date,version=business_inventory.inventory_policy.version+1,updated_at=excluded.updated_at returning version", context.Connection, context.Transaction);
            Add(command, tenant.Value, entity, request.AdjustmentApprovalMode.ToString(), request.SeparationOfDuties, request.OperationalCutoffDate ?? (object)DBNull.Value, now);
            var version = (long)(await command.ExecuteScalarAsync(token) ?? 1L);
            await AuditAsync(context, tenant.Value, entity, actor, "inventory.policy.saved", "InventoryPolicy", entity, correlationId, now, token);
            return new InventoryPolicyView(entity, request.AdjustmentApprovalMode, request.SeparationOfDuties, request.OperationalCutoffDate, version);
        }, ct);
    }

    public async Task<AdjustmentRequestView> CreateAdjustmentRequestAsync(TenantId tenant, Guid actor, Guid entity, CreateAdjustmentRequest request, Guid correlationId, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Direction) || !Enum.IsDefined(request.Reason) || request.Quantity <= 0m || request.Status == StockStatus.InTransit)
            throw new InventoryException("inventory.adjustment_request_invalid");
        var policy = await GetPolicyAsync(tenant, entity, ct);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        var status = policy.AdjustmentApprovalMode == AdjustmentApprovalMode.ApprovalRequired ? AdjustmentRequestStatus.PendingApproval : AdjustmentRequestStatus.Draft;
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_inventory.inventory_adjustment_request(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,entered_uom_id,quantity,direction,reason,status,approval_mode,stock_status,lot_id,serial_id,unit_cost,currency_code,operational_date,posting_date,idempotency_key,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$22) on conflict(tenant_id,legal_entity_id,idempotency_key) do nothing", context.Connection, context.Transaction);
            Add(command, tenant.Value, id, entity, request.ProductVariantId, request.WarehouseId, request.LocationId ?? (object)DBNull.Value, request.EnteredUomId, request.Quantity, request.Direction.ToString(), request.Reason.ToString(), status.ToString(), policy.AdjustmentApprovalMode.ToString(), request.Status.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, request.UnitCost ?? (object)DBNull.Value, request.CurrencyCode ?? (object)DBNull.Value, request.OperationalDate, request.PostingDate, request.IdempotencyKey, actor, now);
            try { await command.ExecuteNonQueryAsync(token); }
            catch (PostgresException ex) when (ex.SqlState == "23505") { throw new InventoryException("inventory.adjustment_duplicate"); }
            await AuditAsync(context, tenant.Value, entity, actor, "adjustment.requested", "InventoryAdjustmentRequest", id, correlationId, now, token);
            return true;
        }, ct);
        return new(id, entity, request.ProductVariantId, request.WarehouseId, request.LocationId, request.Quantity, request.Direction, request.Reason, request.OperationalDate, request.PostingDate, policy.AdjustmentApprovalMode, status, actor, null, null);
    }

    public async Task<AdjustmentRequestView> ApproveAdjustmentRequestAsync(TenantId tenant, Guid actor, Guid entity, Guid requestId, Guid correlationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select product_variant_id,warehouse_id,location_id,quantity,direction,reason,operational_date,posting_date,approval_mode,status,created_by,approved_by,posted_operation_id from business_inventory.inventory_adjustment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
            Add(command, tenant.Value, entity, requestId); await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.adjustment_request_not_found");
            var status = Enum.Parse<AdjustmentRequestStatus>(reader.GetString(9));
            var createdBy = reader.GetGuid(10);
            var mode = Enum.Parse<AdjustmentApprovalMode>(reader.GetString(8));
            var view = ReadAdjustmentView(reader, requestId, entity, mode, status, createdBy);
            await reader.DisposeAsync();
            if (status != AdjustmentRequestStatus.PendingApproval) throw new InventoryException("inventory.adjustment_not_pending_approval");
            if (mode == AdjustmentApprovalMode.ApprovalRequired && await SeparationOfDutiesAsync(context, tenant.Value, entity, token) && createdBy == actor)
                throw new InventoryException("inventory.adjustment_creator_cannot_approve");
            await using var update = new NpgsqlCommand("update business_inventory.inventory_adjustment_request set status='Approved',approved_by=$4,updated_at=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
            Add(update, tenant.Value, entity, requestId, actor, now); await update.ExecuteNonQueryAsync(token);
            await AuditAsync(context, tenant.Value, entity, actor, "adjustment.approved", "InventoryAdjustmentRequest", requestId, correlationId, now, token);
            return view with { Status = AdjustmentRequestStatus.Approved, ApprovedBy = actor };
        }, ct);
    }

    public async Task<InventoryOperationResult> PostAdjustmentRequestAsync(TenantId tenant, Guid actor, Guid entity, Guid requestId, Guid correlationId, string? traceParent, CancellationToken ct, bool canOverrideNegativeStock = false)
    {
        var request = await LoadAdjustmentRequestAsync(tenant, entity, requestId, ct);
        var adjustment = new AdjustStockRequest(request.ProductVariantId, request.WarehouseId, request.LocationId, request.EnteredUomId, request.Quantity, request.Direction, request.Reason, request.OperationalDate, request.PostingDate, request.IdempotencyKey, request.StockStatus, request.LotId, request.SerialId, request.UnitCost, request.CurrencyCode);
        return await ConfirmMovementAsync(tenant, actor, entity, new ConfirmStockMovementRequest(adjustment.ProductVariantId, adjustment.WarehouseId, adjustment.LocationId, adjustment.EnteredUomId, adjustment.Quantity, adjustment.OperationalDate, adjustment.PostingDate, adjustment.IdempotencyKey, "inventory", null, adjustment.Status, adjustment.LotId, adjustment.SerialId, adjustment.UnitCost, adjustment.CurrencyCode), MovementKind.Adjustment, correlationId, traceParent, ct, canOverrideNegativeStock, adjustmentSign: adjustment.Direction == AdjustmentDirection.Increase ? 1 : -1, reasonCode: adjustment.Reason.ToString(), adjustmentRequestId: requestId);
    }

    public Task<InventoryOperationResult> ReceiveAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct) =>
        ConfirmMovementAsync(tenant, actor, entity, request, MovementKind.Receipt, correlationId, traceParent, ct);

    public async Task<InventoryOperationResult> IssueAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct, bool canOverrideNegativeStock = false, Guid? reservationId = null)
    {
        await ExpireReservationsAsync(tenant, entity, 500, correlationId, ct);
        return await ConfirmMovementAsync(tenant, actor, entity, request, MovementKind.Issue, correlationId, traceParent, ct, canOverrideNegativeStock, reservationId);
    }

    public Task<InventoryOperationResult> OpenAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct) =>
        ConfirmMovementAsync(tenant, actor, entity, request, MovementKind.Opening, correlationId, traceParent, ct);

    public async Task<InventoryOperationResult> AdjustAsync(TenantId tenant, Guid actor, Guid entity, AdjustStockRequest request,
        Guid correlationId, string? traceParent, CancellationToken ct, bool canOverrideNegativeStock = false)
    {
        if (!Enum.IsDefined(request.Reason) || !Enum.IsDefined(request.Direction) || request.Quantity <= 0m || request.Status == StockStatus.InTransit)
            throw new InventoryException("inventory.adjustment_request_invalid");
        var policy = await GetPolicyAsync(tenant, entity, ct);
        if (policy.AdjustmentApprovalMode == AdjustmentApprovalMode.ApprovalRequired)
            throw new InventoryException("inventory.adjustment_approval_required");
        var movement = new ConfirmStockMovementRequest(request.ProductVariantId, request.WarehouseId, request.LocationId,
            request.EnteredUomId, request.Quantity, request.OperationalDate, request.PostingDate, request.IdempotencyKey,
            "inventory", null, request.Status, request.LotId, request.SerialId, request.UnitCost, request.CurrencyCode);
        return await ConfirmMovementAsync(tenant, actor, entity, movement, MovementKind.Adjustment, correlationId, traceParent,
            ct, canOverrideNegativeStock, adjustmentSign: request.Direction == AdjustmentDirection.Increase ? 1 : -1,
            reasonCode: request.Reason.ToString());
    }

    public async Task<InventoryOperationResult> ReverseOperationAsync(TenantId tenant, Guid actor, Guid entity,
        ReverseInventoryOperationRequest request, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 180
            || request.PostingDate < request.OperationalDate)
            throw new InventoryException("inventory.reversal_request_invalid");
        var now = clock.GetUtcNow();
        var idempotencyKey = $"reverse:{request.IdempotencyKey}";
        var hash = Hash(request);
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await LockIdempotencyAsync(context, tenant.Value, entity, "reversal", idempotencyKey, token);
            await using (var prior = new NpgsqlCommand("select id,operation_number,idempotency_hash from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and source_module='inventory' and idempotency_key=$3", context.Connection, context.Transaction))
            {
                Add(prior, tenant.Value, entity, idempotencyKey); await using var reader = await prior.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (!string.Equals(reader.GetString(2).Trim(), hash, StringComparison.Ordinal)) throw new InventoryException("inventory.idempotency_conflict");
                    return new InventoryOperationResult(reader.GetGuid(0), reader.GetString(1), "Confirmed", [], true);
                }
            }

            string originalType; string originalStatus; Guid? originalProjectId; Guid? originalTaskId; string? originalCostCategory;
            await using (var original = new NpgsqlCommand("select operation_type,status,project_id,task_id,cost_category from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(original, tenant.Value, entity, request.OperationId); await using var reader = await original.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.operation_not_found");
                originalType = reader.GetString(0); originalStatus = reader.GetString(1); originalProjectId = reader.IsDBNull(2) ? null : reader.GetGuid(2); originalTaskId = reader.IsDBNull(3) ? null : reader.GetGuid(3); originalCostCategory = reader.IsDBNull(4) ? null : reader.GetString(4);
            }
            if (originalStatus != "Confirmed" || originalType is not ("Receipt" or "Issue" or "Adjustment"))
                throw new InventoryException("inventory.operation_not_reversible");
            await using (var alreadyReversed = new NpgsqlCommand("select exists(select 1 from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and reversal_of_id=$3)", context.Connection, context.Transaction))
            {
                Add(alreadyReversed, tenant.Value, entity, request.OperationId);
                if ((bool)(await alreadyReversed.ExecuteScalarAsync(token) ?? false)) throw new InventoryException("inventory.operation_already_reversed");
            }

            var movements = new List<ReversalMovement>();
            await using (var command = new NpgsqlCommand("select m.id,m.product_variant_id,m.warehouse_id,m.location_id,m.stock_status,m.lot_id,m.serial_id,m.entered_quantity,m.entered_uom_id,m.conversion_factor_snapshot,m.base_quantity,m.signed_base_quantity,m.operational_date,v.id,v.quantity_delta,v.value_delta,v.unit_cost,v.currency_code,v.accounting_fact_id from business_inventory.stock_movement_entry m left join business_inventory.valuation_entry v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.movement_id=m.id where m.tenant_id=$1 and m.legal_entity_id=$2 and m.operation_id=$3 order by m.sequence for update of m", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, entity, request.OperationId); await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) movements.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
                    Enum.Parse<StockStatus>(reader.GetString(4)), NullGuid(reader, 5), NullGuid(reader, 6), reader.GetDecimal(7), reader.GetGuid(8),
                    reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), DateOnly.FromDateTime(reader.GetDateTime(12)),
                    NullGuid(reader, 13), reader.IsDBNull(14) ? null : reader.GetDecimal(14), reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                    reader.IsDBNull(16) ? null : reader.GetDecimal(16), reader.IsDBNull(17) ? null : reader.GetString(17), NullGuid(reader, 18)));
            }
            if (movements.Count == 0) throw new InventoryException("inventory.operation_has_no_movements");
            foreach (var movement in movements)
            {
                var profile = await GetProfileInTransactionAsync(context, tenant.Value, entity, movement.ProductVariantId, token)
                    ?? throw new InventoryException("inventory.profile_not_found");
                if (profile.Valuation == ValuationMode.Perpetual && movement.OriginalValuationId is null)
                    throw new InventoryException("inventory.original_valuation_missing");
            }
            await EnsurePostingDateOpenAsync(context, tenant, entity, request.PostingDate, token);
            await EnsurePostingDateOpenAsync(context, tenant, entity, request.OperationalDate, token);
            var reversalId = Guid.CreateVersion7();
            var number = OperationNumber(now, reversalId);
            await using (var insert = new NpgsqlCommand("insert into business_inventory.inventory_operation(tenant_id,id,legal_entity_id,operation_number,operation_type,status,source_module,source_document_id,idempotency_key,idempotency_hash,operational_date,posting_date,reason_code,actor_id,correlation_id,created_at,confirmed_at,reversal_of_id) values($1,$2,$3,$4,'Reversal','Confirmed','inventory',$5,$6,$7,$8,$9,'Reversal',$10,$11,$12,$12,$5)", context.Connection, context.Transaction))
            {
                Add(insert, tenant.Value, reversalId, entity, number, request.OperationId, idempotencyKey, hash, request.OperationalDate, request.PostingDate, actor, correlationId, now);
                await insert.ExecuteNonQueryAsync(token);
            }
            await using (var allocation = new NpgsqlCommand("update business_inventory.inventory_operation set project_id=$3,task_id=$4,cost_category=$5 where tenant_id=$1 and id=$2", context.Connection, context.Transaction))
            { Add(allocation, tenant.Value, reversalId, originalProjectId ?? (object)DBNull.Value, originalTaskId ?? (object)DBNull.Value, originalCostCategory ?? (object)DBNull.Value); await allocation.ExecuteNonQueryAsync(token); }

            var reversedMovementIds = new List<Guid>(movements.Count);
            foreach (var movement in movements)
            {
                var signed = -movement.SignedBaseQuantity;
                var position = new StockPosition(tenant.Value, entity, movement.ProductVariantId, movement.WarehouseId,
                    movement.LocationId, movement.Status, movement.LotId, movement.SerialId);
                var balance = await ReadBalanceForUpdateAsync(context, position, token);
                if (signed < 0m && -signed > balance.OnHand - balance.Reserved)
                    throw new InventoryException("inventory.reversal_insufficient_available_stock");
                var sequence = await NextMovementSequenceAsync(context, tenant.Value, entity, token);
                var movementId = Guid.CreateVersion7(); reversedMovementIds.Add(movementId);
                await using (var insertMovement = new NpgsqlCommand("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,source_document_id,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,'inventory',$17,$18,$19,$20,'Reversal',$21,$20)", context.Connection, context.Transaction))
                {
                    Add(insertMovement, tenant.Value, movementId, entity, reversalId, sequence, movement.ProductVariantId, movement.WarehouseId,
                        movement.LocationId, movement.Status.ToString(), movement.LotId ?? (object)DBNull.Value, movement.SerialId ?? (object)DBNull.Value,
                        movement.EnteredQuantity, movement.EnteredUomId, movement.ConversionFactor, movement.BaseQuantity, signed,
                        request.OperationId, request.OperationalDate, request.PostingDate, now, actor);
                    await insertMovement.ExecuteNonQueryAsync(token);
                }
                await ApplyBalanceDeltaAsync(context, position, signed, 0m, movementId, token);
                if (movement.OriginalValuationId is not null)
                    await ApplyValuationReversalAsync(context, tenant, actor, entity, reversalId, movement, movementId,
                        request, correlationId, now, token);
                if (movement.SerialId is { } serialId)
                    await MoveSerialAsync(context, tenant.Value, entity, movement.ProductVariantId, serialId,
                        signed > 0m ? "OnHand" : "Issued", signed > 0m ? movement.WarehouseId : null,
                        signed > 0m ? movement.LocationId : null, token);
                await ReconcileNegativeStockConditionAsync(context, position, movementId, token);
            }
            await using (var mark = new NpgsqlCommand("update business_inventory.inventory_operation set status='Reversed' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Confirmed'", context.Connection, context.Transaction))
            { Add(mark, tenant.Value, entity, request.OperationId); if (await mark.ExecuteNonQueryAsync(token) != 1) throw new InventoryException("inventory.operation_not_reversible"); }
            await AuditAsync(context, tenant.Value, entity, actor, "inventory_operation.reversed", "InventoryOperation", request.OperationId, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "StockReversedV1", JsonSerializer.Serialize(new { operationId = reversalId, reversalOfOperationId = request.OperationId, movementIds = reversedMovementIds, projectId = originalProjectId, taskId = originalTaskId, costCategory = originalCostCategory, costAmount = Math.Abs(movements.Sum(x => x.OriginalValueDelta ?? 0m)), currencyCode = movements.FirstOrDefault(x => x.CurrencyCode is not null)?.CurrencyCode }), tenant, now, correlationId, reversalId, "inventory", traceParent));
            return new InventoryOperationResult(reversalId, number, "Confirmed", reversedMovementIds, false);
        }, ct);
    }

    public async Task<InventoryOperationResult> ChangeStockStatusAsync(TenantId tenant, Guid actor, Guid entity,
        ChangeStockStatusRequest request, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (request.FromStatus == request.ToStatus || request.FromStatus == StockStatus.InTransit || request.ToStatus == StockStatus.InTransit
            || request.EnteredQuantity <= 0m || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200
            || request.PostingDate < request.OperationalDate)
            throw new InventoryException("inventory.status_change_request_invalid");
        var now = clock.GetUtcNow();
        var hash = Hash(request);
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await LockIdempotencyAsync(context, tenant.Value, entity, "inventory", request.IdempotencyKey, token);
            await using (var existing = new NpgsqlCommand("select id,idempotency_hash,operation_number from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and source_module='inventory' and idempotency_key=$3", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, entity, request.IdempotencyKey);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (!string.Equals(reader.GetString(1).Trim(), hash, StringComparison.Ordinal)) throw new InventoryException("inventory.idempotency_conflict");
                    return new InventoryOperationResult(reader.GetGuid(0), reader.GetString(2), "Confirmed", [], true);
                }
            }
            var profile = await GetProfileInTransactionAsync(context, tenant.Value, entity, request.ProductVariantId, token)
                ?? throw new InventoryException("inventory.profile_not_found");
            if (profile.State != InventoryState.StockTracked) throw new InventoryException("inventory.product_not_stock_tracked");
            if (profile.Tracking == TrackingMode.Lot && request.LotId is null) throw new InventoryException("inventory.lot_required");
            if (profile.Tracking == TrackingMode.Serial && request.SerialId is null) throw new InventoryException("inventory.serial_required");
            if (profile.Tracking == TrackingMode.Quantity && (request.LotId is not null || request.SerialId is not null)) throw new InventoryException("inventory.traceability_not_enabled");
            var variant = await catalog.FindVariantAsync(tenant, request.ProductVariantId, entity, token)
                ?? throw new InventoryException("inventory.catalog_variant_not_found");
            if (variant.Status != "Active" || variant.Nature == "Service") throw new InventoryException("inventory.product_not_stock_tracked");
            var effectiveAt = new DateTimeOffset(request.OperationalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var conversion = await quantityConversion.ConvertAsync(tenant, request.ProductVariantId, request.EnteredUomId,
                request.EnteredQuantity, effectiveAt, entity, token) ?? throw new InventoryException("inventory.uom_conversion_not_available");
            var location = request.LocationId;
            if (location is null)
            {
                await using var defaultLocation = new NpgsqlCommand("select default_location_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", context.Connection, context.Transaction);
                Add(defaultLocation, tenant.Value, entity, request.WarehouseId);
                location = await defaultLocation.ExecuteScalarAsync(token) as Guid?;
            }
            var locationId = location ?? throw new InventoryException("inventory.location_not_found");
            await using (var usable = new NpgsqlCommand("select exists(select 1 from business_inventory.location l join business_inventory.warehouse w on w.tenant_id=l.tenant_id and w.legal_entity_id=l.legal_entity_id and w.id=l.warehouse_id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.warehouse_id=$3 and l.id=$4 and l.status='Active' and l.can_hold_stock and l.can_pick_from and w.status='Active')", context.Connection, context.Transaction))
            {
                Add(usable, tenant.Value, entity, request.WarehouseId, locationId);
                if (!(bool)(await usable.ExecuteScalarAsync(token) ?? false)) throw new InventoryException("inventory.location_not_usable");
            }
            var position = new StockPosition(tenant.Value, entity, request.ProductVariantId, request.WarehouseId, locationId, request.FromStatus, request.LotId, request.SerialId);
            var target = position with { Status = request.ToStatus };
            var ordered = new[] { position, target }.OrderBy(item => item.Status).ToArray();
            var balances = new Dictionary<StockStatus, BalanceLock>();
            foreach (var item in ordered) balances[item.Status] = await ReadBalanceForUpdateAsync(context, item, token);
            if (conversion.BaseQuantity <= 0m || balances[request.FromStatus].OnHand - balances[request.FromStatus].Reserved < conversion.BaseQuantity)
                throw new InventoryException("inventory.insufficient_available_stock");
            var draft = new MovementDraft(Guid.CreateVersion7(), tenant.Value, entity, request.ProductVariantId, request.WarehouseId,
                locationId, request.EnteredQuantity, request.EnteredUomId, conversion.FactorToBase, conversion.BaseQuantity,
                "inventory", null, request.IdempotencyKey, request.OperationalDate, request.PostingDate, MovementKind.StatusChange,
                request.FromStatus, request.LotId, request.SerialId);
            draft.Validate(profile);
            await ValidateTraceabilityAsync(context, draft, MovementKind.StatusChange, token);
            await EnsurePostingDateOpenAsync(context, tenant, entity, request.PostingDate, token);

            var operationId = Guid.CreateVersion7();
            var operationNumber = OperationNumber(now, operationId);
            await using (var operation = new NpgsqlCommand("insert into business_inventory.inventory_operation(tenant_id,id,legal_entity_id,operation_number,operation_type,status,source_module,idempotency_key,idempotency_hash,operational_date,posting_date,actor_id,correlation_id,created_at,confirmed_at) values($1,$2,$3,$4,'StatusChange','Confirmed','inventory',$5,$6,$7,$8,$9,$10,$11,$11)", context.Connection, context.Transaction))
            { Add(operation, tenant.Value, operationId, entity, operationNumber, request.IdempotencyKey, hash, request.OperationalDate, request.PostingDate, actor, correlationId, now); await operation.ExecuteNonQueryAsync(token); }

            var movementIds = new List<Guid>(2);
            foreach (var movement in new[] { (Position: position, Signed: -conversion.BaseQuantity), (Position: target, Signed: conversion.BaseQuantity) })
            {
                var movementId = Guid.CreateVersion7(); movementIds.Add(movementId);
                await using var sequenceCommand = new NpgsqlCommand("insert into business_inventory.movement_sequence(tenant_id,legal_entity_id,last_value) values($1,$2,1) on conflict(tenant_id,legal_entity_id) do update set last_value=business_inventory.movement_sequence.last_value+1 returning last_value", context.Connection, context.Transaction);
                Add(sequenceCommand, tenant.Value, entity); var sequence = (long)(await sequenceCommand.ExecuteScalarAsync(token) ?? 0L);
                await using (var entry = new NpgsqlCommand("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,'inventory',$17,$18,$19,'StatusChange',$20,$19)", context.Connection, context.Transaction))
                {
                    Add(entry, tenant.Value, movementId, entity, operationId, sequence, request.ProductVariantId, request.WarehouseId, locationId,
                        movement.Position.Status.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value,
                        request.EnteredQuantity, request.EnteredUomId, conversion.FactorToBase, conversion.BaseQuantity, movement.Signed,
                        request.OperationalDate, request.PostingDate, now, actor);
                    await entry.ExecuteNonQueryAsync(token);
                }
                await using (var balance = new NpgsqlCommand("insert into business_inventory.stock_balance(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,on_hand_base_quantity,reserved_base_quantity,version,last_movement_sequence) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,0,1,$11) on conflict(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id) do update set on_hand_base_quantity=business_inventory.stock_balance.on_hand_base_quantity+excluded.on_hand_base_quantity,version=business_inventory.stock_balance.version+1,last_movement_sequence=excluded.last_movement_sequence", context.Connection, context.Transaction))
                {
                    Add(balance, tenant.Value, Guid.CreateVersion7(), entity, request.ProductVariantId, request.WarehouseId, locationId,
                        movement.Position.Status.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value,
                        movement.Signed, sequence);
                    await balance.ExecuteNonQueryAsync(token);
                }
            }
            await AuditAsync(context, tenant.Value, entity, actor, "stock.status_changed", "InventoryOperation", operationId, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "StockStatusChangedV1",
                JsonSerializer.Serialize(new { operationId, productVariantId = request.ProductVariantId, warehouseId = request.WarehouseId, locationId, quantity = conversion.BaseQuantity, fromStatus = request.FromStatus, toStatus = request.ToStatus }),
                tenant, now, correlationId, operationId, "inventory", traceParent));
            return new InventoryOperationResult(operationId, operationNumber, "Confirmed", movementIds, false);
        }, ct);
    }

    public async Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(TenantId tenant, Guid entity, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,code,name,location_mode,default_location_id,status,branch_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 order by code,id limit 500", connection);
        Add(command, tenant.Value, entity);
        var result = new List<Warehouse>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), tenant.Value, reader.GetGuid(1), reader.GetString(2), reader.GetString(3), Enum.Parse<LocationTrackingMode>(reader.GetString(4)), reader.GetGuid(5), Enum.Parse<WarehouseStatus>(reader.GetString(6)), reader.IsDBNull(7) ? null : reader.GetGuid(7)));
        return result;
    }

    public async Task<Guid?> GetWarehouseBranchAsync(TenantId tenant, Guid entity, Guid warehouseId, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select branch_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, entity, warehouseId);
        return await command.ExecuteScalarAsync(ct) is Guid branch ? branch : null;
    }

    public async Task<Warehouse> ChangeWarehouseStatusAsync(TenantId tenant, Guid actor, Guid entity, Guid warehouseId,
        WarehouseStatus requestedStatus, Guid correlationId, CancellationToken ct)
    {
        if (requestedStatus is not (WarehouseStatus.Active or WarehouseStatus.Suspended or WarehouseStatus.Archived))
            throw new InventoryException("inventory.warehouse_status_invalid");
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            string current;
            await using (var read = new NpgsqlCommand("select status from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(read, tenant.Value, entity, warehouseId);
                current = (string?)await read.ExecuteScalarAsync(token) ?? throw new InventoryException("inventory.warehouse_not_found");
            }
            if (current == requestedStatus.ToString()) return await GetWarehouseAsync(context, tenant.Value, entity, warehouseId, token);
            if (current == "Archived" || current == "Active" && requestedStatus == WarehouseStatus.Active)
                throw new InventoryException("inventory.warehouse_status_transition_invalid");
            if (requestedStatus == WarehouseStatus.Archived)
            {
                await using var blockers = new NpgsqlCommand("""
                    select exists(select 1 from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and warehouse_id=$3 and (on_hand_base_quantity<>0 or reserved_base_quantity<>0))
                      or exists(select 1 from business_inventory.product_inventory_profile where tenant_id=$1 and legal_entity_id=$2 and default_warehouse_id=$3 and status='Active')
                      or exists(select 1 from business_inventory.inventory_transfer where tenant_id=$1 and legal_entity_id=$2 and (source_warehouse_id=$3 or destination_warehouse_id=$3) and status in ('Draft','Shipped','PartiallyReceived'))
                    """, context.Connection, context.Transaction);
                Add(blockers, tenant.Value, entity, warehouseId);
                if ((bool)(await blockers.ExecuteScalarAsync(token) ?? true)) throw new InventoryException("inventory.warehouse_archive_blocked_by_activity");
            }
            await using (var update = new NpgsqlCommand("update business_inventory.warehouse set status=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            {
                Add(update, tenant.Value, entity, warehouseId, requestedStatus.ToString(), now);
                await update.ExecuteNonQueryAsync(token);
            }
            await AuditAsync(context, tenant.Value, entity, actor, "warehouse.status_changed", "Warehouse", warehouseId, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "WarehouseStatusChangedV1", JsonSerializer.Serialize(new { warehouseId, fromStatus = current, toStatus = requestedStatus.ToString() }), tenant, now, correlationId, warehouseId, "inventory"));
            return await GetWarehouseAsync(context, tenant.Value, entity, warehouseId, token);
        }, ct);
    }

    public async Task ArchiveLocationAsync(TenantId tenant, Guid actor, Guid entity, Guid locationId, Guid correlationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var location = new NpgsqlCommand("select warehouse_id,status,exists(select 1 from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=l.warehouse_id and default_location_id=l.id) from business_inventory.location l where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
            Add(location, tenant.Value, entity, locationId);
            Guid warehouseId;
            await using (var reader = await location.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.location_not_found");
                warehouseId = reader.GetGuid(0);
                if (reader.GetString(1) == "Archived") return true;
                if (reader.GetBoolean(2)) throw new InventoryException("inventory.default_location_cannot_be_archived");
            }
            await using var blockers = new NpgsqlCommand("""
                select exists(select 1 from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and location_id=$3 and (on_hand_base_quantity<>0 or reserved_base_quantity<>0))
                  or exists(select 1 from business_inventory.location where tenant_id=$1 and legal_entity_id=$2 and parent_location_id=$3 and status='Active')
                  or exists(select 1 from business_inventory.reservation_allocation a join business_inventory.reservation r on r.tenant_id=a.tenant_id and r.legal_entity_id=a.legal_entity_id and r.id=a.reservation_id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.location_id=$3 and r.status='Active')
                """, context.Connection, context.Transaction);
            Add(blockers, tenant.Value, entity, locationId);
            if ((bool)(await blockers.ExecuteScalarAsync(token) ?? true)) throw new InventoryException("inventory.location_archive_blocked_by_activity");
            await using (var update = new NpgsqlCommand("update business_inventory.location set status='Archived' where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, locationId); await update.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenant.Value, entity, actor, "location.archived", "Location", locationId, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "InventoryLocationArchivedV1", JsonSerializer.Serialize(new { warehouseId, locationId }), tenant, now, correlationId, locationId, "inventory"));
            return true;
        }, ct);
    }

    private static async Task<Warehouse> GetWarehouseAsync(ITransactionContext context, Guid tenant, Guid entity, Guid warehouseId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,code,name,location_mode,default_location_id,status,branch_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
        Add(command, tenant, entity, warehouseId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InventoryException("inventory.warehouse_not_found");
        return new(reader.GetGuid(0), tenant, entity, reader.GetString(1), reader.GetString(2), Enum.Parse<LocationTrackingMode>(reader.GetString(3)), reader.GetGuid(4), Enum.Parse<WarehouseStatus>(reader.GetString(5)), reader.IsDBNull(6) ? null : reader.GetGuid(6));
    }

    public async Task<IReadOnlyList<InventoryBalanceView>> GetBalancesAsync(TenantId tenant, Guid entity, Guid? productVariantId, Guid? warehouseId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 500) throw new InventoryException("inventory.page_size_invalid");
        await ExpireReservationsAsync(tenant, entity, 500, Guid.CreateVersion7(), ct);
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,on_hand_base_quantity,reserved_base_quantity,greatest(0,on_hand_base_quantity-reserved_base_quantity),version,last_movement_sequence from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or product_variant_id=$3) and ($4::uuid is null or warehouse_id=$4) and on_hand_base_quantity<>0 order by product_variant_id,warehouse_id,location_id,stock_status,lot_id nulls first,serial_id nulls first limit $5", connection);
        Add(command, tenant.Value, entity, productVariantId ?? (object)DBNull.Value, warehouseId ?? (object)DBNull.Value, limit);
        var result = new List<InventoryBalanceView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), NullGuid(reader, 4), NullGuid(reader, 5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetInt64(9), reader.GetInt64(10)));
        return result;
    }

    public async Task<IReadOnlyList<InventoryAvailabilityLine>> GetBatchAvailabilityAsync(TenantId tenant, InventoryAvailabilityRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.ProductVariantId == Guid.Empty || request.WarehouseIds?.Count > 500)
            throw new InventoryException("inventory.availability_request_invalid");
        await ExpireReservationsAsync(tenant, request.LegalEntityId, 500, Guid.CreateVersion7(), ct);
        var allowedUoms = await catalog.GetAllowedUomsBatchAsync(tenant, [request.ProductVariantId], clock.GetUtcNow(), request.LegalEntityId, ct);
        if (!allowedUoms.TryGetValue(request.ProductVariantId, out var units)) throw new InventoryException("inventory.catalog_variant_not_found");
        var baseUnitCode = units.SingleOrDefault(unit => unit.IsBase)?.UnitCode ?? throw new InventoryException("inventory.base_uom_missing");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select product_variant_id,warehouse_id,sum(case when stock_status='Available' then on_hand_base_quantity else 0 end),sum(case when stock_status='Available' then greatest(0,on_hand_base_quantity-reserved_base_quantity) else 0 end),sum(case when stock_status='Available' then reserved_base_quantity else 0 end),sum(case when stock_status='InTransit' then on_hand_base_quantity else 0 end) from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and stock_status in ('Available','InTransit') and ($4::uuid is null or warehouse_id=$4) and ($5::uuid[] is null or warehouse_id=any($5)) group by product_variant_id,warehouse_id order by warehouse_id limit 500", connection);
        Add(command, tenant.Value, request.LegalEntityId, request.ProductVariantId, request.WarehouseId ?? (object)DBNull.Value,
            request.WarehouseIds is { Count: > 0 } ? request.WarehouseIds.Distinct().ToArray() : (object)DBNull.Value);
        var rows = new List<InventoryAvailabilityLine>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new InventoryAvailabilityLine(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2),
                request.IncludeReservations ? reader.GetDecimal(4) : 0m,
                request.IncludeReservations ? reader.GetDecimal(3) : reader.GetDecimal(2), reader.GetDecimal(5), baseUnitCode));
        return rows;
    }

    public async Task<InventoryValuationSnapshot?> GetValuationAsync(TenantId tenant, InventoryValuationQuery query, CancellationToken ct)
    {
        if (query.LegalEntityId == Guid.Empty || query.ProductVariantId == Guid.Empty) throw new InventoryException("inventory.valuation_request_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select coalesce(sum(v.quantity_delta),0),coalesce(sum(v.value_delta),0),max(v.currency_code),max(v.costing_method) from business_inventory.valuation_entry v join business_inventory.product_inventory_profile p on p.tenant_id=v.tenant_id and p.legal_entity_id=v.legal_entity_id and p.product_variant_id=v.product_variant_id where v.tenant_id=$1 and v.legal_entity_id=$2 and v.product_variant_id=$3 and p.valuation_mode='Perpetual' and v.effective_at<($4::date + interval '1 day')", connection);
        Add(command, tenant.Value, query.LegalEntityId, query.ProductVariantId, query.AsOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(2) || reader.IsDBNull(3)) return null;
        return new InventoryValuationSnapshot(query.ProductVariantId, reader.GetDecimal(0), reader.GetDecimal(1), reader.GetString(2), reader.GetString(3), query.AsOfDate);
    }

    public async Task<InventoryValuationTotal?> GetValuationTotalAsync(TenantId tenant, Guid entity, DateOnly asOfDate, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select coalesce(sum(v.quantity_delta),0),coalesce(sum(v.value_delta),0),max(v.currency_code) from business_inventory.valuation_entry v join business_inventory.product_inventory_profile p on p.tenant_id=v.tenant_id and p.legal_entity_id=v.legal_entity_id and p.product_variant_id=v.product_variant_id where v.tenant_id=$1 and v.legal_entity_id=$2 and p.valuation_mode='Perpetual' and v.effective_at<($3::date + interval '1 day')", connection);
        Add(command, tenant.Value, entity, asOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(2)) return null;
        return new InventoryValuationTotal(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetString(2));
    }

    public async Task<InventoryMovementPage> GetMovementHistoryAsync(TenantId tenant, Guid entity, Guid? productVariantId, Guid? warehouseId,
        Guid? locationId, Guid? lotId, Guid? serialId, Guid? sourceDocumentId, DateTimeOffset? beforeEffectiveAt,
        long? beforeSequence, Guid? beforeId, int limit, CancellationToken ct, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        if (limit is < 1 or > 200 || (beforeSequence is null) != (beforeId is null) || (beforeSequence is null) != (beforeEffectiveAt is null)
            || from is { } start && to is { } end && start >= end)
            throw new InventoryException("inventory.page_request_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("""
            select id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,
                   entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,
                   movement_type,source_module,source_document_id,operational_date,posting_date,effective_at
            from business_inventory.stock_movement_entry
            where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or product_variant_id=$3)
              and ($4::uuid is null or warehouse_id=$4) and ($5::uuid is null or location_id=$5)
              and ($6::uuid is null or lot_id=$6) and ($7::uuid is null or serial_id=$7)
              and ($8::uuid is null or source_document_id=$8)
              and ($9::timestamptz is null or (effective_at,sequence,id)<($9,$10,$11))
              and ($12::timestamptz is null or effective_at >= $12) and ($13::timestamptz is null or effective_at < $13)
            order by effective_at desc,sequence desc,id desc limit $14
            """, connection);
        Add(command, tenant.Value, entity, productVariantId ?? (object)DBNull.Value, warehouseId ?? (object)DBNull.Value,
            locationId ?? (object)DBNull.Value, lotId ?? (object)DBNull.Value, serialId ?? (object)DBNull.Value,
            sourceDocumentId ?? (object)DBNull.Value, beforeEffectiveAt ?? (object)DBNull.Value, beforeSequence ?? (object)DBNull.Value,
            beforeId ?? (object)DBNull.Value, from ?? (object)DBNull.Value, to ?? (object)DBNull.Value, limit + 1);
        var items = new List<InventoryMovementView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetString(6), NullGuid(reader, 7), NullGuid(reader, 8), reader.GetDecimal(9), reader.GetGuid(10), reader.GetDecimal(11), reader.GetDecimal(12), reader.GetDecimal(13), reader.GetString(14), reader.GetString(15), NullGuid(reader, 16), DateOnly.FromDateTime(reader.GetDateTime(17)), DateOnly.FromDateTime(reader.GetDateTime(18)), reader.GetFieldValue<DateTimeOffset>(19)));
        var hasMore = items.Count > limit; if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items, hasMore);
    }

    public async Task<IReadOnlyList<NegativeStockConditionView>> GetNegativeStockConditionsAsync(TenantId tenant, Guid entity,
        string? status, DateTimeOffset? beforeOpenedAt, Guid? beforeId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 500 || (beforeOpenedAt is null) != (beforeId is null)
            || status is not (null or "Active" or "Resolved")) throw new InventoryException("inventory.page_request_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,condition_status,shortfall_base_quantity,opened_by_movement_id,last_seen_movement_id,resolved_by_movement_id,opened_at,resolved_at from business_inventory.negative_stock_condition where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or condition_status=$3) and ($4::timestamptz is null or (opened_at,id)<($4,$5)) order by opened_at desc,id desc limit $6", connection);
        Add(command, tenant.Value, entity, status ?? (object)DBNull.Value, beforeOpenedAt ?? (object)DBNull.Value,
            beforeId ?? (object)DBNull.Value, limit);
        var rows = new List<NegativeStockConditionView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
            reader.GetString(4), NullGuid(reader, 5), NullGuid(reader, 6), reader.GetString(7), reader.GetDecimal(8),
            reader.GetGuid(9), reader.GetGuid(10), NullGuid(reader, 11), reader.GetFieldValue<DateTimeOffset>(12),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13)));
        return rows;
    }

    public async Task<IReadOnlyList<InventoryAsOfView>> GetStockAsOfAsync(TenantId tenant, Guid entity, DateTimeOffset asOf,
        Guid? productVariantId, Guid? warehouseId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 500) throw new InventoryException("inventory.page_size_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,sum(signed_base_quantity) from business_inventory.stock_movement_entry where tenant_id=$1 and legal_entity_id=$2 and effective_at<=$3 and ($4::uuid is null or product_variant_id=$4) and ($5::uuid is null or warehouse_id=$5) group by product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id having sum(signed_base_quantity)<>0 order by product_variant_id,warehouse_id,location_id,stock_status,lot_id nulls first,serial_id nulls first limit $6", connection);
        Add(command, tenant.Value, entity, asOf, productVariantId ?? (object)DBNull.Value, warehouseId ?? (object)DBNull.Value, limit);
        var result = new List<InventoryAsOfView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), NullGuid(reader, 4), NullGuid(reader, 5), reader.GetDecimal(6)));
        return result;
    }

    public async Task<IReadOnlyList<ProductLocatorView>> LocateProductAsync(TenantId tenant, Guid entity, Guid productVariantId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 500) throw new InventoryException("inventory.page_size_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("""
            with recursive location_path as (
              select l.tenant_id,l.legal_entity_id,l.warehouse_id,l.id,l.parent_location_id,l.code::text as path
              from business_inventory.location l where l.tenant_id=$1 and l.legal_entity_id=$2 and l.parent_location_id is null
              union all
              select child.tenant_id,child.legal_entity_id,child.warehouse_id,child.id,child.parent_location_id,(parent.path || ' / ' || child.code)
              from business_inventory.location child join location_path parent on parent.tenant_id=child.tenant_id and parent.warehouse_id=child.warehouse_id and parent.id=child.parent_location_id
            )
            select b.product_variant_id,b.warehouse_id,w.code,b.location_id,coalesce(lp.path,l.code),b.stock_status,b.lot_id,b.serial_id,
                   b.on_hand_base_quantity,b.reserved_base_quantity,greatest(0,b.on_hand_base_quantity-b.reserved_base_quantity)
            from business_inventory.stock_balance b join business_inventory.warehouse w on w.tenant_id=b.tenant_id and w.legal_entity_id=b.legal_entity_id and w.id=b.warehouse_id
            join business_inventory.location l on l.tenant_id=b.tenant_id and l.warehouse_id=b.warehouse_id and l.id=b.location_id
            left join location_path lp on lp.tenant_id=b.tenant_id and lp.legal_entity_id=b.legal_entity_id and lp.warehouse_id=b.warehouse_id and lp.id=b.location_id
            where b.tenant_id=$1 and b.legal_entity_id=$2 and b.product_variant_id=$3 and b.on_hand_base_quantity<>0
            order by w.code,coalesce(lp.path,l.code),b.stock_status,b.lot_id,b.serial_id limit $4
            """, connection);
        Add(command, tenant.Value, entity, productVariantId, limit); var result = new List<ProductLocatorView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), NullGuid(reader, 6), NullGuid(reader, 7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10)));
        return result;
    }

    public async Task<InventoryLocationSuggestion?> SuggestPutawayAsync(TenantId tenant, Guid entity, Guid productVariantId,
        Guid? warehouseId, CancellationToken ct)
    {
        var profile = await GetProfileAsync(tenant, entity, productVariantId, ct) ?? throw new InventoryException("inventory.profile_not_found");
        if (profile.State != InventoryState.StockTracked) throw new InventoryException("inventory.product_not_stock_tracked");
        var variant = await catalog.FindVariantAsync(tenant, productVariantId, entity, ct)
            ?? throw new InventoryException("inventory.catalog_variant_not_found");
        if (variant.Nature == "Service" || variant.Status != "Active") throw new InventoryException("inventory.product_not_stock_tracked");
        var targetWarehouse = warehouseId ?? profile.DefaultWarehouseId;
        if (targetWarehouse is null) throw new InventoryException("inventory.default_warehouse_required");
        await using var connection = await OpenTenantAsync(tenant, ct);
        const string sql = "select w.id,l.id,l.code,w.default_location_id,exists(select 1 from business_inventory.stock_balance b where b.tenant_id=w.tenant_id and b.legal_entity_id=w.legal_entity_id and b.product_variant_id=$4 and b.warehouse_id=w.id and b.location_id=l.id and b.on_hand_base_quantity<>0) from business_inventory.warehouse w join business_inventory.location l on l.tenant_id=w.tenant_id and l.legal_entity_id=w.legal_entity_id and l.warehouse_id=w.id where w.tenant_id=$1 and w.legal_entity_id=$2 and w.id=$3 and w.status='Active' and l.status='Active' and l.can_hold_stock and l.can_receive_into and (w.location_mode<>'WarehouseOnly' or l.id=w.default_location_id) order by case when exists(select 1 from business_inventory.stock_balance b where b.tenant_id=w.tenant_id and b.legal_entity_id=w.legal_entity_id and b.product_variant_id=$4 and b.warehouse_id=w.id and b.location_id=l.id and b.on_hand_base_quantity<>0) then 0 else 1 end,case when l.id=w.default_location_id then 0 else 1 end,l.code,l.id limit 1";
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, tenant.Value, entity, targetWarehouse.Value, productVariantId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var isDefault = reader.GetGuid(1) == reader.GetGuid(3);
        return new InventoryLocationSuggestion(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), null, null, null,
            reader.GetBoolean(4) ? "ConsolidateExistingStock" : isDefault ? "DefaultLocation" : "FirstUsableLocation");
    }

    public async Task<IReadOnlyList<InventoryLocationSuggestion>> SuggestPickingAsync(TenantId tenant, Guid entity, Guid productVariantId,
        Guid? warehouseId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new InventoryException("inventory.page_size_invalid");
        await ExpireReservationsAsync(tenant, entity, 500, Guid.CreateVersion7(), ct);
        await using var connection = await OpenTenantAsync(tenant, ct);
        const string sql = "select b.warehouse_id,b.location_id,l.code,greatest(0,b.on_hand_base_quantity-b.reserved_base_quantity),b.lot_id,b.serial_id from business_inventory.stock_balance b join business_inventory.warehouse w on w.tenant_id=b.tenant_id and w.legal_entity_id=b.legal_entity_id and w.id=b.warehouse_id and w.status='Active' join business_inventory.location l on l.tenant_id=b.tenant_id and l.legal_entity_id=b.legal_entity_id and l.warehouse_id=b.warehouse_id and l.id=b.location_id and l.status='Active' and l.can_pick_from left join business_inventory.lot lot on lot.tenant_id=b.tenant_id and lot.legal_entity_id=b.legal_entity_id and lot.id=b.lot_id where b.tenant_id=$1 and b.legal_entity_id=$2 and b.product_variant_id=$3 and b.stock_status='Available' and b.on_hand_base_quantity>b.reserved_base_quantity and ($4::uuid is null or b.warehouse_id=$4) order by lot.expires_on nulls last,l.code,b.last_movement_sequence,b.location_id limit $5";
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, tenant.Value, entity, productVariantId, warehouseId ?? (object)DBNull.Value, limit);
        var result = new List<InventoryLocationSuggestion>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), NullGuid(reader, 4), NullGuid(reader, 5), "AvailableStock"));
        return result;
    }

    public async Task<InventoryScanResult?> ResolveOwnedScanAsync(TenantId tenant, Guid entity, string code, CancellationToken ct)
    {
        var normalized = code.Trim();
        if (normalized.Length is < 1 or > 160) throw new InventoryException("inventory.scan_code_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        var matches = new List<InventoryScanResult>(3);
        await using (var location = new NpgsqlCommand("select l.id,l.warehouse_id,l.code,l.status from business_inventory.location l join business_inventory.warehouse w on w.tenant_id=l.tenant_id and w.legal_entity_id=l.legal_entity_id and w.id=l.warehouse_id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.barcode=$3 and l.status='Active' and w.status='Active' limit 2", connection))
        {
            Add(location, tenant.Value, entity, normalized); await using var reader = await location.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) matches.Add(new InventoryScanResult("Location", reader.GetGuid(0), null, reader.GetGuid(1), reader.GetString(2), reader.GetString(3)));
        }
        await using (var lot = new NpgsqlCommand("select id,product_variant_id,lot_number,status from business_inventory.lot where tenant_id=$1 and legal_entity_id=$2 and lot_number=$3 and status='Active' limit 2", connection))
        {
            Add(lot, tenant.Value, entity, normalized); await using var reader = await lot.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) matches.Add(new InventoryScanResult("Lot", reader.GetGuid(0), reader.GetGuid(1), null, reader.GetString(2), reader.GetString(3)));
        }
        await using (var serial = new NpgsqlCommand("select id,product_variant_id,serial_number,status,current_warehouse_id from business_inventory.serial where tenant_id=$1 and legal_entity_id=$2 and serial_number=$3 limit 2", connection))
        {
            Add(serial, tenant.Value, entity, normalized); await using var reader = await serial.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) matches.Add(new InventoryScanResult("Serial", reader.GetGuid(0), reader.GetGuid(1), NullGuid(reader, 4), reader.GetString(2), reader.GetString(3)));
        }
        if (matches.Count > 1) throw new InventoryException("inventory.scan_ambiguous");
        return matches.SingleOrDefault();
    }

    public async Task<PhysicalCountView> CreatePhysicalCountAsync(TenantId tenant, Guid actor, Guid entity,
        CreatePhysicalCountRequest request, Guid correlationId, CancellationToken ct)
    {
        if (!Enum.TryParse<PhysicalCountMode>(request.Mode, true, out var mode)) throw new InventoryException("inventory.count_mode_invalid");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var number = $"CNT-{now:yyyyMMdd}-{id.ToString("N")[16..].ToUpperInvariant()}";
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var check = new NpgsqlCommand("select exists(select 1 from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active')", context.Connection, context.Transaction))
            { Add(check, tenant.Value, entity, request.WarehouseId); if (!(bool)(await check.ExecuteScalarAsync(token) ?? false)) throw new InventoryException("inventory.warehouse_not_found_or_inactive"); }
            await using (var count = new NpgsqlCommand("insert into business_inventory.physical_count(tenant_id,id,legal_entity_id,count_number,warehouse_id,status,count_mode,snapshot_at,created_by,created_at,version) values($1,$2,$3,$4,$5,'Draft',$6,$7,$8,$7,1)", context.Connection, context.Transaction))
            { Add(count, tenant.Value, id, entity, number, request.WarehouseId, mode.ToString(), now, actor); await count.ExecuteNonQueryAsync(token); }
            var lines = 0;
            await using (var insertLines = new NpgsqlCommand("insert into business_inventory.count_line(tenant_id,id,count_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,expected_quantity) select tenant_id,$4,$5,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,on_hand_base_quantity from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and warehouse_id=$3 and on_hand_base_quantity<>0", context.Connection, context.Transaction))
            { Add(insertLines, tenant.Value, entity, request.WarehouseId, Guid.CreateVersion7(), id); lines = await insertLines.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenant.Value, entity, actor, "physical_count.created", "PhysicalCount", id, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "PhysicalCountStartedV1", JsonSerializer.Serialize(new { countId = id, request.WarehouseId, mode = mode.ToString(), lines }), tenant, now, correlationId, id, "inventory"));
            return new PhysicalCountView(id, number, "Draft", lines, 0, 0m);
        }, ct);
    }

    public async Task<PhysicalCountView> EnterPhysicalCountLineAsync(TenantId tenant, Guid actor, Guid entity, Guid countId,
        Guid lineId, EnterCountLineRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.CountedBaseQuantity < 0m || request.CountedUnitCost is < 0m) throw new InventoryException("inventory.count_quantity_invalid");
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            Guid productVariantId; Guid? serialId; decimal expected;
            await using (var identity = new NpgsqlCommand("select product_variant_id,serial_id,expected_quantity from business_inventory.count_line where tenant_id=$1 and legal_entity_id=$2 and count_id=$3 and id=$4", context.Connection, context.Transaction))
            {
                Add(identity, tenant.Value, entity, countId, lineId); await using var reader = await identity.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.count_line_not_editable");
                productVariantId = reader.GetGuid(0); serialId = NullGuid(reader, 1); expected = reader.GetDecimal(2);
            }
            var profile = await GetProfileInTransactionAsync(context, tenant.Value, entity, productVariantId, token)
                ?? throw new InventoryException("inventory.profile_not_found");
            if (profile.Tracking == TrackingMode.Serial && (serialId is null || expected is not (0m or 1m) || request.CountedBaseQuantity is not (0m or 1m)))
                throw new InventoryException("inventory.serial_count_must_identify_one_unit");
            if (profile.Tracking != TrackingMode.Serial && serialId is not null)
                throw new InventoryException("inventory.traceability_not_enabled");
            await using (var update = new NpgsqlCommand("update business_inventory.count_line l set counted_quantity=$5,counted_unit_cost=$6,difference_quantity=$5-l.expected_quantity,entered_by=$7,entered_at=$8 from business_inventory.physical_count c where l.tenant_id=$1 and l.legal_entity_id=$2 and l.count_id=$3 and l.id=$4 and c.tenant_id=l.tenant_id and c.id=l.count_id and c.status in ('Draft','Counting')", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, countId, lineId, request.CountedBaseQuantity, request.CountedUnitCost ?? (object)DBNull.Value, actor, now); if (await update.ExecuteNonQueryAsync(token) != 1) throw new InventoryException("inventory.count_line_not_editable"); }
            await using (var status = new NpgsqlCommand("update business_inventory.physical_count set status='Counting',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status in ('Draft','Counting')", context.Connection, context.Transaction))
            { Add(status, tenant.Value, entity, countId); await status.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenant.Value, entity, actor, "physical_count.line_entered", "CountLine", lineId, correlationId, now, token);
            return await GetPhysicalCountAsync(context, tenant.Value, entity, countId, token);
        }, ct);
    }

    public async Task<PhysicalCountLineView> AddPhysicalCountLineAsync(TenantId tenant, Guid actor, Guid entity, Guid countId,
        AddCountLineRequest request, Guid correlationId, CancellationToken ct)
    {
        if (!Enum.TryParse<StockStatus>(request.StockStatus, true, out var stockStatus) || stockStatus == StockStatus.InTransit)
            throw new InventoryException("inventory.stock_status_invalid");
        var now = clock.GetUtcNow(); var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            Guid warehouseId; string status;
            await using (var count = new NpgsqlCommand("select warehouse_id,status from business_inventory.physical_count where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            { Add(count, tenant.Value, entity, countId); await using var reader = await count.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.count_not_found"); warehouseId = reader.GetGuid(0); status = reader.GetString(1); }
            if (status is not ("Draft" or "Counting")) throw new InventoryException("inventory.count_not_editable");
            var profile = await GetProfileInTransactionAsync(context, tenant.Value, entity, request.ProductVariantId, token) ?? throw new InventoryException("inventory.profile_not_found");
            if (profile.State != InventoryState.StockTracked) throw new InventoryException("inventory.product_not_stock_tracked");
            if (profile.Tracking == TrackingMode.Lot && request.LotId is null || profile.Tracking == TrackingMode.Serial && request.SerialId is null)
                throw new InventoryException("inventory.traceability_required");
            if (profile.Tracking == TrackingMode.Quantity && (request.LotId is not null || request.SerialId is not null)) throw new InventoryException("inventory.traceability_not_enabled");
            if (request.SerialId is { } serialId)
            {
                await using var serial = new NpgsqlCommand("select status from business_inventory.serial where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4", context.Connection, context.Transaction);
                Add(serial, tenant.Value, entity, request.ProductVariantId, serialId);
                if (await serial.ExecuteScalarAsync(token) is not string) throw new InventoryException("inventory.serial_not_found");
            }
            var position = new StockPosition(tenant.Value, entity, request.ProductVariantId, warehouseId, request.LocationId, stockStatus, request.LotId, request.SerialId);
            var balance = await ReadBalanceForUpdateAsync(context, position, token);
            await using (var line = new NpgsqlCommand("insert into business_inventory.count_line(tenant_id,id,count_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,expected_quantity) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", context.Connection, context.Transaction))
            { Add(line, tenant.Value, id, countId, entity, request.ProductVariantId, warehouseId, request.LocationId, stockStatus.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, balance.OnHand); await line.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenant.Value, entity, actor, "physical_count.line_added", "CountLine", id, correlationId, now, token);
            return new PhysicalCountLineView(id, request.ProductVariantId, warehouseId, request.LocationId, stockStatus.ToString(), request.LotId, request.SerialId, balance.OnHand, null, null, null, null);
        }, ct);
    }

    public async Task<IReadOnlyList<PhysicalCountLineView>> GetPhysicalCountLinesAsync(TenantId tenant, Guid entity, Guid countId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 1000) throw new InventoryException("inventory.page_size_invalid");
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,expected_quantity,counted_quantity,difference_quantity,entered_by,entered_at from business_inventory.count_line where tenant_id=$1 and legal_entity_id=$2 and count_id=$3 order by id limit $4", connection);
        Add(command, tenant.Value, entity, countId, limit); var result = new List<PhysicalCountLineView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), NullGuid(reader, 5), NullGuid(reader, 6), reader.GetDecimal(7), reader.IsDBNull(8) ? null : reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetDecimal(9), NullGuid(reader, 10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)));
        return result;
    }

    public async Task<PhysicalCountView> PostPhysicalCountAsync(TenantId tenant, Guid actor, Guid entity, Guid countId,
        string idempotencyKey, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new InventoryException("inventory.idempotency_key_invalid");
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            Guid warehouseId; string status; DateTimeOffset snapshot;
            await using (var count = new NpgsqlCommand("select warehouse_id,status,snapshot_at from business_inventory.physical_count where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            { Add(count, tenant.Value, entity, countId); await using var reader = await count.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.count_not_found"); warehouseId = reader.GetGuid(0); status = reader.GetString(1); snapshot = reader.GetFieldValue<DateTimeOffset>(2); }
            if (status == "Posted") return await GetPhysicalCountAsync(context, tenant.Value, entity, countId, token);
            if (status is not ("Counting" or "Draft")) throw new InventoryException("inventory.count_not_postable");
            var lines = new List<CountLineRecord>();
            await using (var command = new NpgsqlCommand("select id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,expected_quantity,counted_quantity,counted_unit_cost,entered_at from business_inventory.count_line where tenant_id=$1 and legal_entity_id=$2 and count_id=$3 order by id for update", context.Connection, context.Transaction))
            { Add(command, tenant.Value, entity, countId); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) lines.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), Enum.Parse<StockStatus>(reader.GetString(4)), NullGuid(reader, 5), NullGuid(reader, 6), reader.GetDecimal(7), reader.IsDBNull(8) ? null : reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10))); }
            if (lines.Count == 0 || lines.Any(line => line.Counted is null)) throw new InventoryException("inventory.count_lines_incomplete");
            var totalVariance = 0m; var posted = 0;
            foreach (var line in lines)
            {
                var position = new StockPosition(tenant.Value, entity, line.ProductVariantId, line.WarehouseId, line.LocationId, line.Status, line.LotId, line.SerialId);
                var balance = await ReadBalanceForUpdateAsync(context, position, token);
                var movementSinceSnapshot = 0m;
                await using (var movementSum = new NpgsqlCommand("select coalesce(sum(signed_base_quantity),0) from business_inventory.stock_movement_entry where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and lot_id is not distinct from $7::uuid and serial_id is not distinct from $8::uuid and effective_at>$9 and effective_at<=$10", context.Connection, context.Transaction))
                { Add(movementSum, tenant.Value, entity, line.ProductVariantId, line.WarehouseId, line.LocationId, line.Status.ToString(), line.LotId ?? (object)DBNull.Value, line.SerialId ?? (object)DBNull.Value, snapshot, line.EnteredAt ?? now); movementSinceSnapshot = (decimal)(await movementSum.ExecuteScalarAsync(token) ?? 0m); }
                var expectedAtCount = line.Expected + movementSinceSnapshot;
                var delta = line.Counted!.Value - expectedAtCount; totalVariance += delta;
                var profile = await GetProfileInTransactionAsync(context, tenant.Value, entity, line.ProductVariantId, token)
                    ?? throw new InventoryException("inventory.profile_not_found");
                if (profile.Tracking == TrackingMode.Serial)
                {
                    if (line.SerialId is null || expectedAtCount is not (0m or 1m) || line.Counted.Value is not (0m or 1m)
                        || delta is not (0m or 1m or -1m))
                        throw new InventoryException("inventory.serial_count_must_identify_one_unit");
                    await ValidateSerialCountAsync(context, tenant.Value, entity, line, line.Counted.Value, expectedAtCount, token);
                }
                else if (line.SerialId is not null)
                    throw new InventoryException("inventory.traceability_not_enabled");
                if (delta == 0m) continue;
                var signed = delta; var quantity = Math.Abs(delta);
                if (balance.OnHand + delta < balance.Reserved) throw new InventoryException("inventory.count_variance_conflicts_with_reservations");
                var operationalDate = DateOnly.FromDateTime((line.EnteredAt ?? now).UtcDateTime); var postingDate = DateOnly.FromDateTime(now.UtcDateTime);
                var baseCurrency = await EnsurePostingDateOpenAsync(context, tenant, entity, postingDate, token);
                var operationId = Guid.CreateVersion7(); var movementId = Guid.CreateVersion7(); var opNumber = OperationNumber(now, operationId);
                var key = $"count:{countId:D}:line:{line.Id:D}";
                await InsertConfirmedOperationAsync(context, tenant.Value, operationId, entity, opNumber, MovementKind.Adjustment, "inventory", key, Hash(new { countId, line.Id, delta }), operationalDate, postingDate, actor, correlationId, now, token);
                var uoms = await catalog.GetBaseUomsBatchAsync(tenant, [line.ProductVariantId], new DateTimeOffset(operationalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), entity, token);
                if (!uoms.TryGetValue(line.ProductVariantId, out var baseUom)) throw new InventoryException("inventory.base_uom_not_found");
                var sequence = await NextMovementSequenceAsync(context, tenant.Value, entity, token);
                await using (var movement = new NpgsqlCommand("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,source_document_id,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,1,$12,$14,'inventory',$15,$16,$17,$18,'Adjustment',$19,$18)", context.Connection, context.Transaction))
                { Add(movement, tenant.Value, movementId, entity, operationId, sequence, line.ProductVariantId, line.WarehouseId, line.LocationId, line.Status.ToString(), line.LotId ?? (object)DBNull.Value, line.SerialId ?? (object)DBNull.Value, quantity, baseUom, signed, countId, operationalDate, postingDate, now, actor); await movement.ExecuteNonQueryAsync(token); }
                var draft = new MovementDraft(movementId, tenant.Value, entity, line.ProductVariantId, line.WarehouseId, line.LocationId, quantity, baseUom, 1m, quantity, "inventory", countId, key, operationalDate, postingDate, MovementKind.Adjustment, line.Status, line.LotId, line.SerialId);
                if (profile.Valuation == ValuationMode.Perpetual) await ApplyValuationAsync(context, tenant, actor, entity, operationId, movementId, profile, draft with { UnitCost = line.CountedUnitCost, CurrencyCode = baseCurrency }, baseCurrency, MovementKind.Adjustment, correlationId, now, token, signed);
                await ApplyBalanceDeltaAsync(context, position, signed, 0m, movementId, token);
                if (line.SerialId is { } serialId)
                    await MoveSerialAsync(context, tenant.Value, entity, line.ProductVariantId, serialId,
                        signed > 0m ? "OnHand" : "Issued", signed > 0m ? line.WarehouseId : null,
                        signed > 0m ? line.LocationId : null, token);
                await AuditAsync(context, tenant.Value, entity, actor, "stock.count_adjustment_posted", "InventoryOperation", operationId, correlationId, now, token);
                context.AddOutbox(new(Guid.CreateVersion7(), "StockAdjustedV1", JsonSerializer.Serialize(new { operationId, movementId, countId, productVariantId = line.ProductVariantId, delta = signed }), tenant, now, correlationId, operationId, "inventory", traceParent)); posted++;
            }
            await using (var update = new NpgsqlCommand("update business_inventory.physical_count set status='Posted',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, countId); await update.ExecuteNonQueryAsync(token); }
            await AuditAsync(context, tenant.Value, entity, actor, "physical_count.posted", "PhysicalCount", countId, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "PhysicalCountPostedV1", JsonSerializer.Serialize(new { countId, warehouseId, adjustmentCount = posted, varianceQuantity = totalVariance }), tenant, now, correlationId, countId, "inventory", traceParent));
            return await GetPhysicalCountAsync(context, tenant.Value, entity, countId, token);
        }, ct);
    }

    public async Task<TransferResult> CreateTransferAsync(TenantId tenant, Guid actor, Guid entity, CreateTransferRequest request, Guid correlationId, CancellationToken ct)
    {
        if (!Enum.TryParse<TransferMode>(request.Mode, true, out var mode) || request.SourceWarehouseId == request.DestinationWarehouseId && request.SourceLocationId == request.DestinationLocationId)
            throw new InventoryException("inventory.transfer_request_invalid");
        if (request.IdempotencyKey.Length is < 1 or > 200 || request.EnteredQuantity <= 0m || request.PostingDate < request.OperationalDate)
            throw new InventoryException("inventory.transfer_request_invalid");
        var variant = await catalog.FindVariantAsync(tenant, request.ProductVariantId, entity, ct)
            ?? throw new InventoryException("inventory.catalog_variant_not_found");
        if (variant.Status != "Active" || variant.Nature == "Service") throw new InventoryException("inventory.product_not_stock_tracked");
        var profile = await GetProfileAsync(tenant, entity, request.ProductVariantId, ct)
            ?? throw new InventoryException("inventory.profile_not_found");
        var effectiveAt = new DateTimeOffset(request.OperationalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var conversion = await quantityConversion.ConvertAsync(tenant, request.ProductVariantId, request.EnteredUomId, request.EnteredQuantity, effectiveAt, entity, ct)
            ?? throw new InventoryException("inventory.uom_conversion_not_available");
        if (profile.Tracking == TrackingMode.Lot && request.LotId is null || profile.Tracking == TrackingMode.Serial && request.SerialId is null)
            throw new InventoryException("inventory.traceability_required");
        if (profile.Tracking == TrackingMode.Quantity && (request.LotId is not null || request.SerialId is not null)) throw new InventoryException("inventory.traceability_not_enabled");
        if (request.SerialId is not null && conversion.BaseQuantity != 1m) throw new InventoryException("inventory.serial_quantity_must_be_one");

        var sourceLocation = await ResolveLocationAsync(tenant, entity, request.SourceWarehouseId, request.SourceLocationId, effectiveAt, canPick: true, ct);
        var destinationLocation = await ResolveLocationAsync(tenant, entity, request.DestinationWarehouseId, request.DestinationLocationId, effectiveAt, canPick: false, ct);
        var now = clock.GetUtcNow();
        var requestHash = Hash(new { request.ProductVariantId, request.SourceWarehouseId, sourceLocation, request.DestinationWarehouseId, destinationLocation, conversion.BaseQuantity, mode, request.LotId, request.SerialId, request.OperationalDate, request.PostingDate });
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await LockIdempotencyAsync(context, tenant.Value, entity, "inventory", request.IdempotencyKey, token);
            await using (var existing = new NpgsqlCommand("select id,request_hash,status,requested_quantity,shipped_quantity,received_quantity from business_inventory.inventory_transfer where tenant_id=$1 and legal_entity_id=$2 and source_module='inventory' and idempotency_key=$3", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, entity, request.IdempotencyKey); await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (!string.Equals(reader.GetString(1).Trim(), requestHash, StringComparison.Ordinal)) throw new InventoryException("inventory.idempotency_conflict");
                    return new TransferResult(reader.GetGuid(0), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), true);
                }
            }
            await using (var warehouseCheck = new NpgsqlCommand("select count(*) from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=any($3) and status='Active'", context.Connection, context.Transaction))
            {
                Add(warehouseCheck, tenant.Value, entity, new[] { request.SourceWarehouseId, request.DestinationWarehouseId });
                if ((long)(await warehouseCheck.ExecuteScalarAsync(token) ?? 0L) != 2) throw new InventoryException("inventory.warehouse_not_found_or_inactive");
            }
            var source = new StockPosition(tenant.Value, entity, request.ProductVariantId, request.SourceWarehouseId, sourceLocation, StockStatus.Available, request.LotId, request.SerialId);
            var destination = new StockPosition(tenant.Value, entity, request.ProductVariantId, request.DestinationWarehouseId, destinationLocation, StockStatus.Available, request.LotId, request.SerialId);
            InventoryInvariants.CreateTransfer(source, destination, conversion.BaseQuantity, mode == TransferMode.InTransit);
            var id = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_inventory.inventory_transfer(tenant_id,id,legal_entity_id,product_variant_id,source_warehouse_id,source_location_id,destination_warehouse_id,destination_location_id,lot_id,serial_id,transfer_mode,status,requested_quantity,entered_quantity,entered_uom_id,conversion_factor_snapshot,source_module,source_document_id,idempotency_key,request_hash,actor_id,operational_date,posting_date,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Draft',$12,$13,$14,$15,'inventory',$16,$17,$18,$19,$20,$21,$22)", context.Connection, context.Transaction);
            Add(insert, tenant.Value, id, entity, request.ProductVariantId, request.SourceWarehouseId, sourceLocation, request.DestinationWarehouseId, destinationLocation, request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, mode.ToString(), conversion.BaseQuantity, request.EnteredQuantity, request.EnteredUomId, conversion.FactorToBase, request.SourceDocumentId ?? (object)DBNull.Value, request.IdempotencyKey, requestHash, actor, request.OperationalDate, request.PostingDate, now); await insert.ExecuteNonQueryAsync(token);
            await InsertTransferEventAsync(context, tenant.Value, entity, id, "Created", conversion.BaseQuantity, actor, now, token);
            await AuditAsync(context, tenant.Value, entity, actor, "transfer.created", "InventoryTransfer", id, correlationId, now, token);
            return new TransferResult(id, "Draft", conversion.BaseQuantity, 0m, 0m, false);
        }, ct);
    }

    public async Task<TransferResult> ShipTransferAsync(TenantId tenant, Guid actor, Guid entity, Guid transferId, Guid idempotencyCorrelation, string? traceParent, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            TransferRecord transfer;
            await using (var command = new NpgsqlCommand("select product_variant_id,source_warehouse_id,source_location_id,destination_warehouse_id,destination_location_id,lot_id,serial_id,transfer_mode,status,requested_quantity,shipped_quantity,received_quantity,operational_date,posting_date,idempotency_key,entered_quantity,entered_uom_id,conversion_factor_snapshot from business_inventory.inventory_transfer where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, entity, transferId); await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.transfer_not_found");
                transfer = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), NullGuid(reader, 5), NullGuid(reader, 6), Enum.Parse<TransferMode>(reader.GetString(7)), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), DateOnly.FromDateTime(reader.GetDateTime(12)), DateOnly.FromDateTime(reader.GetDateTime(13)), reader.GetString(14), reader.GetDecimal(15), reader.GetGuid(16), reader.GetDecimal(17));
            }
            if (transfer.Status != "Draft") return new TransferResult(transferId, transfer.Status, transfer.RequestedQuantity, transfer.ShippedQuantity, transfer.ReceivedQuantity, true);
            var source = new StockPosition(tenant.Value, entity, transfer.ProductVariantId, transfer.SourceWarehouseId, transfer.SourceLocationId, StockStatus.Available, transfer.LotId, transfer.SerialId);
            var destination = new StockPosition(tenant.Value, entity, transfer.ProductVariantId, transfer.DestinationWarehouseId, transfer.DestinationLocationId, StockStatus.Available, transfer.LotId, transfer.SerialId);
            var legs = InventoryInvariants.CreateTransfer(source, destination, transfer.RequestedQuantity, transfer.Mode == TransferMode.InTransit);
            var sourceBalance = await ReadBalanceForUpdateAsync(context, source, token);
            if (transfer.RequestedQuantity > sourceBalance.OnHand - sourceBalance.Reserved) throw new InventoryException("inventory.insufficient_stock");
            var operationId = Guid.CreateVersion7(); var operationNumber = OperationNumber(now, operationId);
            await EnsurePostingDateOpenAsync(context, tenant, entity, transfer.PostingDate, token);
            await InsertConfirmedOperationAsync(context, tenant.Value, operationId, entity, operationNumber, MovementKind.Transfer, "inventory", $"transfer:{transferId:D}:ship", Hash(new { transferId, transfer.RequestedQuantity, transfer.Mode }), transfer.OperationalDate, transfer.PostingDate, actor, idempotencyCorrelation, now, token);
            var sourceDelta = -transfer.RequestedQuantity;
            var transitDelta = transfer.Mode == TransferMode.InTransit ? transfer.RequestedQuantity : 0m;
            var destinationDelta = transfer.Mode == TransferMode.Instant ? transfer.RequestedQuantity : 0m;
            var movementIds = new List<Guid>();
            movementIds.Add(await InsertMovementAsync(context, tenant.Value, operationId, entity, transfer.ProductVariantId, source, transfer.EnteredUomId, transfer.EnteredQuantity, transfer.ConversionFactor, sourceDelta, "Transfer", transfer.OperationalDate, transfer.PostingDate, actor, now, token));
            if (transfer.Mode == TransferMode.InTransit)
            {
                var transit = source with { Status = StockStatus.InTransit };
                movementIds.Add(await InsertMovementAsync(context, tenant.Value, operationId, entity, transfer.ProductVariantId, transit, transfer.EnteredUomId, transfer.EnteredQuantity, transfer.ConversionFactor, transitDelta, "Transfer", transfer.OperationalDate, transfer.PostingDate, actor, now, token));
            }
            else movementIds.Add(await InsertMovementAsync(context, tenant.Value, operationId, entity, transfer.ProductVariantId, destination, transfer.EnteredUomId, transfer.EnteredQuantity, transfer.ConversionFactor, destinationDelta, "Transfer", transfer.OperationalDate, transfer.PostingDate, actor, now, token));
            await ApplyBalanceDeltaAsync(context, source, sourceDelta, 0m, movementIds[0], token);
            if (transfer.Mode == TransferMode.InTransit) await ApplyBalanceDeltaAsync(context, source with { Status = StockStatus.InTransit }, transitDelta, 0m, movementIds[1], token);
            else await ApplyBalanceDeltaAsync(context, destination, destinationDelta, 0m, movementIds[1], token);
            if (transfer.SerialId is { } serialId)
                await MoveSerialAsync(context, tenant.Value, entity, transfer.ProductVariantId, serialId,
                    transfer.Mode == TransferMode.InTransit ? "InTransit" : "OnHand",
                    transfer.Mode == TransferMode.InTransit ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId,
                    transfer.Mode == TransferMode.InTransit ? transfer.SourceLocationId : transfer.DestinationLocationId, token);
            var status = transfer.Mode == TransferMode.Instant ? "Received" : "Shipped";
            await using (var update = new NpgsqlCommand("update business_inventory.inventory_transfer set status=$4,shipped_quantity=$5,received_quantity=$6,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, transferId, status, transfer.RequestedQuantity, transfer.Mode == TransferMode.Instant ? transfer.RequestedQuantity : 0m); await update.ExecuteNonQueryAsync(token); }
            await InsertTransferEventAsync(context, tenant.Value, entity, transferId, "Shipped", transfer.RequestedQuantity, actor, now, token);
            if (status == "Received") await InsertTransferEventAsync(context, tenant.Value, entity, transferId, "Received", transfer.RequestedQuantity, actor, now, token);
            await AuditAsync(context, tenant.Value, entity, actor, "transfer.shipped", "InventoryTransfer", transferId, idempotencyCorrelation, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "StockTransferredV1", JsonSerializer.Serialize(new { transferId, operationId, status, quantity = transfer.RequestedQuantity }), tenant, now, idempotencyCorrelation, operationId, "inventory", traceParent));
            return new TransferResult(transferId, status, transfer.RequestedQuantity, transfer.RequestedQuantity, transfer.Mode == TransferMode.Instant ? transfer.RequestedQuantity : 0m, false);
        }, ct);
    }

    public async Task<TransferResult> ReceiveTransferAsync(TenantId tenant, Guid actor, Guid entity, Guid transferId, ReceiveTransferRequest request, Guid correlationId, string? traceParent, CancellationToken ct)
    {
        if (request.Quantity <= 0m || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200 || request.PostingDate < request.OperationalDate)
            throw new InventoryException("inventory.transfer_receipt_invalid");
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await LockIdempotencyAsync(context, tenant.Value, entity, $"transfer:{transferId:D}", request.IdempotencyKey, token);
            TransferRecord transfer;
            await using (var command = new NpgsqlCommand("select product_variant_id,source_warehouse_id,source_location_id,destination_warehouse_id,destination_location_id,lot_id,serial_id,transfer_mode,status,requested_quantity,shipped_quantity,received_quantity,operational_date,posting_date,idempotency_key,entered_quantity,entered_uom_id,conversion_factor_snapshot from business_inventory.inventory_transfer where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, entity, transferId); await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.transfer_not_found");
                transfer = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), NullGuid(reader, 5), NullGuid(reader, 6), Enum.Parse<TransferMode>(reader.GetString(7)), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), DateOnly.FromDateTime(reader.GetDateTime(12)), DateOnly.FromDateTime(reader.GetDateTime(13)), reader.GetString(14), reader.GetDecimal(15), reader.GetGuid(16), reader.GetDecimal(17));
            }
            if (transfer.Mode != TransferMode.InTransit || transfer.Status is not ("Shipped" or "PartiallyReceived")) throw new InventoryException("inventory.transfer_not_receivable");
            var remaining = transfer.ShippedQuantity - transfer.ReceivedQuantity;
            if (request.Quantity > remaining) throw new InventoryException("inventory.transfer_receipt_exceeds_transit");
            await using (var existing = new NpgsqlCommand("select id,received_quantity from business_inventory.transfer_receipt where tenant_id=$1 and legal_entity_id=$2 and transfer_id=$3 and idempotency_key=$4", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, entity, transferId, request.IdempotencyKey); await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (reader.GetDecimal(1) != request.Quantity) throw new InventoryException("inventory.idempotency_conflict");
                    return new TransferResult(transferId, transfer.Status, transfer.RequestedQuantity, transfer.ShippedQuantity, transfer.ReceivedQuantity, true);
                }
            }
            var transit = new StockPosition(tenant.Value, entity, transfer.ProductVariantId, transfer.SourceWarehouseId, transfer.SourceLocationId, StockStatus.InTransit, transfer.LotId, transfer.SerialId);
            var destination = new StockPosition(tenant.Value, entity, transfer.ProductVariantId, transfer.DestinationWarehouseId, transfer.DestinationLocationId, StockStatus.Available, transfer.LotId, transfer.SerialId);
            var transitBalance = await ReadBalanceForUpdateAsync(context, transit, token);
            if (request.Quantity > transitBalance.OnHand) throw new InventoryException("inventory.transfer_transit_balance_invalid");
            var operationId = Guid.CreateVersion7(); var operationNumber = OperationNumber(now, operationId);
            await EnsurePostingDateOpenAsync(context, tenant, entity, request.PostingDate, token);
            var operationKey = $"transfer:{transferId:D}:receive:{request.IdempotencyKey}";
            await InsertConfirmedOperationAsync(context, tenant.Value, operationId, entity, operationNumber, MovementKind.Transfer, "inventory", operationKey, Hash(new { transferId, request.Quantity, request.IdempotencyKey }), request.OperationalDate, request.PostingDate, actor, correlationId, now, token);
            var uom = await catalog.GetBaseUomsBatchAsync(tenant, [transfer.ProductVariantId], new DateTimeOffset(request.OperationalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), entity, token);
            if (!uom.TryGetValue(transfer.ProductVariantId, out var baseUom)) throw new InventoryException("inventory.base_uom_not_found");
            var transitMovement = await InsertMovementAsync(context, tenant.Value, operationId, entity, transfer.ProductVariantId, transit, baseUom, request.Quantity, 1m, -request.Quantity, "Transfer", request.OperationalDate, request.PostingDate, actor, now, token);
            var destinationMovement = await InsertMovementAsync(context, tenant.Value, operationId, entity, transfer.ProductVariantId, destination, baseUom, request.Quantity, 1m, request.Quantity, "Transfer", request.OperationalDate, request.PostingDate, actor, now, token);
            await ApplyBalanceDeltaAsync(context, transit, -request.Quantity, 0m, transitMovement, token);
            await ApplyBalanceDeltaAsync(context, destination, request.Quantity, 0m, destinationMovement, token);
            if (transfer.SerialId is { } serialId)
                await MoveSerialAsync(context, tenant.Value, entity, transfer.ProductVariantId, serialId, "OnHand", transfer.DestinationWarehouseId, transfer.DestinationLocationId, token);
            var received = transfer.ReceivedQuantity + request.Quantity;
            var status = received == transfer.ShippedQuantity ? "Received" : "PartiallyReceived";
            await using (var update = new NpgsqlCommand("update business_inventory.inventory_transfer set received_quantity=$4,status=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, transferId, received, status); await update.ExecuteNonQueryAsync(token); }
            await using (var receipt = new NpgsqlCommand("insert into business_inventory.transfer_receipt(tenant_id,id,legal_entity_id,transfer_id,operation_id,received_quantity,idempotency_key,actor_id,received_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", context.Connection, context.Transaction))
            { Add(receipt, tenant.Value, Guid.CreateVersion7(), entity, transferId, operationId, request.Quantity, request.IdempotencyKey, actor, now); await receipt.ExecuteNonQueryAsync(token); }
            await InsertTransferEventAsync(context, tenant.Value, entity, transferId, status, request.Quantity, actor, now, token);
            await AuditAsync(context, tenant.Value, entity, actor, "transfer.received", "InventoryTransfer", transferId, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "StockTransferredV1", JsonSerializer.Serialize(new { transferId, operationId, status, quantity = request.Quantity }), tenant, now, correlationId, operationId, "inventory", traceParent));
            return new TransferResult(transferId, status, transfer.RequestedQuantity, transfer.ShippedQuantity, received, false);
        }, ct);
    }

    public async Task<ReservationResult> ReserveAsync(TenantId tenant, Guid actor, Guid entity, ReserveStockRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.Quantity <= 0m || request.SourceDocumentId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
            throw new InventoryException("inventory.reservation_request_invalid");
        await ExpireReservationsAsync(tenant, entity, 500, correlationId, ct);
        var profile = await GetProfileAsync(tenant, entity, request.ProductVariantId, ct) ?? throw new InventoryException("inventory.profile_not_found");
        if (profile.State != InventoryState.StockTracked) throw new InventoryException("inventory.product_not_stock_tracked");
        if (profile.Tracking == TrackingMode.Lot && request.LotId is null || profile.Tracking == TrackingMode.Serial && request.SerialId is null
            || profile.Tracking == TrackingMode.Quantity && (request.LotId is not null || request.SerialId is not null)
            || profile.Tracking == TrackingMode.Lot && request.SerialId is not null || profile.Tracking == TrackingMode.Serial && request.LotId is not null)
            throw new InventoryException("inventory.reservation_traceability_invalid");
        if (request.SerialId is not null && request.Quantity != 1m) throw new InventoryException("inventory.serial_quantity_must_be_one");
        var locationId = await ResolveLocationAsync(tenant, entity, request.WarehouseId, request.LocationId,
            clock.GetUtcNow(), canPick: true, ct);
        var now = clock.GetUtcNow();
        var sourceModule = string.IsNullOrWhiteSpace(request.SourceModule) ? "inventory" : request.SourceModule.Trim().ToLowerInvariant();
        var reservationHash = Hash(new { request.ProductVariantId, request.WarehouseId, locationId, request.Quantity, request.SourceDocumentId, request.SourceLineId, request.ExpiresAt, request.LotId, request.SerialId });
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await LockIdempotencyAsync(context, tenant.Value, entity, $"reservation:{sourceModule}:{request.SourceDocumentId:D}:{request.SourceLineId:D}", request.IdempotencyKey, token);
            await using (var existing = new NpgsqlCommand("select id,requested_quantity,reserved_quantity,status,idempotency_key,request_hash from business_inventory.reservation where tenant_id=$1 and legal_entity_id=$2 and source_module=$3 and source_document_id=$4 and source_line_id is not distinct from $5::uuid for update", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, entity, sourceModule, request.SourceDocumentId, request.SourceLineId ?? (object)DBNull.Value); await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (reader.GetDecimal(1) != request.Quantity || reader.GetString(3) != "Active"
                        || reader.GetString(4) != request.IdempotencyKey || !string.Equals(reader.GetString(5).Trim(), reservationHash, StringComparison.Ordinal))
                        throw new InventoryException("inventory.idempotency_conflict");
                    return new ReservationResult(reader.GetGuid(0), request.ProductVariantId, request.WarehouseId, locationId, reader.GetDecimal(1), reader.GetDecimal(2), reader.GetString(3), true);
                }
            }
            if (profile.ReservationMode == ReservationMode.Disabled) throw new InventoryException("inventory.reservations_disabled");
            if (request.LotId is { } lotId)
            {
                await using var lot = new NpgsqlCommand("select status,expires_on from business_inventory.lot where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4 for share", context.Connection, context.Transaction);
                Add(lot, tenant.Value, entity, request.ProductVariantId, lotId); await using var reader = await lot.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token) || reader.GetString(0) != "Active" || !reader.IsDBNull(1) && DateOnly.FromDateTime(reader.GetDateTime(1)) < DateOnly.FromDateTime(now.UtcDateTime))
                    throw new InventoryException("inventory.lot_unavailable_or_expired");
            }
            if (request.SerialId is { } serialId)
            {
                await using var serial = new NpgsqlCommand("select status,current_warehouse_id,current_location_id from business_inventory.serial where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4 for update", context.Connection, context.Transaction);
                Add(serial, tenant.Value, entity, request.ProductVariantId, serialId); await using var reader = await serial.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token) || reader.GetString(0) != "OnHand" || reader.IsDBNull(1) || reader.IsDBNull(2)
                    || reader.GetGuid(1) != request.WarehouseId || reader.GetGuid(2) != locationId)
                    throw new InventoryException("inventory.serial_not_at_location");
            }
            var position = new StockPosition(tenant.Value, entity, request.ProductVariantId, request.WarehouseId, locationId, StockStatus.Available, request.LotId, request.SerialId);
            var balance = await ReadBalanceForUpdateAsync(context, position, token);
            InventoryInvariants.EnsureReservationAllowed(profile.ReservationMode, balance.OnHand, balance.Reserved, request.Quantity);
            var id = Guid.CreateVersion7();
            await using (var insert = new NpgsqlCommand("insert into business_inventory.reservation(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,lot_id,serial_id,source_module,source_document_id,source_line_id,requested_quantity,reserved_quantity,idempotency_key,request_hash,status,expires_at,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$12,$13,$14,'Active',$15,$16,$17)", context.Connection, context.Transaction))
            { Add(insert, tenant.Value, id, entity, request.ProductVariantId, request.WarehouseId, locationId, request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, sourceModule, request.SourceDocumentId, request.SourceLineId ?? (object)DBNull.Value, request.Quantity, request.IdempotencyKey, reservationHash, request.ExpiresAt ?? (object)DBNull.Value, actor, now); await insert.ExecuteNonQueryAsync(token); }
            await using (var allocation = new NpgsqlCommand("insert into business_inventory.reservation_allocation(tenant_id,id,legal_entity_id,reservation_id,warehouse_id,location_id,stock_status,lot_id,serial_id,quantity) values($1,$2,$3,$4,$5,$6,'Available',$7,$8,$9)", context.Connection, context.Transaction))
            { Add(allocation, tenant.Value, Guid.CreateVersion7(), entity, id, request.WarehouseId, locationId, request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, request.Quantity); await allocation.ExecuteNonQueryAsync(token); }
            if (request.SerialId is { } reservedSerialId)
                await MoveSerialAsync(context, tenant.Value, entity, request.ProductVariantId, reservedSerialId, "Reserved", request.WarehouseId, locationId, token);
            await ApplyBalanceDeltaAsync(context, position, 0m, request.Quantity, Guid.CreateVersion7(), token);
            await InsertReservationEventAsync(context, tenant.Value, entity, id, "Created", request.Quantity, actor, now, token);
            await AuditAsync(context, tenant.Value, entity, actor, "reservation.created", "Reservation", id, correlationId, now, token);
            context.AddOutbox(new(Guid.CreateVersion7(), "ReservationCreatedV1", JsonSerializer.Serialize(new { reservationId = id, request.ProductVariantId, request.WarehouseId, quantity = request.Quantity, sourceModule }), tenant, now, correlationId, id, "inventory"));
            return new ReservationResult(id, request.ProductVariantId, request.WarehouseId, locationId, request.Quantity, request.Quantity, "Active", false);
        }, ct);
    }

    public Task ReleaseReservationAsync(TenantId tenant, Guid actor, Guid entity, Guid reservationId, Guid correlationId, CancellationToken ct) =>
        CloseReservationAsync(tenant, actor, entity, reservationId, correlationId, "Released", ct);

    public Task CancelReservationAsync(TenantId tenant, Guid actor, Guid entity, Guid reservationId, Guid correlationId, CancellationToken ct) =>
        CloseReservationAsync(tenant, actor, entity, reservationId, correlationId, "Cancelled", ct);

    private async Task CloseReservationAsync(TenantId tenant, Guid actor, Guid entity, Guid reservationId, Guid correlationId, string finalStatus, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            ReservationRecord reservation;
            await using (var command = new NpgsqlCommand("select product_variant_id,warehouse_id,location_id,reserved_quantity,status,lot_id,serial_id from business_inventory.reservation where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, entity, reservationId); await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.reservation_not_found");
                reservation = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4), NullGuid(reader, 5), NullGuid(reader, 6));
            }
            if (reservation.Status == finalStatus) return true;
            if (reservation.Status != "Active") throw new InventoryException("inventory.reservation_not_active");
            var position = new StockPosition(tenant.Value, entity, reservation.ProductVariantId, reservation.WarehouseId, reservation.LocationId, StockStatus.Available, reservation.LotId, reservation.SerialId);
            var balance = await ReadBalanceForUpdateAsync(context, position, token);
            if (reservation.Quantity > balance.Reserved) throw new InventoryException("inventory.reservation_projection_invalid");
            await using (var update = new NpgsqlCommand("update business_inventory.reservation set reserved_quantity=0,status=$4,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, reservationId, finalStatus); await update.ExecuteNonQueryAsync(token); }
            if (reservation.SerialId is { } releasedSerialId)
                await MoveSerialAsync(context, tenant.Value, entity, reservation.ProductVariantId, releasedSerialId, "OnHand", reservation.WarehouseId, reservation.LocationId, token);
            await ApplyBalanceDeltaAsync(context, position, 0m, -reservation.Quantity, Guid.CreateVersion7(), token);
            await InsertReservationEventAsync(context, tenant.Value, entity, reservationId, finalStatus, reservation.Quantity, actor, now, token);
            await AuditAsync(context, tenant.Value, entity, actor, $"reservation.{finalStatus.ToLowerInvariant()}", "Reservation", reservationId, correlationId, now, token);
            var eventType = finalStatus == "Cancelled" ? "ReservationCancelledV1" : "ReservationReleasedV1";
            context.AddOutbox(new(Guid.CreateVersion7(), eventType, JsonSerializer.Serialize(new { reservationId, quantity = reservation.Quantity }), tenant, now, correlationId, reservationId, "inventory"));
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<(Guid TenantId, Guid LegalEntityId)>> GetDueReservationScopesAsync(int limit, DateTimeOffset? asOf, CancellationToken ct)
    {
        if (limit is < 1 or > 500) throw new InventoryException("inventory.page_size_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select tenant_id,legal_entity_id from business_inventory.due_reservation_scopes($1,$2)", connection);
        Add(command, limit, asOf ?? clock.GetUtcNow());
        await using var reader = await command.ExecuteReaderAsync(ct);
        var scopes = new List<(Guid TenantId, Guid LegalEntityId)>();
        while (await reader.ReadAsync(ct)) scopes.Add((reader.GetGuid(0), reader.GetGuid(1)));
        return scopes;
    }

    public async Task<int> ExpireReservationsAsync(TenantId tenant, Guid entity, int limit, Guid correlationId, CancellationToken ct, DateTimeOffset? asOf = null)
    {
        if (limit is < 1 or > 500) throw new InventoryException("inventory.page_size_invalid");
        var now = asOf ?? clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var expired = new List<(Guid Id, ReservationRecord Record)>();
            await using (var query = new NpgsqlCommand("select id,product_variant_id,warehouse_id,location_id,reserved_quantity,status,lot_id,serial_id from business_inventory.reservation where tenant_id=$1 and legal_entity_id=$2 and status='Active' and expires_at<=$3 order by expires_at,id for update skip locked limit $4", context.Connection, context.Transaction))
            {
                Add(query, tenant.Value, entity, now, limit); await using var reader = await query.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) expired.Add((reader.GetGuid(0), new(reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetDecimal(4), reader.GetString(5), NullGuid(reader, 6), NullGuid(reader, 7))));
            }
            foreach (var (id, reservation) in expired)
            {
                var position = new StockPosition(tenant.Value, entity, reservation.ProductVariantId, reservation.WarehouseId,
                    reservation.LocationId, StockStatus.Available, reservation.LotId, reservation.SerialId);
                var balance = await ReadBalanceForUpdateAsync(context, position, token);
                if (reservation.Quantity > balance.Reserved) throw new InventoryException("inventory.reservation_projection_invalid");
                await using (var update = new NpgsqlCommand("update business_inventory.reservation set reserved_quantity=0,status='Expired',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", context.Connection, context.Transaction))
                { Add(update, tenant.Value, entity, id); await update.ExecuteNonQueryAsync(token); }
                await using (var delete = new NpgsqlCommand("delete from business_inventory.reservation_allocation where tenant_id=$1 and legal_entity_id=$2 and reservation_id=$3", context.Connection, context.Transaction))
                { Add(delete, tenant.Value, entity, id); await delete.ExecuteNonQueryAsync(token); }
                if (reservation.SerialId is { } serialId)
                    await MoveSerialAsync(context, tenant.Value, entity, reservation.ProductVariantId, serialId, "OnHand", reservation.WarehouseId, reservation.LocationId, token);
                await ApplyBalanceDeltaAsync(context, position, 0m, -reservation.Quantity, Guid.CreateVersion7(), token);
                await InsertReservationEventAsync(context, tenant.Value, entity, id, "Expired", reservation.Quantity, Guid.Empty, now, token);
                await AuditAsync(context, tenant.Value, entity, Guid.Empty, "reservation.expired", "Reservation", id, correlationId, now, token);
                context.AddOutbox(new(Guid.CreateVersion7(), "ReservationExpiredV1", JsonSerializer.Serialize(new { reservationId = id, quantity = reservation.Quantity }), tenant, now, correlationId, id, "inventory"));
            }
            return expired.Count;
        }, ct);
    }

    private async Task<InventoryOperationResult> ConfirmMovementAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request,
        MovementKind kind, Guid correlationId, string? traceParent, CancellationToken ct, bool canOverrideNegativeStock = false,
        Guid? reservationId = null, int? adjustmentSign = null, string? reasonCode = null, Guid? adjustmentRequestId = null)
    {
        var sourceModule = string.IsNullOrWhiteSpace(request.SourceModule) ? "inventory" : request.SourceModule.Trim().ToLowerInvariant();
        if (request.IdempotencyKey.Length is < 1 or > 200) throw new InventoryException("inventory.idempotency_key_invalid");
        var now = clock.GetUtcNow();
        var hashInput = reservationId is null && adjustmentSign is null
            ? JsonSerializer.Serialize(request with { SourceModule = sourceModule })
            : JsonSerializer.Serialize(new { Request = request with { SourceModule = sourceModule }, reservationId, adjustmentSign, reasonCode });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();

        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var idempotencyLock = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1,0))", context.Connection, context.Transaction))
            {
                idempotencyLock.Parameters.AddWithValue($"inventory:{tenant.Value:D}:{entity:D}:{sourceModule}:{request.IdempotencyKey}");
                await idempotencyLock.ExecuteNonQueryAsync(token);
            }
            if (adjustmentRequestId is { } pendingRequestId)
            {
                await using var approval = new NpgsqlCommand("select status,approval_mode,created_by,posted_operation_id from business_inventory.inventory_adjustment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
                Add(approval, tenant.Value, entity, pendingRequestId); await using var approvalReader = await approval.ExecuteReaderAsync(token);
                if (!await approvalReader.ReadAsync(token)) throw new InventoryException("inventory.adjustment_request_not_found");
                var approvalStatus = Enum.Parse<AdjustmentRequestStatus>(approvalReader.GetString(0));
                var approvalMode = Enum.Parse<AdjustmentApprovalMode>(approvalReader.GetString(1));
                var creator = approvalReader.GetGuid(2);
                var postedId = NullGuid(approvalReader, 3);
                await approvalReader.DisposeAsync();
                if (approvalStatus == AdjustmentRequestStatus.Posted && postedId is { } alreadyPosted)
                {
                    await using var replay = new NpgsqlCommand("select operation_number from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
                    Add(replay, tenant.Value, entity, alreadyPosted);
                    return new InventoryOperationResult(alreadyPosted, (string?)await replay.ExecuteScalarAsync(token) ?? string.Empty, "Confirmed", [], true);
                }
                if (approvalStatus == AdjustmentRequestStatus.PendingApproval) throw new InventoryException("inventory.adjustment_not_approved");
                if (approvalStatus != AdjustmentRequestStatus.Approved && !(approvalMode == AdjustmentApprovalMode.DirectConfirm && approvalStatus == AdjustmentRequestStatus.Draft))
                    throw new InventoryException("inventory.adjustment_request_not_postable");
                if (approvalMode == AdjustmentApprovalMode.ApprovalRequired && await SeparationOfDutiesAsync(context, tenant.Value, entity, token) && creator == actor)
                    throw new InventoryException("inventory.adjustment_creator_cannot_post");
            }
            InventoryProfile? storedProfile = null;
            await using (var command = new NpgsqlCommand("select inventory_state,tracking_mode,valuation_mode,costing_method,negative_stock_policy,default_warehouse_id,reservation_mode,version from business_inventory.product_inventory_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and status='Active' for share", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, entity, request.ProductVariantId); await using var reader = await command.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token)) storedProfile = new InventoryProfile(tenant.Value, entity, request.ProductVariantId, Enum.Parse<InventoryState>(reader.GetString(0)), Enum.Parse<TrackingMode>(reader.GetString(1)), Enum.Parse<ValuationMode>(reader.GetString(2)), reader.IsDBNull(3) ? null : Enum.Parse<CostingMethod>(reader.GetString(3)), Enum.Parse<NegativeStockPolicy>(reader.GetString(4)), NullGuid(reader, 5), Enum.Parse<ReservationMode>(reader.GetString(6)), reader.GetInt64(7));
            }
            if (storedProfile is null) throw new InventoryException("inventory.profile_not_found");
            if (storedProfile.Valuation == ValuationMode.Perpetual && ledgerPosting is null)
                throw new InventoryException("inventory.ledger_posting_capability_required");
            if (storedProfile.Valuation == ValuationMode.Perpetual && request.OperationalDate < request.PostingDate)
                throw new InventoryException("inventory.valued_backdating_requires_revaluation");
            await EnsureOperationalCutoffOpenAsync(context, tenant, entity, request.OperationalDate, token);
            await EnsurePostingDateOpenAsync(context, tenant, entity, request.OperationalDate, token);
            if (request.Status == StockStatus.InTransit)
                throw new InventoryException("inventory.transit_requires_transfer");
            if (kind == MovementKind.Issue && request.Status != StockStatus.Available)
                throw new InventoryException("inventory.issue_requires_available_status");
            var variant = await catalog.FindVariantAsync(tenant, request.ProductVariantId, entity, token)
                ?? throw new InventoryException("inventory.catalog_variant_not_found");
            if (variant.Status != "Active" || variant.Nature == "Service") throw new InventoryException("inventory.product_not_stock_tracked");
            var effectiveAt = new DateTimeOffset(request.OperationalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var conversion = await quantityConversion.ConvertAsync(tenant, request.ProductVariantId, request.EnteredUomId,
                request.EnteredQuantity, effectiveAt, entity, token)
                ?? throw new InventoryException("inventory.uom_conversion_not_available");
            if (conversion.BaseQuantity <= 0m || conversion.FactorToBase <= 0m)
                throw new InventoryException("inventory.quantity_invalid");
            var location = request.LocationId;
            if (location is null)
            {
                await using var command = new NpgsqlCommand("select default_location_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", context.Connection, context.Transaction);
                Add(command, tenant.Value, entity, request.WarehouseId);
                location = await command.ExecuteScalarAsync(token) as Guid?;
            }
            var effectiveLocation = location ?? throw new InventoryException("inventory.location_not_found");
            var draft = new MovementDraft(Guid.CreateVersion7(), tenant.Value, entity, request.ProductVariantId, request.WarehouseId,
                effectiveLocation, request.EnteredQuantity, request.EnteredUomId, conversion.FactorToBase,
                conversion.BaseQuantity, sourceModule, request.SourceDocumentId, request.IdempotencyKey,
                request.OperationalDate, request.PostingDate, kind, request.Status, request.LotId, request.SerialId, request.UnitCost, request.CurrencyCode);
            draft.Validate(storedProfile);
            var signedAdjustmentQuantity = kind == MovementKind.Adjustment
                ? checked(conversion.BaseQuantity * (adjustmentSign ?? throw new InventoryException("inventory.adjustment_request_invalid")))
                : (decimal?)null;

            await using (var existing = new NpgsqlCommand("select id,idempotency_hash,operation_number from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and source_module=$3 and idempotency_key=$4", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, entity, sourceModule, request.IdempotencyKey); await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (!string.Equals(reader.GetString(1).Trim(), hash, StringComparison.Ordinal)) throw new InventoryException("inventory.idempotency_conflict");
                    return new InventoryOperationResult(reader.GetGuid(0), reader.GetString(2), "Confirmed", [], true);
                }
            }

            await using (var locationCheck = new NpgsqlCommand("select exists(select 1 from business_inventory.location l join business_inventory.warehouse w on w.tenant_id=l.tenant_id and w.legal_entity_id=l.legal_entity_id and w.id=l.warehouse_id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.warehouse_id=$3 and l.id=$4 and l.status='Active' and l.can_hold_stock and ($5 <> 'Issue' or l.can_pick_from) and ($5 = 'Issue' or l.can_receive_into) and w.status='Active')", context.Connection, context.Transaction))
            {
                Add(locationCheck, tenant.Value, entity, request.WarehouseId, effectiveLocation, kind.ToString());
                if (!(bool)(await locationCheck.ExecuteScalarAsync(token) ?? false)) throw new InventoryException("inventory.location_not_usable");
            }

            var position = new StockPosition(tenant.Value, entity, request.ProductVariantId, request.WarehouseId, effectiveLocation, request.Status, request.LotId, request.SerialId);
            await using var lockBalance = new NpgsqlCommand("select on_hand_base_quantity,reserved_base_quantity from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and lot_id is not distinct from $7::uuid and serial_id is not distinct from $8::uuid for update", context.Connection, context.Transaction);
            Add(lockBalance, tenant.Value, entity, request.ProductVariantId, request.WarehouseId, effectiveLocation, request.Status.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value);
            decimal onHand = 0m; decimal reserved = 0m;
            await using (var reader = await lockBalance.ExecuteReaderAsync(token)) if (await reader.ReadAsync(token)) { onHand = reader.GetDecimal(0); reserved = reader.GetDecimal(1); }
            ReservationRecord? consumedReservation = null;
            if (reservationId is { } id)
            {
                if (kind != MovementKind.Issue) throw new InventoryException("inventory.reservation_consume_requires_issue");
                await using var reservation = new NpgsqlCommand("select product_variant_id,warehouse_id,location_id,reserved_quantity,status,lot_id,serial_id,expires_at from business_inventory.reservation where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
                Add(reservation, tenant.Value, entity, id); await using var reader = await reservation.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InventoryException("inventory.reservation_not_found");
                consumedReservation = new ReservationRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4), NullGuid(reader, 5), NullGuid(reader, 6));
                var expiresAt = reader.IsDBNull(7) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(7);
                if (consumedReservation.Status != "Active" || expiresAt is { } expiry && expiry <= now
                    || consumedReservation.ProductVariantId != request.ProductVariantId || consumedReservation.WarehouseId != request.WarehouseId
                    || consumedReservation.LocationId != effectiveLocation || consumedReservation.LotId != request.LotId
                    || consumedReservation.SerialId != request.SerialId || consumedReservation.Quantity < draft.BaseQuantity)
                    throw new InventoryException("inventory.reservation_not_consumable");
            }
            var negativeStockConditionTriggered = false;
            var consumableReserved = consumedReservation is null ? 0m : draft.BaseQuantity;
            var decreasesStock = kind == MovementKind.Issue || kind == MovementKind.Adjustment && signedAdjustmentQuantity < 0m;
            if (decreasesStock && draft.BaseQuantity > onHand - reserved + consumableReserved)
            {
                if (reserved > 0m || storedProfile.NegativeStock == NegativeStockPolicy.Disallow
                    || storedProfile.NegativeStock == NegativeStockPolicy.AllowWithWarningAndPermission && !canOverrideNegativeStock
                    || storedProfile.Valuation == ValuationMode.Perpetual)
                    throw new InventoryException("inventory.insufficient_stock");
                negativeStockConditionTriggered = true;
            }
            var traceabilityKind = kind == MovementKind.Adjustment
                ? signedAdjustmentQuantity > 0m ? MovementKind.Receipt : MovementKind.Issue
                : kind;
            await ValidateTraceabilityAsync(context, draft, traceabilityKind, token, reservationId is not null);

            var operationId = Guid.CreateVersion7();
            var operationNumber = OperationNumber(now, operationId);
            await EnsurePostingDateOpenAsync(context, tenant, entity, request.PostingDate, token);
            await using (var command = new NpgsqlCommand("insert into business_inventory.inventory_operation(tenant_id,id,legal_entity_id,operation_number,operation_type,status,source_module,source_document_id,idempotency_key,idempotency_hash,operational_date,posting_date,reason_code,actor_id,correlation_id,project_id,task_id,cost_category,created_at,confirmed_at) values($1,$2,$3,$4,$5,'Confirmed',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$18)", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, operationId, entity, operationNumber, kind.ToString(), sourceModule, request.SourceDocumentId ?? (object)DBNull.Value, request.IdempotencyKey, hash, request.OperationalDate, request.PostingDate, reasonCode ?? (object)DBNull.Value, actor, correlationId, request.ProjectId ?? (object)DBNull.Value, request.TaskId ?? (object)DBNull.Value, request.CostCategory ?? (object)DBNull.Value, now); await command.ExecuteNonQueryAsync(token);
            }
            if (adjustmentRequestId is { } postedRequestId)
            {
                await using var updateRequest = new NpgsqlCommand("update business_inventory.inventory_adjustment_request set status='Posted',posted_operation_id=$4,updated_at=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
                Add(updateRequest, tenant.Value, entity, postedRequestId, operationId, now); await updateRequest.ExecuteNonQueryAsync(token);
            }
            long sequence;
            await using (var command = new NpgsqlCommand("insert into business_inventory.movement_sequence(tenant_id,legal_entity_id,last_value) values($1,$2,1) on conflict(tenant_id,legal_entity_id) do update set last_value=business_inventory.movement_sequence.last_value+1 returning last_value", context.Connection, context.Transaction))
            { Add(command, tenant.Value, entity); sequence = (long)(await command.ExecuteScalarAsync(token) ?? 0L); }
            var movementId = draft.Id;
            var signedQuantity = kind == MovementKind.Issue ? -draft.BaseQuantity : kind == MovementKind.Adjustment ? signedAdjustmentQuantity!.Value : draft.BaseQuantity;
            await using (var command = new NpgsqlCommand("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,source_document_id,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$21)", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, movementId, entity, operationId, sequence, request.ProductVariantId, request.WarehouseId, effectiveLocation, request.Status.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, request.EnteredQuantity, request.EnteredUomId, draft.ConversionFactorSnapshot, draft.BaseQuantity, signedQuantity, sourceModule, request.SourceDocumentId ?? (object)DBNull.Value, request.OperationalDate, request.PostingDate, now, kind.ToString(), actor); await command.ExecuteNonQueryAsync(token);
            }
            if (storedProfile.Valuation == ValuationMode.Perpetual)
                await ApplyValuationAsync(context, tenant, actor, entity, operationId, movementId, storedProfile, draft, request.CurrencyCode, kind, correlationId, now, token, signedAdjustmentQuantity);
            if (consumedReservation is not null)
            {
                var remainingReserved = consumedReservation.Quantity - draft.BaseQuantity;
                await using (var update = new NpgsqlCommand("update business_inventory.reservation set reserved_quantity=$4,status=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
                { Add(update, tenant.Value, entity, reservationId!.Value, remainingReserved, remainingReserved == 0m ? "Consumed" : "Active"); await update.ExecuteNonQueryAsync(token); }
                await using (var delete = new NpgsqlCommand("delete from business_inventory.reservation_allocation where tenant_id=$1 and legal_entity_id=$2 and reservation_id=$3", context.Connection, context.Transaction))
                { Add(delete, tenant.Value, entity, reservationId!.Value); await delete.ExecuteNonQueryAsync(token); }
                if (remainingReserved > 0m)
                {
                    await using var allocation = new NpgsqlCommand("insert into business_inventory.reservation_allocation(tenant_id,id,legal_entity_id,reservation_id,warehouse_id,location_id,stock_status,lot_id,serial_id,quantity) values($1,$2,$3,$4,$5,$6,'Available',$7,$8,$9)", context.Connection, context.Transaction);
                    Add(allocation, tenant.Value, Guid.CreateVersion7(), entity, reservationId.Value, request.WarehouseId, effectiveLocation, request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, remainingReserved);
                    await allocation.ExecuteNonQueryAsync(token);
                }
                await InsertReservationEventAsync(context, tenant.Value, entity, reservationId.Value, "Consumed", draft.BaseQuantity, actor, now, token);
                context.AddOutbox(new(Guid.CreateVersion7(), "ReservationConsumedV1", JsonSerializer.Serialize(new { reservationId, operationId, quantity = draft.BaseQuantity, remainingReserved }), tenant, now, correlationId, reservationId.Value, "inventory", traceParent));
            }
            if (draft.SerialId is { } serialId)
                await MoveSerialAsync(context, tenant.Value, entity, draft.ProductVariantId, serialId,
                    kind == MovementKind.Issue || kind == MovementKind.Adjustment && signedAdjustmentQuantity < 0m ? "Issued" : kind == MovementKind.Return ? "Returned" : "OnHand",
                    kind == MovementKind.Issue || kind == MovementKind.Adjustment && signedAdjustmentQuantity < 0m ? null : draft.WarehouseId,
                    kind == MovementKind.Issue || kind == MovementKind.Adjustment && signedAdjustmentQuantity < 0m ? null : draft.LocationId, token);
            await using (var command = new NpgsqlCommand("insert into business_inventory.stock_balance(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,on_hand_base_quantity,reserved_base_quantity,version,last_movement_sequence) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,1,$12) on conflict(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id) do update set on_hand_base_quantity=business_inventory.stock_balance.on_hand_base_quantity+excluded.on_hand_base_quantity,reserved_base_quantity=excluded.reserved_base_quantity,version=business_inventory.stock_balance.version+1,last_movement_sequence=excluded.last_movement_sequence", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, Guid.CreateVersion7(), entity, request.ProductVariantId, request.WarehouseId, effectiveLocation, request.Status.ToString(), request.LotId ?? (object)DBNull.Value, request.SerialId ?? (object)DBNull.Value, signedQuantity, reserved - consumableReserved, sequence); await command.ExecuteNonQueryAsync(token);
            }
            if (signedQuantity != 0m)
                await ReconcileNegativeStockConditionAsync(context, position, movementId, token);
            await AuditAsync(context, tenant.Value, entity, actor, negativeStockConditionTriggered ? "stock.issue_negative_condition_raised" : $"stock.{kind.ToString().ToLowerInvariant()}_confirmed", "InventoryOperation", operationId, correlationId, now, token);
            var eventType = kind == MovementKind.Adjustment ? "StockAdjustedV1" : kind == MovementKind.Issue ? "StockIssuedV1" : kind == MovementKind.Opening ? "StockOpeningPostedV1" : "StockReceivedV1";
            var payload = JsonSerializer.Serialize(new { operationId, movementId, productVariantId = request.ProductVariantId, warehouseId = request.WarehouseId, locationId = effectiveLocation, quantity = draft.BaseQuantity, costAmount = request.UnitCost is { } unitCost ? Math.Abs(unitCost * draft.BaseQuantity) : 0m, currencyCode = request.CurrencyCode, projectId = request.ProjectId, taskId = request.TaskId, costCategory = request.CostCategory, sourceModule, reasonCode });
            context.AddOutbox(new(Guid.CreateVersion7(), eventType, payload, tenant, now, correlationId, operationId, "inventory", traceParent));
            return new InventoryOperationResult(operationId, operationNumber, "Confirmed", [movementId], false);
        }, ct);
    }

    private async Task<NpgsqlConnection> OpenTenantAsync(TenantId tenant, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
            command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private async Task<AdjustmentRequestRecord> LoadAdjustmentRequestAsync(TenantId tenant, Guid entity, Guid requestId, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select product_variant_id,warehouse_id,location_id,entered_uom_id,quantity,direction,reason,status,operational_date,posting_date,stock_status,lot_id,serial_id,unit_cost,currency_code,idempotency_key from business_inventory.inventory_adjustment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, entity, requestId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InventoryException("inventory.adjustment_request_not_found");
        return new(reader.GetGuid(0), reader.GetGuid(1), NullGuid(reader, 2), reader.GetGuid(3), reader.GetDecimal(4), Enum.Parse<AdjustmentDirection>(reader.GetString(5)), Enum.Parse<AdjustmentReason>(reader.GetString(6)), Enum.Parse<AdjustmentRequestStatus>(reader.GetString(7)), reader.GetFieldValue<DateOnly>(8), reader.GetFieldValue<DateOnly>(9), Enum.Parse<StockStatus>(reader.GetString(10)), NullGuid(reader, 11), NullGuid(reader, 12), reader.IsDBNull(13) ? null : reader.GetDecimal(13), reader.IsDBNull(14) ? null : reader.GetString(14), reader.GetString(15));
    }

    private static AdjustmentRequestView ReadAdjustmentView(NpgsqlDataReader reader, Guid id, Guid entity, AdjustmentApprovalMode mode, AdjustmentRequestStatus status, Guid createdBy)
        => new(id, entity, reader.GetGuid(0), reader.GetGuid(1), NullGuid(reader, 2), reader.GetDecimal(3), Enum.Parse<AdjustmentDirection>(reader.GetString(4)), Enum.Parse<AdjustmentReason>(reader.GetString(5)), reader.GetFieldValue<DateOnly>(6), reader.GetFieldValue<DateOnly>(7), mode, status, createdBy, NullGuid(reader, 11), NullGuid(reader, 12));

    private sealed record AdjustmentRequestRecord(Guid ProductVariantId, Guid WarehouseId, Guid? LocationId, Guid EnteredUomId,
        decimal Quantity, AdjustmentDirection Direction, AdjustmentReason Reason, AdjustmentRequestStatus Status,
        DateOnly OperationalDate, DateOnly PostingDate, StockStatus StockStatus, Guid? LotId, Guid? SerialId,
        decimal? UnitCost, string? CurrencyCode, string IdempotencyKey);

    private async Task<string> EnsurePostingDateOpenAsync(ITransactionContext context, TenantId tenant, Guid entity, DateOnly postingDate, CancellationToken ct)
    {
        if (ledgerPosting is null) throw new InventoryException("inventory.ledger_period_capability_required");
        return await ledgerPosting.ValidateInventoryPostingDateAsync(context, tenant, new Shared.Primitives.Identifiers.LegalEntityId(entity), postingDate, ct)
            ?? throw new InventoryException("inventory.period_locked");
    }

    private static async Task EnsureOperationalCutoffOpenAsync(ITransactionContext context, TenantId tenant, Guid entity, DateOnly operationalDate, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select operational_cutoff_date from business_inventory.inventory_policy where tenant_id=$1 and legal_entity_id=$2 for share", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity);
        var cutoff = await command.ExecuteScalarAsync(ct);
        if (cutoff is DateOnly date && operationalDate < date) throw new InventoryException("inventory.before_operational_cutoff");
    }

    private static async Task<bool> SeparationOfDutiesAsync(ITransactionContext context, Guid tenant, Guid entity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select coalesce(separation_of_duties,true) from business_inventory.inventory_policy where tenant_id=$1 and legal_entity_id=$2", context.Connection, context.Transaction);
        Add(command, tenant, entity); return (bool?)await command.ExecuteScalarAsync(ct) ?? true;
    }

    private static async Task<InventoryProfile?> GetProfileInTransactionAsync(ITransactionContext context, Guid tenant, Guid entity, Guid variant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select inventory_state,tracking_mode,valuation_mode,costing_method,negative_stock_policy,default_warehouse_id,reservation_mode,version from business_inventory.product_inventory_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and status='Active' for share", context.Connection, context.Transaction);
        Add(command, tenant, entity, variant); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new InventoryProfile(tenant, entity, variant, Enum.Parse<InventoryState>(reader.GetString(0)), Enum.Parse<TrackingMode>(reader.GetString(1)), Enum.Parse<ValuationMode>(reader.GetString(2)), reader.IsDBNull(3) ? null : Enum.Parse<CostingMethod>(reader.GetString(3)), Enum.Parse<NegativeStockPolicy>(reader.GetString(4)), NullGuid(reader, 5), Enum.Parse<ReservationMode>(reader.GetString(6)), reader.GetInt64(7)) : null;
    }

    private static async Task<PhysicalCountView> GetPhysicalCountAsync(ITransactionContext context, Guid tenant, Guid entity, Guid countId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select c.count_number,c.status,count(l.id),count(l.counted_quantity),coalesce(sum(l.difference_quantity),0) from business_inventory.physical_count c left join business_inventory.count_line l on l.tenant_id=c.tenant_id and l.count_id=c.id where c.tenant_id=$1 and c.legal_entity_id=$2 and c.id=$3 group by c.count_number,c.status", context.Connection, context.Transaction);
        Add(command, tenant, entity, countId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InventoryException("inventory.count_not_found");
        return new PhysicalCountView(countId, reader.GetString(0), reader.GetString(1), checked((int)reader.GetInt64(2)), checked((int)reader.GetInt64(3)), reader.GetDecimal(4));
    }

    private async Task ApplyValuationReversalAsync(ITransactionContext context, TenantId tenant, Guid actor, Guid entity,
        Guid operationId, ReversalMovement original, Guid reversalMovementId, ReverseInventoryOperationRequest request,
        Guid correlationId, DateTimeOffset now, CancellationToken ct)
    {
        if (original.OriginalValuationId is null || original.OriginalQuantityDelta is null || original.OriginalValueDelta is null
            || original.OriginalUnitCost is null || original.CurrencyCode is null)
            throw new InventoryException("inventory.original_valuation_incomplete");

        var quantityDelta = -original.OriginalQuantityDelta.Value;
        var valueDelta = -original.OriginalValueDelta.Value;
        if (quantityDelta == 0m)
            throw new InventoryException("inventory.original_valuation_incomplete");
        decimal quantity;
        decimal carryingValue;
        string costingMethod;
        string currency;
        await using (var state = new NpgsqlCommand("select quantity,carrying_value,costing_method,currency_code from business_inventory.cost_state where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 for update", context.Connection, context.Transaction))
        {
            Add(state, tenant.Value, entity, original.ProductVariantId);
            await using var reader = await state.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new InventoryException("inventory.cost_state_missing");
            quantity = reader.GetDecimal(0); carryingValue = reader.GetDecimal(1); costingMethod = reader.GetString(2); currency = reader.GetString(3).Trim();
        }
        if (!string.Equals(currency, original.CurrencyCode, StringComparison.OrdinalIgnoreCase)
            || quantity + quantityDelta < 0m || carryingValue + valueDelta < 0m)
            throw new InventoryException("inventory.reversal_insufficient_valued_stock");

        var restoredFifoLayers = new List<(decimal Quantity, decimal UnitCost)>();
        var removedFifoLayerIds = new List<(Guid Id, decimal Quantity)>();
        if (costingMethod == CostingMethod.FIFO.ToString())
        {
            if (quantityDelta > 0m)
            {
                await using var consumption = new NpgsqlCommand("select quantity,unit_cost from business_inventory.cost_layer_consumption where tenant_id=$1 and legal_entity_id=$2 and movement_id=$3 order by id", context.Connection, context.Transaction);
                Add(consumption, tenant.Value, entity, original.Id);
                await using var reader = await consumption.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) restoredFifoLayers.Add((reader.GetDecimal(0), reader.GetDecimal(1)));
                if (restoredFifoLayers.Count == 0) restoredFifoLayers.Add((quantityDelta, original.OriginalUnitCost.Value));
                if (restoredFifoLayers.Sum(layer => layer.Quantity) != quantityDelta
                    || decimal.Round(restoredFifoLayers.Sum(layer => layer.Quantity * layer.UnitCost), 6, MidpointRounding.ToEven) != valueDelta)
                    throw new InventoryException("inventory.reversal_fifo_cost_evidence_inconsistent");
            }
            else
            {
                var remaining = -quantityDelta;
                await using var layers = new NpgsqlCommand("select id,remaining_quantity from business_inventory.cost_layer where tenant_id=$1 and legal_entity_id=$2 and source_valuation_id=$3 and remaining_quantity>0 order by id for update", context.Connection, context.Transaction);
                Add(layers, tenant.Value, entity, original.OriginalValuationId.Value);
                await using var reader = await layers.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct) && remaining > 0m)
                {
                    var used = Math.Min(remaining, reader.GetDecimal(1));
                    removedFifoLayerIds.Add((reader.GetGuid(0), used));
                    remaining -= used;
                }
                if (remaining > 0m) throw new InventoryException("inventory.reversal_fifo_layer_consumed");
                await reader.DisposeAsync();
                foreach (var layer in removedFifoLayerIds)
                {
                    await using var update = new NpgsqlCommand("update business_inventory.cost_layer set remaining_quantity=remaining_quantity-$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
                    Add(update, tenant.Value, entity, layer.Id, layer.Quantity); await update.ExecuteNonQueryAsync(ct);
                }
            }
        }

        Guid? reversedFactId = null;
        if (valueDelta != 0m)
        {
            if (original.AccountingFactId is null) throw new InventoryException("inventory.original_valuation_incomplete");
            var factId = Guid.CreateVersion7();
            var reason = $"Reversal of Inventory operation {request.OperationId:D}";
            var payload = new InventoryValuationReversalPayload(original.AccountingFactId.Value, original.Id, reversalMovementId, reason);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new LegalEntityId(entity),
                AccountingFactTypes.InventoryValuationReversal, 1, "inventory", reversalMovementId.ToString("D"),
                $"inventory:reversal:{reversalMovementId:D}:valuation", request.OperationalDate, request.PostingDate, payload, correlationId, actor);
            reversedFactId = (await ledgerPosting!.ReverseInventoryValuationAsync(context, fact, payload, ct)).FactId;
        }

        var valuationId = Guid.CreateVersion7();
        await using (var valuation = new NpgsqlCommand("insert into business_inventory.valuation_entry(tenant_id,id,legal_entity_id,operation_id,movement_id,product_variant_id,effective_at,quantity_delta,value_delta,unit_cost,currency_code,costing_method,fact_type,accounting_fact_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", context.Connection, context.Transaction))
        {
            Add(valuation, tenant.Value, valuationId, entity, operationId, reversalMovementId, original.ProductVariantId, now,
                quantityDelta, valueDelta, original.OriginalUnitCost.Value, currency, costingMethod,
                AccountingFactTypes.InventoryValuationReversal, reversedFactId ?? (object)DBNull.Value, now);
            await valuation.ExecuteNonQueryAsync(ct);
        }
        foreach (var layer in restoredFifoLayers)
        {
            await using var insertLayer = new NpgsqlCommand("insert into business_inventory.cost_layer(tenant_id,id,legal_entity_id,product_variant_id,source_valuation_id,effective_at,original_quantity,remaining_quantity,unit_cost,currency_code,created_at) values($1,$2,$3,$4,$5,$6,$7,$7,$8,$9,$6)", context.Connection, context.Transaction);
            Add(insertLayer, tenant.Value, Guid.CreateVersion7(), entity, original.ProductVariantId, valuationId, now, layer.Quantity, layer.UnitCost, currency);
            await insertLayer.ExecuteNonQueryAsync(ct);
        }
        var nextQuantity = quantity + quantityDelta;
        var nextValue = carryingValue + valueDelta;
        var average = nextQuantity == 0m ? 0m : decimal.Round(nextValue / nextQuantity, 10, MidpointRounding.ToEven);
        await using var updateState = new NpgsqlCommand("update business_inventory.cost_state set quantity=$4,carrying_value=$5,average_unit_cost=$6,version=version+1,updated_at=$7 where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3", context.Connection, context.Transaction);
        Add(updateState, tenant.Value, entity, original.ProductVariantId, nextQuantity, nextValue, average, now);
        if (await updateState.ExecuteNonQueryAsync(ct) != 1) throw new InventoryException("inventory.cost_state_missing");
    }

    private sealed record CountLineRecord(Guid Id, Guid ProductVariantId, Guid WarehouseId, Guid LocationId, StockStatus Status,
        Guid? LotId, Guid? SerialId, decimal Expected, decimal? Counted, decimal? CountedUnitCost, DateTimeOffset? EnteredAt);
    private sealed record ReversalMovement(Guid Id, Guid ProductVariantId, Guid WarehouseId, Guid LocationId,
        StockStatus Status, Guid? LotId, Guid? SerialId, decimal EnteredQuantity, Guid EnteredUomId,
        decimal ConversionFactor, decimal BaseQuantity, decimal SignedBaseQuantity, DateOnly OperationalDate,
        Guid? OriginalValuationId, decimal? OriginalQuantityDelta, decimal? OriginalValueDelta, decimal? OriginalUnitCost,
        string? CurrencyCode, Guid? AccountingFactId);

    private async Task<InventoryProfile?> GetProfileAsync(TenantId tenant, Guid entity, Guid productVariantId, CancellationToken ct)
    {
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select inventory_state,tracking_mode,valuation_mode,costing_method,negative_stock_policy,default_warehouse_id,reservation_mode,version from business_inventory.product_inventory_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and status='Active'", connection);
        Add(command, tenant.Value, entity, productVariantId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new InventoryProfile(tenant.Value, entity, productVariantId,
            Enum.Parse<InventoryState>(reader.GetString(0)), Enum.Parse<TrackingMode>(reader.GetString(1)),
            Enum.Parse<ValuationMode>(reader.GetString(2)), reader.IsDBNull(3) ? null : Enum.Parse<CostingMethod>(reader.GetString(3)),
            Enum.Parse<NegativeStockPolicy>(reader.GetString(4)), NullGuid(reader, 5),
            Enum.Parse<ReservationMode>(reader.GetString(6)), reader.GetInt64(7));
    }

    private async Task<Guid> ResolveLocationAsync(TenantId tenant, Guid entity, Guid warehouseId, Guid? requestedLocationId,
        DateTimeOffset effectiveAt, bool canPick, CancellationToken ct)
    {
        _ = effectiveAt;
        await using var connection = await OpenTenantAsync(tenant, ct);
        await using var command = new NpgsqlCommand("""
            select l.id from business_inventory.warehouse w
            join business_inventory.location l on l.tenant_id=w.tenant_id and l.legal_entity_id=w.legal_entity_id
              and l.warehouse_id=w.id and l.id=coalesce($4::uuid,w.default_location_id)
            where w.tenant_id=$1 and w.legal_entity_id=$2 and w.id=$3 and w.status='Active'
              and l.status='Active' and l.can_hold_stock and (($5::boolean and l.can_pick_from) or (not $5::boolean and l.can_receive_into))
            """, connection);
        Add(command, tenant.Value, entity, warehouseId, requestedLocationId ?? (object)DBNull.Value, canPick);
        return await command.ExecuteScalarAsync(ct) as Guid? ?? throw new InventoryException("inventory.location_not_usable");
    }

    private static async Task LockIdempotencyAsync(ITransactionContext context, Guid tenant, Guid entity, string scope, string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1,0))", context.Connection, context.Transaction);
        command.Parameters.AddWithValue($"inventory:{tenant:D}:{entity:D}:{scope}:{key}");
        await command.ExecuteNonQueryAsync(ct);
    }

    private sealed record BalanceLock(decimal OnHand, decimal Reserved, long LastSequence);

    private static async Task<BalanceLock> ReadBalanceForUpdateAsync(ITransactionContext context, StockPosition position, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select on_hand_base_quantity,reserved_base_quantity,last_movement_sequence from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and lot_id is not distinct from $7::uuid and serial_id is not distinct from $8::uuid for update", context.Connection, context.Transaction);
        Add(command, position.TenantId, position.LegalEntityId, position.ProductVariantId, position.WarehouseId, position.LocationId,
            position.Status.ToString(), position.LotId ?? (object)DBNull.Value, position.SerialId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetInt64(2)) : new(0m, 0m, 0L);
    }

    private static async Task ValidateTraceabilityAsync(ITransactionContext context, MovementDraft movement, MovementKind kind, CancellationToken ct, bool consumingReservation = false)
    {
        if (movement.LotId is { } lotId)
        {
            await using var lot = new NpgsqlCommand("select status,expires_on from business_inventory.lot where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4 for share", context.Connection, context.Transaction);
            Add(lot, movement.TenantId, movement.LegalEntityId, movement.ProductVariantId, lotId);
            await using (var reader = await lot.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct) || reader.GetString(0) != "Active" || !reader.IsDBNull(1) && DateOnly.FromDateTime(reader.GetDateTime(1)) < movement.OperationalDate)
                    throw new InventoryException("inventory.lot_unavailable_or_expired");
            }
        }
        if (movement.SerialId is not { } serialId) return;
        await using var serial = new NpgsqlCommand("select status,current_warehouse_id,current_location_id from business_inventory.serial where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4 for update", context.Connection, context.Transaction);
        Add(serial, movement.TenantId, movement.LegalEntityId, movement.ProductVariantId, serialId); await using var serialReader = await serial.ExecuteReaderAsync(ct);
        if (!await serialReader.ReadAsync(ct)) throw new InventoryException("inventory.serial_not_found");
        var status = serialReader.GetString(0);
        if (kind is MovementKind.Receipt or MovementKind.Opening && status is not ("PendingReceipt" or "Issued" or "Returned"))
            throw new InventoryException("inventory.serial_already_on_hand");
        if (kind == MovementKind.Issue && (status != (consumingReservation ? "Reserved" : "OnHand") || NullGuid(serialReader, 1) != movement.WarehouseId || NullGuid(serialReader, 2) != movement.LocationId))
            throw new InventoryException("inventory.serial_not_at_location");
        if (kind == MovementKind.Return && status != "Issued") throw new InventoryException("inventory.serial_not_issued");
    }

    private static async Task ValidateSerialCountAsync(ITransactionContext context, Guid tenant, Guid entity,
        CountLineRecord line, decimal counted, decimal expected, CancellationToken ct)
    {
        var serialId = line.SerialId ?? throw new InventoryException("inventory.serial_count_must_identify_one_unit");
        await using var command = new NpgsqlCommand("select status,current_warehouse_id,current_location_id from business_inventory.serial where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4 for update", context.Connection, context.Transaction);
        Add(command, tenant, entity, line.ProductVariantId, serialId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InventoryException("inventory.serial_not_found");
        var status = reader.GetString(0);
        var warehouse = NullGuid(reader, 1);
        var location = NullGuid(reader, 2);
        if (counted == 0m && expected == 1m
            && (status != "OnHand" || warehouse != line.WarehouseId || location != line.LocationId))
            throw new InventoryException("inventory.serial_not_at_count_location");
        if (counted == 1m && status == "OnHand"
            && (warehouse != line.WarehouseId || location != line.LocationId))
            throw new InventoryException("inventory.serial_already_on_hand");
        if (counted == 1m && status is not ("OnHand" or "PendingReceipt" or "Issued" or "Returned"))
            throw new InventoryException("inventory.serial_unavailable_for_count");
    }

    private static async Task MoveSerialAsync(ITransactionContext context, Guid tenant, Guid entity, Guid productVariantId,
        Guid serialId, string status, Guid? warehouseId, Guid? locationId, CancellationToken ct)
    {
        await using var update = new NpgsqlCommand("update business_inventory.serial set status=$5,current_warehouse_id=$6,current_location_id=$7 where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and id=$4", context.Connection, context.Transaction);
        Add(update, tenant, entity, productVariantId, serialId, status, warehouseId ?? (object)DBNull.Value, locationId ?? (object)DBNull.Value);
        if (await update.ExecuteNonQueryAsync(ct) != 1) throw new InventoryException("inventory.serial_not_found");
    }

    private static async Task<long> NextMovementSequenceAsync(ITransactionContext context, Guid tenant, Guid entity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_inventory.movement_sequence(tenant_id,legal_entity_id,last_value) values($1,$2,1) on conflict(tenant_id,legal_entity_id) do update set last_value=business_inventory.movement_sequence.last_value+1 returning last_value", context.Connection, context.Transaction);
        Add(command, tenant, entity);
        return (long)(await command.ExecuteScalarAsync(ct) ?? throw new InventoryException("inventory.sequence_unavailable"));
    }

    private static async Task InsertConfirmedOperationAsync(ITransactionContext context, Guid tenant, Guid operationId, Guid entity,
        string number, MovementKind type, string sourceModule, string key, string hash, DateOnly operationalDate, DateOnly postingDate,
        Guid actor, Guid correlation, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_inventory.inventory_operation(tenant_id,id,legal_entity_id,operation_number,operation_type,status,source_module,idempotency_key,idempotency_hash,operational_date,posting_date,actor_id,correlation_id,created_at,confirmed_at) values($1,$2,$3,$4,$5,'Confirmed',$6,$7,$8,$9,$10,$11,$12,$13,$13)", context.Connection, context.Transaction);
        Add(command, tenant, operationId, entity, number, type.ToString(), sourceModule, key, hash, operationalDate, postingDate, actor, correlation, now);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> InsertMovementAsync(ITransactionContext context, Guid tenant, Guid operationId, Guid entity,
        Guid productVariantId, StockPosition position, Guid enteredUomId, decimal enteredQuantity, decimal factor, decimal signedBaseQuantity,
        string sourceModule, DateOnly operationalDate, DateOnly postingDate, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var sequence = await NextMovementSequenceAsync(context, tenant, entity, ct);
        await using var command = new NpgsqlCommand("insert into business_inventory.stock_movement_entry(tenant_id,id,legal_entity_id,operation_id,sequence,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,entered_quantity,entered_uom_id,conversion_factor_snapshot,base_quantity,signed_base_quantity,source_module,operational_date,posting_date,effective_at,movement_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$20)", context.Connection, context.Transaction);
        Add(command, tenant, id, entity, operationId, sequence, productVariantId, position.WarehouseId, position.LocationId, position.Status.ToString(),
            position.LotId ?? (object)DBNull.Value, position.SerialId ?? (object)DBNull.Value, enteredQuantity, enteredUomId, factor,
            Math.Abs(signedBaseQuantity), signedBaseQuantity, sourceModule, operationalDate, postingDate, now, "Transfer", actor);
        await command.ExecuteNonQueryAsync(ct);
        return id;
    }

    private static async Task ApplyBalanceDeltaAsync(ITransactionContext context, StockPosition position, decimal onHandDelta,
        decimal reservedDelta, Guid? movementId, CancellationToken ct)
    {
        long sequence = 0;
        if (movementId is { } id)
        {
            await using var sequenceQuery = new NpgsqlCommand("select sequence from business_inventory.stock_movement_entry where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            Add(sequenceQuery, position.TenantId, id);
            sequence = (long)(await sequenceQuery.ExecuteScalarAsync(ct) ?? 0L);
        }
        if (reservedDelta < 0m)
        {
            await using var release = new NpgsqlCommand("update business_inventory.stock_balance set on_hand_base_quantity=on_hand_base_quantity+$9,reserved_base_quantity=reserved_base_quantity+$10,version=version+1,last_movement_sequence=greatest(last_movement_sequence,$11) where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and lot_id is not distinct from $7::uuid and serial_id is not distinct from $8::uuid and reserved_base_quantity+$10>=0", context.Connection, context.Transaction);
            Add(release, position.TenantId, position.LegalEntityId, position.ProductVariantId, position.WarehouseId, position.LocationId,
                position.Status.ToString(), position.LotId ?? (object)DBNull.Value, position.SerialId ?? (object)DBNull.Value, onHandDelta, reservedDelta, sequence);
            if (await release.ExecuteNonQueryAsync(ct) != 1) throw new InventoryException("inventory.reservation_projection_invalid");
            return;
        }
        await using var command = new NpgsqlCommand("insert into business_inventory.stock_balance(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,on_hand_base_quantity,reserved_base_quantity,last_movement_sequence) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) on conflict(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id) do update set on_hand_base_quantity=business_inventory.stock_balance.on_hand_base_quantity+excluded.on_hand_base_quantity,reserved_base_quantity=business_inventory.stock_balance.reserved_base_quantity+excluded.reserved_base_quantity,version=business_inventory.stock_balance.version+1,last_movement_sequence=greatest(business_inventory.stock_balance.last_movement_sequence,excluded.last_movement_sequence)", context.Connection, context.Transaction);
        Add(command, position.TenantId, Guid.CreateVersion7(), position.LegalEntityId, position.ProductVariantId, position.WarehouseId,
            position.LocationId, position.Status.ToString(), position.LotId ?? (object)DBNull.Value, position.SerialId ?? (object)DBNull.Value,
            onHandDelta, reservedDelta, sequence);
        await command.ExecuteNonQueryAsync(ct);
        if (onHandDelta != 0m && movementId is { } movementIdValue)
            await ReconcileNegativeStockConditionAsync(context, position, movementIdValue, ct);
    }

    private static async Task ReconcileNegativeStockConditionAsync(ITransactionContext context, StockPosition position,
        Guid movementId, CancellationToken ct)
    {
        decimal onHand;
        await using (var read = new NpgsqlCommand("select on_hand_base_quantity from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and lot_id is not distinct from $7::uuid and serial_id is not distinct from $8::uuid for update", context.Connection, context.Transaction))
        {
            Add(read, position.TenantId, position.LegalEntityId, position.ProductVariantId, position.WarehouseId,
                position.LocationId, position.Status.ToString(), position.LotId ?? (object)DBNull.Value, position.SerialId ?? (object)DBNull.Value);
            onHand = (decimal)(await read.ExecuteScalarAsync(ct) ?? 0m);
        }

        var now = DateTimeOffset.UtcNow;
        Guid? conditionId;
        decimal? previousShortfall;
        await using (var current = new NpgsqlCommand("select id,shortfall_base_quantity from business_inventory.negative_stock_condition where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and lot_id is not distinct from $7::uuid and serial_id is not distinct from $8::uuid and condition_status='Active' for update", context.Connection, context.Transaction))
        {
            Add(current, position.TenantId, position.LegalEntityId, position.ProductVariantId, position.WarehouseId,
                position.LocationId, position.Status.ToString(), position.LotId ?? (object)DBNull.Value, position.SerialId ?? (object)DBNull.Value);
            await using var reader = await current.ExecuteReaderAsync(ct);
            conditionId = await reader.ReadAsync(ct) ? reader.GetGuid(0) : null;
            previousShortfall = conditionId is null ? null : reader.GetDecimal(1);
        }

        if (onHand < 0m)
        {
            if (conditionId is { } activeId)
            {
                await using var update = new NpgsqlCommand("update business_inventory.negative_stock_condition set shortfall_base_quantity=$3,last_seen_movement_id=$4 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
                Add(update, position.TenantId, activeId, -onHand, movementId); await update.ExecuteNonQueryAsync(ct);
                if (previousShortfall != -onHand)
                    context.AddOutbox(new(Guid.CreateVersion7(), "NegativeStockConditionUpdatedV1",
                        JsonSerializer.Serialize(new { conditionId = activeId, productVariantId = position.ProductVariantId, warehouseId = position.WarehouseId, shortfall = -onHand, requiresReconciliation = true }),
                        new TenantId(position.TenantId), now, movementId, movementId, "inventory"));
            }
            else
            {
                var id = Guid.CreateVersion7();
                await using var insert = new NpgsqlCommand("insert into business_inventory.negative_stock_condition(tenant_id,id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id,condition_status,shortfall_base_quantity,opened_by_movement_id,last_seen_movement_id,opened_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$11,$11,$12)", context.Connection, context.Transaction);
                Add(insert, position.TenantId, id, position.LegalEntityId, position.ProductVariantId, position.WarehouseId,
                    position.LocationId, position.Status.ToString(), position.LotId ?? (object)DBNull.Value,
                    position.SerialId ?? (object)DBNull.Value, -onHand, movementId, now);
                await insert.ExecuteNonQueryAsync(ct);
                context.AddOutbox(new(Guid.CreateVersion7(), "NegativeStockConditionRaisedV1",
                    JsonSerializer.Serialize(new { conditionId = id, productVariantId = position.ProductVariantId, warehouseId = position.WarehouseId, shortfall = -onHand, requiresReconciliation = true }),
                    new TenantId(position.TenantId), now, movementId, movementId, "inventory"));
            }
        }
        else if (conditionId is { } resolvedId)
        {
            await using var resolve = new NpgsqlCommand("update business_inventory.negative_stock_condition set condition_status='Resolved',shortfall_base_quantity=0,resolved_by_movement_id=$3,resolved_at=$4,last_seen_movement_id=$3 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            Add(resolve, position.TenantId, resolvedId, movementId, now); await resolve.ExecuteNonQueryAsync(ct);
            context.AddOutbox(new(Guid.CreateVersion7(), "NegativeStockConditionResolvedV1",
                JsonSerializer.Serialize(new { conditionId = resolvedId, resolvingMovementId = movementId, productVariantId = position.ProductVariantId, warehouseId = position.WarehouseId }),
                new TenantId(position.TenantId), now, movementId, movementId, "inventory"));
        }
    }

    private static async Task InsertTransferEventAsync(ITransactionContext context, Guid tenant, Guid entity, Guid transferId,
        string eventType, decimal quantity, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_inventory.transfer_event(tenant_id,id,legal_entity_id,transfer_id,event_type,quantity,actor_id,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8)", context.Connection, context.Transaction);
        Add(command, tenant, Guid.CreateVersion7(), entity, transferId, eventType, quantity, actor, now);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertReservationEventAsync(ITransactionContext context, Guid tenant, Guid entity, Guid reservationId,
        string eventType, decimal quantity, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_inventory.reservation_event(tenant_id,id,legal_entity_id,reservation_id,event_type,quantity,actor_id,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8)", context.Connection, context.Transaction);
        Add(command, tenant, Guid.CreateVersion7(), entity, reservationId, eventType, quantity, actor, now);
        await command.ExecuteNonQueryAsync(ct);
    }

    private sealed record TransferRecord(Guid ProductVariantId, Guid SourceWarehouseId, Guid SourceLocationId, Guid DestinationWarehouseId,
        Guid DestinationLocationId, Guid? LotId, Guid? SerialId, TransferMode Mode, string Status, decimal RequestedQuantity,
        decimal ShippedQuantity, decimal ReceivedQuantity, DateOnly OperationalDate, DateOnly PostingDate, string IdempotencyKey,
        decimal EnteredQuantity, Guid EnteredUomId, decimal ConversionFactor);
    private sealed record ReservationRecord(Guid ProductVariantId, Guid WarehouseId, Guid LocationId, decimal Quantity, string Status, Guid? LotId, Guid? SerialId);

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    // UUIDv7 prefixes are time ordered, so suffixes must use the random portion to avoid same-millisecond collisions.
    private static string OperationNumber(DateTimeOffset now, Guid id) => $"INV-{now:yyyyMMdd}-{id.ToString("N")[16..].ToUpperInvariant()}";

    private async Task ApplyValuationAsync(ITransactionContext context, TenantId tenant, Guid actor, Guid entity,
        Guid operationId, Guid movementId, InventoryProfile profile, MovementDraft movement, string? currencyCode,
        MovementKind kind, Guid correlationId, DateTimeOffset now, CancellationToken ct, decimal? signedAdjustmentQuantity = null)
    {
        var currency = currencyCode?.Trim().ToUpperInvariant() ?? "XXX";
        var isAdjustment = kind == MovementKind.Adjustment;
        if ((currencyCode is not null && currency.Length != 3) ||
            (movement.Kind is MovementKind.Receipt or MovementKind.Return or MovementKind.Opening && movement.UnitCost is null) ||
            (isAdjustment && (signedAdjustmentQuantity is null or 0m)))
            throw new InventoryException("inventory.unit_cost_invalid");
        var costStateId = Guid.CreateVersion7();
        await using (var initialize = new NpgsqlCommand("insert into business_inventory.cost_state(tenant_id,id,legal_entity_id,product_variant_id,costing_method,quantity,carrying_value,average_unit_cost,currency_code,updated_at) values($1,$2,$3,$4,$5,0,0,0,$6,$7) on conflict(tenant_id,legal_entity_id,product_variant_id) do nothing", context.Connection, context.Transaction))
        {
            Add(initialize, tenant.Value, costStateId, entity, profile.ProductVariantId, profile.Costing!.Value.ToString(), currency, now);
            await initialize.ExecuteNonQueryAsync(ct);
        }

        decimal quantity; decimal carryingValue; decimal average; string storedCurrency;
        await using (var lockState = new NpgsqlCommand("select quantity,carrying_value,average_unit_cost,currency_code,costing_method from business_inventory.cost_state where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 for update", context.Connection, context.Transaction))
        {
            Add(lockState, tenant.Value, entity, profile.ProductVariantId);
            await using var reader = await lockState.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new InventoryException("inventory.cost_state_missing");
            quantity = reader.GetDecimal(0); carryingValue = reader.GetDecimal(1); average = reader.GetDecimal(2);
            storedCurrency = reader.GetString(3).Trim();
            if (!string.Equals(reader.GetString(4), profile.Costing!.Value.ToString(), StringComparison.Ordinal))
                throw new InventoryException("inventory.costing_method_change_requires_migration");
        }
        if (quantity > 0m && currencyCode is not null && !string.Equals(storedCurrency, currency, StringComparison.Ordinal))
            throw new InventoryException("inventory.valuation_currency_mismatch");
        currency = currencyCode is null ? storedCurrency : currency;
        var value = 0m;
        var unitCost = movement.UnitCost ?? 0m;
        var costMethod = profile.Costing!.Value;
        var isIncrease = kind is MovementKind.Receipt or MovementKind.Return or MovementKind.Opening || isAdjustment && signedAdjustmentQuantity > 0m;
        var isDecrease = kind == MovementKind.Issue || isAdjustment && signedAdjustmentQuantity < 0m;
        if (isAdjustment && signedAdjustmentQuantity > 0m)
        {
            if (costMethod == CostingMethod.MovingAverage && quantity > 0m) unitCost = average;
            else if (movement.UnitCost is null) throw new InventoryException("inventory.count_unit_cost_required");
        }
        if (isIncrease)
        {
            if (costMethod == CostingMethod.MovingAverage)
            {
                value = decimal.Round(checked(movement.BaseQuantity * unitCost), 6, MidpointRounding.ToEven);
                quantity = checked(quantity + movement.BaseQuantity);
                carryingValue = checked(carryingValue + value);
                average = quantity == 0m ? 0m : decimal.Round(carryingValue / quantity, 10, MidpointRounding.ToEven);
            }
            else
            {
                value = decimal.Round(checked(movement.BaseQuantity * unitCost), 6, MidpointRounding.ToEven);
                quantity = checked(quantity + movement.BaseQuantity);
                carryingValue = checked(carryingValue + value);
            }
        }
        else if (isDecrease)
        {
            var requested = movement.BaseQuantity;
            if (requested > quantity) throw new InventoryException("inventory.insufficient_valued_stock");
            if (costMethod == CostingMethod.MovingAverage)
            {
                unitCost = average;
                value = decimal.Round(requested * average, 6, MidpointRounding.ToEven);
            }
            else
            {
                var remaining = requested;
                await using var layers = new NpgsqlCommand("select id,remaining_quantity,unit_cost from business_inventory.cost_layer where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and remaining_quantity>0 order by effective_at,id for update", context.Connection, context.Transaction);
                Add(layers, tenant.Value, entity, profile.ProductVariantId);
                await using var reader = await layers.ExecuteReaderAsync(ct);
                var consumption = new List<(Guid Id, decimal Quantity, decimal UnitCost)>();
                while (await reader.ReadAsync(ct) && remaining > 0m)
                {
                    var used = Math.Min(remaining, reader.GetDecimal(1));
                    consumption.Add((reader.GetGuid(0), used, reader.GetDecimal(2)));
                    value = decimal.Round(checked(value + used * reader.GetDecimal(2)), 6, MidpointRounding.ToEven);
                    remaining -= used;
                }
                if (remaining != 0m) throw new InventoryException("inventory.cost_layers_inconsistent");
                await reader.DisposeAsync();
                foreach (var item in consumption)
                {
                    await using var update = new NpgsqlCommand("update business_inventory.cost_layer set remaining_quantity=remaining_quantity-$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
                    Add(update, tenant.Value, entity, item.Id, item.Quantity); await update.ExecuteNonQueryAsync(ct);
                    await using var history = new NpgsqlCommand("insert into business_inventory.cost_layer_consumption(tenant_id,id,legal_entity_id,cost_layer_id,movement_id,quantity,unit_cost,consumed_at) values($1,$2,$3,$4,$5,$6,$7,$8)", context.Connection, context.Transaction);
                    Add(history, tenant.Value, Guid.CreateVersion7(), entity, item.Id, movementId, item.Quantity, item.UnitCost, now); await history.ExecuteNonQueryAsync(ct);
                }
                unitCost = requested == 0m ? 0m : value / requested;
            }
            quantity -= requested;
            carryingValue -= value;
            if (quantity == 0m) { carryingValue = 0m; average = 0m; }
            else average = decimal.Round(carryingValue / quantity, 10, MidpointRounding.ToEven);
        }
        else throw new InventoryException("inventory.valuation_movement_not_supported");

        var delta = isDecrease ? -value : value;
        var factType = isAdjustment ? AccountingFactTypes.InventoryCostAdjustment : kind == MovementKind.Issue ? AccountingFactTypes.InventoryIssueValued : AccountingFactTypes.InventoryReceiptValued;
        Guid? accountingFactId = null;
        if (value > 0m)
        {
            var factId = Guid.CreateVersion7(); accountingFactId = factId;
            var role = isAdjustment ? isIncrease ? "Increase" : "Decrease" : kind == MovementKind.Issue ? "Issue" : kind == MovementKind.Opening ? "Opening" : "Receipt";
            var payload = new InventoryValuationFactPayload(value, currency, "PRODUCT", role, movement.BaseQuantity, unitCost, profile.ProductVariantId, movementId, currency, value);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new LegalEntityId(entity), factType, 1, "inventory",
                movementId.ToString("D"), $"inventory:{movementId:D}:valuation", movement.OperationalDate, movement.PostingDate,
                payload, correlationId, actor);
            var result = await ledgerPosting!.PostInventoryValuationAsync(context, fact, payload, ct);
            accountingFactId = result.FactId;
        }
        var valuationId = Guid.CreateVersion7();
        await using (var valuation = new NpgsqlCommand("insert into business_inventory.valuation_entry(tenant_id,id,legal_entity_id,operation_id,movement_id,product_variant_id,effective_at,quantity_delta,value_delta,unit_cost,currency_code,costing_method,fact_type,accounting_fact_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", context.Connection, context.Transaction))
        {
            Add(valuation, tenant.Value, valuationId, entity, operationId, movementId, profile.ProductVariantId, now, isAdjustment ? signedAdjustmentQuantity!.Value : kind == MovementKind.Issue ? -movement.BaseQuantity : movement.BaseQuantity, delta, unitCost, currency, costMethod.ToString(), factType, accountingFactId ?? (object)DBNull.Value, now);
            await valuation.ExecuteNonQueryAsync(ct);
        }
        if (costMethod == CostingMethod.FIFO && isIncrease)
        {
            await using var layer = new NpgsqlCommand("insert into business_inventory.cost_layer(tenant_id,id,legal_entity_id,product_variant_id,source_valuation_id,effective_at,original_quantity,remaining_quantity,unit_cost,currency_code,created_at) values($1,$2,$3,$4,$5,$6,$7,$7,$8,$9,$6)", context.Connection, context.Transaction);
            Add(layer, tenant.Value, Guid.CreateVersion7(), entity, profile.ProductVariantId, valuationId, now, movement.BaseQuantity, unitCost, currency); await layer.ExecuteNonQueryAsync(ct);
        }
        await using (var update = new NpgsqlCommand("update business_inventory.cost_state set quantity=$4,carrying_value=$5,average_unit_cost=$6,currency_code=$7,version=version+1,updated_at=$8 where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3", context.Connection, context.Transaction))
        {
            Add(update, tenant.Value, entity, profile.ProductVariantId, quantity, carryingValue, average, currency, now); await update.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task AuditAsync(ITransactionContext context, Guid tenant, Guid entity, Guid actor, string action, string entityType, Guid entityId, Guid correlation, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_inventory.inventory_audit(tenant_id,id,legal_entity_id,actor_id,action,entity_type,entity_id,occurred_at,correlation_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", context.Connection, context.Transaction);
        Add(command, tenant, Guid.CreateVersion7(), entity, actor, action, entityType, entityId, now, correlation); await command.ExecuteNonQueryAsync(ct);
    }

    private static Guid? NullGuid(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
}
