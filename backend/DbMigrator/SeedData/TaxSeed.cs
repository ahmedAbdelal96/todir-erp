namespace DbMigrator.SeedData;

public static class TaxSeed
{
    public static async Task<SeedTaxSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenant = context.Tenant.Id;
        var main = context.EntityId;
        var secondary = SeedIds.TenantSecondaryEntity(tenant);
        var from = new DateOnly(2026, 1, 1);
        var country = await context.ScalarAsync<string>("select country_code from platform.legal_entity where tenant_id=$1 and id=$2", cancellationToken, tenant, main);
        var timezone = await context.ScalarAsync<string>("select timezone from platform.legal_entity where tenant_id=$1 and id=$2", cancellationToken, tenant, main);
        var categoryIds = new Dictionary<string, Guid>
        {
            ["GENERAL"] = SeedIds.Child(tenant,"tax-category-general",1),
            ["ZERO-DEMO"] = SeedIds.Child(tenant,"tax-category-zero-demo",1),
            ["EXEMPT-DEMO"] = SeedIds.Child(tenant,"tax-category-exempt-demo",1),
            ["OUT-OF-SCOPE-DEMO"] = SeedIds.Child(tenant,"tax-category-out-of-scope-demo",1),
            ["PARTY-GENERAL"] = SeedIds.Child(tenant,"tax-category-party-general",1)
        };
        foreach (var item in categoryIds)
        {
            var scope = item.Key == "PARTY-GENERAL" ? "Party" : "Product";
            var name = item.Key switch
            {
                "ZERO-DEMO" => "Zero treatment demo classification (no statutory rule attached)",
                "EXEMPT-DEMO" => "Exempt treatment demo classification (no statutory rule attached)",
                "OUT-OF-SCOPE-DEMO" => "Out-of-scope demo classification (no statutory rule attached)",
                "PARTY-GENERAL" => "General party classification",
                _ => "General product classification"
            };
            await context.ExecuteAsync("insert into foundation_tax.tax_category(tenant_id,id,code,name,scope,status) values($1,$2,$3,$4,$5,'Active') on conflict(tenant_id,id) do update set name=excluded.name,status='Active'", cancellationToken, tenant, item.Value, item.Key, name, scope);
        }

        await context.ExecuteAsync("insert into foundation_tax.tax_scheme(tenant_id,code,country_code,name,status,source_kind,mutable) values($1,'DEVELOPMENT',$2,'Development indirect tax scheme','Active','TenantConfiguration',true) on conflict(tenant_id,code) do update set country_code=excluded.country_code,name=excluded.name,status='Active',source_kind='TenantConfiguration',mutable=true", cancellationToken, tenant, country);

        await context.ExecuteAsync("insert into foundation_tax.tax_capability_state(tenant_id,legal_entity_id,capability,effective_from,effective_to,version,updated_by,updated_at) values($1,$2,'Disabled',$3,null,1,$4,$5) on conflict(tenant_id,legal_entity_id,effective_from) do update set capability='Disabled'", cancellationToken, tenant, main, from, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into foundation_tax.tax_capability_state(tenant_id,legal_entity_id,capability,effective_from,effective_to,version,updated_by,updated_at) values($1,$2,'Enabled',$3,null,1,$4,$5) on conflict(tenant_id,legal_entity_id,effective_from) do update set capability='Enabled'", cancellationToken, tenant, secondary, from, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into foundation_tax.tax_policy(tenant_id,legal_entity_id,policy_code,rounding_strategy,money_scale,tax_date_basis,timezone_id,version,updated_at) values($1,$2,'DEVELOPMENT','PerLine',2,'SupplyDate',$3,1,$4) on conflict(tenant_id,legal_entity_id) do nothing", cancellationToken, tenant, main, timezone, context.AsOf);
        await context.ExecuteAsync("insert into foundation_tax.tax_policy(tenant_id,legal_entity_id,policy_code,rounding_strategy,money_scale,tax_date_basis,timezone_id,version,updated_at) values($1,$2,'DEVELOPMENT','PerLine',2,'SupplyDate',$3,1,$4) on conflict(tenant_id,legal_entity_id) do nothing", cancellationToken, tenant, secondary, timezone, context.AsOf);

