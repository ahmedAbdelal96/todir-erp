namespace DbMigrator.SeedData;

public static class QualitySeed
{
    public static async Task<QualitySeedSummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var tenant = context.Tenant.Id;
        var plan = SeedIds.Child(tenant, "quality-plan-receiving", 1);
        var version = SeedIds.Child(tenant, "quality-plan-version", 1);
        var characteristic = SeedIds.Child(tenant, "quality-characteristic-weight", 1);
        var specification = SeedIds.Child(tenant, "quality-specification", 1);
        var planCharacteristic = SeedIds.Child(tenant, "quality-plan-characteristic", 1);
        var sampling = SeedIds.Child(tenant, "quality-sampling-plan", 1);
        var samplingVersion = SeedIds.Child(tenant, "quality-sampling-version", 1);
        var supplier = SeedIds.Party(tenant, 4);
        var product = SeedIds.Variant(tenant, 1, "STD");
        var inspection = SeedIds.Child(tenant, "quality-inspection-receiving", 1);
        var result = SeedIds.Child(tenant, "quality-inspection-result", 1);
        var sample = SeedIds.Child(tenant, "quality-sample", 1);
        var evaluation = SeedIds.Child(tenant, "quality-evaluation", 1);
        var restriction = SeedIds.Child(tenant, "quality-restriction", 1);
        var ncr = SeedIds.Child(tenant, "quality-ncr", 1);
        var capa = SeedIds.Child(tenant, "quality-capa", 1);

