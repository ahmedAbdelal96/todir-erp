using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class OnboardingRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<OnboardingTemplateView> CreateTemplateAsync(TenantId tenant,Guid actor,Guid membershipId,CreateOnboardingTemplateRequest request,Guid correlationId,CancellationToken ct)
    {
        var code=request.Code?.Trim().ToUpperInvariant();var name=request.NameAr?.Trim();var nameEn=string.IsNullOrWhiteSpace(request.NameEn)?null:request.NameEn.Trim();var tasks=request.Tasks??[];
        if(request.LegalEntityId==Guid.Empty||string.IsNullOrWhiteSpace(code)||code.Length>60||string.IsNullOrWhiteSpace(name)||name.Length>160||nameEn?.Length>160||tasks.Count is <1 or >40
            ||tasks.Any(t=>string.IsNullOrWhiteSpace(t.Code)||t.Code.Trim().Length>60||string.IsNullOrWhiteSpace(t.TitleAr)||t.TitleAr.Trim().Length>160||t.TitleEn?.Trim().Length>160||t.DueDaysFromStart is < -90 or >365)
            ||tasks.Select(t=>t.Code.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count()!=tasks.Count)
            throw new HrException("hr.onboarding_template_invalid");
        return await transactions.ExecuteAsync<OnboardingTemplateView>(tenant,async(context,token)=>
        {
            var id=Guid.CreateVersion7();var now=clock.GetUtcNow();
            await using(var insert=new NpgsqlCommand("insert into business_hr.onboarding_template(id,tenant_id,legal_entity_id,code,name_ar,name_en,version,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,1,'Active',$7,$8)",context.Connection,context.Transaction))
            {insert.Parameters.AddWithValue(id);insert.Parameters.AddWithValue(tenant.Value);insert.Parameters.AddWithValue(request.LegalEntityId);insert.Parameters.AddWithValue(code);insert.Parameters.AddWithValue(name);insert.Parameters.AddWithValue((object?)nameEn??DBNull.Value);insert.Parameters.AddWithValue(actor);insert.Parameters.AddWithValue(now);await insert.ExecuteNonQueryAsync(token);}
            for(var index=0;index<tasks.Count;index++)
            {var task=tasks[index];await using var insertTask=new NpgsqlCommand("insert into business_hr.onboarding_template_task(id,tenant_id,template_id,code,title_ar,title_en,is_required,due_days_from_start,sort_order) values($1,$2,$3,$4,$5,$6,$7,$8,$9)",context.Connection,context.Transaction);insertTask.Parameters.AddWithValue(Guid.CreateVersion7());insertTask.Parameters.AddWithValue(tenant.Value);insertTask.Parameters.AddWithValue(id);insertTask.Parameters.AddWithValue(task.Code.Trim().ToUpperInvariant());insertTask.Parameters.AddWithValue(task.TitleAr.Trim());insertTask.Parameters.AddWithValue((object?)(string.IsNullOrWhiteSpace(task.TitleEn)?null:task.TitleEn.Trim())??DBNull.Value);insertTask.Parameters.AddWithValue(task.IsRequired);insertTask.Parameters.AddWithValue((object?)task.DueDaysFromStart??DBNull.Value);insertTask.Parameters.AddWithValue((short)index);await insertTask.ExecuteNonQueryAsync(token);}
            await NpgsqlAuditWriter.WriteAsync(context.Connection,context.Transaction,new AuditEntry(Guid.CreateVersion7(),tenant,request.LegalEntityId,null,"Human",actor,membershipId,"hr","hr.onboarding_template.created","onboarding_template",id,null,JsonSerializer.Serialize(new { code,name,taskCount=tasks.Count,requiredCount=tasks.Count(t=>t.IsRequired) }),null,"hr-api",correlationId,System.Diagnostics.Activity.Current?.TraceId.ToString(),now,null),token);
            return new(id,request.LegalEntityId,code,name,nameEn,1,tasks.Count);
        },ct);
    }

    public async Task<IReadOnlyList<OnboardingTemplateView>> ListTemplatesAsync(TenantId tenant,Guid legalEntityId,CancellationToken ct)
    {
        await using var connection=await dataSource.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);await SetTenantAsync(connection,tx,tenant,ct);
        await using var command=new NpgsqlCommand("select t.id,t.legal_entity_id,t.code,t.name_ar,t.name_en,t.version,(select count(*)::int from business_hr.onboarding_template_task x where x.tenant_id=t.tenant_id and x.template_id=t.id) from business_hr.onboarding_template t where t.tenant_id=$1 and t.legal_entity_id=$2 and t.status='Active' order by t.code,t.id limit 100",connection,tx);command.Parameters.AddWithValue(tenant.Value);command.Parameters.AddWithValue(legalEntityId);var rows=new List<OnboardingTemplateView>();await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.GetInt32(5),reader.GetInt32(6)));await reader.CloseAsync();await tx.CommitAsync(ct);return rows;
    }

    public async Task<OnboardingCaseView> CreateCaseAsync(TenantId tenant,Guid actor,Guid membershipId,Guid employeeId,Guid employmentId,CreateOnboardingCaseRequest request,Guid correlationId,CancellationToken ct)=>
        await transactions.ExecuteAsync<OnboardingCaseView>(tenant,async(context,token)=>
        {
            if(request.TargetStartDate==default)throw new HrException("hr.onboarding_start_date_invalid");
            await using(var employment=new NpgsqlCommand("select 1 from business_hr.employment where tenant_id=$1 and id=$2 and employee_id=$3 and legal_entity_id=$4 and status='PreHire'",context.Connection,context.Transaction))
            {employment.Parameters.AddWithValue(tenant.Value);employment.Parameters.AddWithValue(employmentId);employment.Parameters.AddWithValue(employeeId);employment.Parameters.AddWithValue(request.LegalEntityId);if(await employment.ExecuteScalarAsync(token)is null)throw new HrException("hr.prehire_employment_required");}
            var caseId=Guid.CreateVersion7();var now=clock.GetUtcNow();var status=request.TemplateId is null?"Completed":"Open";
            if(request.TemplateId is { } templateId)
            {await using var templateCheck=new NpgsqlCommand("select 1 from business_hr.onboarding_template where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status='Active'",context.Connection,context.Transaction);templateCheck.Parameters.AddWithValue(tenant.Value);templateCheck.Parameters.AddWithValue(templateId);templateCheck.Parameters.AddWithValue(request.LegalEntityId);if(await templateCheck.ExecuteScalarAsync(token)is null)throw new HrException("hr.onboarding_template_not_found");}
            await using(var insert=new NpgsqlCommand("insert into business_hr.onboarding_case(id,tenant_id,legal_entity_id,employee_id,employment_id,template_id,target_start_date,status,created_by,created_at,completed_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",context.Connection,context.Transaction))
            {insert.Parameters.AddWithValue(caseId);insert.Parameters.AddWithValue(tenant.Value);insert.Parameters.AddWithValue(request.LegalEntityId);insert.Parameters.AddWithValue(employeeId);insert.Parameters.AddWithValue(employmentId);insert.Parameters.AddWithValue((object?)request.TemplateId??DBNull.Value);insert.Parameters.AddWithValue(request.TargetStartDate);insert.Parameters.AddWithValue(status);insert.Parameters.AddWithValue(actor);insert.Parameters.AddWithValue(now);insert.Parameters.AddWithValue(status=="Completed"?now:DBNull.Value);await insert.ExecuteNonQueryAsync(token);}
            if(request.TemplateId is { } template)
            {
                await using var copy=new NpgsqlCommand("insert into business_hr.onboarding_task(id,tenant_id,legal_entity_id,case_id,code,title_ar,title_en,is_required,due_date,status) select gen_random_uuid(),tenant_id,$3,$4,code,title_ar,title_en,is_required,case when due_days_from_start is null then null else $5+due_days_from_start end,'Pending' from business_hr.onboarding_template_task where tenant_id=$1 and template_id=$2 order by sort_order",context.Connection,context.Transaction);copy.Parameters.AddWithValue(tenant.Value);copy.Parameters.AddWithValue(template);copy.Parameters.AddWithValue(request.LegalEntityId);copy.Parameters.AddWithValue(caseId);copy.Parameters.AddWithValue(request.TargetStartDate);await copy.ExecuteNonQueryAsync(token);
                await using var events=new NpgsqlCommand("insert into business_hr.onboarding_task_event(id,tenant_id,task_id,event_type,from_status,to_status,actor_id,occurred_at) select gen_random_uuid(),tenant_id,id,'Created',null,'Pending',$2,$3 from business_hr.onboarding_task where tenant_id=$1 and case_id=$4",context.Connection,context.Transaction);events.Parameters.AddWithValue(tenant.Value);events.Parameters.AddWithValue(actor);events.Parameters.AddWithValue(now);events.Parameters.AddWithValue(caseId);await events.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection,context.Transaction,new AuditEntry(Guid.CreateVersion7(),tenant,request.LegalEntityId,null,"Human",actor,membershipId,"hr","hr.onboarding_case.created","onboarding_case",caseId,null,JsonSerializer.Serialize(new { employeeId,employmentId,request.TemplateId,request.TargetStartDate,status }),null,"hr-api",correlationId,System.Diagnostics.Activity.Current?.TraceId.ToString(),now,null),token);
            var tasks=await ReadTasksAsync(context.Connection,context.Transaction,tenant,caseId,token);return new(caseId,employeeId,employmentId,request.TemplateId,request.TargetStartDate,status,tasks);
        },ct);

    public async Task<OnboardingCaseView?> GetCaseAsync(TenantId tenant,Guid employmentId,Guid legalEntityId,CancellationToken ct)
    {
        await using var connection=await dataSource.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);await SetTenantAsync(connection,tx,tenant,ct);
        await using var command=new NpgsqlCommand("select id,employee_id,employment_id,template_id,target_start_date,status from business_hr.onboarding_case where tenant_id=$1 and legal_entity_id=$2 and employment_id=$3",connection,tx);command.Parameters.AddWithValue(tenant.Value);command.Parameters.AddWithValue(legalEntityId);command.Parameters.AddWithValue(employmentId);await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct)){await reader.CloseAsync();await tx.CommitAsync(ct);return null;}var id=reader.GetGuid(0);var employee=reader.GetGuid(1);var employment=reader.GetGuid(2);Guid? template=reader.IsDBNull(3)?null:reader.GetGuid(3);var start=reader.GetFieldValue<DateOnly>(4);var status=reader.GetString(5);await reader.CloseAsync();var tasks=await ReadTasksAsync(connection,tx,tenant,id,ct);await tx.CommitAsync(ct);return new(id,employee,employment,template,start,status,tasks);
    }

    public async Task<OnboardingTaskView> CompleteTaskAsync(TenantId tenant,Guid actor,Guid membershipId,Guid taskId,Guid legalEntityId,CompleteOnboardingTaskRequest request,Guid correlationId,CancellationToken ct)=>
        await transactions.ExecuteAsync<OnboardingTaskView>(tenant,async(context,token)=>
        {
            if(request.ExpectedVersion<1||request.Note?.Length>500)throw new HrException("hr.onboarding_task_invalid");var now=clock.GetUtcNow();string code;string title;string? titleEn;bool required;DateOnly? due;string status;long version;
            await using(var read=new NpgsqlCommand("select t.code,t.title_ar,t.title_en,t.is_required,t.due_date,t.status,t.version from business_hr.onboarding_task t where t.tenant_id=$1 and t.legal_entity_id=$2 and t.id=$3 for update",context.Connection,context.Transaction))
            {read.Parameters.AddWithValue(tenant.Value);read.Parameters.AddWithValue(legalEntityId);read.Parameters.AddWithValue(taskId);await using var reader=await read.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))throw new HrException("hr.onboarding_task_not_found");code=reader.GetString(0);title=reader.GetString(1);titleEn=reader.IsDBNull(2)?null:reader.GetString(2);required=reader.GetBoolean(3);due=reader.IsDBNull(4)?null:reader.GetFieldValue<DateOnly>(4);status=reader.GetString(5);version=reader.GetInt64(6);}
            if(status!="Pending"||version!=request.ExpectedVersion)throw new HrException("hr.onboarding_task_concurrency");
            await using(var update=new NpgsqlCommand("update business_hr.onboarding_task set status='Completed',version=version+1,completed_by=$1,completed_at=$2,completion_note=$3 where tenant_id=$4 and id=$5 and version=$6",context.Connection,context.Transaction)){update.Parameters.AddWithValue(actor);update.Parameters.AddWithValue(now);update.Parameters.AddWithValue((object?)request.Note??DBNull.Value);update.Parameters.AddWithValue(tenant.Value);update.Parameters.AddWithValue(taskId);update.Parameters.AddWithValue(version);if(await update.ExecuteNonQueryAsync(token)!=1)throw new HrException("hr.onboarding_task_concurrency");}
            await using(var history=new NpgsqlCommand("insert into business_hr.onboarding_task_event(id,tenant_id,task_id,event_type,from_status,to_status,actor_id,note,occurred_at) values($1,$2,$3,'Completed','Pending','Completed',$4,$5,$6)",context.Connection,context.Transaction)){history.Parameters.AddWithValue(Guid.CreateVersion7());history.Parameters.AddWithValue(tenant.Value);history.Parameters.AddWithValue(taskId);history.Parameters.AddWithValue(actor);history.Parameters.AddWithValue((object?)request.Note??DBNull.Value);history.Parameters.AddWithValue(now);await history.ExecuteNonQueryAsync(token);}
            await using(var completeCase=new NpgsqlCommand("update business_hr.onboarding_case c set status='Completed',completed_at=$3 where c.tenant_id=$1 and c.id=(select t.case_id from business_hr.onboarding_task t where t.tenant_id=$1 and t.id=$2) and c.status='Open' and not exists(select 1 from business_hr.onboarding_task t where t.tenant_id=c.tenant_id and t.case_id=c.id and t.is_required and t.status<>'Completed')",context.Connection,context.Transaction)){completeCase.Parameters.AddWithValue(tenant.Value);completeCase.Parameters.AddWithValue(taskId);completeCase.Parameters.AddWithValue(now);await completeCase.ExecuteNonQueryAsync(token);}
            await NpgsqlAuditWriter.WriteAsync(context.Connection,context.Transaction,new AuditEntry(Guid.CreateVersion7(),tenant,legalEntityId,null,"Human",actor,membershipId,"hr","hr.onboarding_task.completed","onboarding_task",taskId,JsonSerializer.Serialize(new { status,version }),JsonSerializer.Serialize(new { status="Completed",version=version+1,request.Note }),null,"hr-api",correlationId,System.Diagnostics.Activity.Current?.TraceId.ToString(),now,null),token);
            return new(taskId,code,title,titleEn,required,due,"Completed",version+1,request.Note);
        },ct);

    private static async Task<IReadOnlyList<OnboardingTaskView>> ReadTasksAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,TenantId tenant,Guid caseId,CancellationToken ct)
    {await using var command=new NpgsqlCommand("select id,code,title_ar,title_en,is_required,due_date,status,version,completion_note from business_hr.onboarding_task where tenant_id=$1 and case_id=$2 order by due_date nulls last,code,id",connection,transaction);command.Parameters.AddWithValue(tenant.Value);command.Parameters.AddWithValue(caseId);var rows=new List<OnboardingTaskView>();await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetBoolean(4),reader.IsDBNull(5)?null:reader.GetFieldValue<DateOnly>(5),reader.GetString(6),reader.GetInt64(7),reader.IsDBNull(8)?null:reader.GetString(8)));await reader.CloseAsync();return rows;}
    private static async Task SetTenantAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,TenantId tenant,CancellationToken ct){await using var command=new NpgsqlCommand("select set_config('app.tenant_id',$1,true)",connection,transaction);command.Parameters.AddWithValue(tenant.Value.ToString("D"));await command.ExecuteNonQueryAsync(ct);}
}
