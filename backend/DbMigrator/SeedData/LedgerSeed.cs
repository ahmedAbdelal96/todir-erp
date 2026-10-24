namespace DbMigrator.SeedData;

public static class LedgerSeed
{
    public static async Task<SeedLedgerSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var entities = new[] { context.EntityId, SeedIds.TenantSecondaryEntity(context.Tenant.Id) };
        var accountCount = 0; var periodCount = 0;
        foreach (var entityId in entities)
        {
            var suffix = entityId.ToString("N");
            var assetCategory = SeedIds.Child(context.Tenant.Id, $"ledger-category:{suffix}", 1);
            var liabilityCategory = SeedIds.Child(context.Tenant.Id, $"ledger-category:{suffix}", 2);
            var revenueCategory = SeedIds.Child(context.Tenant.Id, $"ledger-category:{suffix}", 3);
            var categories = new[]
            {
                (assetCategory, "ASSET", "الأصول", "Assets", "Asset", "Debit"),
                (liabilityCategory, "LIABILITY", "الالتزامات", "Liabilities", "Liability", "Credit"),
                (revenueCategory, "REVENUE", "الإيرادات", "Revenue", "Revenue", "Credit")
            };
            foreach (var category in categories)
                await context.ExecuteAsync("insert into finance_ledger.account_category (tenant_id,legal_entity_id,id,code,name_ar,name_en,account_type,normal_balance,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Active',1,$9,$9) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, category.Item1, category.Item2, category.Item3, category.Item4, category.Item5, category.Item6, context.AsOf);

            var assets = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 1);
            var receivable = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 2);
            var cash = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 3);
            var revenue = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 4);
            var salesRevenue = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 5);
            var outputTax = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 6);
            var accounts = new[]
            {
                (assets, "110000", "الأصول", "Assets", "Asset", "Group", assetCategory, (Guid?)null, false),
                (receivable, "110100", "العملاء", "Accounts receivable", "Asset", "Receivable", assetCategory, (Guid?)assets, true),
                (cash, "110200", "النقدية والبنوك", "Cash and banks", "Asset", "Cash", assetCategory, (Guid?)assets, true),
                (revenue, "410000", "الإيرادات", "Revenue", "Revenue", "Group", revenueCategory, (Guid?)null, false),
                (salesRevenue, "410100", "إيرادات المبيعات", "Sales revenue", "Revenue", "Sales", revenueCategory, (Guid?)revenue, true),
                (outputTax, "210100", "ضريبة القيمة المضافة المستحقة", "Output VAT", "Liability", "OutputTax", liabilityCategory, (Guid?)null, true)
            };
            foreach (var account in accounts)
                await context.ExecuteAsync("insert into finance_ledger.account (tenant_id,legal_entity_id,id,category_id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,currency_policy,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,case when $8 in ('Liability','Revenue','Equity') then 'Credit' else 'Debit' end,$10,$11,'BaseOnly','Active',1,$12,$12) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, account.Item1, account.Item7, account.Item2, account.Item3, account.Item4, account.Item5, account.Item6, account.Item8, account.Item9, context.AsOf);
            accountCount += accounts.Length;

            foreach (var journal in new[] { ("SALES", "المبيعات", "Sales", "Sales", 1), ("GENERAL", "عام", "General", "General", 2), ("OPENING", "الأرصدة الافتتاحية", "Opening balances", "Opening", 3) })
                await context.ExecuteAsync("insert into finance_ledger.journal (tenant_id,legal_entity_id,id,code,name_ar,name_en,journal_type,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Active',1,$8,$8) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, SeedIds.Child(context.Tenant.Id, $"ledger-journal:{suffix}", journal.Item5), journal.Item1, journal.Item2, journal.Item3, journal.Item4, context.AsOf);

            var fiscalYearId = SeedIds.Child(context.Tenant.Id, $"ledger-year:{suffix}", 1);
            await context.ExecuteAsync("insert into finance_ledger.fiscal_year (tenant_id,legal_entity_id,id,code,start_date,end_date,status,version,created_at,updated_at) values ($1,$2,$3,'FY2026','2026-01-01','2026-12-31','Open',1,$4,$4) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, fiscalYearId, context.AsOf);
            for (var month = 1; month <= 12; month++)
            {
                var start = new DateOnly(2026, month, 1); var end = start.AddMonths(1).AddDays(-1);
                await context.ExecuteAsync("insert into finance_ledger.accounting_period (tenant_id,legal_entity_id,id,fiscal_year_id,code,start_date,end_date,sequence_no,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Open',1,$9,$9) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, SeedIds.Child(context.Tenant.Id, $"ledger-period:{suffix}", month), fiscalYearId, $"{month:00}", start, end, month, context.AsOf);
                periodCount++;
            }

            var configId = SeedIds.Child(context.Tenant.Id, $"ledger-config:{suffix}", 1);
            await context.ExecuteAsync("insert into finance_ledger.accounting_configuration_version (tenant_id,legal_entity_id,id,version_number,effective_from,status,snapshot_hash,created_at) values ($1,$2,$3,1,'2026-01-01','Active','development-ledger-v1',$4) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, configId, context.AsOf);
            var roleReceivable = await AddRoleAsync(context, entityId, suffix, 1, "Receivable", "حساب العملاء", "Receivables", cancellationToken);
            var roleRevenue = await AddRoleAsync(context, entityId, suffix, 2, "SalesRevenue", "إيرادات المبيعات", "Sales revenue", cancellationToken);
            var roleTax = await AddRoleAsync(context, entityId, suffix, 3, "OutputTax", "ضريبة المخرجات", "Output tax", cancellationToken);
            await AddMappingAsync(context, entityId, suffix, 1, roleReceivable, receivable, "Receivable", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, suffix, 2, roleRevenue, salesRevenue, "SalesRevenue", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, suffix, 3, roleTax, outputTax, "OutputTax", null, null, "VAT14", cancellationToken);

            var ruleId = SeedIds.Child(context.Tenant.Id, $"ledger-rule:{suffix}", 1); var ruleVersionId = SeedIds.Child(context.Tenant.Id, $"ledger-rule-version:{suffix}", 1);
            await context.ExecuteAsync("insert into finance_ledger.posting_rule (tenant_id,legal_entity_id,id,fact_type,fact_version,code,status,version,created_at) values ($1,$2,$3,'CustomerInvoicePosted',1,'CUSTOMER_INVOICE_V1','Active',1,$4) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, ruleId, context.AsOf);
            await context.ExecuteAsync("insert into finance_ledger.posting_rule_version (tenant_id,legal_entity_id,id,posting_rule_id,version_number,effective_from,status) values ($1,$2,$3,$4,1,'2026-01-01','Active') on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, ruleVersionId, ruleId);
            foreach (var leg in new[] { (1, "Receivable", "GrossAmount", "Debit"), (2, "SalesRevenue", "NetAmount", "Credit"), (3, "OutputTax", "TaxAmount", "Credit") })
                await context.ExecuteAsync("insert into finance_ledger.posting_rule_leg (tenant_id,legal_entity_id,id,posting_rule_version_id,sequence_no,semantic_role,amount_expression,direction) values ($1,$2,$3,$4,$5,$6,$7,$8) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, SeedIds.Child(context.Tenant.Id, $"ledger-rule-leg:{suffix}", leg.Item1), ruleVersionId, leg.Item1, leg.Item2, leg.Item3, leg.Item4);

            await AddSimpleRuleAsync(context, entityId, suffix, 2, "ManualGeneralJournalPosted", "MANUAL_GENERAL_JOURNAL_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 3, "OpeningBalancePosted", "OPENING_BALANCE_V1", cancellationToken);
        }
        return new SeedLedgerSummary(entities.Length, accountCount, periodCount, true);
    }

    private static async Task AddSimpleRuleAsync(SeedContext context, Guid entityId, string suffix, int index, string factType, string code, CancellationToken cancellationToken)
    {
        var ruleId = SeedIds.Child(context.Tenant.Id, $"ledger-rule:{suffix}", index); var versionId = SeedIds.Child(context.Tenant.Id, $"ledger-rule-version:{suffix}", index);
        await context.ExecuteAsync("insert into finance_ledger.posting_rule (tenant_id,legal_entity_id,id,fact_type,fact_version,code,status,version,created_at) values ($1,$2,$3,$4,1,$5,'Active',1,$6) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, ruleId, factType, code, context.AsOf);
        await context.ExecuteAsync("insert into finance_ledger.posting_rule_version (tenant_id,legal_entity_id,id,posting_rule_id,version_number,effective_from,status) values ($1,$2,$3,$4,1,'2026-01-01','Active') on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, versionId, ruleId);
    }

    private static async Task<Guid> AddRoleAsync(SeedContext context, Guid entityId, string suffix, int index, string code, string nameAr, string nameEn, CancellationToken cancellationToken)
    {
        var id = SeedIds.Child(context.Tenant.Id, $"ledger-role:{suffix}", index);
        await context.ExecuteAsync("insert into finance_ledger.account_role (tenant_id,legal_entity_id,id,code,name_ar,name_en,status,version) values ($1,$2,$3,$4,$5,$6,'Active',1) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, id, code, nameAr, nameEn);
        return id;
    }

    private static async Task AddMappingAsync(SeedContext context, Guid entityId, string suffix, int index, Guid roleId, Guid accountId, string code, string? productCategory, string? partyGroup, string? taxCode, CancellationToken cancellationToken)
    {
        var mappingId = SeedIds.Child(context.Tenant.Id, $"ledger-mapping:{suffix}", index); var versionId = SeedIds.Child(context.Tenant.Id, $"ledger-mapping-version:{suffix}", index);
        await context.ExecuteAsync("insert into finance_ledger.account_mapping (tenant_id,legal_entity_id,id,role_id,code,status,version) values ($1,$2,$3,$4,$5,'Active',1) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, mappingId, roleId, code);
        await context.ExecuteAsync("insert into finance_ledger.account_mapping_version (tenant_id,legal_entity_id,id,mapping_id,version_number,effective_from,product_category_code,party_group_code,tax_code,specificity_rank,target_account_id,status) values ($1,$2,$3,$4,1,'2026-01-01',$5::text,$6::text,$7::text,case when $5::text is not null or $6::text is not null or $7::text is not null then 1 else 0 end,$8,'Active') on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, versionId, mappingId, (object?)productCategory ?? DBNull.Value, (object?)partyGroup ?? DBNull.Value, (object?)taxCode ?? DBNull.Value, accountId);
    }
}

public sealed record SeedLedgerSummary(int LegalEntities, int Accounts, int Periods, bool HasInvoiceRule);
