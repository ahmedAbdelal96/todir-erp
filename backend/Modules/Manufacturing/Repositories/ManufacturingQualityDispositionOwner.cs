using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using Npgsql;
using Shared.Contracts.Quality;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Repositories;

/// <summary>Manufacturing-owned side effect for a Quality rework disposition.</summary>
public sealed class ManufacturingQualityDispositionOwner(ITransactionCoordinator coordinator) : IManufacturingQualityDispositionCapabilities
{
    public async Task<QualityDispositionOwnerResult> ExecuteReworkAsync(Guid tenantId, Guid actorId,
        QualityReworkRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await coordinator.ExecuteAsync(new TenantId(tenantId), async (context, token) =>
            {
                var order = await ReadSingleAsync(context, "select legal_entity_id,state,product_variant_id from business_manufacturing.production_order where tenant_id=$1 and id=$2 for update", token, tenantId, request.ProductionOrderId)
                    ?? throw new InvalidOperationException("quality.rework_production_order_not_found");
                if ((Guid)order[0] != request.LegalEntityId) throw new InvalidOperationException("quality.rework_legal_entity_mismatch");
                var existing = await ScalarAsync<Guid>(context, "select id from business_manufacturing.production_external_reference where tenant_id=$1 and production_order_id=$2 and source_module='quality' and source_type='Disposition' and source_id=$3 and purpose='Rework'", token, tenantId, request.ProductionOrderId, request.DispositionId.ToString("D"));
                if (existing != Guid.Empty) return new QualityDispositionOwnerResult(existing, "Confirmed", true);
                var operationId = request.ProductionOperationId ?? throw new InvalidOperationException("quality.rework_operation_required");
                var operation = await ReadSingleAsync(context, "select id,state from business_manufacturing.production_operation where tenant_id=$1 and production_order_id=$2 and id=$3 for update", token, tenantId, request.ProductionOrderId, operationId)
                    ?? throw new InvalidOperationException("quality.rework_operation_not_found");
                if ((string)operation[1] == "Complete") throw new InvalidOperationException("quality.rework_operation_complete");
                var referenceId = Guid.CreateVersion7();
                await ExecuteAsync(context, "insert into business_manufacturing.production_external_reference(tenant_id,id,production_order_id,source_module,source_type,source_id,purpose,snapshot) values($1,$2,$3,'quality','Disposition',$4,'Rework',$5::jsonb)", token, tenantId, referenceId, request.ProductionOrderId, request.DispositionId.ToString("D"), JsonSerializer.Serialize(new { request.Quantity, request.Reason, request.ProductionOperationId }));
                await ExecuteAsync(context, "insert into business_manufacturing.production_pause(tenant_id,id,production_operation_id,reason,started_at,notes,actor_id) values($1,$2,$3,'QualityHold',now(),$4,$5)", token, tenantId, Guid.CreateVersion7(), operationId, $"Quality rework disposition {request.DispositionId:D}: {request.Reason.Trim()}", actorId);
                await ExecuteAsync(context, "update business_manufacturing.production_operation set state='Blocked',row_version=row_version+1 where tenant_id=$1 and id=$2 and state <> 'Complete'", token, tenantId, operationId);
                await ExecuteAsync(context, "update business_manufacturing.production_order set state='OnHold',quality_state='Blocked',operation_state='Blocked',updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2 and state not in ('Completed','Closed','Cancelled')", token, tenantId, request.ProductionOrderId);
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityReworkRequestedV1", JsonSerializer.Serialize(new { dispositionId = request.DispositionId, productionOrderId = request.ProductionOrderId, productionOperationId = operationId, request.Quantity }), new TenantId(tenantId), DateTimeOffset.UtcNow, Guid.CreateVersion7(), referenceId, "manufacturing"));
                return new QualityDispositionOwnerResult(referenceId, "Confirmed", false);
            }, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new QualityDispositionOwnerException(exception.Message, false, exception);
        }
    }

    private static async Task<object[]?> ReadSingleAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values)
    { await using var command = Command(context, sql, values); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray() : null; }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken token, params object?[] values) where T : struct
    { await using var command = Command(context, sql, values); var value = await command.ExecuteScalarAsync(token); return value is DBNull or null ? default : (T)value; }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteNonQueryAsync(token); }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); return command; }
}
