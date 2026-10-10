namespace DbMigrator.SeedData;

public static class FinanceBaseSeed
{
    public static async Task<SeedFinanceSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var currencies = context.Tenant.Id == SeedIds.AtlasTenant ? new[] { "EGP", "USD" } : new[] { "USD", "EGP" };
        foreach (var currency in currencies)
            await context.ExecuteAsync("insert into foundation_finbase.legal_entity_currency (tenant_id,legal_entity_id,currency_code,enabled,allow_sales,allow_purchase,allow_payment,allow_manual_rate,status,version,created_at,updated_at) values ($1,$2,$3,true,true,true,true,false,'Active',1,$4,$4) on conflict (tenant_id,legal_entity_id,currency_code) do update set enabled=true,status='Active',updated_at=excluded.updated_at,version=foundation_finbase.legal_entity_currency.version+1", cancellationToken, context.Tenant.Id, context.EntityId, currency, context.AsOf);

        var roundingId = SeedIds.Child(context.Tenant.Id, "rounding-policy", 1);
        await context.ExecuteAsync("insert into foundation_finbase.financial_rounding_policy (tenant_id,id,code,name_ar,name_en,mode,money_scale,cash_increment,status,version,created_at,updated_at) values ($1,$2,'STANDARD','تقريب قياسي','Standard rounding','HalfUp',2,0.01,'Active',1,$3,$3) on conflict (tenant_id,code) do update set name_ar=excluded.name_ar,name_en=excluded.name_en,mode=excluded.mode,money_scale=excluded.money_scale,cash_increment=excluded.cash_increment,status='Active',updated_at=excluded.updated_at,version=foundation_finbase.financial_rounding_policy.version+1", cancellationToken, context.Tenant.Id, roundingId, context.AsOf);
        await context.ExecuteAsync("insert into foundation_finbase.financial_policy (tenant_id,legal_entity_id,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,version,updated_at) values ($1,$2,$3,7,false,1,$4) on conflict (tenant_id,legal_entity_id) do update set rounding_policy_id=excluded.rounding_policy_id,exchange_rate_max_age_days=excluded.exchange_rate_max_age_days,manual_rate_override_allowed=excluded.manual_rate_override_allowed,updated_at=excluded.updated_at,version=foundation_finbase.financial_policy.version+1", cancellationToken, context.Tenant.Id, context.EntityId, roundingId, context.AsOf);

        await context.ExecuteAsync("insert into foundation_finbase.exchange_rate (tenant_id,id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,status,approved_at,approved_by,version,created_at) values ($1,$2,$3,'USD','EGP',50.125,'Commercial','2026-01-02','Provider','development-seed','Approved',$4,$5,1,$4) on conflict (tenant_id,id) do update set rate=excluded.rate,status='Approved',approved_at=excluded.approved_at,approved_by=excluded.approved_by,version=foundation_finbase.exchange_rate.version+1", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "exchange-rate", 1), context.EntityId, context.AsOf, context.AdministratorId);

        var terms = context.Tenant.Id == SeedIds.AtlasTenant
            ? new[] { new Term("CASH", "نقدي", "Cash", "Balance", 0, "Active"), new Term("NET15", "15 يوم", "15 days", "Balance", 15, "Active"), new Term("NET30", "30 يوم", "30 days", "Balance", 30, "Active"), new Term("SPLIT50", "50% مقدم / 30 يوم", "50% upfront / 30 days", "Split", 30, "Active"), new Term("LEGACY", "شروط قديمة", "Legacy terms", "Balance", 0, "Archived") }
            : new[] { new Term("CASH", "نقدي", "Cash", "Balance", 0, "Active"), new Term("NET30", "30 يوم", "30 days", "Balance", 30, "Active"), new Term("NET45", "45 يوم", "45 days", "Balance", 45, "Active"), new Term("LEGACY", "شروط قديمة", "Legacy terms", "Balance", 0, "Archived") };
        for (var index = 0; index < terms.Length; index++)
        {
            var term = terms[index];
            var termId = SeedIds.Child(context.Tenant.Id, "payment-term", index + 1);
            var versionId = SeedIds.Child(context.Tenant.Id, "payment-term-version", index + 1);
            await context.ExecuteAsync("insert into foundation_finbase.payment_term (tenant_id,id,code,display_name_ar,display_name_en,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,1,$7,$7) on conflict (tenant_id,code) do update set display_name_ar=excluded.display_name_ar,display_name_en=excluded.display_name_en,status=excluded.status,updated_at=excluded.updated_at,version=foundation_finbase.payment_term.version+1", cancellationToken, context.Tenant.Id, termId, term.Code, term.NameAr, term.NameEn, term.Status, context.AsOf);
            await context.ExecuteAsync("insert into foundation_finbase.payment_term_version (tenant_id,id,payment_term_id,version_number,effective_from,effective_to,status,description) values ($1,$2,$3,1,'2026-01-01',null,$4,$5) on conflict (tenant_id,id) do update set effective_to=null,status=excluded.status,description=excluded.description", cancellationToken, context.Tenant.Id, versionId, termId, term.Status == "Active" ? "Active" : "Retired", $"Canonical development scenario: {term.NameEn}");
            if (term.Code == "SPLIT50")
            {
                await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,1,'Percentage',50,null,'InvoiceDate',0,null,0) on conflict (tenant_id,id) do update set percentage=50,allocation_type='Percentage',days=0", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + 1), versionId);
                await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,2,'Balance',null,null,'InvoiceDate',$4,null,0) on conflict (tenant_id,id) do update set days=excluded.days,allocation_type='Balance',percentage=null,fixed_amount=null", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + 2), versionId, term.Days);
            }
            else
            {
                await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,1,'Balance',null,null,'InvoiceDate',$4,null,0) on conflict (tenant_id,id) do update set days=excluded.days,allocation_type='Balance',percentage=null,fixed_amount=null", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + 1), versionId, term.Days);
            }
        }
        return new(currencies.Length, terms.Length, terms.Count(term => term.Status == "Active"), true);
    }

    private sealed record Term(string Code, string NameAr, string NameEn, string Shape, int Days, string Status);
}

public sealed record SeedFinanceSummary(int EnabledCurrencies, int PaymentTerms, int ActivePaymentTerms, bool HasApprovedRate);
