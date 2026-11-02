using Core.Services;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace DbMigrator;

internal static class HrProjectionRebuilder
{
    public static async Task<int> RunAsync(NpgsqlConnection connection, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using (var privilege = new NpgsqlCommand("select r.rolsuper or r.rolbypassrls from pg_roles r where r.rolname=current_user", connection))
            if (await privilege.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("HR projection rebuild requires a privileged maintenance database role.");

        var correlationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var validate = new NpgsqlCommand("select exists(select 1 from platform.tenant where id=$1)", connection, transaction))
        {
            validate.Parameters.AddWithValue(tenantId);
            if (await validate.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("The requested tenant does not exist.");
        }
        await using (var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue($"hr-projection-rebuild:{tenantId:D}");
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var sourceLocks = new NpgsqlCommand("lock table business_hr.employee, business_hr.employment_event in share mode", connection, transaction))
            await sourceLocks.ExecuteNonQueryAsync(cancellationToken);

        await using (var deleteTimeline = new NpgsqlCommand("delete from business_hr.employee_timeline_projection where tenant_id=$1", connection, transaction))
        {
            deleteTimeline.Parameters.AddWithValue(tenantId);
            await deleteTimeline.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var rebuildTimeline = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) select tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload from business_hr.employment_event where tenant_id=$1", connection, transaction))
        {
            rebuildTimeline.Parameters.AddWithValue(tenantId);
            await rebuildTimeline.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteList = new NpgsqlCommand("delete from business_hr.employee_list_projection where tenant_id=$1", connection, transaction))
        {
            deleteList.Parameters.AddWithValue(tenantId);
            await deleteList.ExecuteNonQueryAsync(cancellationToken);
        }
        int rebuiltEmployees;
        await using (var rebuildList = new NpgsqlCommand("insert into business_hr.employee_list_projection(tenant_id,employee_id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,projected_at) select tenant_id,id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,$2 from business_hr.employee where tenant_id=$1", connection, transaction))
        {
            rebuildList.Parameters.AddWithValue(tenantId);
            rebuildList.Parameters.AddWithValue(now);
            rebuiltEmployees = await rebuildList.ExecuteNonQueryAsync(cancellationToken);
        }

        await NpgsqlAuditWriter.WriteAsync(connection, transaction,
            new AuditEntry(Guid.CreateVersion7(), new TenantId(tenantId), null, null, "System", null, null, "hr", "hr.projections.rebuilt", "projection_rebuild", Guid.CreateVersion7(),
                null, System.Text.Json.JsonSerializer.Serialize(new { rebuiltEmployees }), null, "db-migrator", correlationId, null, now, null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rebuiltEmployees;
    }
}
