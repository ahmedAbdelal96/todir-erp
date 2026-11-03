using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrDocumentRequirementRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    public async Task<RequiredDocumentRuleView> SaveAsync(TenantId tenant, Guid actor, Guid membershipId,
        SaveRequiredDocumentRuleRequest request, Guid correlationId, CancellationToken ct)
    {
        var category = request.Category?.Trim() ?? string.Empty;
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (request.LegalEntityId == Guid.Empty || category.Length is < 1 or > 80 || request.ExpectedVersion < 0
            || reason.Length is < 1 or > 500)
            throw new HrException("hr.document_requirement_invalid");

        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<RequiredDocumentRuleView>(tenant, async (context, token) =>
        {
            const string sql = """
                insert into business_hr.employee_document_requirement(id,tenant_id,legal_entity_id,category,category_key,enabled,created_by,updated_by,updated_at,version)
                values($1,$2,$3,$4,upper($4),$5,$6,$6,$7,1)
                on conflict (tenant_id,legal_entity_id,category_key) do update
                set category=excluded.category,enabled=excluded.enabled,updated_by=excluded.updated_by,
                    updated_at=excluded.updated_at,version=business_hr.employee_document_requirement.version+1
                where business_hr.employee_document_requirement.version=$8
                returning id,legal_entity_id,category,enabled,version
                """;
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(request.LegalEntityId);
            command.Parameters.AddWithValue(category); command.Parameters.AddWithValue(request.Enabled); command.Parameters.AddWithValue(actor);
            command.Parameters.AddWithValue(clock.GetUtcNow()); command.Parameters.AddWithValue(request.ExpectedVersion);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.document_requirement_concurrency_conflict");
            var result = new RequiredDocumentRuleView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetBoolean(3), reader.GetInt64(4));
            await reader.CloseAsync();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr",
                    "employee.document_requirement_saved", "employee_document_requirement", result.Id, null,
                    JsonSerializer.Serialize(new { result.Category, result.Enabled, result.Version, reason }), null,
                    "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return result;
        }, ct);
    }

    public async Task<IReadOnlyList<RequiredDocumentRuleView>> ListAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<IReadOnlyList<RequiredDocumentRuleView>>(tenant, async (context, token) =>
        {
            var result = new List<RequiredDocumentRuleView>();
            await using var command = new NpgsqlCommand("select id,legal_entity_id,category,enabled,version from business_hr.employee_document_requirement where tenant_id=$1 and legal_entity_id=$2 order by category_key,id", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetBoolean(3), reader.GetInt64(4)));
            return result;
        }, ct);

    public async Task<EmployeeFileCompletenessView> GetCompletenessAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<EmployeeFileCompletenessView>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("""
                with scoped_employee as (
                    select employee.id from business_hr.employee employee
                    where employee.tenant_id=$1 and employee.id=$2 and exists(
                        select 1 from business_hr.employment employment where employment.tenant_id=employee.tenant_id
                            and employment.employee_id=employee.id and employment.legal_entity_id=$3)
                ), missing as (
                    select requirement.category from business_hr.employee_document_requirement requirement
                    cross join scoped_employee employee
                    where requirement.tenant_id=$1 and requirement.legal_entity_id=$3 and requirement.enabled
                        and not exists(select 1 from business_hr.employee_document document
                            where document.tenant_id=$1 and document.employee_id=employee.id
                                and upper(document.category)=requirement.category_key and document.status='Active' and document.scope_status='Resolved'
                                and document.verification_status='Verified'
                                and (document.legal_entity_id is null or document.legal_entity_id=$3)
                                and (document.employment_id is null or exists(select 1 from business_hr.employment scoped where scoped.tenant_id=document.tenant_id and scoped.id=document.employment_id and scoped.legal_entity_id=$3))
                                and (document.expiry_date is null or document.expiry_date>=current_date))
                ), expiring as (
                    select count(*)::integer as total from business_hr.employee_document document
                    cross join scoped_employee employee
                    where document.tenant_id=$1 and document.employee_id=employee.id and document.status='Active' and document.scope_status='Resolved'
                        and (document.legal_entity_id is null or document.legal_entity_id=$3)
                        and document.expiry_date between current_date and current_date+30
                        and (document.employment_id is null or exists(select 1 from business_hr.employment scoped where scoped.tenant_id=document.tenant_id and scoped.id=document.employment_id and scoped.legal_entity_id=$3))
                )
                select employee.id,coalesce((select array_agg(missing.category order by missing.category) from missing),'{}'::text[]),
                    (select count(*)::integer from missing),expiring.total
                from scoped_employee employee cross join expiring
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.employee_not_found_or_outside_entity");
            var missing = reader.GetFieldValue<string[]>(1);
            var count = reader.GetInt32(2);
            return new EmployeeFileCompletenessView(reader.GetGuid(0), count == 0 ? "Complete" : "Incomplete", count, missing, reader.GetInt32(3));
        }, ct);
}