        var registrationId = SeedIds.Child(tenant,"tax-registration-pending",1);
        var activationId = SeedIds.Child(tenant,"tax-pack-unreviewed",1);
        await context.ExecuteAsync("insert into foundation_tax.tax_registration(tenant_id,id,country_code,scheme_code,registration_number,status,effective_from,effective_to,authority,evidence_reference,version,created_at,updated_at) values($1,$2,$3,'DEVELOPMENT','DEMO-REGISTERED','Registered',$4,null,'Development fixture','Synthetic development registration; not a statutory identifier.',1,$5,$5) on conflict(tenant_id,id) do update set status='Registered',registration_number='DEMO-REGISTERED'", cancellationToken, tenant, registrationId, country, from, context.AsOf);
        await context.ExecuteAsync("insert into foundation_tax.tax_registration_member(tenant_id,id,registration_id,legal_entity_id,effective_from,effective_to,membership_status) values($1,$2,$3,$4,$5,null,'Active') on conflict(tenant_id,id) do update set membership_status='Active',effective_to=null", cancellationToken, tenant, SeedIds.Child(tenant,"tax-registration-member",1), registrationId, secondary, from);
        await context.ExecuteAsync("insert into foundation_tax.tax_pack_activation(tenant_id,id,pack_id,country_code,pack_code,pack_version,engine_contract_version,source_authority,authority_reference,source_title,effective_from,effective_to,review_status,status,source_kind,capabilities,rule_set_references,golden_vector_count,golden_vector_status,validation_status,validation_issues,installed_at,installed_by) values($1,$2,'DEVELOPMENT-UNREVIEWED',$3,'DEVELOPMENT-UNREVIEWED','0.0.0','tax_calculation_v1','Internal Development Fixture','development:synthetic','Development/Test Country Pack',$4,null,'Unreviewed','Installed','TestOnly','{}'::jsonb,'[\"DEVELOPMENT\"]'::jsonb,0,'Missing','NotValidated','[]'::jsonb,$5,$6) on conflict(tenant_id,id) do update set pack_id=excluded.pack_id,country_code=excluded.country_code,pack_code=excluded.pack_code,pack_version=excluded.pack_version,engine_contract_version=excluded.engine_contract_version,source_authority=excluded.source_authority,authority_reference=excluded.authority_reference,source_title=excluded.source_title,effective_from=excluded.effective_from,effective_to=excluded.effective_to,review_status='Unreviewed',status='Installed',source_kind='TestOnly',capabilities=excluded.capabilities,rule_set_references=excluded.rule_set_references,golden_vector_count=excluded.golden_vector_count,golden_vector_status='Missing',validation_status='NotValidated',validation_issues='[]'::jsonb,installed_at=excluded.installed_at,installed_by=excluded.installed_by", cancellationToken, tenant, activationId, country, from, context.AsOf, context.AdministratorId);

        var demoRuleSet = SeedIds.Child(tenant,"tax-rule-set-engine-demo",1);
        await context.ExecuteAsync("insert into foundation_tax.tax_rule_set(tenant_id,id,legal_entity_id,country_code,scheme_code,pack_activation_id,version,status,effective_from,effective_to,source_kind,mutable,created_at) values($1,$2,$3,$4,'DEVELOPMENT',null,1,'Active',$5,null,'TenantConfiguration',true,$6) on conflict(tenant_id,id) do update set legal_entity_id=excluded.legal_entity_id,country_code=excluded.country_code,scheme_code=excluded.scheme_code,pack_activation_id=excluded.pack_activation_id,version=excluded.version,status=excluded.status,effective_from=excluded.effective_from,effective_to=excluded.effective_to,source_kind=excluded.source_kind,mutable=excluded.mutable", cancellationToken, tenant, demoRuleSet, secondary, country, from, context.AsOf);
        await SeedDemoRule(context, demoRuleSet, categoryIds["GENERAL"], "StandardRated", 10m, country, context.Tenant.BaseCurrency, cancellationToken);
        await SeedDemoRule(context, demoRuleSet, categoryIds["ZERO-DEMO"], "ZeroRated", null, country, context.Tenant.BaseCurrency, cancellationToken);
        await SeedDemoRule(context, demoRuleSet, categoryIds["EXEMPT-DEMO"], "Exempt", null, country, context.Tenant.BaseCurrency, cancellationToken);
        await SeedDemoRule(context, demoRuleSet, categoryIds["OUT-OF-SCOPE-DEMO"], "OutOfScope", null, country, context.Tenant.BaseCurrency, cancellationToken);

