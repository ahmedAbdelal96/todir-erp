namespace DbMigrator.SeedData;

public static class FinanceBaseSeed
{
    public static async Task<SeedFinanceSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        foreach (var currency in new[]
        {
            (Code: "EGP", NumericCode: (short)818, NameAr: "جنيه مصري", NameEn: "Egyptian Pound"),
            (Code: "USD", NumericCode: (short)840, NameAr: "دولار أمريكي", NameEn: "US Dollar"),
            (Code: "JPY", NumericCode: (short)392, NameAr: "ين ياباني", NameEn: "Japanese Yen"),
            (Code: "KWD", NumericCode: (short)414, NameAr: "دينار كويتي", NameEn: "Kuwaiti Dinar")
        })
        {
            var minorUnit = currency.Code == "JPY" ? 0 : currency.Code == "KWD" ? 3 : 2;
            await context.ExecuteAsync("insert into foundation_finbase.currency_reference (code,numeric_code,name_ar,name_en,minor_unit,status,valid_from,valid_to,dataset_version) values ($1,$2,$3,$4,$5,'Active','2026-01-01',null,'development-2026-10') on conflict (code) do update set numeric_code=excluded.numeric_code,name_ar=excluded.name_ar,name_en=excluded.name_en,minor_unit=excluded.minor_unit,status='Active',valid_from=excluded.valid_from,valid_to=null,dataset_version=excluded.dataset_version where (foundation_finbase.currency_reference.numeric_code,foundation_finbase.currency_reference.name_ar,foundation_finbase.currency_reference.name_en,foundation_finbase.currency_reference.minor_unit,foundation_finbase.currency_reference.status,foundation_finbase.currency_reference.valid_from,foundation_finbase.currency_reference.valid_to,foundation_finbase.currency_reference.dataset_version) is distinct from (excluded.numeric_code,excluded.name_ar,excluded.name_en,excluded.minor_unit,excluded.status,excluded.valid_from,excluded.valid_to,excluded.dataset_version)", cancellationToken, currency.Code, currency.NumericCode, currency.NameAr, currency.NameEn, minorUnit);
        }

