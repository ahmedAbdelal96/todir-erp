using Npgsql;

namespace DbMigrator.SeedData;

public static class CrmSeed
{
    public static async Task<CrmSeedSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var pipeline = SeedIds.Child(context.Tenant.Id, "crm-pipeline", 1);
        await context.ExecuteAsync("insert into business_crm.crm_pipeline(tenant_id,id,legal_entity_id,code,name,is_default,created_at,updated_at) values($1,$2,$3,'DEFAULT','المبيعات العامة',true,$4,$4) on conflict(tenant_id,legal_entity_id,code) do nothing", cancellationToken, context.Tenant.Id, pipeline, context.EntityId, context.AsOf);
        var stages = new[] { (1, "جديد", 0.10m, 14, false, false), (2, "مؤهل", 0.30m, 14, false, false), (3, "عرض سعر", 0.55m, 14, false, false), (4, "تفاوض", 0.75m, 14, false, false), (5, "فوز", 1m, 30, true, false), (6, "خسارة", 0m, 30, false, true) };
        foreach (var stage in stages)
            await context.ExecuteAsync("insert into business_crm.crm_pipeline_stage(tenant_id,id,pipeline_id,legal_entity_id,name,sequence,probability,stale_after_days,is_won,is_lost,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) on conflict(tenant_id,pipeline_id,sequence) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "crm-stage", stage.Item1), pipeline, context.EntityId, stage.Item2, stage.Item1, stage.Item3, stage.Item4, stage.Item5, stage.Item6, context.AsOf);
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
            var stageIndex = index switch { 10 => 5, 11 => 6, _ => ((index - 1) % 4) + 1 };
            var stage = SeedIds.Child(context.Tenant.Id, "crm-stage", stageIndex);
            var status = index == 10 ? "Won" : index == 11 ? "Lost" : "Open";
            var category = index == 10 ? "Won" : index == 11 ? "Omitted" : index % 3 == 0 ? "Commit" : "Pipeline";
            var closeDate = index == 9
                ? DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(-10)
                : DateOnly.FromDateTime(context.AsOf.UtcDateTime).AddDays(index * 5);
            var createdAt = index == 8 ? context.AsOf.AddDays(-40) : context.AsOf;
            var updatedAt = index == 8 ? context.AsOf.AddDays(-40) : context.AsOf;
            var lastActivityAt = index == 8 ? (DateTimeOffset?)context.AsOf.AddDays(-40) : null;
            var closedAt = index is 10 or 11 ? (DateTimeOffset?)context.AsOf.AddDays(-2) : null;
            var lossReason = index == 11 ? "السعر مرتفع مقارنة بالمنافس" : null;
            var competitor = index == 11 ? "شركة النيل للحلول" : null;
            var healthStatus = index == 8 ? "Stale" : index == 9 ? "AtRisk" : "Healthy";
            var healthScore = index == 8 ? 20 : index == 9 ? 50 : 100;

            await context.ExecuteAsync("insert into business_crm.opportunity(tenant_id,id,legal_entity_id,party_id,pipeline_id,stage_id,name,estimated_amount,currency_code,expected_close_date,forecast_category,status,loss_reason,competitor,last_activity_at,health_score,health_status,created_at,updated_at,closed_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, opportunity, context.EntityId, SeedIds.Party(context.Tenant.Id, index), pipeline, stage, $"فرصة مبيعات {index:000}", 25000m + index * 1750m, context.Tenant.BaseCurrency, closeDate, category, status, lossReason, competitor, lastActivityAt, healthScore, healthStatus, createdAt, updatedAt, closedAt);
            await context.ExecuteAsync("insert into business_crm.crm_custom_field_value(tenant_id,id,legal_entity_id,entity_type,entity_id,field_id,value) values($1,$2,$3,'Opportunity',$4,$5,$6::jsonb) on conflict(tenant_id,entity_type,entity_id,field_id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "crm-custom-value", index), context.EntityId, opportunity, decisionField, System.Text.Json.JsonSerializer.Serialize(index % 2 == 0 ? "المدير المالي" : "المدير التنفيذي"));
            if (index == 10)
                await context.ExecuteAsync("insert into business_crm.opportunity_interest(tenant_id,id,legal_entity_id,opportunity_id,product_variant_id,description,quantity,uom_code) values($1,$2,$3,$4,$5,'طلب تصنيع مؤكد من فرصة فائزة',400,'EA') on conflict(tenant_id,id) do update set product_variant_id=excluded.product_variant_id,description=excluded.description,quantity=excluded.quantity,uom_code=excluded.uom_code", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "crm-opportunity-interest", index), context.EntityId, opportunity, SeedIds.Variant(context.Tenant.Id, 1, "default"));
        }
        var act1 = SeedIds.Child(context.Tenant.Id, "crm-seed-activity", 1);
        await context.ExecuteAsync("insert into business_crm.crm_activity(tenant_id,id,legal_entity_id,opportunity_id,party_id,type,subject,state,due_at,idempotency_key,created_at,updated_at) values($1,$2,$3,$4,$5,'Call','متابعة تفاصيل العرض الفني','Scheduled',$6,$7,$8,$8) on conflict(tenant_id,idempotency_key) do nothing", cancellationToken, context.Tenant.Id, act1, context.EntityId, SeedIds.Child(context.Tenant.Id, "crm-opportunity", 1), SeedIds.Party(context.Tenant.Id, 1), context.AsOf.AddDays(1), "seed-activity-1", context.AsOf);
        var act2 = SeedIds.Child(context.Tenant.Id, "crm-seed-activity", 2);
        await context.ExecuteAsync("insert into business_crm.crm_activity(tenant_id,id,legal_entity_id,opportunity_id,party_id,type,subject,state,due_at,idempotency_key,created_at,updated_at) values($1,$2,$3,$4,$5,'Meeting','اجتماع مراجعة الميزانية المتأخر','Scheduled',$6,$7,$8,$8) on conflict(tenant_id,idempotency_key) do nothing", cancellationToken, context.Tenant.Id, act2, context.EntityId, SeedIds.Child(context.Tenant.Id, "crm-opportunity", 9), SeedIds.Party(context.Tenant.Id, 9), context.AsOf.AddDays(-3), "seed-activity-2", context.AsOf);
        return new(1, stages.Length, 40, 12);
    }
}

public sealed record CrmSeedSummary(int Pipelines, int Stages, int Leads, int Opportunities);