        foreach (var (productIndex, categoryCode) in new[] { (1,"GENERAL"), (2,"GENERAL"), (3,"ZERO-DEMO"), (4,"EXEMPT-DEMO"), (5,"OUT-OF-SCOPE-DEMO") })
        {
            var variant = SeedIds.Variant(tenant, productIndex, "default");
            var profileId = SeedIds.Child(tenant,$"tax-product-profile-{productIndex:D2}",1);
            await context.ExecuteAsync("insert into foundation_tax.product_tax_profile(tenant_id,id,product_template_id,product_variant_id,legal_entity_id,country_code,scheme_code,category_id,effective_from,effective_to,version,created_at) values($1,$2,$3,$4,null,$5,null,$6,$7,null,1,$8) on conflict(tenant_id,id) do update set category_id=excluded.category_id,effective_to=null", cancellationToken, tenant, profileId, SeedIds.Product(tenant,productIndex), variant, country, categoryIds[categoryCode], from, context.AsOf);
        }
        foreach (var partyIndex in new[] { 2, 3 })
        {
            var profileId = SeedIds.Child(tenant,$"tax-party-profile-{partyIndex:D2}",1);
            await context.ExecuteAsync("insert into foundation_tax.party_tax_profile(tenant_id,id,party_id,legal_entity_id,registration_id,country_code,category_id,treatment,reverse_charge_eligible,exemption_evidence_reference,effective_from,effective_to,version,created_at) values($1,$2,$3,null,null,$4,$5,null,false,null,$6,null,1,$7) on conflict(tenant_id,id) do update set category_id=excluded.category_id,effective_to=null", cancellationToken, tenant, profileId, SeedIds.Party(tenant,partyIndex), country, categoryIds["PARTY-GENERAL"], from, context.AsOf);
        }
        return new(2, 1, 1, categoryIds.Count);
    }

    private static async Task SeedDemoRule(SeedContext context, Guid ruleSetId, Guid categoryId, string treatment, decimal? rate, string countryCode, string currencyCode, CancellationToken cancellationToken)
    {
        var tenant = context.Tenant.Id;
        var key = treatment.ToLowerInvariant();
        var ruleId = SeedIds.Child(tenant,$"tax-rule-engine-demo-{key}",1);
        var versionId = SeedIds.Child(tenant,$"tax-rule-version-engine-demo-{key}",1);
        var stackId = treatment == "StandardRated" ? SeedIds.Child(tenant,"tax-stack-engine-demo-standard",1) : Guid.Empty;
        await context.ExecuteAsync("insert into foundation_tax.tax_rule_version(tenant_id,id,rule_id,version,treatment,effective_from,effective_to,source_reference,legal_reason_code,locked_by_country_pack,immutable,created_at) values($1,$2,$3,1,$4,'2026-01-01',null,'Development engine fixture; not statutory advice.',null,false,false,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, versionId, ruleId, treatment, context.AsOf);
        if (stackId != Guid.Empty)
        {
            await context.ExecuteAsync("insert into foundation_tax.tax_stack_definition(tenant_id,id,rule_set_id,code,status) values($1,$2,$3,'ENGINE-DEMO-STANDARD','Active') on conflict(tenant_id,id) do nothing", cancellationToken, tenant, stackId, ruleSetId);
            await context.ExecuteAsync("insert into foundation_tax.tax_component_definition(tenant_id,id,stack_id,sequence,tax_type_code,tax_code,treatment,calculation_method,rate,fixed_amount,currency_code,base_component_sequence,recoverable_percentage,legal_reason_code,reporting_code) values($1,$2,$3,1,'DEMO','DEMO-STANDARD','StandardRated','PercentageExtractedFromGross',$4,null,$5,null,100,null,'ENGINE-DEMO') on conflict(tenant_id,id) do nothing", cancellationToken, tenant, SeedIds.Child(tenant,"tax-component-engine-demo-standard",1), stackId, rate!.Value, currencyCode);
        }
        Guid? ruleStackId = stackId == Guid.Empty ? null : stackId;
        await context.ExecuteAsync("insert into foundation_tax.tax_rule(tenant_id,id,rule_set_id,stack_id,rule_version_id,explicit_priority,registration_id,jurisdiction_code,direction,party_category_id,product_category_id,document_kind,place_of_supply_country,tax_date_from,tax_date_to,legal_reason_code,legal_reason_text) values($1,$2,$3,$4,$5,0,null,null,null,null,$6,null,$7,'2026-01-01',null,null,'Development engine fixture; not statutory advice.') on conflict(tenant_id,id) do nothing", cancellationToken, tenant, ruleId, ruleSetId, ruleStackId, versionId, categoryId, countryCode);
    }
}

public sealed record SeedTaxSummary(int CapabilityEntities, int RegisteredDevelopmentFixtures, int UnreviewedCountryPacks, int Categories);
