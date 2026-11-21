using Core.Transactions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Repositories;

/// <summary>
/// Operational seams for shop-floor attribution, quality gates, subcontract
/// commitments, MPS versioning, and output allocation. Manufacturing records
/// the decision/evidence; Inventory, Quality, HR, Service, Projects and Ledger
/// remain the owners of their corresponding facts.
/// </summary>
public sealed partial class ManufacturingRepository
{
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();

    public Task AssignOperationResourceAsync(TenantId tenant, Guid actor, Guid orderId, Guid operationId, AssignOperationResourceRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.RequiredQuantity <= 0) throw new ManufacturingException("manufacturing.resource_assignment_invalid");
            var plant = await ScalarAsync<Guid?>(context, "select plant_id from business_manufacturing.production_order where tenant_id=$1 and id=$2", token, tenant.Value, orderId) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
            if (!await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation where tenant_id=$1 and id=$2 and production_order_id=$3)", token, tenant.Value, operationId, orderId)) throw new ManufacturingException("manufacturing.operation_not_found");
            if (!await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.manufacturing_resource where tenant_id=$1 and id=$2 and plant_id=$3 and state='Active')", token, tenant.Value, request.ResourceId, plant)) throw new ManufacturingException("manufacturing.resource_not_found");
            await ExecuteAsync(context, "insert into business_manufacturing.production_operation_resource(tenant_id,id,production_operation_id,resource_id,required_quantity,assigned_quantity,assigned_at,released_at) values($1,$2,$3,$4,$5,$5,now(),null) on conflict (tenant_id,production_operation_id,resource_id) do update set required_quantity=excluded.required_quantity,assigned_quantity=excluded.assigned_quantity,assigned_at=now(),released_at=null", token, tenant.Value, Guid.CreateVersion7(), operationId, request.ResourceId, request.RequiredQuantity);
            await AuditAsync(context, tenant.Value, "ProductionOperationResource", operationId, "assigned", actor, null, request, token);
            return true;
        }, ct);

    public Task<ProductionCostResponse> RecordLaborFactAsync(TenantId tenant, Guid actor, Guid orderId, RecordLaborFactRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.Minutes <= 0 || request.RateAmount < 0 || request.EmployeeId is null || string.IsNullOrWhiteSpace(request.SourceRef) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.labor_fact_invalid");
            var order = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
            if (hrPayroll is null) throw new ManufacturingException("manufacturing.hr_provider_unavailable");
            if (!await hrPayroll.EmployeeBelongsToLegalEntityAsync(tenant, request.EmployeeId.Value, order.LegalEntityId, token)) throw new ManufacturingException("manufacturing.employee_not_eligible");
            if (request.ProductionOperationId is { } operationId && !await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation where tenant_id=$1 and id=$2 and production_order_id=$3)", token, tenant.Value, operationId, orderId)) throw new ManufacturingException("manufacturing.operation_not_found");
            if (request.ResourceId is { } resourceId && !await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.manufacturing_resource r join business_manufacturing.production_order o on o.tenant_id=r.tenant_id and o.plant_id=r.plant_id where r.tenant_id=$1 and r.id=$2 and o.id=$3)", token, tenant.Value, resourceId, orderId)) throw new ManufacturingException("manufacturing.resource_not_found");
            var requestHash = Hash(request);
            var existingLaborId = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_labor_fact where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, request.IdempotencyKey);
            var factId = existingLaborId ?? Guid.CreateVersion7();
            if (existingLaborId is { } existing)
            {
                var storedHash = await ScalarAsync<string?>(context, "select request_hash from business_manufacturing.production_labor_fact where tenant_id=$1 and id=$2", token, tenant.Value, existing);
                if (storedHash is not null && !string.Equals(storedHash, requestHash, StringComparison.Ordinal)) throw new ManufacturingException("manufacturing.idempotency_conflict");
                if (storedHash is null) await ExecuteAsync(context, "update business_manufacturing.production_labor_fact set request_hash=$3 where tenant_id=$1 and id=$2", token, tenant.Value, existing, requestHash);
            }
            if (existingLaborId is null)
            {
                var amount = request.RateAmount * request.Minutes / 60m;
                await ExecuteAsync(context, "insert into business_manufacturing.production_labor_fact(tenant_id,id,production_order_id,production_operation_id,resource_id,employee_id,work_date,minutes,rate_amount,currency_code,source_ref,idempotency_key,request_hash) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", token, tenant.Value, factId, orderId, Db(request.ProductionOperationId), Db(request.ResourceId), request.EmployeeId.Value, request.WorkDate, request.Minutes, request.RateAmount, request.CurrencyCode.Trim().ToUpperInvariant(), request.SourceRef.Trim(), request.IdempotencyKey, requestHash);
                var costFactId = Guid.CreateVersion7();
                await ExecuteAsync(context, "insert into business_manufacturing.production_actual_cost_fact(tenant_id,id,production_order_id,source_type,source_id,cost_category,amount,currency_code,quantity,source_date,idempotency_key) values($1,$2,$3,'LaborFact',$4,'Labor',$5,$6,$7,$8,$9)", token, tenant.Value, costFactId, orderId, factId.ToString("D"), amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.Minutes, request.WorkDate, $"labor:{request.IdempotencyKey}");
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProductionCostFactRecordedV1", JsonSerializer.Serialize(new { productionOrderId = orderId, legalEntityId = order.LegalEntityId, costFactId, costCategory = "Labor", amount, currencyCode = request.CurrencyCode.Trim().ToUpperInvariant(), sourceDate = request.WorkDate, sourceReference = factId.ToString("D") }), tenant, DateTimeOffset.UtcNow, orderId, orderId, "manufacturing"));
                if (request.ProductionOperationId is { } laborOperationId) await ExecuteAsync(context, "update business_manufacturing.production_operation set actual_minutes=coalesce(actual_minutes,0)+$1,row_version=row_version+1 where tenant_id=$2 and id=$3", token, request.Minutes, tenant.Value, laborOperationId);
                await ExecuteAsync(context, "update business_manufacturing.production_order set cost_state='Accumulating',updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2", token, tenant.Value, orderId);
                await RefreshVarianceAsync(context, tenant.Value, orderId, token);
                await AuditAsync(context, tenant.Value, "ProductionLaborFact", factId, "recorded", actor, null, request, token);
            }
            return await GetProductionCostInContextAsync(context, tenant.Value, orderId, token);
        }, ct);

    public Task<IReadOnlyList<ProductionOperationResponse>> ListShopFloorQueueAsync(TenantId tenant, Guid plantId, DateOnly from, DateOnly to, string? state, int limit, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var rows = new List<ProductionOperationResponse>();
            await using var command = Command(context, "select p.id,p.production_order_id,p.sequence_no,p.name,p.work_center_id,p.state,p.completed_qty,p.actual_good_qty,p.actual_scrap_qty,p.actual_minutes,p.quality_gate_required,p.row_version from business_manufacturing.production_operation p join business_manufacturing.production_order o on o.tenant_id=p.tenant_id and o.id=p.production_order_id where p.tenant_id=$1 and o.plant_id=$2 and o.need_date between $3 and $4 and o.state in ('Released','InProgress','OnHold') and ($5::text is null or p.state=$5) order by o.need_date,p.sequence_no,p.id limit $6", tenant.Value, plantId, from, to, Db(state), Math.Clamp(limit, 1, 1000));
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(ReadOperation(reader));
            return (IReadOnlyList<ProductionOperationResponse>)rows;
        }, ct);

    public Task<QualityGateResponse> SetQualityGateAsync(TenantId tenant, Guid actor, Guid orderId, QualityGateDecisionRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.DecisionState is not ("Pending" or "Passed" or "Failed" or "Blocked")) throw new ManufacturingException("manufacturing.quality_gate_invalid");
            var legalEntityId = await ScalarAsync<Guid?>(context, "select legal_entity_id from business_manufacturing.production_order where tenant_id=$1 and id=$2", token, tenant.Value, orderId) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
            if (request.ProductionOperationId is { } operationId && !await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation where tenant_id=$1 and id=$2 and production_order_id=$3)", token, tenant.Value, operationId, orderId)) throw new ManufacturingException("manufacturing.operation_not_found");
            var capabilityAvailable = false; var decisionState = request.DecisionState; var evidence = request.SourceRef;
            if (request.DecisionState is "Passed" or "Failed")
            {
                if (qualityDecisions is null) throw new ManufacturingException("manufacturing.quality_provider_unavailable");
                var snapshot = await qualityDecisions.GetProductionDecisionAsync(tenant, legalEntityId, orderId, request.ProductionOperationId, token);
                if (snapshot is null || snapshot.ProductionOrderId != orderId || snapshot.ProductionOperationId != request.ProductionOperationId || snapshot.DecisionState != request.DecisionState || !snapshot.CapabilityAvailable || string.IsNullOrWhiteSpace(snapshot.EvidenceReference)) throw new ManufacturingException("manufacturing.quality_decision_unverified");
                capabilityAvailable = true; evidence = snapshot.EvidenceReference;
            }
            else if (request.DecisionState == "Pending")
            {
                if (request.Required && qualityDecisions is null) capabilityAvailable = false;
                else capabilityAvailable = request.CapabilityAvailable;
            }
            var gateId = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_quality_gate where tenant_id=$1 and production_order_id=$2 and production_operation_id is not distinct from $3::uuid", token, tenant.Value, orderId, Db(request.ProductionOperationId)) ?? Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.production_quality_gate(tenant_id,id,production_order_id,production_operation_id,required,capability_available,decision_state,source_ref,decided_at) values($1,$2,$3,$4,$5,$6,$7,$8,case when $7 in ('Passed','Failed') then now() else null end) on conflict (tenant_id,id) do update set required=excluded.required,capability_available=excluded.capability_available,decision_state=excluded.decision_state,source_ref=excluded.source_ref,decided_at=excluded.decided_at", token, tenant.Value, gateId, orderId, Db(request.ProductionOperationId), request.Required, capabilityAvailable, decisionState, Db(evidence));
            await ExecuteAsync(context, "update business_manufacturing.production_order set quality_state=case when exists(select 1 from business_manufacturing.production_quality_gate g where g.tenant_id=$1 and g.production_order_id=$2 and g.required and (g.decision_state in ('Failed','Blocked') or not g.capability_available)) then 'Blocked' when exists(select 1 from business_manufacturing.production_quality_gate g where g.tenant_id=$1 and g.production_order_id=$2 and g.required and g.decision_state='Pending') then 'Pending' when exists(select 1 from business_manufacturing.production_quality_gate g where g.tenant_id=$1 and g.production_order_id=$2 and g.required) then 'Passed' else 'NotRequired' end,updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2", token, tenant.Value, orderId);
            await AuditAsync(context, tenant.Value, "ProductionQualityGate", gateId, "decision_recorded", actor, null, request, token);
            return await ReadQualityGateAsync(context, tenant.Value, gateId, token) ?? throw new ManufacturingException("manufacturing.quality_gate_not_found");
        }, ct);

    public Task<IReadOnlyList<QualityGateResponse>> ListQualityGatesAsync(TenantId tenant, Guid orderId, int limit, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var result = new List<QualityGateResponse>();
            await using var command = Command(context, "select id,production_order_id,production_operation_id,required,capability_available,decision_state,source_ref from business_manufacturing.production_quality_gate where tenant_id=$1 and production_order_id=$2 order by id limit $3", tenant.Value, orderId, Math.Clamp(limit, 1, 1000));
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(ReadQualityGate(reader));
            return (IReadOnlyList<QualityGateResponse>)result;
        }, ct);

    public Task<SubcontractCommitmentResponse> CreateSubcontractCommitmentAsync(TenantId tenant, Guid actor, Guid orderId, CreateSubcontractCommitmentRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.Quantity <= 0 || request.CommittedAmount < 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.subcontract_invalid");
            var order = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
            if (request.ProductionOperationId is { } operationId && !await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation where tenant_id=$1 and id=$2 and production_order_id=$3)", token, tenant.Value, operationId, orderId)) throw new ManufacturingException("manufacturing.operation_not_found");
            if (!string.Equals(request.SourceModule, "purchasing", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(request.SourceDocumentId, out var purchaseOrderId)) throw new ManufacturingException("manufacturing.subcontract_source_unverified");
            if (request.CommittedAmount > 0m)
            {
                if (subcontractSources is null || request.CurrencyCode is null) throw new ManufacturingException("manufacturing.subcontract_source_unverified");
                var source = await subcontractSources.GetManufacturingSubcontractSourceAsync(tenant, order.LegalEntityId, purchaseOrderId, token);
                if (source is null || source.State is not ("Approved" or "Sent" or "Open" or "PartiallyCompleted") || source.AuthorizedAmount < request.CommittedAmount || !string.Equals(source.CurrencyCode, request.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase) || request.SupplierPartyId is not null && source.SupplierPartyId != request.SupplierPartyId) throw new ManufacturingException("manufacturing.subcontract_source_unverified");
            }
            var requestHash = Hash(request);
            var existingId = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_subcontract_commitment where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, request.IdempotencyKey);
            if (existingId is { } replayId)
            {
                var storedHash = await ScalarAsync<string?>(context, "select request_hash from business_manufacturing.production_subcontract_commitment where tenant_id=$1 and id=$2", token, tenant.Value, replayId);
                if (storedHash is not null && !string.Equals(storedHash, requestHash, StringComparison.Ordinal)) throw new ManufacturingException("manufacturing.idempotency_conflict");
                if (storedHash is null) await ExecuteAsync(context, "update business_manufacturing.production_subcontract_commitment set request_hash=$3 where tenant_id=$1 and id=$2", token, tenant.Value, replayId, requestHash);
                return await ReadSubcontractCommitmentAsync(context, tenant.Value, replayId, token) ?? throw new ManufacturingException("manufacturing.subcontract_not_found");
            }
            var id = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.production_subcontract_commitment(tenant_id,id,production_order_id,production_operation_id,source_module,source_document_id,supplier_party_id,quantity,uom_code,expected_date,committed_amount,currency_code,idempotency_key,request_hash) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, id, orderId, Db(request.ProductionOperationId), request.SourceModule.Trim().ToLowerInvariant(), request.SourceDocumentId.Trim(), Db(request.SupplierPartyId), request.Quantity, request.UomCode.Trim(), request.ExpectedDate, request.CommittedAmount, Db(request.CurrencyCode?.Trim().ToUpperInvariant()), request.IdempotencyKey, requestHash);
            if (request.CommittedAmount > 0 && request.CurrencyCode is not null)
            {
                var costFactId = Guid.CreateVersion7();
                await ExecuteAsync(context, "insert into business_manufacturing.production_actual_cost_fact(tenant_id,id,production_order_id,source_type,source_id,cost_category,amount,currency_code,source_date,idempotency_key) values($1,$2,$3,'PurchaseOrder',$4,'Subcontract',$5,$6,$7,$8)", token, tenant.Value, costFactId, orderId, request.SourceDocumentId, request.CommittedAmount, request.CurrencyCode.Trim().ToUpperInvariant(), request.ExpectedDate, $"subcontract:{request.IdempotencyKey}");
                // The purchasing document remains the commercial source, but the
                // ledger fact identity must be unique per immutable cost fact.
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProductionCostFactRecordedV1", JsonSerializer.Serialize(new { productionOrderId = orderId, legalEntityId = order.LegalEntityId, costFactId, costCategory = "Subcontract", amount = request.CommittedAmount, currencyCode = request.CurrencyCode.Trim().ToUpperInvariant(), sourceDate = request.ExpectedDate, sourceReference = costFactId.ToString("D") }), tenant, DateTimeOffset.UtcNow, orderId, orderId, "manufacturing"));
            }
            await ExecuteAsync(context, "update business_manufacturing.production_order set cost_state=case when $3 > 0 then 'Accumulating' else cost_state end,updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2", token, tenant.Value, orderId, request.CommittedAmount);
            await RefreshVarianceAsync(context, tenant.Value, orderId, token);
            await AuditAsync(context, tenant.Value, "ProductionSubcontractCommitment", id, "created", actor, null, request, token);
            return await ReadSubcontractCommitmentAsync(context, tenant.Value, id, token) ?? throw new ManufacturingException("manufacturing.subcontract_not_found");
        }, ct);

    public Task<MpsPlanResponse> CreateMpsPlanAsync(TenantId tenant, Guid actor, CreateMpsPlanRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.Quantity <= 0 || request.BucketEnd < request.BucketStart) throw new ManufacturingException("manufacturing.mps_invalid");
            var version = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version),0)+1 from business_manufacturing.mps_plan where tenant_id=$1 and legal_entity_id=$2 and plant_id=$3 and product_variant_id=$4 and bucket_start=$5", token, tenant.Value, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.BucketStart), System.Globalization.CultureInfo.InvariantCulture);
            await ExecuteAsync(context, "update business_manufacturing.mps_plan set state='Superseded' where tenant_id=$1 and legal_entity_id=$2 and plant_id=$3 and product_variant_id=$4 and bucket_start=$5 and state in ('Draft','Approved')", token, tenant.Value, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.BucketStart);
            var id = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.mps_plan(tenant_id,id,legal_entity_id,plant_id,product_variant_id,bucket_start,bucket_end,quantity,uom_code,state,source_ref,version,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',$10,$11,$12)", token, tenant.Value, id, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.BucketStart, request.BucketEnd, request.Quantity, request.UomCode, Db(request.SourceRef), version, actor);
            return new MpsPlanResponse(id, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.BucketStart, request.BucketEnd, request.Quantity, request.UomCode, "Draft", request.SourceRef, version);
        }, ct);

    public Task<MpsPlanResponse> PublishMpsPlanAsync(TenantId tenant, Guid actor, Guid planId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var plan = await ReadMpsPlanAsync(context, tenant.Value, planId, token) ?? throw new ManufacturingException("manufacturing.mps_not_found");
            if (plan.State == "Approved") return plan;
            if (plan.State != "Draft") throw new ManufacturingException("manufacturing.mps_transition_invalid");
            await ExecuteAsync(context, "update business_manufacturing.mps_plan set state='Approved' where tenant_id=$1 and id=$2 and state='Draft'", token, tenant.Value, planId);
            await ExecuteAsync(context, "update business_manufacturing.mps_plan set state='Superseded' where tenant_id=$1 and legal_entity_id=$2 and plant_id=$3 and product_variant_id=$4 and bucket_start=$5 and id<>$6 and state='Approved'", token, tenant.Value, plan.LegalEntityId, plan.PlantId, plan.ProductVariantId, plan.BucketStart, planId);
            return await ReadMpsPlanAsync(context, tenant.Value, planId, token) ?? throw new ManufacturingException("manufacturing.mps_not_found");
        }, ct);

    public Task<IReadOnlyList<MpsPlanResponse>> ListMpsPlansAsync(TenantId tenant, Guid legalEntityId, Guid? plantId, Guid? productVariantId, DateOnly? from, DateOnly? to, int limit, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var result = new List<MpsPlanResponse>();
            await using var command = Command(context, "select id,legal_entity_id,plant_id,product_variant_id,bucket_start,bucket_end,quantity,uom_code,state,source_ref,version from business_manufacturing.mps_plan where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or plant_id=$3) and ($4::uuid is null or product_variant_id=$4) and ($5::date is null or bucket_end >= $5) and ($6::date is null or bucket_start <= $6) order by bucket_start,product_variant_id,version desc limit $7", tenant.Value, legalEntityId, Db(plantId), Db(productVariantId), Db(from), Db(to), Math.Clamp(limit, 1, 1000));
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(ReadMpsPlan(reader));
            return (IReadOnlyList<MpsPlanResponse>)result;
        }, ct);

    private static async Task<MpsPlanResponse?> ReadMpsPlanAsync(ITransactionContext context, Guid tenant, Guid planId, CancellationToken token)
    {
        await using var command = Command(context, "select id,legal_entity_id,plant_id,product_variant_id,bucket_start,bucket_end,quantity,uom_code,state,source_ref,version from business_manufacturing.mps_plan where tenant_id=$1 and id=$2", tenant, planId);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? ReadMpsPlan(reader) : null;
    }

    private static MpsPlanResponse ReadMpsPlan(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetFieldValue<DateOnly>(4), reader.GetFieldValue<DateOnly>(5), reader.GetDecimal(6), reader.GetString(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetInt32(10));

    public Task<OutputCostAllocationResponse> AllocateOutputCostAsync(TenantId tenant, Guid actor, Guid orderId, AllocateOutputCostRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.AllocatedAmount < 0 || request.AllocationValue < 0 || request.AllocationBasis is not ("FixedValue" or "Percentage" or "Residual") || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.output_allocation_invalid");
            if (await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_output where tenant_id=$1 and id=$2 and production_order_id=$3", token, tenant.Value, request.OutputId, orderId) is null) throw new ManufacturingException("manufacturing.output_not_found");
            var id = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_cost_allocation where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, request.IdempotencyKey) ?? Guid.CreateVersion7();
            var currency = request.CurrencyCode.Trim().ToUpperInvariant();
            var total = await ScalarAsync<decimal>(context, "select coalesce(sum(amount) filter (where currency_code=$3),0) from business_manufacturing.production_actual_cost_fact where tenant_id=$1 and production_order_id=$2", token, tenant.Value, orderId, currency);
            var alreadyAllocated = await ScalarAsync<decimal>(context, "select coalesce(sum(allocated_amount),0) from business_manufacturing.production_cost_allocation where tenant_id=$1 and production_order_id=$2 and currency_code=$3 and idempotency_key<>$4", token, tenant.Value, orderId, currency, request.IdempotencyKey);
            var allocatedAmount = request.AllocatedAmount;
            if (request.AllocationBasis == "Percentage")
            {
                if (request.AllocationValue > 100) throw new ManufacturingException("manufacturing.output_allocation_invalid");
                allocatedAmount = decimal.Round(total * request.AllocationValue / 100m, 10, MidpointRounding.AwayFromZero);
            }
            else if (request.AllocationBasis == "FixedValue" && request.AllocationValue != request.AllocatedAmount)
                throw new ManufacturingException("manufacturing.output_allocation_invalid");
            else if (request.AllocationBasis == "Residual")
                allocatedAmount = Math.Max(0m, total - alreadyAllocated);
            if (alreadyAllocated + allocatedAmount > total + 0.00000001m) throw new ManufacturingException("manufacturing.output_allocation_exceeds_cost");
            await ExecuteAsync(context, "insert into business_manufacturing.production_cost_allocation(tenant_id,id,production_order_id,output_id,allocation_basis,allocation_value,allocated_amount,currency_code,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict (tenant_id,idempotency_key) do nothing", token, tenant.Value, id, orderId, request.OutputId, request.AllocationBasis, request.AllocationValue, allocatedAmount, currency, request.IdempotencyKey);
            await AuditAsync(context, tenant.Value, "ProductionCostAllocation", id, "created", actor, null, request, token);
            return new OutputCostAllocationResponse(id, orderId, request.OutputId, request.AllocationBasis, request.AllocationValue, allocatedAmount, currency, request.IdempotencyKey);
        }, ct);

    public Task<ManufacturingProjectContext?> GetProjectContextAsync(TenantId tenant, Guid orderId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = Command(context, "select project_id,project_task_id,legal_entity_id,need_date from business_manufacturing.production_order where tenant_id=$1 and id=$2", tenant.Value, orderId);
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? new ManufacturingProjectContext(reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3)) : null;
        }, ct);

    private static async Task<QualityGateResponse?> ReadQualityGateAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        await using var command = Command(context, "select id,production_order_id,production_operation_id,required,capability_available,decision_state,source_ref from business_manufacturing.production_quality_gate where tenant_id=$1 and id=$2", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? ReadQualityGate(reader) : null;
    }

    private static QualityGateResponse ReadQualityGate(Npgsql.NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6));

    private static async Task<SubcontractCommitmentResponse?> ReadSubcontractCommitmentAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        await using var command = Command(context, "select id,production_order_id,production_operation_id,source_module,source_document_id,quantity,uom_code,expected_date,state,committed_amount,currency_code from business_manufacturing.production_subcontract_commitment where tenant_id=$1 and id=$2", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetFieldValue<DateOnly>(7), reader.GetString(8), reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetString(10).Trim()) : null;
    }

    private static async Task RefreshVarianceAsync(ITransactionContext context, Guid tenant, Guid orderId, CancellationToken token)
    {
        decimal plannedMaterial = 0m, plannedLabor = 0m, plannedMachine = 0m, plannedOverhead = 0m, plannedSubcontract = 0m;
        string currency = "EGP";
        await using (var planned = Command(context, "select currency_code,material_cost,labor_cost,machine_cost,overhead_cost,subcontract_cost from business_manufacturing.production_planned_cost_snapshot where tenant_id=$1 and production_order_id=$2", tenant, orderId))
        await using (var reader = await planned.ExecuteReaderAsync(token))
        {
            if (await reader.ReadAsync(token))
            {
                currency = reader.GetString(0).Trim();
                plannedMaterial = reader.GetDecimal(1);
                plannedLabor = reader.GetDecimal(2);
                plannedMachine = reader.GetDecimal(3);
                plannedOverhead = reader.GetDecimal(4);
                plannedSubcontract = reader.GetDecimal(5);
            }
        }

        decimal actualMaterial = 0m, actualLabor = 0m, actualMachine = 0m, actualOverhead = 0m, actualSubcontract = 0m;
        await using (var actual = Command(context, "select coalesce(sum(amount) filter(where cost_category='Material'),0),coalesce(sum(amount) filter(where cost_category='Labor'),0),coalesce(sum(amount) filter(where cost_category='Machine'),0),coalesce(sum(amount) filter(where cost_category='Overhead'),0),coalesce(sum(amount) filter(where cost_category='Subcontract'),0),coalesce((select max(rtrim(currency_code)) from business_manufacturing.production_actual_cost_fact where tenant_id=$1 and production_order_id=$2),$3) from business_manufacturing.production_actual_cost_fact where tenant_id=$1 and production_order_id=$2", tenant, orderId, currency))
        await using (var reader = await actual.ExecuteReaderAsync(token))
        {
            if (await reader.ReadAsync(token))
            {
                actualMaterial = reader.GetDecimal(0);
                actualLabor = reader.GetDecimal(1);
                actualMachine = reader.GetDecimal(2);
                actualOverhead = reader.GetDecimal(3);
                actualSubcontract = reader.GetDecimal(4);
                currency = reader.GetString(5).Trim();
            }
        }

        foreach (var item in new[]
        {
            (Category: "MaterialUsage", Planned: plannedMaterial, Actual: actualMaterial),
            (Category: "LaborEfficiency", Planned: plannedLabor, Actual: actualLabor),
            (Category: "MachineEfficiency", Planned: plannedMachine, Actual: actualMachine),
            (Category: "Overhead", Planned: plannedOverhead, Actual: actualOverhead),
            (Category: "Subcontract", Planned: plannedSubcontract, Actual: actualSubcontract)
        })
        {
            var varianceId = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_variance where tenant_id=$1 and production_order_id=$2 and category=$3 order by created_at desc,id desc limit 1", token, tenant, orderId, item.Category) ?? Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.production_variance(tenant_id,id,production_order_id,category,planned_amount,actual_amount,variance_amount,currency_code,reason) values($1,$2,$3,$4,$5,$6,$7,$8,'Derived from the immutable planned snapshot and actual cost facts') on conflict (tenant_id,id) do update set planned_amount=excluded.planned_amount,actual_amount=excluded.actual_amount,variance_amount=excluded.variance_amount,currency_code=excluded.currency_code,reason=excluded.reason,created_at=now()", token, tenant, varianceId, orderId, item.Category, item.Planned, item.Actual, item.Actual - item.Planned, currency);
        }
    }
}