        await context.ExecuteAsync("insert into business_quality.quality_plan(tenant_id,id,legal_entity_id,code,name,event_type,mandatory,created_by) values($1,$2,$3,'IN-RECEIVE-STD','فحص استلام المواد القياسي','Receiving',true,$4) on conflict(tenant_id,id) do update set status='Active',mandatory=true", ct, tenant, plan, context.EntityId, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.quality_plan_version(tenant_id,id,plan_id,version_no,effective_from,status,created_by) values($1,$2,$3,1,'2026-01-01','Active',$4) on conflict(tenant_id,id) do update set status='Active'", ct, tenant, version, plan, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.quality_plan_applicability(tenant_id,id,plan_id,product_variant_id,supplier_party_id,event_type) values($1,$2,$3,$4,$5,'Receiving') on conflict do nothing", ct, tenant, SeedIds.Child(tenant, "quality-applicability", 1), plan, product, supplier);
        await context.ExecuteAsync("insert into business_quality.quality_characteristic(tenant_id,id,legal_entity_id,code,name,data_type,base_uom_code,required,allowed_values,created_by) values($1,$2,$3,'WEIGHT','الوزن الصافي','Numeric','KG',true,null,$4) on conflict(tenant_id,id) do update set required=true", ct, tenant, characteristic, context.EntityId, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.quality_specification_version(tenant_id,id,legal_entity_id,code,name,effective_from,status,created_by) values($1,$2,$3,'SPEC-STD','مواصفة المنتج القياسية','2026-01-01','Active',$4) on conflict(tenant_id,id) do update set status='Active'", ct, tenant, specification, context.EntityId, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.quality_plan_characteristic(tenant_id,id,plan_version_id,characteristic_id,specification_version_id,minimum,maximum,target,required) values($1,$2,$3,$4,$5,9.5,10.5,'10 KG',true) on conflict(tenant_id,id) do update set minimum=9.5,maximum=10.5,required=true", ct, tenant, planCharacteristic, version, characteristic, specification);
        await context.ExecuteAsync("insert into business_quality.sampling_plan(tenant_id,id,legal_entity_id,code,name,method,percentage,created_by) values($1,$2,$3,'SAMPLE-10P','عينة استلام 10٪','Percentage',10,$4) on conflict(tenant_id,id) do update set percentage=10,status='Active'", ct, tenant, sampling, context.EntityId, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.sampling_plan_version(tenant_id,id,sampling_plan_id,version_no,effective_from,status) values($1,$2,$3,1,'2026-01-01','Active') on conflict(tenant_id,id) do update set status='Active'", ct, tenant, samplingVersion, sampling);
        await context.ExecuteAsync("insert into business_quality.supplier_quality_profile(tenant_id,id,legal_entity_id,supplier_party_id,risk_level,target_ppm,receiving_inspection_required) values($1,$2,$3,$4,'Medium',250,true) on conflict(tenant_id,legal_entity_id,supplier_party_id) do update set receiving_inspection_required=true,risk_level='Medium'", ct, tenant, SeedIds.Child(tenant, "quality-supplier-profile", 1), context.EntityId, supplier);
        await context.ExecuteAsync("insert into business_quality.inspection(tenant_id,id,legal_entity_id,number,event_type,state,decision_state,product_variant_id,source_document_id,supplier_party_id,plan_version_id,sampling_plan_version_id,lot_quantity,sample_size,lot_reference,source_fingerprint,created_by,created_at,completed_at) values($1,$2,$3,'QI-SEED-0001','Receiving','Completed','Passed',$4,$5,$6,$7,$8,100,10,'LOT-QM-001','seed-quality-receipt-1',$9,$10,$10) on conflict(tenant_id,id) do update set state='Completed',decision_state='Passed',sample_size=10", ct, tenant, inspection, context.EntityId, SeedIds.Child(tenant, "purchase-order", 1), supplier, version, samplingVersion, product, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_quality.inspection_plan_snapshot(tenant_id,id,inspection_id,plan_id,plan_version_id,snapshot) values($1,$2,$3,$4,$5,'{\"source\":\"development\",\"frozen\":true}'::jsonb) on conflict(tenant_id,inspection_id) do nothing", ct, tenant, SeedIds.Child(tenant, "quality-snapshot", 1), inspection, plan, version);
        await context.ExecuteAsync("insert into business_quality.quality_sample(tenant_id,id,inspection_id,sample_no,quantity,status) values($1,$2,$3,1,10,'Tested') on conflict(tenant_id,id) do update set status='Tested'", ct, tenant, sample, inspection);
        await context.ExecuteAsync("insert into business_quality.inspection_result(tenant_id,id,inspection_id,sample_id,characteristic_id,numeric_value,uom_code,passed,evidence_reference,recorded_by,recorded_at) values($1,$2,$3,$4,$5,10,'KG',true,'seed:scale-01',$6,$7) on conflict(tenant_id,id) do update set numeric_value=10,passed=true", ct, tenant, result, inspection, sample, characteristic, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_quality.inspection_evaluation(tenant_id,id,inspection_id,decision,evaluated_by,evaluated_at,evidence_reference,reason) values($1,$2,$3,'Passed',$4,$5,'inspection:'||$3||':evaluation','seed passed') on conflict(tenant_id,inspection_id) do update set decision='Passed',evidence_reference='inspection:'||$3||':evaluation'", ct, tenant, evaluation, inspection, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_quality.quality_restriction(tenant_id,id,legal_entity_id,restriction_type,status,reason_code,product_variant_id,inspection_id,quantity,created_by) values($1,$2,$3,'QualityHold','Released','RECEIVING_INSPECTION',$4,$5,100,$6) on conflict(tenant_id,id) do update set status='Released'", ct, tenant, restriction, context.EntityId, product, inspection, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.nonconformance(tenant_id,id,number,legal_entity_id,title,description,severity,state,created_by) values($1,$2,'NCR-SEED-0001',$3,'تفاوت ملصق مورد مراجعة','سيناريو NCR مغلق للتدريب','Minor','Closed',$4) on conflict(tenant_id,id) do update set state='Closed'", ct, tenant, ncr, context.EntityId, context.AdministratorId);
        await context.ExecuteAsync("insert into business_quality.capa(tenant_id,id,number,legal_entity_id,type,title,problem_statement,state,ncr_id,created_by,closed_at) values($1,$2,'CAPA-SEED-0001',$3,'Corrective','تحسين فحص المورد','تم اعتماد الإجراء التصحيحي','Closed',$4,$5,$6) on conflict(tenant_id,id) do update set state='Closed',closed_at=excluded.closed_at", ct, tenant, capa, context.EntityId, ncr, context.AdministratorId, context.AsOf);
        return new QualitySeedSummary(1, 1, 1, 1, 1);
    }
}

public sealed record QualitySeedSummary(int Plans, int Inspections, int Restrictions, int Nonconformances, int Capas);