        var currencies = context.Tenant.Id == SeedIds.AtlasTenant ? new[] { "EGP", "USD" } : new[] { "USD", "EGP" };
        var entityIds = new[] { context.EntityId, SeedIds.TenantSecondaryEntity(context.Tenant.Id) };
        var roundingId = SeedIds.Child(context.Tenant.Id, "rounding-policy", 1);
        await context.ExecuteAsync("insert into foundation_finbase.financial_rounding_policy (tenant_id,id,code,name_ar,name_en,mode,money_scale,cash_increment,status,version,created_at,updated_at) values ($1,$2,'STANDARD','تقريب قياسي','Standard rounding','HalfUp',2,0.01,'Active',1,$3,$3) on conflict (tenant_id,code) do nothing", cancellationToken, context.Tenant.Id, roundingId, context.AsOf);
        foreach (var roundingContext in new[] { "MoneySettlement", "LineNetAmount", "DocumentSubtotal", "DocumentTotal", "FXBaseAmount", "PaymentScheduleLine", "PaymentAllocation", "CashRounding" })
            await context.ExecuteAsync("insert into foundation_finbase.financial_rounding_policy_rule (tenant_id,rounding_policy_id,rounding_context,mode) values ($1,$2,$3,'HalfUp') on conflict (tenant_id,rounding_policy_id,rounding_context) do nothing", cancellationToken, context.Tenant.Id, roundingId, roundingContext);
        for (var entityIndex = 0; entityIndex < entityIds.Length; entityIndex++)
        {
            var entityId = entityIds[entityIndex];
            foreach (var currency in currencies)
                await context.ExecuteAsync("insert into foundation_finbase.legal_entity_currency (tenant_id,legal_entity_id,currency_code,enabled,allow_sales,allow_purchase,allow_payment,allow_manual_rate,status,version,created_at,updated_at) values ($1,$2,$3,true,true,true,true,false,'Active',1,$4,$4) on conflict (tenant_id,legal_entity_id,currency_code) do update set enabled=true,allow_sales=true,allow_purchase=true,allow_payment=true,allow_manual_rate=false,status='Active',updated_at=excluded.updated_at,version=foundation_finbase.legal_entity_currency.version+1 where (foundation_finbase.legal_entity_currency.enabled,foundation_finbase.legal_entity_currency.allow_sales,foundation_finbase.legal_entity_currency.allow_purchase,foundation_finbase.legal_entity_currency.allow_payment,foundation_finbase.legal_entity_currency.allow_manual_rate,foundation_finbase.legal_entity_currency.status) is distinct from (true,true,true,true,false,'Active')", cancellationToken, context.Tenant.Id, entityId, currency, context.AsOf);

            await context.ExecuteAsync("insert into foundation_finbase.financial_policy (tenant_id,legal_entity_id,rounding_policy_id,exchange_rate_max_age_days,manual_rate_override_allowed,version,updated_at) values ($1,$2,$3,7,false,1,$4) on conflict (tenant_id,legal_entity_id) do update set rounding_policy_id=excluded.rounding_policy_id,exchange_rate_max_age_days=excluded.exchange_rate_max_age_days,manual_rate_override_allowed=excluded.manual_rate_override_allowed,updated_at=excluded.updated_at,version=foundation_finbase.financial_policy.version+1 where (foundation_finbase.financial_policy.rounding_policy_id,foundation_finbase.financial_policy.exchange_rate_max_age_days,foundation_finbase.financial_policy.manual_rate_override_allowed) is distinct from (excluded.rounding_policy_id,excluded.exchange_rate_max_age_days,excluded.manual_rate_override_allowed)", cancellationToken, context.Tenant.Id, entityId, roundingId, context.AsOf);

            var rate = entityIndex == 0 ? 50.125m : 50.250m;
            var rateId = SeedIds.Child(context.Tenant.Id, $"exchange-rate:{entityId:N}", 1);
            await context.ExecuteAsync("insert into foundation_finbase.exchange_rate (tenant_id,id,legal_entity_id,from_currency,to_currency,rate,purpose,effective_date,source_type,provider_reference,reason,status,approved_at,approved_by,version,created_at) values ($1,$2,$3,'USD','EGP',$4,'Accounting','2026-01-02','Manual','development-seed','Canonical development manual rate scenario','Approved',$5,$6,1,$5) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, rateId, entityId, rate, context.AsOf, context.AdministratorId);
        }

        var terms = context.Tenant.Id == SeedIds.AtlasTenant
            ? new[] { new Term("CASH", "نقدي", "Cash", "Balance", 0, "Active"), new Term("NET15", "15 يوم", "15 days", "Balance", 15, "Active"), new Term("NET30", "30 يوم", "30 days", "Balance", 30, "Active"), new Term("SPLIT50", "50% مقدم / 30 يوم", "50% upfront / 30 days", "Split", 30, "Active"), new Term("LEGACY", "شروط قديمة", "Legacy terms", "Balance", 0, "Archived"), new Term("INSTALL3", "أقساط 30 / 60 / رصيد", "Three installments with balance", "Installment", 60, "Active") }
            : new[] { new Term("CASH", "نقدي", "Cash", "Balance", 0, "Active"), new Term("NET30", "30 يوم", "30 days", "Balance", 30, "Active"), new Term("NET45", "45 يوم", "45 days", "Balance", 45, "Active"), new Term("LEGACY", "شروط قديمة", "Legacy terms", "Balance", 0, "Archived"), new Term("INSTALL3", "أقساط 30 / 60 / رصيد", "Three installments with balance", "Installment", 60, "Active") };
        for (var index = 0; index < terms.Length; index++)
        {
            var term = terms[index];
            var termId = SeedIds.Child(context.Tenant.Id, "payment-term", index + 1);
            var versionId = SeedIds.Child(context.Tenant.Id, "payment-term-version", index + 1);
            await context.ExecuteAsync("insert into foundation_finbase.payment_term (tenant_id,id,code,display_name_ar,display_name_en,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,1,$7,$7) on conflict (tenant_id,code) do nothing", cancellationToken, context.Tenant.Id, termId, term.Code, term.NameAr, term.NameEn, term.Status, context.AsOf);
            await context.ExecuteAsync("insert into foundation_finbase.payment_term_version (tenant_id,id,payment_term_id,version_number,effective_from,effective_to,status,description) values ($1,$2,$3,1,'2026-01-01',null,$4,$5) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, versionId, termId, term.Status == "Active" ? "Active" : "Retired", $"Canonical development scenario: {term.NameEn}");
            if (term.Code is "SPLIT50" or "INSTALL3")
            {
                var firstRatio = term.Code == "SPLIT50" ? 0.5m : 0.3333m;
                await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,1,'Percentage',$4,null,'Immediate',0,null,0) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + 1), versionId, firstRatio);
                if (term.Code == "INSTALL3")
                    await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,2,'Percentage',0.3333,null,'DaysAfterDate',30,null,0) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + 2), versionId);
                await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,$4,'Balance',null,null,'DaysAfterDate',$5,null,0) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + (term.Code == "INSTALL3" ? 3 : 2)), versionId, term.Code == "INSTALL3" ? 3 : 2, term.Days);
            }
            else
            {
                await context.ExecuteAsync("insert into foundation_finbase.payment_term_rule (tenant_id,id,version_id,sequence_no,allocation_type,percentage,fixed_amount,due_date_basis,days,day_of_month,month_offset) values ($1,$2,$3,1,'Balance',null,null,$4,$5,null,0) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "payment-term-rule", index * 2 + 1), versionId, term.Days == 0 ? "Immediate" : "DaysAfterDate", term.Days);
            }
        }
        return new(currencies.Length * entityIds.Length, terms.Length, terms.Count(term => term.Status == "Active"), true);
    }

    private sealed record Term(string Code, string NameAr, string NameEn, string Shape, int Days, string Status);
}

public sealed record SeedFinanceSummary(int EnabledCurrencies, int PaymentTerms, int ActivePaymentTerms, bool HasApprovedRate);
