namespace DbMigrator.SeedData;

public static class ExpensesSeed
{
    public static async Task<ExpensesSeedSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var category = SeedIds.Child(context.Tenant.Id, "expenses-category", 1);
        var version = SeedIds.Child(context.Tenant.Id, "expenses-category-version", 1);
        await context.ExecuteAsync("insert into business_expenses.expense_category(tenant_id,id,legal_entity_id,code,name,status,created_at) values($1,$2,$3,'TRAVEL','تنقلات ومهمات عمل','Active',$4) on conflict(tenant_id,legal_entity_id,code) do update set name=excluded.name,status='Active'", cancellationToken, context.Tenant.Id, category, context.EntityId, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense_category_version(tenant_id,id,category_id,version,status,requires_receipt,receipt_threshold,effective_from) values($1,$2,$3,1,'Published',true,500,'2025-01-01') on conflict(tenant_id,category_id,version) do nothing", cancellationToken, context.Tenant.Id, version, category);

        // Published Rate Policies
        var mileagePolicyId = SeedIds.Child(context.Tenant.Id, "expenses-rate-mileage", 1);
        await context.ExecuteAsync("insert into business_expenses.expense_rate_policy(tenant_id,id,legal_entity_id,kind,version,status,effective_from,vehicle_type,currency_code,rate,created_at) values($1,$2,$3,'Mileage',1,'Published','2025-01-01','StandardCar','EGP',5.0000,$4) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, mileagePolicyId, context.EntityId, context.AsOf);

        var perDiemPolicyId = SeedIds.Child(context.Tenant.Id, "expenses-rate-perdiem", 1);
        await context.ExecuteAsync("insert into business_expenses.expense_rate_policy(tenant_id,id,legal_entity_id,kind,version,status,effective_from,location_code,currency_code,rate,breakfast_reduction,lunch_reduction,dinner_reduction,created_at) values($1,$2,$3,'PerDiem',1,'Published','2025-01-01','CAIRO','EGP',300.0000,50.0000,100.0000,100.0000,$4) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, perDiemPolicyId, context.EntityId, context.AsOf);

        var draft = SeedIds.Child(context.Tenant.Id, "expenses-draft", 1);
        var submitted = SeedIds.Child(context.Tenant.Id, "expenses-submitted", 2);
        var finalized = SeedIds.Child(context.Tenant.Id, "expenses-finalized", 3);
        var employee = SeedIds.Stable($"{context.Tenant.Id:D}:hr:employee:0001");
        var adminUser = SeedIds.Identity(context.Tenant.Id, "admin");

        await context.ExecuteAsync("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,created_at,updated_at) values($1,$2,$3,$4,'مواصلات القاهرة',$5,180,'EGP','EmployeePaid',$6,'Draft','Pending','NotFinalized','NotSettled','Missing','NotEvaluated','مهمة عميل','مصروف مسودة',$7,$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, draft, context.EntityId, employee, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date), version, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,created_at,updated_at) values($1,$2,$3,$4,'فندق النيل',$5,1650,'EGP','EmployeePaid',$6,'Submitted','Pending','NotFinalized','NotSettled','Verified','Passed','مهمة عمل','بانتظار اعتماد المدير',$7,$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, submitted, context.EntityId, employee, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date.AddDays(-1)), version, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,finalized_at,created_at,updated_at) values($1,$2,$3,$4,'تاكسي الشركة',$5,320,'EGP','EmployeePaid',$6,'Finalized','Approved','Pending','ReimbursementPending','Verified','Passed','اجتماع عميل','التزام سداد موظف',$7,$7,$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, finalized, context.EntityId, employee, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date.AddDays(-2)), version, context.AsOf);

        // Platform Approval Request for submitted expense
        var approvalId = SeedIds.Child(context.Tenant.Id, "expenses-approval", 1);
        await context.ExecuteAsync("insert into platform.approval_request(tenant_id,id,legal_entity_id,owner_module,owner_entity_type,owner_entity_id,resource_version,state,requested_by,request_reason,created_at) values($1,$2,$3,'expenses','expense',$4,1,'Pending',$5,'طلب اعتماد مصروف مهمة عمل',$6) on conflict(tenant_id,owner_module,owner_entity_type,owner_entity_id,resource_version) do nothing", cancellationToken, context.Tenant.Id, approvalId, context.EntityId, submitted, adminUser, context.AsOf);
        await context.ExecuteAsync("update business_expenses.expense set approval_request_id=$3 where tenant_id=$1 and id=$2 and approval_request_id is null", cancellationToken, context.Tenant.Id, submitted, approvalId);

        // Corporate Card Transaction
        var cardTxId = SeedIds.Child(context.Tenant.Id, "expenses-card-tx", 1);
        await context.ExecuteAsync("insert into business_expenses.expense_card_transaction(tenant_id,id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,created_at) values($1,$2,$3,'TX-CARD-9901','محطة وقود النيل',$4,650.0000,'EGP','Unmatched',$5,$6) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, cardTxId, context.EntityId, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date.AddDays(-1)), employee, context.AsOf);

        // Business Advance
        var advanceId = SeedIds.Child(context.Tenant.Id, "expenses-advance", 1);
        await context.ExecuteAsync("insert into business_expenses.business_advance(tenant_id,id,legal_entity_id,employee_id,amount,currency_code,issued_amount,applied_amount,returned_amount,state,purpose,created_at,issued_at) values($1,$2,$3,$4,5000.0000,'EGP',5000.0000,0.0000,0.0000,'Issued','سلفة سفر لمعرض الإسكندرية الدولي',$5,$5) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, advanceId, context.EntityId, employee, context.AsOf);

        // Reimbursement Obligation
        await context.ExecuteAsync("insert into business_expenses.expense_reimbursement_obligation(tenant_id,id,legal_entity_id,expense_id,employee_id,amount,currency_code,status) values($1,$2,$3,$4,$5,320,'EGP','Pending') on conflict(tenant_id,expense_id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "expenses-reimbursement", 1), context.EntityId, finalized, employee);

        return new(1, 3, 1);
    }
}

public sealed record ExpensesSeedSummary(int Categories, int Expenses, int FinalizedExpenses);
