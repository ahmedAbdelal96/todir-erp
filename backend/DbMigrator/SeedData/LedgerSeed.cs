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
            var expenseCategory = SeedIds.Child(context.Tenant.Id, $"ledger-category:{suffix}", 4);
            var equityCategory = SeedIds.Child(context.Tenant.Id, $"ledger-category:{suffix}", 5);
            var categories = new[]
            {
                (assetCategory, "ASSET", "الأصول", "Assets", "Asset", "Debit"),
                (liabilityCategory, "LIABILITY", "الالتزامات", "Liabilities", "Liability", "Credit"),
                (revenueCategory, "REVENUE", "الإيرادات", "Revenue", "Revenue", "Credit"),
                (expenseCategory, "EXPENSE", "المصروفات", "Expenses", "Expense", "Debit"),
                (equityCategory, "EQUITY", "حقوق الملكية", "Equity", "Equity", "Credit")
            };
            foreach (var category in categories)
                await context.ExecuteAsync("insert into finance_ledger.account_category (tenant_id,legal_entity_id,id,code,name_ar,name_en,account_type,normal_balance,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Active',1,$9,$9) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, category.Item1, category.Item2, category.Item3, category.Item4, category.Item5, category.Item6, context.AsOf);

            var assets = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 1);
            var receivable = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 2);
            var cash = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 3);
            var revenue = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 4);
            var salesRevenue = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 5);
            var outputTax = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 6);
            var payable = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 7);
            var inputTax = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 13);
            var purchaseExpense = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 14);
            var inventoryAsset = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 15);
            var cogs = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 16);
            var inventoryOffset = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 17);
            var inventoryAdjustmentGain = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 18);
            var inventoryAdjustmentLoss = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 19);
            var fxGain = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 8);
            var fxLoss = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 9);
            var retainedEarnings = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 10);
            var badDebtExpense = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 11);
            var writeOffIncome = SeedIds.Child(context.Tenant.Id, $"ledger-account:{suffix}", 12);
            var accounts = new[]
            {
                (assets, "110000", "الأصول", "Assets", "Asset", "Group", assetCategory, (Guid?)null, false),
                (receivable, "110100", "العملاء", "Accounts receivable", "Asset", "Receivable", assetCategory, (Guid?)assets, true),
                (cash, "110200", "النقدية والبنوك", "Cash and banks", "Asset", "Cash", assetCategory, (Guid?)assets, true),
                (revenue, "410000", "الإيرادات", "Revenue", "Revenue", "Group", revenueCategory, (Guid?)null, false),
                (salesRevenue, "410100", "إيرادات المبيعات", "Sales revenue", "Revenue", "Sales", revenueCategory, (Guid?)revenue, true),
                (outputTax, "210100", "ضريبة القيمة المضافة المستحقة", "Output VAT", "Liability", "OutputTax", liabilityCategory, (Guid?)null, true),
                (payable, "210200", "الموردون", "Accounts payable", "Liability", "Payable", liabilityCategory, (Guid?)null, true),
                (inputTax, "120100", "ضريبة القيمة المضافة القابلة للاسترداد", "Recoverable input VAT", "Asset", "InputTax", assetCategory, (Guid?)assets, true),
                (purchaseExpense, "510100", "مصروفات المشتريات", "Purchase expenses", "Expense", "Purchases", expenseCategory, (Guid?)null, true),
                (inventoryAsset, "130000", "المخزون", "Inventory asset", "Asset", "InventoryAsset", assetCategory, (Guid?)assets, true),
                (cogs, "520000", "تكلفة المبيعات", "Cost of goods sold", "Expense", "COGS", expenseCategory, (Guid?)null, true),
                (inventoryOffset, "210300", "بضاعة مستلمة غير مفوترة", "Goods received not invoiced", "Liability", "InventoryClearing", liabilityCategory, (Guid?)null, true),
                (inventoryAdjustmentGain, "430000", "أرباح تسويات المخزون", "Inventory adjustment gains", "Revenue", "InventoryVariance", revenueCategory, (Guid?)revenue, true),
                (inventoryAdjustmentLoss, "530100", "خسائر وتسويات المخزون", "Inventory adjustment losses", "Expense", "InventoryVariance", expenseCategory, (Guid?)null, true),
                (fxGain, "999000", "أرباح فروق العملة", "FX gain", "Revenue", "FXGain", revenueCategory, (Guid?)null, true),
                (fxLoss, "999100", "خسائر فروق العملة", "FX loss", "Expense", "FXLoss", expenseCategory, (Guid?)null, true),
                (retainedEarnings, "310000", "الأرباح المحتجزة", "Retained earnings", "Equity", "RetainedEarnings", equityCategory, (Guid?)null, true),
                (badDebtExpense, "530000", "ديون معدومة", "Bad debt expense", "Expense", "BadDebt", expenseCategory, (Guid?)null, true),
                (writeOffIncome, "420000", "إيرادات تسوية الديون", "Write-off income", "Revenue", "WriteOff", revenueCategory, (Guid?)revenue, true)
            };
            foreach (var account in accounts)
                await context.ExecuteAsync("insert into finance_ledger.account (tenant_id,legal_entity_id,id,category_id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,currency_policy,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,case when $8 in ('Liability','Revenue','Equity') then 'Credit' else 'Debit' end,$10,$11,'BaseOnly','Active',1,$12,$12) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, account.Item1, account.Item7, account.Item2, account.Item3, account.Item4, account.Item5, account.Item6, account.Item8, account.Item9, context.AsOf);
            accountCount += accounts.Length;

            foreach (var journal in new[] { ("SALES", "المبيعات", "Sales", "Sales", 1), ("PURCHASE", "المشتريات", "Purchases", "Purchases", 6), ("GENERAL", "عام", "General", "General", 2), ("OPENING", "الأرصدة الافتتاحية", "Opening balances", "Opening", 3), ("FX", "فروق العملة", "Foreign exchange", "Fx", 4), ("ADJUSTMENT", "تسويات", "Adjustments", "General", 5) })
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
            var entityConfigurationId = SeedIds.Child(context.Tenant.Id, $"ledger-entity-configuration:{suffix}", 1);
            await context.ExecuteAsync("insert into finance_ledger.ledger_entity_configuration (tenant_id,legal_entity_id,id,mode,setup_template_code,active_configuration_version_id,initialized_at,version) values ($1,$2,$3,'Managed','development-core',$4,$5,1) on conflict (tenant_id,legal_entity_id) do nothing", cancellationToken, context.Tenant.Id, entityId, entityConfigurationId, configId, context.AsOf);
            var activeConfigId = await context.ScalarAsync<Guid>("select active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", cancellationToken, context.Tenant.Id, entityId);
            foreach (var producer in new[] { ("sales", "CustomerInvoicePosted"), ("sales", "CustomerCreditNotePosted"), ("invoicing", "CustomerInvoicePosted"), ("invoicing", "CustomerCreditNotePosted"), ("invoicing", "VendorBillPosted"), ("invoicing", "VendorCreditNotePosted"), ("invoicing", "CustomerDebitNotePosted"), ("invoicing", "VendorDebitNotePosted"), ("payments", "PaymentReceived"), ("payments", "PaymentMade"), ("payments", "RefundPaid"), ("payments", "PaymentFeeRecognized"), ("payments", "PaymentFeeReversed"), ("payments", "InternalTransferCompleted"), ("inventory", "InventoryReceiptValued"), ("inventory", "InventoryIssueValued"), ("inventory", "InventoryCostAdjustment"), ("inventory", "InventoryValuationReversal"), ("payroll", "PayrollPosted"), ("tax", "TaxAdjustment"), ("subledger", "SettlementFxDifference"), ("subledger", "SettlementWriteOff") })
            {
                var producerId = SeedIds.Child(context.Tenant.Id, $"ledger-producer:{suffix}:{producer.Item1}:{producer.Item2}", 1);
                await context.ExecuteAsync("insert into finance_ledger.fact_producer_registration (tenant_id,legal_entity_id,id,producer_module,fact_type,fact_version,status,registered_at) values ($1,$2,$3,$4,$5,1,'Active',$6) on conflict (tenant_id,legal_entity_id,producer_module,fact_type,fact_version) do nothing", cancellationToken, context.Tenant.Id, entityId, producerId, producer.Item1, producer.Item2, context.AsOf);
            }
            var roleReceivable = await AddRoleAsync(context, entityId, suffix, 1, "Receivable", "حساب العملاء", "Receivables", cancellationToken);
            var roleRevenue = await AddRoleAsync(context, entityId, suffix, 2, "SalesRevenue", "إيرادات المبيعات", "Sales revenue", cancellationToken);
            var roleTax = await AddRoleAsync(context, entityId, suffix, 3, "OutputTax", "ضريبة المخرجات", "Output tax", cancellationToken);
            var rolePayable = await AddRoleAsync(context, entityId, suffix, 4, "Payable", "حساب الموردين", "Payables", cancellationToken);
            var roleInputTax = await AddRoleAsync(context, entityId, suffix, 10, "InputTax", "ضريبة المدخلات", "Input tax", cancellationToken);
            var rolePurchaseExpense = await AddRoleAsync(context, entityId, suffix, 11, "PurchaseExpense", "مصروفات المشتريات", "Purchase expenses", cancellationToken);
            var roleFxGain = await AddRoleAsync(context, entityId, suffix, 5, "FXGain", "أرباح فروق العملة", "FX gain", cancellationToken);
            var roleFxLoss = await AddRoleAsync(context, entityId, suffix, 6, "FXLoss", "خسائر فروق العملة", "FX loss", cancellationToken);
            var roleRetained = await AddRoleAsync(context, entityId, suffix, 7, "RetainedEarnings", "الأرباح المحتجزة", "Retained earnings", cancellationToken);
            var roleBadDebt = await AddRoleAsync(context, entityId, suffix, 8, "BadDebtExpense", "مصروف الديون المعدومة", "Bad debt expense", cancellationToken);
            var roleWriteOffIncome = await AddRoleAsync(context, entityId, suffix, 9, "WriteOffIncome", "إيراد تسوية الديون", "Write-off income", cancellationToken);
            var roleCash = await AddRoleAsync(context, entityId, suffix, 12, "Cash", "النقدية", "Cash and bank", cancellationToken);
            var rolePaymentFeeExpense = await AddRoleAsync(context, entityId, suffix, 13, "PaymentFeeExpense", "مصروف رسوم الدفع", "Payment fee expense", cancellationToken);
            var roleInventoryAsset = await AddRoleAsync(context, entityId, suffix, 14, "InventoryAsset", "المخزون", "Inventory asset", cancellationToken);
            var roleCogs = await AddRoleAsync(context, entityId, suffix, 15, "COGS", "تكلفة المبيعات", "Cost of goods sold", cancellationToken);
            var roleInventoryOffset = await AddRoleAsync(context, entityId, suffix, 16, "InventoryOffset", "بضاعة مستلمة غير مفوترة", "Goods received not invoiced", cancellationToken);
            var roleInventoryGain = await AddRoleAsync(context, entityId, suffix, 17, "InventoryAdjustmentGain", "أرباح تسويات المخزون", "Inventory adjustment gains", cancellationToken);
            var roleInventoryLoss = await AddRoleAsync(context, entityId, suffix, 18, "InventoryAdjustmentLoss", "خسائر وتسويات المخزون", "Inventory adjustment losses", cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 1, roleReceivable, receivable, "Receivable", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 2, roleRevenue, salesRevenue, "SalesRevenue", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 3, roleTax, outputTax, "OutputTax", null, null, "VAT14", cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 12, roleTax, outputTax, "OutputTax-Demo-Standard", null, null, "DEMO-STANDARD", cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 4, rolePayable, payable, "Payable", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 10, roleInputTax, inputTax, "InputTax", null, null, "VAT14", cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 13, roleInputTax, inputTax, "InputTax-Demo-Standard", null, null, "DEMO-STANDARD", cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 11, rolePurchaseExpense, purchaseExpense, "PurchaseExpense", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 5, roleFxGain, fxGain, "FXGain", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 6, roleFxLoss, fxLoss, "FXLoss", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 7, roleRetained, retainedEarnings, "RetainedEarnings", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 8, roleBadDebt, badDebtExpense, "BadDebtExpense", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 9, roleWriteOffIncome, writeOffIncome, "WriteOffIncome", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 14, roleCash, cash, "Cash", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 15, rolePaymentFeeExpense, purchaseExpense, "PaymentFeeExpense", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 16, roleInventoryAsset, inventoryAsset, "InventoryAsset", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 17, roleCogs, cogs, "COGS", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 18, roleInventoryOffset, inventoryOffset, "InventoryOffset", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 19, roleInventoryGain, inventoryAdjustmentGain, "InventoryAdjustmentGain", null, null, null, cancellationToken);
            await AddMappingAsync(context, entityId, activeConfigId, suffix, 20, roleInventoryLoss, inventoryAdjustmentLoss, "InventoryAdjustmentLoss", null, null, null, cancellationToken);

            var ruleId = SeedIds.Child(context.Tenant.Id, $"ledger-rule:{suffix}", 1); var ruleVersionId = SeedIds.Child(context.Tenant.Id, $"ledger-rule-version:{suffix}", 1);
            await context.ExecuteAsync("insert into finance_ledger.posting_rule (tenant_id,legal_entity_id,id,fact_type,fact_version,code,status,version,created_at) values ($1,$2,$3,'CustomerInvoicePosted',1,'CUSTOMER_INVOICE_V1','Active',1,$4) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, ruleId, context.AsOf);
            await context.ExecuteAsync("insert into finance_ledger.posting_rule_version (tenant_id,legal_entity_id,id,posting_rule_id,version_number,effective_from,status) values ($1,$2,$3,$4,1,'2026-01-01','Active') on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, ruleVersionId, ruleId);
            foreach (var leg in new[] { (1, "Receivable", "GrossAmount", "Debit"), (2, "SalesRevenue", "NetAmount", "Credit"), (3, "OutputTax", "TaxAmount", "Credit") })
                await context.ExecuteAsync("insert into finance_ledger.posting_rule_leg (tenant_id,legal_entity_id,id,posting_rule_version_id,sequence_no,semantic_role,amount_expression,direction) values ($1,$2,$3,$4,$5,$6,$7,$8) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, SeedIds.Child(context.Tenant.Id, $"ledger-rule-leg:{suffix}", leg.Item1), ruleVersionId, leg.Item1, leg.Item2, leg.Item3, leg.Item4);

            await AddSimpleRuleAsync(context, entityId, suffix, 2, "ManualGeneralJournalPosted", "MANUAL_GENERAL_JOURNAL_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 3, "OpeningBalancePosted", "OPENING_BALANCE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 4, "SettlementFxDifference", "SETTLEMENT_FX_DIFFERENCE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 5, "SettlementWriteOff", "SETTLEMENT_WRITE_OFF_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 6, "CustomerCreditNotePosted", "CUSTOMER_CREDIT_NOTE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 7, "VendorBillPosted", "VENDOR_BILL_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 8, "VendorCreditNotePosted", "VENDOR_CREDIT_NOTE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 9, "CustomerDebitNotePosted", "CUSTOMER_DEBIT_NOTE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 10, "VendorDebitNotePosted", "VENDOR_DEBIT_NOTE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 11, "PaymentReceived", "PAYMENT_RECEIVED_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 12, "PaymentMade", "PAYMENT_MADE_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 13, "RefundPaid", "PAYMENT_REFUND_PAID_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 16, "PaymentFeeRecognized", "PAYMENT_FEE_RECOGNIZED_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 17, "PaymentFeeReversed", "PAYMENT_FEE_REVERSED_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 14, "InternalTransferCompleted", "INTERNAL_TRANSFER_COMPLETED_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 21, "InventoryReceiptValued", "INVENTORY_RECEIPT_VALUED_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 22, "InventoryIssueValued", "INVENTORY_ISSUE_VALUED_V1", cancellationToken);
            await AddSimpleRuleAsync(context, entityId, suffix, 23, "InventoryCostAdjustment", "INVENTORY_COST_ADJUSTMENT_V1", cancellationToken);
        }
        return new SeedLedgerSummary(entities.Length, accountCount, periodCount, true);
    }

    private static async Task AddSimpleRuleAsync(SeedContext context, Guid entityId, string suffix, int index, string factType, string code, CancellationToken cancellationToken)
    {
        var ruleId = SeedIds.Child(context.Tenant.Id, $"ledger-rule:{suffix}", index); var versionId = SeedIds.Child(context.Tenant.Id, $"ledger-rule-version:{suffix}", index);
        await context.ExecuteAsync("insert into finance_ledger.posting_rule (tenant_id,legal_entity_id,id,fact_type,fact_version,code,status,version,created_at) values ($1,$2,$3,$4,1,$5,'Active',1,$6) on conflict (tenant_id,legal_entity_id,fact_type,fact_version,code) do nothing", cancellationToken, context.Tenant.Id, entityId, ruleId, factType, code, context.AsOf);
        ruleId = await context.ScalarAsync<Guid>("select id from finance_ledger.posting_rule where tenant_id=$1 and legal_entity_id=$2 and fact_type=$3 and fact_version=1 and code=$4", cancellationToken, context.Tenant.Id, entityId, factType, code);
        await context.ExecuteAsync("insert into finance_ledger.posting_rule_version (tenant_id,legal_entity_id,id,posting_rule_id,version_number,effective_from,status) values ($1,$2,$3,$4,1,'2026-01-01','Active') on conflict (tenant_id,posting_rule_id,version_number) do nothing", cancellationToken, context.Tenant.Id, entityId, versionId, ruleId);
        versionId = await context.ScalarAsync<Guid>("select id from finance_ledger.posting_rule_version where tenant_id=$1 and legal_entity_id=$2 and posting_rule_id=$3 and version_number=1", cancellationToken, context.Tenant.Id, entityId, ruleId);
        var legs = factType switch
        {
            "PaymentReceived" => new[] { (1, "Cash", "BaseAmount", "Debit"), (2, "Receivable", "BaseAmount", "Credit") },
            "PaymentMade" => new[] { (1, "Payable", "BaseAmount", "Debit"), (2, "Cash", "BaseAmount", "Credit") },
            "RefundPaid" => new[] { (1, "Receivable", "BaseAmount", "Debit"), (2, "Cash", "BaseAmount", "Credit") },
            "PaymentFeeRecognized" => new[] { (1, "PaymentFeeExpense", "BaseAmount", "Debit"), (2, "Cash", "BaseAmount", "Credit") },
            "PaymentFeeReversed" => new[] { (1, "Cash", "BaseAmount", "Debit"), (2, "PaymentFeeExpense", "BaseAmount", "Credit") },
            "InternalTransferCompleted" => new[] { (1, "Cash", "BaseAmount", "Debit"), (2, "Cash", "BaseAmount", "Credit") },
            _ => Array.Empty<(int, string, string, string)>()
        };
        foreach (var leg in legs)
            await context.ExecuteAsync("insert into finance_ledger.posting_rule_leg (tenant_id,legal_entity_id,id,posting_rule_version_id,sequence_no,semantic_role,amount_expression,direction) values ($1,$2,$3,$4,$5,$6,$7,$8) on conflict (tenant_id,posting_rule_version_id,sequence_no) do nothing", cancellationToken, context.Tenant.Id, entityId, SeedIds.Child(context.Tenant.Id, $"ledger-rule-leg:{suffix}:{factType}", leg.Item1), versionId, leg.Item1, leg.Item2, leg.Item3, leg.Item4);
    }

    private static async Task<Guid> AddRoleAsync(SeedContext context, Guid entityId, string suffix, int index, string code, string nameAr, string nameEn, CancellationToken cancellationToken)
    {
        var id = SeedIds.Child(context.Tenant.Id, $"ledger-role:{suffix}", index);
        await context.ExecuteAsync("insert into finance_ledger.account_role (tenant_id,legal_entity_id,id,code,name_ar,name_en,status,version) values ($1,$2,$3,$4,$5,$6,'Active',1) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, id, code, nameAr, nameEn);
        return id;
    }

    private static async Task AddMappingAsync(SeedContext context, Guid entityId, Guid configurationId, string suffix, int index, Guid roleId, Guid accountId, string code, string? productCategory, string? partyGroup, string? taxCode, CancellationToken cancellationToken)
    {
        var mappingId = SeedIds.Child(context.Tenant.Id, $"ledger-mapping:{suffix}", index);
        await context.ExecuteAsync("insert into finance_ledger.account_mapping (tenant_id,legal_entity_id,id,role_id,code,status,version) values ($1,$2,$3,$4,$5,'Active',1) on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, mappingId, roleId, code);

        // A seed may run after Ledger setup has already materialized this exact mapping version.
        // Find it by its business key so old seed IDs cannot create a second active match.
        var existingVersion = await context.ScalarAsync<long>("select count(*) from finance_ledger.account_mapping_version where tenant_id=$1 and legal_entity_id=$2 and mapping_id=$3 and accounting_configuration_version_id=$4 and effective_from='2026-01-01' and product_category_code is not distinct from $5::text and party_group_code is not distinct from $6::text and tax_code is not distinct from $7::text and target_account_id=$8 and status='Active'", cancellationToken, context.Tenant.Id, entityId, mappingId, configurationId, (object?)productCategory ?? DBNull.Value, (object?)partyGroup ?? DBNull.Value, (object?)taxCode ?? DBNull.Value, accountId);
        if (existingVersion > 0) return;

        var versionId = SeedIds.Child(context.Tenant.Id, $"ledger-mapping-version:{suffix}:{configurationId:N}", index);
        var versionNumber = await context.ScalarAsync<long>("select coalesce((select version_number from finance_ledger.account_mapping_version where tenant_id=$1 and id=$2),(select coalesce(max(version_number),0)+1 from finance_ledger.account_mapping_version where tenant_id=$1 and mapping_id=$3))", cancellationToken, context.Tenant.Id, versionId, mappingId);
        await context.ExecuteAsync("insert into finance_ledger.account_mapping_version (tenant_id,legal_entity_id,id,mapping_id,accounting_configuration_version_id,version_number,effective_from,product_category_code,party_group_code,tax_code,specificity_rank,target_account_id,status) values ($1,$2,$3,$4,$5,$6,'2026-01-01',$7::text,$8::text,$9::text,case when $7::text is not null or $8::text is not null or $9::text is not null then 1 else 0 end,$10,'Active') on conflict (tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entityId, versionId, mappingId, configurationId, versionNumber, (object?)productCategory ?? DBNull.Value, (object?)partyGroup ?? DBNull.Value, (object?)taxCode ?? DBNull.Value, accountId);
    }
}

public sealed record SeedLedgerSummary(int LegalEntities, int Accounts, int Periods, bool HasInvoiceRule);
