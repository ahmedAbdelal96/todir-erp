using Npgsql;

namespace DbMigrator.SeedData;

public static class CrmSeed
{
    public static async Task<CrmSeedSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var pipeline = SeedIds.Child(context.Tenant.Id, "crm-pipeline", 1);
        await context.ExecuteAsync("insert into business_crm.crm_pipeline(tenant_id,id,legal_entity_id,code,name,is_default,created_at,updated_at) values($1,$2,$3,'DEFAULT','المبيعات العامة',true,$4,$4) on conflict(tenant_id,legal_entity_id,code) do nothing", cancellationToken, context.Tenant.Id, pipeline, context.EntityId, context.AsOf);
        var stages = new[] { (1, "جديد", 0.10m, false, false), (2, "مؤهل", 0.30m, false, false), (3, "عرض سعر", 0.55m, false, false), (4, "تفاوض", 0.75m, false, false), (5, "فوز", 1m, true, false), (6, "خسارة", 0m, false, true) };
        foreach (var stage in stages)
            await context.ExecuteAsync("insert into business_crm.crm_pipeline_stage(tenant_id,id,pipeline_id,legal_entity_id,name,sequence,probability,stale_after_days,is_won,is_lost,created_at) values($1,$2,$3,$4,$5,$6,$7,30,$8,$9,$10) on conflict(tenant_id,pipeline_id,sequence) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "crm-stage", stage.Item1), pipeline, context.EntityId, stage.Item2, stage.Item1, stage.Item3, stage.Item4, stage.Item5, context.AsOf);
        var decisionField = SeedIds.Child(context.Tenant.Id, "crm-custom-field-decision", 1);
        await context.ExecuteAsync("insert into business_crm.crm_custom_field_definition(tenant_id,id,legal_entity_id,entity_type,field_key,label,data_type) values($1,$2,$3,'Opportunity','decision_role','دور صاحب القرار','Text') on conflict(tenant_id,legal_entity_id,entity_type,field_key) do nothing", cancellationToken, context.Tenant.Id, decisionField, context.EntityId);
        for (var index = 1; index <= 40; index++)
        {
            var lead = SeedIds.Child(context.Tenant.Id, "crm-lead", index);
            var status = index % 9 == 0 ? "Qualified" : index % 5 == 0 ? "Working" : "New";
            await context.ExecuteAsync("insert into business_crm.lead(tenant_id,id,legal_entity_id,display_name,company_name,email,phone,normalized_email,normalized_phone,source,status,priority,score,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$6,$7,'Website',$8,$9,$10,$11,$11) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, lead, context.EntityId, $"عميل محتمل {index:000}", $"شركة أطلس {index:000}", $"crm{index:000}@example.test", $"0100000{index:000}", status, index % 7 == 0 ? "High" : "Normal", Math.Min(100, 40 + index), context.AsOf);
        }
        for (var index = 1; index <= 12; index++)
        {
            var opportunity = SeedIds.Child(context.Tenant.Id, "crm-opportunity", index);
            var stage = SeedIds.Child(context.Tenant.Id, "crm-stage", (index % 4) + 1);
            await context.ExecuteAsync("insert into business_crm.opportunity(tenant_id,id,legal_entity_id,party_id,pipeline_id,stage_id,name,estimated_amount,currency_code,expected_close_date,forecast_category,status,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Open',$12,$12) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, opportunity, context.EntityId, SeedIds.Party(context.Tenant.Id, index), pipeline, stage, $"فرصة مبيعات {index:000}", 25000m + index * 1750m, context.Tenant.BaseCurrency, DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(index * 5), index % 3 == 0 ? "Commit" : "Pipeline", context.AsOf);
            await context.ExecuteAsync("insert into business_crm.crm_custom_field_value(tenant_id,id,legal_entity_id,entity_type,entity_id,field_id,value) values($1,$2,$3,'Opportunity',$4,$5,$6::jsonb) on conflict(tenant_id,entity_type,entity_id,field_id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "crm-custom-value", index), context.EntityId, opportunity, decisionField, System.Text.Json.JsonSerializer.Serialize(index % 2 == 0 ? "المدير المالي" : "المدير التنفيذي"));
        }
        return new(1, stages.Length, 40, 12);
    }
}

public sealed record CrmSeedSummary(int Pipelines, int Stages, int Leads, int Opportunities);
