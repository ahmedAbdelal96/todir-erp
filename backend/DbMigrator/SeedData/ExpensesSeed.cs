namespace DbMigrator.SeedData;

public static class ExpensesSeed
{
    public static async Task<ExpensesSeedSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var category = SeedIds.Child(context.Tenant.Id, "expenses-category", 1);
        var version = SeedIds.Child(context.Tenant.Id, "expenses-category-version", 1);
        await context.ExecuteAsync("insert into business_expenses.expense_category(tenant_id,id,legal_entity_id,code,name,status,created_at) values($1,$2,$3,'TRAVEL','تنقلات ومهمات عمل','Active',$4) on conflict(tenant_id,legal_entity_id,code) do update set name=excluded.name,status='Active'", cancellationToken, context.Tenant.Id, category, context.EntityId, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense_category_version(tenant_id,id,category_id,version,status,requires_receipt,receipt_threshold,effective_from) values($1,$2,$3,1,'Published',true,500,'2025-01-01') on conflict(tenant_id,category_id,version) do update set status='Published',requires_receipt=true,receipt_threshold=500", cancellationToken, context.Tenant.Id, version, category);
        var draft = SeedIds.Child(context.Tenant.Id, "expenses-draft", 1);
        var submitted = SeedIds.Child(context.Tenant.Id, "expenses-submitted", 2);
        var finalized = SeedIds.Child(context.Tenant.Id, "expenses-finalized", 3);
        var employee = SeedIds.Stable($"{context.Tenant.Id:D}:hr:employee:0001");
        await context.ExecuteAsync("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,created_at,updated_at) values($1,$2,$3,$4,'مواصلات القاهرة',$5,180,'EGP','EmployeePaid',$6,'Draft','Pending','NotFinalized','NotSettled','Missing','NotEvaluated','مهمة عميل','مصروف مسودة',$7,$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, draft, context.EntityId, employee, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date), version, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,created_at,updated_at) values($1,$2,$3,$4,'فندق النيل',$5,1650,'EGP','EmployeePaid',$6,'Submitted','Pending','NotFinalized','NotSettled','Verified','Passed','مهمة عمل','بانتظار اعتماد المدير',$7,$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, submitted, context.EntityId, employee, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date.AddDays(-1)), version, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,finalized_at,created_at,updated_at) values($1,$2,$3,$4,'تاكسي الشركة',$5,320,'EGP','EmployeePaid',$6,'Finalized','Approved','Pending','ReimbursementPending','Verified','Passed','اجتماع عميل','التزام سداد موظف',$7,$7,$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, finalized, context.EntityId, employee, DateOnly.FromDateTime(context.AsOf.UtcDateTime.Date.AddDays(-2)), version, context.AsOf);
        await context.ExecuteAsync("insert into business_expenses.expense_reimbursement_obligation(tenant_id,id,legal_entity_id,expense_id,employee_id,amount,currency_code,status) values($1,$2,$3,$4,$5,320,'EGP','Pending') on conflict(tenant_id,expense_id) do nothing", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "expenses-reimbursement", 1), context.EntityId, finalized, employee);
        return new(1, 3, 1);
    }
}

public sealed record ExpensesSeedSummary(int Categories, int Expenses, int FinalizedExpenses);
