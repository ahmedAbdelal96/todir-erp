using Npgsql;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Ledger;
using Shared.Contracts.Payments;
using Shared.Contracts.Platform;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Tax;
using Shared.Contracts.Hr;
using Modules.Expenses.DTOs;
using Modules.Expenses.Services;
using System.Text.Json;

namespace Modules.Expenses.Repositories;

public sealed class ExpenseRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, ITransactionalLedgerPostingCapability ledgerPosting, IExpensePaymentCapability? expensePayments = null, IPlatformFileReferenceCapability? files = null, IEmployeeRepaymentPaymentCapability? repaymentPayments = null, IBusinessAdvancePaymentCapability? advancePayments = null, IPlatformFileUploadCapability? fileUploads = null, IPlatformApprovalCapability? approvals = null, IExpenseOcrProvider? ocrProvider = null, IFinanceBaseCapabilities? financeBase = null, ITaxCapabilities? tax = null, IHumanResourcesPayrollCapability? hr = null, ITransactionalPlanningBudgetControlCapability? budgetControl = null)
{
    private async Task<Guid> ResolveSelfEmployeeAsync(TenantId tenant, Guid membershipId, Guid entity, CancellationToken ct) =>
        hr is null
            ? throw new ExpenseRepositoryException("expenses.hr_capability_unavailable")
            : await hr.GetSelfEmployeeAsync(tenant, membershipId, entity, ct) ?? throw new ExpenseRepositoryException("expenses.self_employee_not_found");

    public async Task<ExpenseDto> CreateSelfAsync(TenantId tenant, Guid membershipId, CreateMyExpenseRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, request.LegalEntityId, ct);
        return await CreateAsync(tenant, new CreateExpenseRequest(request.LegalEntityId, employeeId, request.MerchantName, request.ExpenseDate, request.Amount, request.CurrencyCode, request.PaymentSource, request.CategoryVersionId, request.BusinessPurpose, request.Description, request.ProjectId, request.TaskId, request.CostCategory, request.Billable, request.BudgetBucketId), ct);
    }

    public async Task<ExpenseDto> SubmitSelfAsync(TenantId tenant, Guid membershipId, Guid entity, Guid expenseId, ExpenseActionRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        var expense = await GetAsync(tenant, entity, expenseId, ct);
        if (expense is null || expense.EmployeeId != employeeId) throw new ExpenseRepositoryException("expenses.expense_not_found");
        return await TransitionAsync(tenant, entity, expenseId, request, "submit", ct);
    }

    public async Task<MissingReceiptDeclarationDto> DeclareMissingReceiptSelfAsync(TenantId tenant, Guid membershipId, Guid actorId, Guid entity, Guid expenseId, DeclareMissingReceiptRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        var expense = await GetAsync(tenant, entity, expenseId, ct);
        if (expense is null || expense.EmployeeId != employeeId) throw new ExpenseRepositoryException("expenses.expense_not_found");
        return await DeclareMissingReceiptAsync(tenant, actorId, entity, expenseId, request, ct);
    }

    public async Task<ExpensePage<ExpenseDto>> ListSelfAsync(TenantId tenant, Guid membershipId, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
        => await ListAsync(tenant, entity, await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct), state, search, page, pageSize, ct);

    public async Task<ExpensePage<ExpenseCardTransactionDto>> ListSelfCardTransactionsAsync(TenantId tenant, Guid membershipId, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and employee_id=$3 and ($4::text is null or state=$4) and ($5::text is null or provider_reference ilike '%'||$5||'%' or merchant_name ilike '%'||$5||'%')";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_card_transaction where {where}", scope.Connection, scope.Transaction);
        Add(count, tenant.Value, entity, employeeId, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where {where} order by transaction_date desc,id desc offset $6 limit $7", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, employeeId, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize);
        var items = new List<ExpenseCardTransactionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadCard(reader)); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task<ExpenseDto> CreateExpenseFromCardTransactionSelfAsync(TenantId tenant, Guid membershipId, Guid entity, Guid cardId, CreateExpenseFromCardTransactionRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        return await CreateExpenseFromCardTransactionAsync(tenant, entity, cardId, request, ct, employeeId);
    }

    public async Task<ExpensePage<ExpenseReportDto>> ListSelfReportsAsync(TenantId tenant, Guid membershipId, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and employee_id=$3 and ($4::text is null or state=$4) and ($5::text is null or title ilike '%'||$5||'%')";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_report where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, employeeId, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at from business_expenses.expense_report where {where} order by created_at desc,id desc offset $6 limit $7", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, employeeId, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize);
        var list = new List<ExpenseReportDto>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize);
    }

    public async Task<ExpensePage<ReimbursementDto>> ListSelfReimbursementsAsync(TenantId tenant, Guid membershipId, Guid entity, int page, int pageSize, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and employee_id=$3";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_reimbursement_obligation where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, employeeId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var cmd = new NpgsqlCommand($"select id,expense_id,employee_id,amount,settled_amount,currency_code,status,created_at from business_expenses.expense_reimbursement_obligation where {where} order by created_at desc,id desc offset $4 limit $5", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, employeeId, (page - 1) * pageSize, pageSize);
        var list = new List<ReimbursementDto>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5).Trim(), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize);
    }

    public async Task<ExpensePage<BusinessAdvanceDto>> ListSelfAdvancesAsync(TenantId tenant, Guid membershipId, Guid entity, string? state, bool overdue, int page, int pageSize, CancellationToken ct)
        => await ListAdvancesAsync(tenant, entity, state, await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct), overdue, page, pageSize, ct);

    public async Task<BusinessAdvanceDto> CreateSelfAdvanceAsync(TenantId tenant, Guid membershipId, CreateBusinessAdvanceRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, request.LegalEntityId, ct);
        return await CreateAdvanceAsync(tenant, request with { EmployeeId = employeeId }, ct);
    }

    public async Task<ExpensePage<ExpenseEmployeeRepaymentDto>> ListSelfRepaymentsAsync(TenantId tenant, Guid membershipId, Guid entity, string? state, int page, int pageSize, CancellationToken ct)
        => await ListEmployeeRepaymentsAsync(tenant, entity, await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct), state, page, pageSize, ct);
    public async Task<ExpensePage<ExpenseDto>> ListAsync(TenantId tenant, Guid entity, Guid? employeeId, string? state, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); var offset = (page - 1) * pageSize;
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or employee_id=$3) and ($4::text is null or expense_state=$4) and ($5::text is null or merchant_name ilike '%'||$5||'%' or coalesce(description,'') ilike '%'||$5||'%')";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(employeeId), Db(state), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,finalized_at,base_amount,base_currency_code,tax_amount,financial_context_snapshot,tax_snapshot,approval_request_id,project_id,task_id,cost_category,billable from business_expenses.expense where {where} order by created_at desc,id desc offset $6 limit $7", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(employeeId), Db(state), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), offset, pageSize); var items = new List<ExpenseDto>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadExpense(reader)); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(items, total, page, pageSize);
    }

    public async Task<ExpenseDto?> GetAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,finalized_at,base_amount,base_currency_code,tax_amount,financial_context_snapshot,tax_snapshot,approval_request_id,project_id,task_id,cost_category,billable from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id); await using var r = await cmd.ExecuteReaderAsync(ct); var result = await r.ReadAsync(ct) ? ReadExpense(r) : null; await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<ExpenseDuplicateCandidateDto>> ListDuplicateCandidatesAsync(TenantId tenant, Guid entity, Guid expenseId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct);
        const string source = "(select employee_id,merchant_name,expense_date,amount,currency_code from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3) s";
        const string predicate = "e.tenant_id=$1 and e.legal_entity_id=$2 and e.id<>$3 and e.amount=s.amount and e.currency_code=s.currency_code and abs(e.expense_date-s.expense_date)<=3 and ((s.employee_id is not null and e.employee_id=s.employee_id) or similarity(lower(e.merchant_name),lower(s.merchant_name))>=0.65)";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense e cross join {source} where {predicate}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, expenseId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select e.id,e.merchant_name,e.expense_date,e.amount,e.currency_code,case when s.employee_id is not null and e.employee_id=s.employee_id and e.expense_date=s.expense_date and e.amount=s.amount then 'EmployeeDateAmount' when e.amount=s.amount and e.currency_code=s.currency_code then 'AmountCurrencyDate' else 'MerchantSimilarity' end,similarity(lower(e.merchant_name),lower(s.merchant_name)) from business_expenses.expense e cross join {source} where {predicate} order by similarity(lower(e.merchant_name),lower(s.merchant_name)) desc,e.expense_date desc,e.id offset $4 limit $5", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, expenseId, (page - 1) * pageSize, pageSize); var list = new List<ExpenseDuplicateCandidateDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetDecimal(3), reader.GetString(4), reader.GetString(5), Convert.ToDecimal(reader.GetDouble(6), CultureInfo.InvariantCulture))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(TenantId tenant, Guid entity, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select c.id,c.legal_entity_id,c.code,c.name,c.status,(select v.id from business_expenses.expense_category_version v where v.tenant_id=c.tenant_id and v.category_id=c.id and v.status='Published' order by v.effective_from desc,v.version desc limit 1) from business_expenses.expense_category c where c.tenant_id=$1 and c.legal_entity_id=$2 order by c.code", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity); var list = new List<ExpenseCategoryDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), GuidValue(r, 5))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpenseCategoryDto> CreateCategoryAsync(TenantId tenant, CreateExpenseCategoryRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ExpenseRepositoryException("expenses.category_invalid"); await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_category(tenant_id,id,legal_entity_id,code,name) values($1,$2,$3,$4,$5) returning id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, request.LegalEntityId, request.Code.Trim().ToUpperInvariant(), request.Name.Trim()); await cmd.ExecuteScalarAsync(ct); await scope.Transaction.CommitAsync(ct); return (await ListCategoriesAsync(tenant, request.LegalEntityId, ct)).Single(x => x.Id == id); }
    public async Task<ExpenseCategoryDto> ArchiveCategoryAsync(TenantId tenant, Guid entity, Guid categoryId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_expenses.expense_category set status='Archived' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active' returning id", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, categoryId);
        if (await command.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.category_not_archivable");
        await scope.Transaction.CommitAsync(ct);
        return (await ListCategoriesAsync(tenant, entity, ct)).Single(x => x.Id == categoryId);
    }
    public async Task<ExpenseCategoryVersionDto> CreateCategoryVersionAsync(TenantId tenant, Guid entity, Guid categoryId, CreateExpenseCategoryVersionRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_category_version(tenant_id,id,category_id,version,status,requires_receipt,receipt_threshold,effective_from,effective_to) select $1,$2,id,coalesce((select max(version)+1 from business_expenses.expense_category_version where tenant_id=$1 and category_id=$3),1),'Draft',$4,$5,$6,$7 from business_expenses.expense_category where tenant_id=$1 and legal_entity_id=$8 and id=$3 and status='Active' returning id,category_id,version,status,requires_receipt,receipt_threshold,effective_from,effective_to", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, categoryId, request.RequiresReceipt, Db(request.ReceiptThreshold), request.EffectiveFrom, Db(request.EffectiveTo), entity); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.category_not_found"); var result = new ExpenseCategoryVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetBoolean(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseCategoryVersionDto> PublishCategoryVersionAsync(TenantId tenant, Guid entity, Guid versionId, PublishExpenseCategoryVersionRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_category_version v set status='Published',effective_to=coalesce($4,effective_to) from business_expenses.expense_category c where v.tenant_id=$1 and v.id=$2 and v.category_id=c.id and c.legal_entity_id=$3 and c.status='Active' and v.status='Draft' returning v.id,v.category_id,v.version,v.status,v.requires_receipt,v.receipt_threshold,v.effective_from,v.effective_to", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, versionId, entity, Db(request.EffectiveTo)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.category_version_not_publishable"); var result = new ExpenseCategoryVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetBoolean(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    public async Task<ExpenseDto> CreateAsync(TenantId tenant, CreateExpenseRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.MerchantName) || string.IsNullOrWhiteSpace(request.CurrencyCode)) throw new ExpenseRepositoryException("expenses.invalid_amount");
        await EnsureEmployeeScopeAsync(tenant, request.EmployeeId, request.LegalEntityId, ct);
        await using var scope = await OpenAsync(tenant, ct);
        await EnsureUsableCategoryVersionAsync(scope.Connection, scope.Transaction, tenant, request.LegalEntityId, request.CategoryVersionId, ct);
        var id = Guid.CreateVersion7();
        if (budgetControl is not null)
        {
            await using var context = new ExistingTransactionContext(scope.Connection, scope.Transaction);
            var bucketId = await budgetControl.ResolveApplicableBucketAsync(context, tenant,
                new(request.LegalEntityId, PlanningPeriod(request.ExpenseDate), "OPEX", PlanningDimension(request.CategoryVersionId, request.ProjectId, request.TaskId, request.CostCategory), request.BudgetBucketId), ct);
            if (bucketId.HasValue)
            {
                var control = await budgetControl.UpsertReservationAsync(context, tenant, request.EmployeeId ?? Guid.Empty,
                    new(request.LegalEntityId, bucketId.Value, "expenses", id.ToString("D"), "expense", "draft:1", request.Amount, request.CurrencyCode, PlanningBudgetReservationKind.Precommitment, PlanningPeriod(request.ExpenseDate)), ct);
                if (control.Decision == "RequireApproval") throw new PlanningBudgetControlException("planning.budget_approval_required");
            }
        }
        await using var cmd = new NpgsqlCommand("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,project_id,task_id,cost_category,billable,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Draft','Pending','NotFinalized','NotSettled','Missing','NotEvaluated',$11,$12,$13,$14,$15,$16,1,now(),now())", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, id, request.LegalEntityId, Db(request.EmployeeId), request.MerchantName.Trim(), request.ExpenseDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.PaymentSource.Trim(), request.CategoryVersionId, Db(request.BusinessPurpose), Db(request.Description), Db(request.ProjectId), Db(request.TaskId), Db(request.CostCategory), request.Billable);
        await cmd.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
        return (await GetAsync(tenant, request.LegalEntityId, id, ct))!;
    }

    public async Task<ExpenseDto> UpdateAsync(TenantId tenant, Guid entity, Guid id, UpdateExpenseRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) throw new ExpenseRepositoryException("expenses.invalid_amount");
        await using var scope = await OpenAsync(tenant, ct);
        await EnsureUsableCategoryVersionAsync(scope.Connection, scope.Transaction, tenant, entity, request.CategoryVersionId, ct);
        if (budgetControl is not null)
        {
            await using var context = new ExistingTransactionContext(scope.Connection, scope.Transaction);
            var hasOpen = await budgetControl.HasOpenReservationAsync(context, tenant, entity, "expenses", id.ToString("D"), "expense", ct);
            var bucketId = request.BudgetBucketId.HasValue
                ? await budgetControl.ResolveApplicableBucketAsync(context, tenant, new(entity, PlanningPeriod(request.ExpenseDate), "OPEX", PlanningDimension(request.CategoryVersionId, request.ProjectId, request.TaskId, request.CostCategory), request.BudgetBucketId), ct)
                : hasOpen ? null : await budgetControl.ResolveApplicableBucketAsync(context, tenant, new(entity, PlanningPeriod(request.ExpenseDate), "OPEX", PlanningDimension(request.CategoryVersionId, request.ProjectId, request.TaskId, request.CostCategory)), ct);
            if (hasOpen || bucketId.HasValue)
            {
                var control = await budgetControl.UpsertReservationAsync(context, tenant, Guid.Empty,
                    new(entity, bucketId ?? Guid.Empty, "expenses", id.ToString("D"), "expense", $"draft:{request.Version + 1}", request.Amount, request.CurrencyCode, PlanningBudgetReservationKind.Precommitment, PlanningPeriod(request.ExpenseDate)), ct);
                if (control.Decision == "RequireApproval") throw new PlanningBudgetControlException("planning.budget_approval_required");
            }
        }
        await using var cmd = new NpgsqlCommand("update business_expenses.expense set merchant_name=$4,expense_date=$5,amount=$6,currency_code=$7,category_version_id=$8,business_purpose=$9,description=$10,project_id=$12,task_id=$13,cost_category=$14,billable=$15,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$11 and expense_state='Draft' returning id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id, request.MerchantName.Trim(), request.ExpenseDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.CategoryVersionId, Db(request.BusinessPurpose), Db(request.Description), request.Version, Db(request.ProjectId), Db(request.TaskId), Db(request.CostCategory), request.Billable); if (await cmd.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.concurrent_change_or_not_editable"); await scope.Transaction.CommitAsync(ct); return (await GetAsync(tenant, entity, id, ct))!;
    }

    public Task<ExpenseDto> TransitionAsync(TenantId tenant, Guid entity, Guid id, ExpenseActionRequest request, string action, CancellationToken ct)
        => action switch
        {
            "finalize" => FinalizeAsync(tenant, entity, id, request, ct),
            "reverse" => ReverseAsync(tenant, entity, id, request, ct),
            _ => TransitionCoreAsync(tenant, entity, id, request, action, ct)
        };

    public async Task<ExpenseApprovalDto> RequestApprovalAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, string? reason, CancellationToken ct)
    {
        if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select e.version,e.approval_state,e.expense_state,e.approval_request_id,pe.policy_version_id,e.employee_id from business_expenses.expense e left join business_expenses.expense_policy_evaluation pe on pe.tenant_id=e.tenant_id and pe.legal_entity_id=e.legal_entity_id and pe.expense_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, expenseId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
        var version = reader.GetInt64(0); var approvalState = reader.GetString(1); var expenseState = reader.GetString(2); var linkedApprovalId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3); var policyVersionId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4); var employeeId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
        await reader.CloseAsync();
        if (expenseState != "Submitted") throw new ExpenseRepositoryException("expenses.approval_request_invalid_state");
        if (employeeId.HasValue && hr is not null && await hr.GetSelfEmployeeAsync(tenant, membershipId, entity, ct) != employeeId) throw new ExpenseRepositoryException("expenses.approval_request_actor_scope_invalid");
        await scope.Transaction.CommitAsync(ct);
        if (linkedApprovalId.HasValue)
        {
            var existing = await approvals.GetAsync(tenant, linkedApprovalId.Value, ct) ?? throw new ExpenseRepositoryException("expenses.approval_request_not_found");
            var resourceVersionMatches = approvalState == "Pending"
                ? existing.ResourceVersion == version && existing.State == "Pending"
                : existing.ResourceVersion + 1 == version && existing.State == approvalState;
            if (existing.OwnerModule != "expenses" || existing.OwnerEntityType != "expense" || existing.OwnerEntityId != expenseId || existing.LegalEntityId != entity || !resourceVersionMatches)
                throw new ExpenseRepositoryException("expenses.approval_scope_invalid");
            return ToExpenseApproval(existing, expenseId);
        }
        if (approvalState != "Pending") throw new ExpenseRepositoryException("expenses.approval_request_invalid_state");
        if (!policyVersionId.HasValue)
        {
            var evaluation = await EvaluatePolicyAsync(tenant, entity, expenseId, ct);
            if (evaluation.Snapshot.TryGetProperty("policyVersionId", out var pvi) && Guid.TryParse(pvi.GetString(), out var parsedId))
            {
                policyVersionId = parsedId;
            }
        }
        try
        {
            var request = await approvals.RequestAsync(tenant, actorId, membershipId, entity, "expenses", "expense", expenseId, version, policyVersionId, reason, "expenses.submit", ct);
            await using var linkScope = await OpenAsync(tenant, ct);
            await using var link = new NpgsqlCommand("update business_expenses.expense set approval_request_id=$4,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 and expense_state='Submitted' and approval_state='Pending' and approval_request_id is null", linkScope.Connection, linkScope.Transaction);
            Add(link, tenant.Value, entity, expenseId, request.Id, version);
            if (await link.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.approval_request_link_conflict");
            await linkScope.Transaction.CommitAsync(ct);
            return ToExpenseApproval(request, expenseId);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ExpenseRepositoryException("expenses.approval_request_duplicate");
        }
    }

    public async Task<ExpenseApprovalDto> DecideApprovalAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, ExpenseApprovalDecisionRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
        if (request.RequestId == Guid.Empty || request.Decision is not ("Approved" or "Rejected")) throw new ExpenseRepositoryException("expenses.approval_decision_invalid");
        Guid? employeeId = null;
        await using (var preflight = await OpenAsync(tenant, ct))
        {
            await using var preflightCommand = new NpgsqlCommand("select e.version,e.expense_state,e.approval_state,e.approval_request_id,e.employee_id from business_expenses.expense e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3", preflight.Connection, preflight.Transaction);
            Add(preflightCommand, tenant.Value, entity, expenseId);
            await using var reader = await preflightCommand.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            var state = reader.GetString(1); var approvalState = reader.GetString(2); var linkedRequest = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3); employeeId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            await reader.CloseAsync(); await preflight.Transaction.CommitAsync(ct);
            if (state != "Submitted" || linkedRequest != request.RequestId) throw new ExpenseRepositoryException("expenses.approval_scope_or_state_invalid");
            if (approvalState == request.Decision)
            {
                var existing = await approvals.GetAsync(tenant, request.RequestId, ct);
                if (existing is null || existing.State != request.Decision || existing.OwnerModule != "expenses" || existing.OwnerEntityType != "expense" || existing.OwnerEntityId != expenseId || existing.LegalEntityId != entity)
                    throw new ExpenseRepositoryException("expenses.approval_stale_or_concurrent");
                return ToExpenseApproval(existing, expenseId);
            }
            if (approvalState != "Pending") throw new ExpenseRepositoryException("expenses.approval_scope_or_state_invalid");
        }
        PlatformApprovalRequestResult approval;
        try
        {
            approval = await approvals.DecideAsync(tenant, actorId, membershipId, request.RequestId, request.Decision, request.Reason, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "platform.approval_stale_or_unauthorized")
        {
            // A decision can be committed by Platform Approvals before a transient local write failure.
            // Re-read the authoritative request so a safe retry repairs the local projection instead of
            // attempting a second decision or leaving the expense permanently pending.
            approval = await approvals.GetAsync(tenant, request.RequestId, ct)
                ?? throw new ExpenseRepositoryException("expenses.approval_not_found");
            if (approval.State is not ("Approved" or "Rejected") || !string.Equals(approval.State, request.Decision, StringComparison.Ordinal))
                throw new ExpenseRepositoryException("expenses.approval_stale_or_concurrent");
        }
        if (approval.OwnerModule != "expenses" || approval.OwnerEntityType != "expense" || approval.OwnerEntityId != expenseId || approval.LegalEntityId != entity)
            throw new ExpenseRepositoryException("expenses.approval_scope_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("update business_expenses.expense e set approval_state=$4,approval_request_id=$5,version=e.version+1,updated_at=now() where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 and e.version=$6 and e.expense_state='Submitted' and e.approval_state='Pending' and coalesce((select policy_version_id from business_expenses.expense_policy_evaluation pe where pe.tenant_id=e.tenant_id and pe.legal_entity_id=e.legal_entity_id and pe.expense_id=e.id),null) is not distinct from $7 returning e.version", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, expenseId, approval.State, approval.Id, approval.ResourceVersion, (object?)approval.PolicyVersionId ?? DBNull.Value);
        if (await command.ExecuteScalarAsync(ct) is null)
        {
            var current = await GetAsync(tenant, entity, expenseId, ct);
            if (current is null || current.ApprovalState != approval.State || current.Version != approval.ResourceVersion + 1) throw new ExpenseRepositoryException("expenses.approval_stale_or_concurrent");
        }
        await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) select $1,$2,$3,$4,$5,jsonb_build_object('expenseState','Submitted','approvalState',$5,'approvalRequestId',$6::text) where not exists (select 1 from business_expenses.expense_history h where h.tenant_id=$1 and h.legal_entity_id=$3 and h.expense_id=$4 and h.action=$5 and h.to_state->>'approvalRequestId'=$6::text)", scope.Connection, scope.Transaction);
        Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, approval.State, approval.Id);
        await history.ExecuteNonQueryAsync(ct);
        await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) select $1,'ExpenseApprovalDecidedV1',$2::jsonb,$3,$4,$5,$6,'expenses','Pending',0 where not exists (select 1 from platform.outbox_message where tenant_id=$3 and message_type='ExpenseApprovalDecidedV1' and causation_id=$6)", scope.Connection, scope.Transaction);
        Add(outbox, Guid.CreateVersion7(), JsonSerializer.Serialize(new { ExpenseId = expenseId, LegalEntityId = entity, EmployeeId = employeeId, Decision = approval.State, Reason = request.Reason }), tenant.Value, DateTimeOffset.UtcNow, expenseId, expenseId);
        await outbox.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
        return ToExpenseApproval(approval, expenseId);
    }

    public async Task<ExpensePolicyExceptionDto> RequestPolicyExceptionAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, RequestExpensePolicyExceptionRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
        if (string.IsNullOrWhiteSpace(request.FindingCode) || request.FindingCode.Trim().Length > 120 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
            throw new ExpenseRepositoryException("expenses.policy_exception_invalid");
        ExpensePolicyExceptionDto? existing = null;
        long expenseVersion;
        Guid? employeeId;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var command = new NpgsqlCommand("select e.version,e.expense_state,e.employee_id,pe.state,pe.snapshot::text from business_expenses.expense e join business_expenses.expense_policy_evaluation pe on pe.tenant_id=e.tenant_id and pe.legal_entity_id=e.legal_entity_id and pe.expense_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3", scope.Connection, scope.Transaction);
            Add(command, tenant.Value, entity, expenseId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            expenseVersion = reader.GetInt64(0); var expenseState = reader.GetString(1); employeeId = reader.IsDBNull(2) ? null : reader.GetGuid(2); var policyState = reader.GetString(3); var snapshot = JsonDocument.Parse(reader.GetString(4)).RootElement.Clone(); await reader.CloseAsync();
            if (expenseState != "Submitted" || expenseVersion != request.Version || policyState is not ("Blocked" or "Warning")) throw new ExpenseRepositoryException("expenses.policy_exception_not_allowed");
            var findings = snapshot.TryGetProperty("findings", out var findingValues) && findingValues.ValueKind == JsonValueKind.Array
                ? findingValues.EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!findings.Contains(request.FindingCode.Trim())) throw new ExpenseRepositoryException("expenses.policy_exception_finding_not_found");
            if (employeeId.HasValue && hr is not null && await hr.GetSelfEmployeeAsync(tenant, membershipId, entity, ct) != employeeId) throw new ExpenseRepositoryException("expenses.policy_exception_actor_scope_invalid");
            await using var existingCommand = new NpgsqlCommand("select id,expense_id,legal_entity_id,finding_code,reason,state,approval_request_id,requested_by,approved_by,version,created_at,decided_at from business_expenses.expense_policy_exception where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and finding_code=$4", scope.Connection, scope.Transaction);
            Add(existingCommand, tenant.Value, entity, expenseId, request.FindingCode.Trim());
            await using var existingReader = await existingCommand.ExecuteReaderAsync(ct);
            if (await existingReader.ReadAsync(ct)) existing = ReadPolicyException(existingReader);
            await existingReader.CloseAsync();
            await scope.Transaction.CommitAsync(ct);
        }
        if (existing is not null)
        {
            if (existing.State is "Pending" or "Approved") return existing;
            throw new ExpenseRepositoryException("expenses.policy_exception_not_retryable");
        }
        var approval = await approvals.RequestAsync(tenant, actorId, membershipId, entity, "expenses", "expense_policy_exception", Guid.CreateVersion7(), expenseVersion, null, request.Reason.Trim(), "expenses.submit", ct);
        var exceptionId = approval.OwnerEntityId;
        await using var linkScope = await OpenAsync(tenant, ct);
        await using var insert = new NpgsqlCommand("insert into business_expenses.expense_policy_exception(tenant_id,id,legal_entity_id,expense_id,finding_code,reason,approval_request_id,requested_by) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict(tenant_id,expense_id,finding_code) do nothing returning id,expense_id,legal_entity_id,finding_code,reason,state,approval_request_id,requested_by,approved_by,version,created_at,decided_at", linkScope.Connection, linkScope.Transaction);
        Add(insert, tenant.Value, exceptionId, entity, expenseId, request.FindingCode.Trim(), request.Reason.Trim(), approval.Id, actorId);
        await using var resultReader = await insert.ExecuteReaderAsync(ct);
        if (!await resultReader.ReadAsync(ct))
        {
            await resultReader.CloseAsync(); await linkScope.Transaction.RollbackAsync(ct);
            throw new ExpenseRepositoryException("expenses.policy_exception_duplicate");
        }
        var result = ReadPolicyException(resultReader); await resultReader.CloseAsync(); await linkScope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpensePolicyExceptionDto> DecidePolicyExceptionAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, Guid exceptionId, DecideExpensePolicyExceptionRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
        if (request.ApprovalRequestId == Guid.Empty || request.Decision is not ("Approved" or "Rejected")) throw new ExpenseRepositoryException("expenses.policy_exception_decision_invalid");
        ExpensePolicyExceptionDto pending;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var command = new NpgsqlCommand("select id,expense_id,legal_entity_id,finding_code,reason,state,approval_request_id,requested_by,approved_by,version,created_at,decided_at from business_expenses.expense_policy_exception where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and approval_request_id=$5", scope.Connection, scope.Transaction);
            Add(command, tenant.Value, entity, expenseId, exceptionId, request.ApprovalRequestId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_exception_not_found");
            pending = ReadPolicyException(reader); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct);
        }
        if (pending.State == request.Decision) return pending;
        if (pending.State != "Pending" || pending.ApprovalRequestId != request.ApprovalRequestId) throw new ExpenseRepositoryException("expenses.policy_exception_stale");
        PlatformApprovalRequestResult approval;
        try { approval = await approvals.DecideAsync(tenant, actorId, membershipId, request.ApprovalRequestId, request.Decision, request.Reason, ct); }
        catch (InvalidOperationException ex) when (ex.Message == "platform.approval_stale_or_unauthorized")
        { approval = await approvals.GetAsync(tenant, request.ApprovalRequestId, ct) ?? throw new ExpenseRepositoryException("expenses.policy_exception_approval_not_found"); }
        if (approval.State != request.Decision || approval.OwnerModule != "expenses" || approval.OwnerEntityType != "expense_policy_exception" || approval.LegalEntityId != entity) throw new ExpenseRepositoryException("expenses.policy_exception_scope_invalid");
        await using var updateScope = await OpenAsync(tenant, ct);
        await using var update = new NpgsqlCommand("update business_expenses.expense_policy_exception set state=$5,approved_by=$6,decided_at=now(),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and state='Pending' and version=$7 returning id,expense_id,legal_entity_id,finding_code,reason,state,approval_request_id,requested_by,approved_by,version,created_at,decided_at", updateScope.Connection, updateScope.Transaction);
        Add(update, tenant.Value, entity, expenseId, pending.Id, approval.State, actorId, pending.Version);
        await using var updatedReader = await update.ExecuteReaderAsync(ct);
        ExpensePolicyExceptionDto result;
        if (await updatedReader.ReadAsync(ct)) result = ReadPolicyException(updatedReader);
        else { await updatedReader.CloseAsync(); await updateScope.Transaction.RollbackAsync(ct); result = (await ListPolicyExceptionsAsync(tenant, entity, expenseId, request.Decision, 1, 1, ct)).Items.SingleOrDefault() ?? throw new ExpenseRepositoryException("expenses.policy_exception_stale"); return result; }
        await updatedReader.CloseAsync();
        await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) select $1,$2,$3,$4,'policy.exception.decided',jsonb_build_object('findingCode',$5::text,'state',$6::text,'approvalRequestId',$7::text) where not exists (select 1 from business_expenses.expense_history where tenant_id=$1 and legal_entity_id=$3 and expense_id=$4 and action='policy.exception.decided' and to_state->>'approvalRequestId'=$7::text)", updateScope.Connection, updateScope.Transaction);
        Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, pending.FindingCode, approval.State, request.ApprovalRequestId); await history.ExecuteNonQueryAsync(ct); await updateScope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpensePage<ExpensePolicyExceptionDto>> ListPolicyExceptionsAsync(TenantId tenant, Guid entity, Guid? expenseId, string? state, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or expense_id=$3) and ($4::text is null or state=$4)";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_policy_exception where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(expenseId), Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select id,expense_id,legal_entity_id,finding_code,reason,state,approval_request_id,requested_by,approved_by,version,created_at,decided_at from business_expenses.expense_policy_exception where {where} order by created_at desc,id desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, Db(expenseId), Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpensePolicyExceptionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(ReadPolicyException(reader)); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize);
    }

    private async Task<ExpenseDto> TransitionCoreAsync(TenantId tenant, Guid entity, Guid id, ExpenseActionRequest request, string action, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        if (action is "approve" or "reject") throw new ExpenseRepositoryException("expenses.platform_approval_required");
        if (action == "submit")
        {
            await using var policy = new NpgsqlCommand("select coalesce((select cv.requires_receipt from business_expenses.expense e join business_expenses.expense_category_version cv on cv.tenant_id=e.tenant_id and cv.id=e.category_version_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3),true) and not exists(select 1 from business_expenses.expense_evidence ev where ev.tenant_id=$1 and ev.legal_entity_id=$2 and ev.expense_id=$3 and ev.status in ('Pending','Ready')) and not exists(select 1 from business_expenses.expense_missing_receipt_declaration md where md.tenant_id=$1 and md.legal_entity_id=$2 and md.expense_id=$3 and md.status='Declared')", scope.Connection, scope.Transaction);
            Add(policy, tenant.Value, entity, id);
            if (Convert.ToBoolean(await policy.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture)) throw new ExpenseRepositoryException("expenses.expense_receipt_required");
        }
        var sql = action switch
        {
            "submit" => "update business_expenses.expense set expense_state='Submitted',approval_state='Pending',version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4 and expense_state='Draft'",
            "approve" => throw new ExpenseRepositoryException("expenses.platform_approval_required"),
            "reject" => throw new ExpenseRepositoryException("expenses.platform_approval_required"),
            _ => throw new ExpenseRepositoryException("expenses.action_invalid")
        };
        await using var cmd = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, entity, id, request.Version);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency");
        await using (var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) select tenant_id,$4,legal_entity_id,id,$5,jsonb_build_object('expenseState',expense_state,'approvalState',approval_state,'accountingState',accounting_state,'settlementState',settlement_state) from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction))
        { Add(history, tenant.Value, entity, id, Guid.CreateVersion7(), action); await history.ExecuteNonQueryAsync(ct); }
        await scope.Transaction.CommitAsync(ct);
        return (await GetAsync(tenant, entity, id, ct))!;
    }

    private async Task<ExpenseDto> FinalizeAsync(TenantId tenant, Guid entity, Guid id, ExpenseActionRequest request, CancellationToken ct)
    {
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var row = new NpgsqlCommand("select e.amount,e.currency_code,e.expense_date,e.payment_source,e.employee_id,e.expense_state,e.approval_state,e.version,e.category_version_id,e.evidence_state,c.code,e.merchant_name,e.project_id,e.task_id,e.cost_category,e.billable from business_expenses.expense e join business_expenses.expense_category_version cv on cv.tenant_id=e.tenant_id and cv.id=e.category_version_id join business_expenses.expense_category c on c.tenant_id=cv.tenant_id and c.id=cv.category_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 for update", context.Connection, context.Transaction);
            Add(row, tenant.Value, entity, id);
            await using var reader = await row.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            var amount = reader.GetDecimal(0); var currency = reader.GetString(1); var date = reader.GetFieldValue<DateOnly>(2); var source = reader.GetString(3); var employee = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4); var state = reader.GetString(5); var approval = reader.GetString(6); var version = reader.GetInt64(7); var categoryVersionId = reader.GetGuid(8); var evidenceState = reader.GetString(9); var category = reader.GetString(10); var merchant = reader.GetString(11); var projectId = reader.IsDBNull(12) ? (Guid?)null : reader.GetGuid(12); var taskId = reader.IsDBNull(13) ? (Guid?)null : reader.GetGuid(13); var costCategory = reader.IsDBNull(14) ? null : reader.GetString(14); var billable = reader.GetBoolean(15);
            await reader.CloseAsync();
            if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
            if (version != request.Version || state != "Submitted") throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency");
            if (source == "CorporateCard")
            {
                await using var card = new NpgsqlCommand("select amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and matched_expense_id=$3 for update", context.Connection, context.Transaction);
                Add(card, tenant.Value, entity, id);
                await using var cardReader = await card.ExecuteReaderAsync(token);
                if (!await cardReader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.card_transaction_required");
                var cardAmount = cardReader.GetDecimal(0); var cardCurrency = cardReader.GetString(1); var cardState = cardReader.GetString(2);
                await cardReader.CloseAsync();
                if (cardState == "Pending") throw new ExpenseRepositoryException("expenses.card_transaction_pending");
                if (cardState is not ("Matched" or "Allocated" or "Cleared") || cardAmount != amount || !string.Equals(cardCurrency, currency, StringComparison.OrdinalIgnoreCase)) throw new ExpenseRepositoryException("expenses.card_transaction_amount_mismatch");
            }
            var policyState = "Passed";
            Guid? applicablePolicyVersion = null;
            await using (var applicable = new NpgsqlCommand("select pv.id from business_expenses.expense_policy_version pv where pv.tenant_id=$1 and pv.legal_entity_id=$2 and pv.status='Published' and pv.effective_from <= $3 and (pv.effective_to is null or pv.effective_to >= $3) and (not (pv.rules ? 'paymentSources') or pv.rules->'paymentSources' ? $4::text) and (not (pv.rules ? 'employeeIds') or ($5::uuid is not null and pv.rules->'employeeIds' ? $5::text)) and (not (pv.rules ? 'categoryVersionIds') or pv.rules->'categoryVersionIds' ? $6::text) and (not (pv.rules ? 'currencyCodes') or pv.rules->'currencyCodes' ? $7::text) and (not (pv.rules ? 'merchantNames') or exists (select 1 from jsonb_array_elements_text(pv.rules->'merchantNames') n where lower($8::text)=lower(n))) order by pv.effective_from desc,pv.version desc,pv.id desc limit 1", context.Connection, context.Transaction))
            { Add(applicable, tenant.Value, entity, date, source, Db(employee), categoryVersionId, currency, merchant); var applicableValue = await applicable.ExecuteScalarAsync(token); applicablePolicyVersion = applicableValue is Guid g ? g : null; }
            Guid? approvalRequestId = null;
            await using (var approvalLink = new NpgsqlCommand("select approval_request_id from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(approvalLink, tenant.Value, entity, id); var value = await approvalLink.ExecuteScalarAsync(token); approvalRequestId = value is Guid g ? g : null; }
            if (applicablePolicyVersion.HasValue)
            {
                await using var policy = new NpgsqlCommand("select state,policy_version_id,snapshot::text from business_expenses.expense_policy_evaluation where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", context.Connection, context.Transaction);
                Add(policy, tenant.Value, entity, id); await using var policyReader = await policy.ExecuteReaderAsync(token);
                if (!await policyReader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.policy_evaluation_required");
                policyState = policyReader.GetString(0); var evaluatedVersion = policyReader.IsDBNull(1) ? (Guid?)null : policyReader.GetGuid(1); var snapshot = System.Text.Json.JsonDocument.Parse(policyReader.GetString(2)).RootElement.Clone(); await policyReader.CloseAsync();
                if (policyState == "Blocked")
                {
                    await using var exception = new NpgsqlCommand("select exists(select 1 from business_expenses.expense_policy_exception where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and state='Approved')", context.Connection, context.Transaction);
                    Add(exception, tenant.Value, entity, id);
                    if (!Convert.ToBoolean(await exception.ExecuteScalarAsync(token), CultureInfo.InvariantCulture)) throw new ExpenseRepositoryException("expenses.policy_blocked");
                    policyState = "Warning";
                }
                if (evaluatedVersion != applicablePolicyVersion || !SnapshotMatches(snapshot, version, amount, currency, source, employee, categoryVersionId, evidenceState)) throw new ExpenseRepositoryException("expenses.policy_evaluation_stale");
            }
            if (approvals is not null)
            {
                if (!approvalRequestId.HasValue) throw new ExpenseRepositoryException("expenses.platform_approval_required");
                var platformApproval = await approvals.GetAsync(tenant, approvalRequestId.Value, token);
                if (platformApproval is null || platformApproval.State != "Approved" || platformApproval.OwnerModule != "expenses" || platformApproval.OwnerEntityType != "expense" || platformApproval.OwnerEntityId != id || platformApproval.LegalEntityId != entity || platformApproval.PolicyVersionId != applicablePolicyVersion || platformApproval.ResourceVersion < version - 1 || platformApproval.ResourceVersion > version)
                    throw new ExpenseRepositoryException("expenses.platform_approval_stale_or_invalid");
            }
            var financialContext = financeBase is null
                ? null
                : await financeBase.ResolveCalculationContextAsync(tenant, new LegalEntityId(entity), currency, FinancialCalculationPurpose.Accounting, date, token)
                    ?? throw new ExpenseRepositoryException("expenses.financial_context_unavailable");
            var baseCurrency = financialContext?.BaseCurrencyCode ?? currency;
            var baseAmount = financialContext is null || string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase)
                ? amount
                : CalculateBaseAmount(amount, financialContext);
            var exchangeRate = financialContext?.ExchangeRate is { } rate
                ? decimal.Parse(rate.Rate, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
                : 1m;
            var financialContextJson = financialContext is null ? null : JsonSerializer.Serialize(financialContext);
            decimal taxAmount = 0m;
            string? taxSnapshotJson = null;
            if (tax is not null)
            {
                var taxContext = new TaxContextContract(tenant, new LegalEntityId(entity), "Purchase", "Expense", "Expense", date, date, null, date,
                    null, null, null, null, null, null, 1m, null, "TaxInclusive", amount.ToString(CultureInfo.InvariantCulture), "0", "0",
                    currency, baseCurrency, null, null, "PurchaseInvoice");
                try
                {
                    var taxResult = await tax.CalculateAsync(taxContext, token);
                    taxAmount = decimal.Parse(taxResult.TaxTotal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                    taxSnapshotJson = JsonSerializer.Serialize(new { context = taxContext, result = taxResult });
                }
                catch (Exception exception) when (IsTaxContextUnavailable(exception))
                {
                    taxSnapshotJson = JsonSerializer.Serialize(new { state = "NotEvaluated", reason = TaxErrorCode(exception), capturedAt = DateTimeOffset.UtcNow });
                }
            }
            if (budgetControl is not null)
            {
                var hasOpen = await budgetControl.HasOpenReservationAsync(context, tenant, entity, "expenses", id.ToString("D"), "expense", token);
                var bucketId = await budgetControl.ResolveApplicableBucketAsync(context, tenant,
                    new(entity, PlanningPeriod(date), "OPEX", PlanningDimension(categoryVersionId, projectId, taskId, costCategory)), token);
                if (hasOpen || bucketId.HasValue)
                {
                    var control = await budgetControl.UpsertReservationAsync(context, tenant, employee ?? Guid.Empty,
                        new(entity, bucketId ?? Guid.Empty, "expenses", id.ToString("D"), "expense", $"committed:{version + 1}", amount, currency, PlanningBudgetReservationKind.Commitment, PlanningPeriod(date)), token);
                    if (control.Decision == "RequireApproval") throw new PlanningBudgetControlException("planning.budget_approval_required");
                    await budgetControl.ConsumeReservationAsync(context, tenant, employee ?? Guid.Empty,
                        new(entity, "expenses", id.ToString("D"), "expense", $"actual:{version + 1}", amount, currency), token);
                }
            }
            var factId = StableFactId(id);
            var payload = new ExpenseRecognizedPayload(amount, currency, category, source == "EmployeePaid" ? "EmployeeReimbursementPayable" : "Cash", baseAmount, baseCurrency, exchangeRate, financialContext?.ExchangeRate?.ApprovedRateId?.ToString("D"), financialContextJson, taxAmount, taxSnapshotJson);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new LegalEntityId(entity), AccountingFactTypes.ExpenseRecognized, 1, "expenses", id.ToString("D"), $"expense-finalize:{id:D}", date, date, payload, Guid.CreateVersion7(), Guid.Empty);
            var posted = await ledgerPosting.PostExpenseAsync(context, fact, payload, token);
            await using (var update = new NpgsqlCommand("update business_expenses.expense set expense_state='Finalized',accounting_state='Posted',accounting_fact_id=$4,ledger_entry_id=$5,settlement_state=case when payment_source='EmployeePaid' then 'ReimbursementPending' else 'NotApplicable' end,policy_state=$6,base_currency_code=$8,base_amount=$9,financial_context_snapshot=$10::jsonb,tax_amount=$11,tax_snapshot=$12::jsonb,version=version+1,finalized_at=now(),updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$7 and expense_state='Submitted' and approval_state='Approved'", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, id, posted.FactId, posted.JournalEntryId, policyState, request.Version, baseCurrency, baseAmount, (object?)financialContextJson ?? DBNull.Value, taxAmount, (object?)taxSnapshotJson ?? DBNull.Value); if (await update.ExecuteNonQueryAsync(token) == 0) throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency"); }
            await using (var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'finalize',jsonb_build_object('expenseState','Finalized','approvalState','Approved','accountingState','Posted','ledgerEntryId',$5::text))", context.Connection, context.Transaction))
            { Add(history, tenant.Value, Guid.CreateVersion7(), entity, id, posted.JournalEntryId); await history.ExecuteNonQueryAsync(token); }
            await using var obligation = new NpgsqlCommand("insert into business_expenses.expense_reimbursement_obligation(tenant_id,id,legal_entity_id,expense_id,employee_id,amount,currency_code,status) select tenant_id,$4,legal_entity_id,id,employee_id,amount,currency_code,'Pending' from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and payment_source='EmployeePaid' and employee_id is not null on conflict(tenant_id,expense_id) do nothing", context.Connection, context.Transaction);
            Add(obligation, tenant.Value, entity, id, Guid.CreateVersion7()); await obligation.ExecuteNonQueryAsync(token);
            context.AddOutbox(new(Guid.CreateVersion7(), "ExpenseFinalizedV1", JsonSerializer.Serialize(new { expenseId = id, legalEntityId = entity, employeeId = employee, amount, baseAmount, currencyCode = currency, projectId, taskId, costCategory, billable }), tenant, DateTimeOffset.UtcNow, id, id, "expenses"));
            if (source == "EmployeePaid" && employee.HasValue)
                context.AddOutbox(new(Guid.CreateVersion7(), "ExpenseReimbursementRequestedV1", JsonSerializer.Serialize(new { ExpenseId = id, LegalEntityId = entity, EmployeeId = employee, Amount = amount, CurrencyCode = currency }), tenant, DateTimeOffset.UtcNow, id, id, "expenses"));
            return posted;
        }, ct);
        return (await GetAsync(tenant, entity, id, ct))!;
    }

    private static decimal CalculateBaseAmount(decimal amount, FinancialCalculationContextSnapshotContract context)
    {
        if (context.ExchangeRate is null || !decimal.TryParse(context.ExchangeRate.Rate, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate <= 0m)
            throw new ExpenseRepositoryException("expenses.fx_rate_missing");
        var scale = Math.Clamp(context.RoundingPolicy.MoneyScale, 0, 8);
        var mode = context.RoundingPolicy.ContextRules.FirstOrDefault(rule => rule.Context == FinancialRoundingContext.FxBaseAmount)?.Mode ?? FinancialRoundingMode.HalfUp;
        var midpoint = mode switch
        {
            FinancialRoundingMode.HalfEven => MidpointRounding.ToEven,
            FinancialRoundingMode.Down => MidpointRounding.ToZero,
            _ => MidpointRounding.AwayFromZero
        };
        return Math.Round(amount * rate, scale, midpoint);
    }

    private static bool IsTaxContextUnavailable(Exception exception)
    {
        var code = TaxErrorCode(exception);
        return code is "tax.jurisdiction_unresolved" or "tax.rule_not_effective" or "tax.tax_date_unresolved" or "tax.capability_disabled";
    }

    private static string TaxErrorCode(Exception exception) => exception.GetType().GetProperty("Code")?.GetValue(exception) as string ?? "tax.unavailable";

    private async Task<ExpenseDto> ReverseAsync(TenantId tenant, Guid entity, Guid id, ExpenseActionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ExpenseRepositoryException("expenses.reversal_reason_required");
        Guid ledgerEntry; long version; Guid? projectId; decimal amount; decimal baseAmount; string currencyCode;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockCommand = new NpgsqlCommand("select ledger_entry_id,version,expense_state,accounting_state,project_id,amount,coalesce(base_amount,amount),currency_code from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(lockCommand, tenant.Value, entity, id);
            await using var reader = await lockCommand.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            if (reader.IsDBNull(0) || reader.GetString(2) != "Finalized" || reader.GetString(3) != "Posted" || reader.GetInt64(1) != request.Version) throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency");
            ledgerEntry = reader.GetGuid(0); version = reader.GetInt64(1); projectId = reader.IsDBNull(4) ? null : reader.GetGuid(4); amount = reader.GetDecimal(5); baseAmount = reader.GetDecimal(6); currencyCode = reader.GetString(7); await reader.CloseAsync();
            await using var pending = new NpgsqlCommand("update business_expenses.expense set accounting_state='ReversalPending',version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4", scope.Connection, scope.Transaction); Add(pending, tenant.Value, entity, id, version); await pending.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct);
        }
        var reversal = await ledgerPosting.ReverseAsync(tenant, new LegalEntityId(entity), ledgerEntry, DateOnly.FromDateTime(DateTime.UtcNow), null, request.Reason!, Guid.Empty, Guid.CreateVersion7(), ct);
        await using (var scope = await OpenAsync(tenant, ct))
        {
            if (budgetControl is not null)
            {
                await using var context = new ExistingTransactionContext(scope.Connection, scope.Transaction);
                await budgetControl.ReverseConsumptionAsync(context, tenant, Guid.Empty,
                    new(entity, "expenses", id.ToString("D"), "expense", "expenses", id.ToString("D"), "expense",
                        $"reversal:{id:D}:{version}", amount, currencyCode), ct);
            }
            await using var update = new NpgsqlCommand("update business_expenses.expense set expense_state='Reversed',accounting_state='Reversed',reversal_entry_id=$4,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and accounting_state='ReversalPending'", scope.Connection, scope.Transaction); Add(update, tenant.Value, entity, id, reversal.JournalEntryId); if (await update.ExecuteNonQueryAsync(ct) == 0) throw new ExpenseRepositoryException("expenses.reversal_state_conflict");
            await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'reverse',jsonb_build_object('expenseState','Reversed','accountingState','Reversed','reversalEntryId',$5::text))", scope.Connection, scope.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, id, reversal.JournalEntryId); await history.ExecuteNonQueryAsync(ct);
            await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status) values($1,'ExpenseReversedV1',$2::jsonb,$3,now(),$4,$5,'expenses','Pending')", scope.Connection, scope.Transaction);
            Add(outbox, Guid.CreateVersion7(), JsonSerializer.Serialize(new { expenseId = id, projectId, amount, baseAmount, currencyCode, correctionOfFactId = (Guid?)null }), tenant.Value, Guid.CreateVersion7(), id);
            await outbox.ExecuteNonQueryAsync(ct);
            await scope.Transaction.CommitAsync(ct);
        }
        return (await GetAsync(tenant, entity, id, ct))!;
    }


    public async Task<IReadOnlyList<ExpenseEvidenceDto>> ListEvidenceAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,evidence_type,file_reference,status,created_at,replaces_evidence_id from business_expenses.expense_evidence where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by created_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseEvidenceDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetGuid(6))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<IReadOnlyList<ExpenseHistoryDto>> ListHistoryAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,action,to_state::text,occurred_at from business_expenses.expense_history where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by occurred_at,id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseHistoryDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), System.Text.Json.JsonDocument.Parse(r.GetString(3)).RootElement.Clone(), r.GetFieldValue<DateTimeOffset>(4))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpenseEvidenceDto> AddEvidenceAsync(TenantId tenant, Guid entity, Guid expenseId, AddExpenseEvidenceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FileReference)) throw new ExpenseRepositoryException("expenses.file_reference_invalid");
        var fileReference = request.FileReference.Trim();
        if (files is not null)
        {
            if (!Guid.TryParse(fileReference, out var fileRef)) throw new ExpenseRepositoryException("expenses.file_reference_invalid");
            var verified = await files.VerifyReadyReferenceAsync(tenant, fileRef, "expenses", "expense", expenseId, ct);
            if (verified is null) throw new ExpenseRepositoryException("expenses.file_not_ready");
            fileReference = verified.FileRef.ToString("D");
        }
        await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7();
        await using (var stateCommand = new NpgsqlCommand("select expense_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction))
        {
            Add(stateCommand, tenant.Value, entity, expenseId);
            var state = await stateCommand.ExecuteScalarAsync(ct) as string;
            if (state is null) throw new ExpenseRepositoryException("expenses.expense_not_found");
            if (state is not ("Draft" or "Submitted")) throw new ExpenseRepositoryException("expenses.evidence_not_editable");
        }
        if (request.ReplacesEvidenceId.HasValue)
        {
            await using var replace = new NpgsqlCommand("update business_expenses.expense_evidence set status='Replaced' where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and status='Ready'", scope.Connection, scope.Transaction); Add(replace, tenant.Value, entity, expenseId, request.ReplacesEvidenceId.Value); if (await replace.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.evidence_replacement_invalid");
        }
        await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_evidence(tenant_id,id,legal_entity_id,expense_id,evidence_type,file_reference,status,replaces_evidence_id,created_at) select $1,$2,$3,id,$4,$5,'Ready',$7,now() from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$6 returning id,expense_id,evidence_type,file_reference,status,created_at,replaces_evidence_id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, entity, request.EvidenceType.Trim(), fileReference, expenseId, Db(request.ReplacesEvidenceId)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var result = new ExpenseEvidenceDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetGuid(6)); await r.CloseAsync(); await using var mark = new NpgsqlCommand("update business_expenses.expense set evidence_state='Provided',updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(mark, tenant.Value, entity, expenseId); await mark.ExecuteNonQueryAsync(ct); await using var resolve = new NpgsqlCommand("update business_expenses.expense_missing_receipt_declaration set status='Resolved',resolved_at=now() where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and status='Declared'", scope.Connection, scope.Transaction); Add(resolve, tenant.Value, entity, expenseId); await resolve.ExecuteNonQueryAsync(ct); await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'evidence.attached',jsonb_build_object('evidenceId',$5::text,'evidenceType',$6::text,'fileReference',$7::text,'status','Ready','replacesEvidenceId',$8::text))", scope.Connection, scope.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, id, request.EvidenceType.Trim(), fileReference, request.ReplacesEvidenceId?.ToString("D")); await history.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpenseEvidenceUploadIntentDto> CreateEvidenceUploadIntentAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, CreateExpenseEvidenceUploadRequest request, CancellationToken ct)
    {
        if (fileUploads is null) throw new ExpenseRepositoryException("expenses.files_capability_unavailable");
        if (request.SizeBytes <= 0 || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentType)) throw new ExpenseRepositoryException("expenses.file_upload_invalid");
        await EnsureExpenseEditableAsync(tenant, entity, expenseId, ct);
        var intent = await fileUploads.CreateUploadIntentAsync(tenant, actorId, membershipId, "expenses", "expense", expenseId, request.FileName.Trim(), request.ContentType.Trim(), request.SizeBytes, request.Classification.Trim(), ct);
        return intent is null ? throw new ExpenseRepositoryException("expenses.file_upload_unavailable") : new(intent.FileRef, intent.Url, intent.Fields, intent.ContentType, intent.SizeBytes, intent.ExpiresAt);
    }

    public async Task<ExpenseEvidenceDto> CompleteEvidenceUploadAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, CompleteExpenseEvidenceUploadRequest request, CancellationToken ct)
    {
        if (fileUploads is null) throw new ExpenseRepositoryException("expenses.files_capability_unavailable");
        var completion = await fileUploads.CompleteUploadAsync(tenant, actorId, membershipId, "expenses", "expense", expenseId, request.FileRef, ct);
        if (completion is null || !completion.Ready) throw new ExpenseRepositoryException(completion?.ReasonCode ?? "expenses.file_not_ready");
        return await AddEvidenceAsync(tenant, entity, expenseId, new AddExpenseEvidenceRequest(request.EvidenceType, request.FileRef.ToString("D"), request.ReplacesEvidenceId), ct);
    }

    public Task<ExpenseOcrJobDto> RequestOcrAsync(TenantId tenant, Guid entity, Guid expenseId, Guid evidenceId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await EnsureOcrEditableAsync(context, tenant, entity, expenseId, token);
            var evidence = await ReadOcrEvidenceAsync(context, tenant, entity, expenseId, evidenceId, token);
            if (evidence is null || evidence.Value.Status != "Ready" || !Guid.TryParse(evidence.Value.FileReference, out var fileRef))
                throw new ExpenseRepositoryException("expenses.ocr_evidence_not_ready");
            var jobId = Guid.CreateVersion7();
            await using (var insert = new NpgsqlCommand("insert into business_expenses.expense_ocr_job(tenant_id,id,legal_entity_id,expense_id,evidence_id) values($1,$2,$3,$4,$5) on conflict do nothing returning id,state,attempt_count,suggestions,failure_code,failure_reason,requested_at,started_at,completed_at", context.Connection, context.Transaction))
            {
                Add(insert, tenant.Value, jobId, entity, expenseId, evidenceId);
                await using var reader = await insert.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var result = ReadOcrJob(reader, expenseId, evidenceId);
                    await reader.CloseAsync();
                    context.AddOutbox(new(Guid.CreateVersion7(), "expenses.ocr.requested", JsonSerializer.Serialize(new { JobId = result.Id, LegalEntityId = entity, ExpenseId = expenseId, EvidenceId = evidenceId, FileReference = fileRef }), tenant, DateTimeOffset.UtcNow, result.Id, result.Id, "expenses"));
                    return result;
                }
                await reader.CloseAsync();
            }
            await using var existing = new NpgsqlCommand("select id,state,attempt_count,suggestions,failure_code,failure_reason,requested_at,started_at,completed_at from business_expenses.expense_ocr_job where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and evidence_id=$4 order by requested_at desc limit 1", context.Connection, context.Transaction);
            Add(existing, tenant.Value, entity, expenseId, evidenceId);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (!await existingReader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.ocr_unavailable");
            var existingResult = ReadOcrJob(existingReader, expenseId, evidenceId);
            await existingReader.CloseAsync();
            return existingResult;
        }, ct);

    public async Task<ExpenseOcrJobDto?> GetOcrAsync(TenantId tenant, Guid entity, Guid expenseId, Guid jobId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,state,attempt_count,suggestions,failure_code,failure_reason,requested_at,started_at,completed_at,evidence_id from business_expenses.expense_ocr_job where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, expenseId, jobId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        ExpenseOcrJobDto? result = null;
        if (await reader.ReadAsync(ct)) result = ReadOcrJob(reader, expenseId, reader.GetGuid(9));
        await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public Task<ExpenseOcrJobDto> RetryOcrAsync(TenantId tenant, Guid entity, Guid expenseId, Guid jobId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await EnsureOcrEditableAsync(context, tenant, entity, expenseId, token);
            await using var update = new NpgsqlCommand("update business_expenses.expense_ocr_job set state='Requested',failure_code=null,failure_reason=null,requested_at=now(),started_at=null,completed_at=null where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and state='Failed' returning id,state,attempt_count,suggestions,failure_code,failure_reason,requested_at,started_at,completed_at,evidence_id", context.Connection, context.Transaction);
            Add(update, tenant.Value, entity, expenseId, jobId);
            await using var reader = await update.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.ocr_not_retryable");
            var result = ReadOcrJob(reader, expenseId, reader.GetGuid(9)); await reader.CloseAsync();
            context.AddOutbox(new(Guid.CreateVersion7(), "expenses.ocr.requested", JsonSerializer.Serialize(new { JobId = result.Id, LegalEntityId = entity, ExpenseId = expenseId, EvidenceId = result.EvidenceId }), tenant, DateTimeOffset.UtcNow, result.Id, result.Id, "expenses"));
            return result;
        }, ct);

    public Task<ExpenseOcrJobDto> CompleteOcrManuallyAsync(TenantId tenant, Guid entity, Guid expenseId, Guid jobId, CompleteExpenseOcrManuallyRequest request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await EnsureOcrEditableAsync(context, tenant, entity, expenseId, token);
            if (request.Suggestions.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || string.IsNullOrWhiteSpace(request.Reason)) throw new ExpenseRepositoryException("expenses.ocr_manual_invalid");
            await using var update = new NpgsqlCommand("update business_expenses.expense_ocr_job set state='Manual',suggestions=$5::jsonb,failure_code=null,failure_reason=$6,completed_at=now() where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and state in ('Failed','Requested') returning id,state,attempt_count,suggestions,failure_code,failure_reason,requested_at,started_at,completed_at,evidence_id", context.Connection, context.Transaction);
            Add(update, tenant.Value, entity, expenseId, jobId, request.Suggestions.GetRawText(), request.Reason.Trim());
            await using var reader = await update.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.ocr_manual_not_allowed");
            var result = ReadOcrJob(reader, expenseId, reader.GetGuid(9)); await reader.CloseAsync();
            await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'ocr.manual',jsonb_build_object('jobId',$5::text,'reason',$6::text))", context.Connection, context.Transaction);
            Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, jobId, request.Reason.Trim()); await history.ExecuteNonQueryAsync(token);
            return result;
        }, ct);

    /// <param name="rethrowProviderFailure">Worker callers set this to true so the broker retry policy can
    /// redeliver a failed provider request. API/manual callers keep the default fail-closed behavior and
    /// receive a durable Failed job that can be retried explicitly.</param>
    public async Task ProcessOcrJobAsync(TenantId tenant, Guid entity, Guid expenseId, Guid jobId, Guid evidenceId, CancellationToken ct, bool rethrowProviderFailure = false)
    {
        Guid fileRef;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockCommand = new NpgsqlCommand("select j.evidence_id,e.file_reference,j.state from business_expenses.expense_ocr_job j join business_expenses.expense_evidence e on e.tenant_id=j.tenant_id and e.id=j.evidence_id where j.tenant_id=$1 and j.legal_entity_id=$2 and j.expense_id=$3 and j.id=$4 for update", scope.Connection, scope.Transaction);
            Add(lockCommand, tenant.Value, entity, expenseId, jobId);
            await using var reader = await lockCommand.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return; }
            if (reader.GetString(2) is not ("Requested" or "Failed")) { await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return; }
            if (!Guid.TryParse(reader.GetString(1), out fileRef)) { await reader.CloseAsync(); await scope.Transaction.RollbackAsync(ct); throw new ExpenseRepositoryException("expenses.ocr_evidence_not_ready"); }
            await reader.CloseAsync();
            await using var mark = new NpgsqlCommand("update business_expenses.expense_ocr_job set state='Processing',attempt_count=attempt_count+1,started_at=now(),failure_code=null,failure_reason=null where tenant_id=$1 and id=$2", scope.Connection, scope.Transaction); Add(mark, tenant.Value, jobId); await mark.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct);
        }
        try
        {
            var result = await (ocrProvider ?? throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", "No approved OCR provider is configured.")).ExtractAsync(tenant, fileRef, ct);
            await using var scope = await OpenAsync(tenant, ct);
            await using var update = new NpgsqlCommand("update business_expenses.expense_ocr_job set state='Succeeded',suggestions=$3::jsonb,completed_at=now() where tenant_id=$1 and id=$2 and state='Processing'", scope.Connection, scope.Transaction); Add(update, tenant.Value, jobId, result.Suggestions.GetRawText()); await update.ExecuteNonQueryAsync(ct);
            await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'ocr.completed',jsonb_build_object('jobId',$5::text))", scope.Connection, scope.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, jobId); await history.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct);
        }
        catch (ExpenseOcrProviderException ex)
        {
            await MarkOcrFailedAsync(tenant, entity, expenseId, jobId, ex.Code, ex.Message, ct);
            if (rethrowProviderFailure) throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkOcrFailedAsync(tenant, entity, expenseId, jobId, "expenses.ocr_provider_failed", ex.Message, ct);
            if (rethrowProviderFailure) throw;
        }
    }

    private async Task MarkOcrFailedAsync(TenantId tenant, Guid entity, Guid expenseId, Guid jobId, string code, string reason, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var update = new NpgsqlCommand("update business_expenses.expense_ocr_job set state='Failed',failure_code=$3,failure_reason=$4,completed_at=now() where tenant_id=$1 and id=$2 and state='Processing'", scope.Connection, scope.Transaction); Add(update, tenant.Value, jobId, code, reason); await update.ExecuteNonQueryAsync(ct);
        await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'ocr.failed',jsonb_build_object('jobId',$5::text,'code',$6::text))", scope.Connection, scope.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, jobId, code); await history.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct);
    }

    public async Task<MissingReceiptDeclarationDto> DeclareMissingReceiptAsync(TenantId tenant, Guid actorId, Guid entity, Guid expenseId, DeclareMissingReceiptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5) throw new ExpenseRepositoryException("expenses.missing_receipt_reason_required");
        await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7();
        Guid? employeeId;
        decimal amount;
        string currencyCode;
        await using (var stateCommand = new NpgsqlCommand("select expense_state,employee_id,amount,currency_code from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction))
        {
            Add(stateCommand, tenant.Value, entity, expenseId);
            await using var stateReader = await stateCommand.ExecuteReaderAsync(ct);
            if (!await stateReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            var state = stateReader.GetString(0);
            employeeId = stateReader.IsDBNull(1) ? null : stateReader.GetGuid(1);
            amount = stateReader.GetDecimal(2);
            currencyCode = stateReader.GetString(3);
            if (state is not ("Draft" or "Submitted")) throw new ExpenseRepositoryException("expenses.missing_receipt_not_editable");
        }
        await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_missing_receipt_declaration(tenant_id,id,legal_entity_id,expense_id,reason,status,declared_by,declared_at) select $1,$2,$3,id,$4,'Declared',$5,now() from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$6 on conflict(tenant_id,expense_id) do update set reason=excluded.reason,status='Declared',declared_by=excluded.declared_by,declared_at=now(),resolved_at=null returning id,expense_id,reason,status,declared_by,declared_at,resolved_at", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, id, entity, request.Reason.Trim(), actorId, expenseId); await using var reader = await cmd.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var result = ReadMissingReceipt(reader); await reader.CloseAsync();
        await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) select $1,'ExpenseMissingReceiptDeclaredV1',$2::jsonb,$3,$4,$5,$6,'expenses','Pending',0 where not exists (select 1 from platform.outbox_message where tenant_id=$3 and message_type='ExpenseMissingReceiptDeclaredV1' and causation_id=$6)", scope.Connection, scope.Transaction);
        Add(outbox, Guid.CreateVersion7(), JsonSerializer.Serialize(new { ExpenseId = expenseId, LegalEntityId = entity, EmployeeId = employeeId, Amount = amount, CurrencyCode = currencyCode, Reason = request.Reason.Trim() }), tenant.Value, DateTimeOffset.UtcNow, expenseId, expenseId);
        await outbox.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<MissingReceiptDeclarationDto?> GetMissingReceiptDeclarationAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,reason,status,declared_by,declared_at,resolved_at from business_expenses.expense_missing_receipt_declaration where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); await using var reader = await cmd.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? ReadMissingReceipt(reader) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    private static async Task EnsureUsableCategoryVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid categoryVersionId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select 1 from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and c.status='Active' and v.id=$3 and v.status='Published'", connection, transaction);
        Add(command, tenant.Value, entity, categoryVersionId);
        if (await command.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.category_not_usable");
    }

    private async Task EnsureEmployeeScopeAsync(TenantId tenant, Guid? employeeId, Guid entity, CancellationToken ct)
    {
        if (!employeeId.HasValue || hr is null) return;
        if (!await hr.EmployeeBelongsToLegalEntityAsync(tenant, employeeId.Value, entity, ct))
            throw new ExpenseRepositoryException("expenses.employee_scope_invalid");
    }

    private static async Task EnsureOcrEditableAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select expense_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity, expenseId);
        var state = await command.ExecuteScalarAsync(ct) as string;
        if (state is null) throw new ExpenseRepositoryException("expenses.expense_not_found");
        if (state is not ("Draft" or "Submitted")) throw new ExpenseRepositoryException("expenses.ocr_not_editable");
    }

    private async Task EnsureExpenseEditableAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select expense_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var state = await cmd.ExecuteScalarAsync(ct) as string; if (state is null) throw new ExpenseRepositoryException("expenses.expense_not_found"); if (state is not ("Draft" or "Submitted")) throw new ExpenseRepositoryException("expenses.evidence_not_editable"); await scope.Transaction.CommitAsync(ct); }

    public async Task<SignedPlatformFileDownload> CreateEvidenceDownloadAsync(TenantId tenant, Guid entity, Guid expenseId, Guid evidenceId, CancellationToken ct)
    {
        if (files is null) throw new ExpenseRepositoryException("expenses.files_capability_unavailable");
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select file_reference from business_expenses.expense_evidence where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, expenseId, evidenceId); var value = await command.ExecuteScalarAsync(ct); await scope.Transaction.CommitAsync(ct); if (value is not string reference || !Guid.TryParse(reference, out var fileRef)) throw new ExpenseRepositoryException("expenses.file_reference_invalid");
        var download = await files.CreateReadyDownloadAsync(tenant, fileRef, "expenses", "expense", expenseId, TimeSpan.FromMinutes(5), ct); return download ?? throw new ExpenseRepositoryException("expenses.file_not_ready");
    }

    private async Task<Scope> OpenAsync(TenantId tenant, CancellationToken ct) { var c = await dataSource.OpenConnectionAsync(ct); var t = await c.BeginTransactionAsync(ct); await using var set = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", c, t); Add(set, tenant.Value.ToString("D")); await set.ExecuteNonQueryAsync(ct); return new(c, t); }
    private static ExpenseDto ReadExpense(NpgsqlDataReader r)
    {
        JsonElement? financialContext = r.IsDBNull(23) ? null : JsonDocument.Parse(r.GetString(23)).RootElement.Clone();
        JsonElement? taxSnapshot = r.IsDBNull(24) ? null : JsonDocument.Parse(r.GetString(24)).RootElement.Clone();
        Guid? approvalRequestId = r.FieldCount > 25 ? GuidValue(r, 25) : null;
        return new(r.GetGuid(0), r.GetGuid(1), GuidValue(r, 2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.GetDecimal(5), r.GetString(6), r.GetString(7), r.GetGuid(8), r.GetString(9), r.GetString(10), r.GetString(11), r.GetString(12), r.GetString(13), r.GetString(14), Text(r, 15), Text(r, 16), r.GetInt64(17), r.GetFieldValue<DateTimeOffset>(18), r.IsDBNull(19) ? null : r.GetFieldValue<DateTimeOffset>(19), r.IsDBNull(20) ? null : r.GetDecimal(20), Text(r, 21)?.Trim(), r.IsDBNull(22) ? null : r.GetDecimal(22), financialContext, taxSnapshot, approvalRequestId, r.FieldCount > 26 ? GuidValue(r, 26) : null, r.FieldCount > 27 ? GuidValue(r, 27) : null, r.FieldCount > 28 ? Text(r, 28) : null, r.FieldCount > 29 && !r.IsDBNull(29) && r.GetBoolean(29));
    }
    private static Guid? GuidValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i); private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i); private static object Db(object? v) => v ?? DBNull.Value; private static void Add(NpgsqlCommand c, params object?[] values) { foreach (var value in values) c.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static string NormalizeMerchantName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static MissingReceiptDeclarationDto ReadMissingReceipt(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetGuid(4), r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6));
    private static ExpensePolicyExceptionDto ReadPolicyException(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetInt64(9), r.GetFieldValue<DateTimeOffset>(10), r.IsDBNull(11) ? null : r.GetFieldValue<DateTimeOffset>(11));
    private static bool SnapshotMatches(System.Text.Json.JsonElement snapshot, long version, decimal amount, string currency, string source, Guid? employee, Guid categoryVersionId, string evidenceState)
    { return snapshot.TryGetProperty("expenseVersion", out var v) && (v.GetInt64() == version || v.GetInt64() == version - 1) && snapshot.TryGetProperty("amount", out var a) && a.GetDecimal() == amount && snapshot.TryGetProperty("currencyCode", out var c) && string.Equals(c.GetString(), currency, StringComparison.OrdinalIgnoreCase) && snapshot.TryGetProperty("paymentSource", out var p) && string.Equals(p.GetString(), source, StringComparison.Ordinal) && snapshot.TryGetProperty("evidenceState", out var es) && string.Equals(es.GetString(), evidenceState, StringComparison.Ordinal) && snapshot.TryGetProperty("categoryVersionId", out var cv) && Guid.TryParse(cv.GetString(), out var category) && category == categoryVersionId && ((employee is null && (!snapshot.TryGetProperty("employeeId", out var e) || e.ValueKind == System.Text.Json.JsonValueKind.Null)) || (employee is not null && snapshot.TryGetProperty("employeeId", out var actual) && Guid.TryParse(actual.GetString(), out var actualEmployee) && actualEmployee == employee)); }
    private static void ValidatePolicyRules(System.Text.Json.JsonElement rules)
    {
        if (rules.ValueKind != System.Text.Json.JsonValueKind.Object) throw new ExpenseRepositoryException("expenses.policy_rules_invalid");
        foreach (var property in rules.EnumerateObject())
        {
            if (property.NameEquals("maxAmount") || property.NameEquals("minAmount"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Number || property.Value.GetDecimal() < 0) throw new ExpenseRepositoryException("expenses.policy_amount_rule_invalid");
                continue;
            }
            if (property.NameEquals("receiptRequired"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.True && property.Value.ValueKind != System.Text.Json.JsonValueKind.False) throw new ExpenseRepositoryException("expenses.policy_receipt_rule_invalid");
                continue;
            }
            if (property.NameEquals("receiptThreshold"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Number || property.Value.GetDecimal() < 0) throw new ExpenseRepositoryException("expenses.policy_receipt_threshold_invalid");
                continue;
            }
            if (property.NameEquals("allowMissingReceiptException") || property.NameEquals("requiresParticipants"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.True && property.Value.ValueKind != System.Text.Json.JsonValueKind.False) throw new ExpenseRepositoryException("expenses.policy_boolean_rule_invalid");
                continue;
            }
            if (property.NameEquals("requiresBusinessPurpose"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.True && property.Value.ValueKind != System.Text.Json.JsonValueKind.False) throw new ExpenseRepositoryException("expenses.policy_purpose_rule_invalid");
                continue;
            }
            if (property.NameEquals("paymentSources"))
            {
                ValidateStringArray(property.Value, new HashSet<string>(StringComparer.Ordinal) { "EmployeePaid", "CompanyPaid", "CorporateCard", "PettyCash" }, "expenses.policy_payment_source_rule_invalid");
                continue;
            }
            if (property.NameEquals("actualMealCategoryVersionIds") || property.NameEquals("fuelCategoryVersionIds"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || !Guid.TryParse(item.GetString(), out _))) throw new ExpenseRepositoryException("expenses.policy_typed_scope_rule_invalid");
                continue;
            }
            if (property.NameEquals("perDiemActualMealHandling") || property.NameEquals("mileageFuelHandling"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.String || property.Value.GetString() is not ("Disallow" or "Reduce" or "AllowException" or "Allow")) throw new ExpenseRepositoryException("expenses.policy_typed_handling_rule_invalid");
                continue;
            }
            if (property.NameEquals("blockedPaymentSources"))
            {
                ValidateStringArray(property.Value, new HashSet<string>(StringComparer.Ordinal) { "EmployeePaid", "CompanyPaid", "CorporateCard", "PettyCash" }, "expenses.policy_payment_source_rule_invalid");
                continue;
            }
            if (property.NameEquals("employeeIds") || property.NameEquals("categoryVersionIds"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || !Guid.TryParse(item.GetString(), out _))) throw new ExpenseRepositoryException("expenses.policy_scope_rule_invalid");
                continue;
            }
            if (property.NameEquals("blockedEmployeeIds") || property.NameEquals("blockedCategoryVersionIds"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || !Guid.TryParse(item.GetString(), out _))) throw new ExpenseRepositoryException("expenses.policy_scope_rule_invalid");
                continue;
            }
            if (property.NameEquals("currencyCodes"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(item.GetString() ?? string.Empty, "^[A-Za-z]{3}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))) throw new ExpenseRepositoryException("expenses.policy_currency_rule_invalid");
                continue;
            }
            if (property.NameEquals("blockedCurrencyCodes"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(item.GetString() ?? string.Empty, "^[A-Za-z]{3}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))) throw new ExpenseRepositoryException("expenses.policy_currency_rule_invalid");
                continue;
            }
            if (property.NameEquals("merchantNames"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))) throw new ExpenseRepositoryException("expenses.policy_merchant_rule_invalid");
                continue;
            }
            if (property.NameEquals("blockedMerchantNames"))
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))) throw new ExpenseRepositoryException("expenses.policy_blocked_merchant_rule_invalid");
                continue;
            }
            throw new ExpenseRepositoryException("expenses.policy_rule_unsupported");
        }
    }

    private static void ValidateStringArray(System.Text.Json.JsonElement value, HashSet<string> allowed, string errorCode)
    {
        if (value.ValueKind != System.Text.Json.JsonValueKind.Array || value.GetArrayLength() == 0 || value.EnumerateArray().Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String || !allowed.Contains(item.GetString() ?? string.Empty))) throw new ExpenseRepositoryException(errorCode);
    }
    private static Guid StableFactId(Guid expenseId) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"expenses:{expenseId:D}")).AsSpan(0, 16));
    private sealed class Scope(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable { public NpgsqlConnection Connection { get; } = connection; public NpgsqlTransaction Transaction { get; } = transaction; public async ValueTask DisposeAsync() { await Transaction.DisposeAsync(); await Connection.DisposeAsync(); } }
    public async Task<IReadOnlyList<ExpenseItemizationDto>> ListItemizationsAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,description,amount,tax_amount,currency_code from business_expenses.expense_itemization where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseItemizationDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpenseItemizationDto> AddItemizationAsync(TenantId tenant, Guid entity, Guid expenseId, CreateExpenseItemizationRequest request, CancellationToken ct)
    { if (request.Amount < 0 || request.TaxAmount < 0 || string.IsNullOrWhiteSpace(request.Description)) throw new ExpenseRepositoryException("expenses.itemization_invalid"); await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_itemization(tenant_id,id,legal_entity_id,expense_id,description,amount,currency_code,tax_amount) select $1,$2,$3,id,$4,$5,$6,$7 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$8 and expense_state='Draft' returning id,expense_id,description,amount,tax_amount,currency_code", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, entity, request.Description.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.TaxAmount, expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.itemization_not_editable"); var result = new ExpenseItemizationDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task DeleteItemizationAsync(TenantId tenant, Guid entity, Guid expenseId, Guid itemizationId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("delete from business_expenses.expense_itemization where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and exists(select 1 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft')", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId, itemizationId); if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new ExpenseRepositoryException("expenses.itemization_not_editable"); await scope.Transaction.CommitAsync(ct); }

    public async Task DeleteDraftExpenseAsync(TenantId tenant, Guid entity, Guid expenseId, Guid actorId, Guid membershipId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var check = new NpgsqlCommand("select expense_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(check, tenant.Value, entity, expenseId);
        var state = await check.ExecuteScalarAsync(ct) as string;
        if (state is null) throw new ExpenseRepositoryException("expenses.expense_not_found");
        if (state != "Draft") throw new ExpenseRepositoryException("expenses.cannot_delete_non_draft");

        if (budgetControl is not null)
        {
            await using var context = new ExistingTransactionContext(scope.Connection, scope.Transaction);
            if (await budgetControl.HasOpenReservationAsync(context, tenant, entity, "expenses", expenseId.ToString("D"), "expense", ct))
                await budgetControl.ReleaseReservationAsync(context, tenant, "expenses", expenseId.ToString("D"), "expense", ct);
        }

        await using var delete = new NpgsqlCommand("select business_expenses.delete_draft_expense_with_actor($1,$2,$3,$4,$5)", scope.Connection, scope.Transaction);
        Add(delete, tenant.Value, entity, expenseId, actorId, membershipId);
        if (!Convert.ToBoolean(await delete.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture)) throw new ExpenseRepositoryException("expenses.expense_not_found");

        await scope.Transaction.CommitAsync(ct);
    }

    private static string PlanningDimension(Guid categoryVersionId, Guid? projectId, Guid? taskId, string? costCategory)
    {
        var values = new Dictionary<string, string> { ["categoryVersionId"] = categoryVersionId.ToString("D") };
        if (projectId.HasValue) values["projectId"] = projectId.Value.ToString("D");
        if (taskId.HasValue) values["taskId"] = taskId.Value.ToString("D");
        if (!string.IsNullOrWhiteSpace(costCategory)) values["costCategory"] = costCategory.Trim();
        return JsonSerializer.Serialize(values);
    }

    private static DateOnly PlanningPeriod(DateOnly date) => new(date.Year, date.Month, 1);

    public Task DeleteDraftExpenseAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
        => DeleteDraftExpenseAsync(tenant, entity, expenseId, Guid.Empty, Guid.Empty, ct);

    public async Task DeleteSelfDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        var expense = await GetAsync(tenant, entity, expenseId, ct);
        if (expense is null || expense.EmployeeId != employeeId) throw new ExpenseRepositoryException("expenses.expense_not_found");
        await DeleteDraftExpenseAsync(tenant, entity, expenseId, actorId, membershipId, ct);
    }

    public async Task<ExpenseParticipantDto> AddParticipantAsync(TenantId tenant, Guid entity, Guid expenseId, AddExpenseParticipantRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new ExpenseRepositoryException("expenses.participant_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_participant(tenant_id,id,legal_entity_id,expense_id,party_id,display_name,role) select $1,$2,$3,id,$4,$5,$6 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$7 and expense_state='Draft' returning id,expense_id,party_id,display_name,role", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), entity, Db(request.PartyId), request.DisplayName.Trim(), request.Role.Trim(), expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.participant_not_editable"); var result = new ExpenseParticipantDto(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<IReadOnlyList<ExpenseParticipantDto>> ListParticipantsAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,party_id,display_name,role from business_expenses.expense_participant where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by created_at,id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseParticipantDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpensePolicyDto> CreatePolicyAsync(TenantId tenant, CreateExpensePolicyRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ExpenseRepositoryException("expenses.policy_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_policy(tenant_id,id,legal_entity_id,code,name) values($1,$2,$3,$4,$5) returning id,legal_entity_id,code,name,status", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, request.Code.Trim().ToUpperInvariant(), request.Name.Trim()); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_invalid"); var result = new ExpensePolicyDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<ExpensePolicyDto>> ListPoliciesAsync(TenantId tenant, Guid entity, string? status, string? search, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) and ($4::text is null or code ilike '%'||$4||'%' or name ilike '%'||$4||'%')"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_policy where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(status) ? null : status.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var command = new NpgsqlCommand($"select id,legal_entity_id,code,name,status from business_expenses.expense_policy where {where} order by code,id offset $5 limit $6", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(status) ? null : status.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpensePolicyDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }
    public async Task<ExpensePage<ExpensePolicyVersionDto>> ListPolicyVersionsAsync(TenantId tenant, Guid entity, Guid policyId, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "v.tenant_id=$1 and v.legal_entity_id=$2 and v.policy_id=$3"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_policy_version v where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, policyId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var command = new NpgsqlCommand($"select v.id,v.policy_id,v.version,v.status,v.effective_from,v.effective_to,v.rules::text from business_expenses.expense_policy_version v where {where} order by v.version desc,v.id desc offset $4 limit $5", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, policyId, (page - 1) * pageSize, pageSize); var list = new List<ExpensePolicyVersionDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetFieldValue<DateOnly>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), JsonDocument.Parse(reader.GetString(6)).RootElement.Clone())); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }
    public async Task<ExpensePolicyVersionDto> CreatePolicyVersionAsync(TenantId tenant, Guid entity, Guid policyId, CreateExpensePolicyVersionRequest request, CancellationToken ct)
    { ValidatePolicyRules(request.Rules); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_policy_version(tenant_id,id,legal_entity_id,policy_id,version,rules,effective_from,effective_to) select $1,$2,p.legal_entity_id,p.id,coalesce((select max(version)+1 from business_expenses.expense_policy_version where tenant_id=$1 and policy_id=$3),1),$4::jsonb,$5,$6 from business_expenses.expense_policy p where p.tenant_id=$1 and p.legal_entity_id=$7 and p.id=$3 returning id,policy_id,version,status,effective_from,effective_to,rules::text", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), policyId, request.Rules.GetRawText(), request.EffectiveFrom, Db(request.EffectiveTo), entity); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_not_found"); var result = new ExpensePolicyVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5), System.Text.Json.JsonDocument.Parse(r.GetString(6)).RootElement.Clone()); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePolicyVersionDto> PublishPolicyVersionAsync(TenantId tenant, Guid entity, Guid versionId, PublishExpensePolicyVersionRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_policy_version v set status='Published',effective_to=coalesce($4,effective_to) where v.tenant_id=$1 and v.id=$2 and v.legal_entity_id=$3 and v.status='Draft' returning v.id,v.policy_id,v.version,v.status,v.effective_from,v.effective_to,v.rules::text", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, versionId, entity, Db(request.EffectiveTo)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_version_not_publishable"); var result = new ExpensePolicyVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5), System.Text.Json.JsonDocument.Parse(r.GetString(6)).RootElement.Clone()); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePolicyVersionDto> RetirePolicyVersionAsync(TenantId tenant, Guid entity, Guid versionId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_policy_version set status='Retired' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Published' returning id,policy_id,version,status,effective_from,effective_to,rules::text", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, versionId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_version_not_retireable"); var result = new ExpensePolicyVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5), System.Text.Json.JsonDocument.Parse(r.GetString(6)).RootElement.Clone()); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    private async Task<ExpensePolicyEvaluationDto> EvaluatePolicyLegacyAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_policy_evaluation(tenant_id,id,legal_entity_id,expense_id,policy_version_id,state,snapshot) select $1,$2,$3,e.id,v.id,case when jsonb_typeof(v.rules->'maxAmount')='number' and e.amount > (v.rules->>'maxAmount')::numeric then 'Blocked' when jsonb_typeof(v.rules->'minAmount')='number' and e.amount < (v.rules->>'minAmount')::numeric then 'Blocked' when jsonb_typeof(v.rules->'requiresBusinessPurpose')='boolean' and (v.rules->>'requiresBusinessPurpose')::boolean and nullif(trim(e.business_purpose),'') is null then 'Blocked' when coalesce((v.rules->>'receiptRequired')::boolean,false) and e.evidence_state='Missing' and not exists(select 1 from business_expenses.expense_missing_receipt_declaration md where md.tenant_id=e.tenant_id and md.legal_entity_id=e.legal_entity_id and md.expense_id=e.id and md.status='Declared') then 'Blocked' when coalesce((v.rules->>'receiptRequired')::boolean,false) and e.evidence_state='Missing' then 'Warning' else 'Passed' end,jsonb_build_object('evidenceState',e.evidence_state,'amount',e.amount,'currencyCode',e.currency_code,'paymentSource',e.payment_source,'employeeId',e.employee_id,'categoryVersionId',e.category_version_id,'expenseVersion',e.version,'policyVersionId',v.id,'rules',coalesce(v.rules,'{}'::jsonb),'missingReceiptExceptionReason',(select md.reason from business_expenses.expense_missing_receipt_declaration md where md.tenant_id=e.tenant_id and md.legal_entity_id=e.legal_entity_id and md.expense_id=e.id and md.status='Declared' order by md.declared_at desc limit 1),'findings',jsonb_build_array(case when jsonb_typeof(v.rules->'maxAmount')='number' and e.amount > (v.rules->>'maxAmount')::numeric then 'MaxAmountExceeded' when jsonb_typeof(v.rules->'minAmount')='number' and e.amount < (v.rules->>'minAmount')::numeric then 'MinAmountNotMet' when jsonb_typeof(v.rules->'requiresBusinessPurpose')='boolean' and (v.rules->>'requiresBusinessPurpose')::boolean and nullif(trim(e.business_purpose),'') is null then 'BusinessPurposeRequired' when coalesce((v.rules->>'receiptRequired')::boolean,false) and e.evidence_state='Missing' and not exists(select 1 from business_expenses.expense_missing_receipt_declaration md where md.tenant_id=e.tenant_id and md.legal_entity_id=e.legal_entity_id and md.expense_id=e.id and md.status='Declared') then 'ReceiptRequired' when coalesce((v.rules->>'receiptRequired')::boolean,false) and e.evidence_state='Missing' then 'ReceiptException' else 'Passed' end),'evaluatedAt',now()) from business_expenses.expense e left join lateral (select pv.id,pv.rules from business_expenses.expense_policy_version pv join business_expenses.expense_policy p on p.tenant_id=pv.tenant_id and p.id=pv.policy_id where pv.tenant_id=e.tenant_id and pv.legal_entity_id=e.legal_entity_id and pv.status='Published' and pv.effective_from <= e.expense_date and (pv.effective_to is null or pv.effective_to >= e.expense_date) and (not (pv.rules ? 'paymentSources') or pv.rules->'paymentSources' ? e.payment_source) and (not (pv.rules ? 'employeeIds') or (e.employee_id is not null and pv.rules->'employeeIds' ? e.employee_id::text)) and (not (pv.rules ? 'categoryVersionIds') or pv.rules->'categoryVersionIds' ? e.category_version_id::text) and (not (pv.rules ? 'currencyCodes') or pv.rules->'currencyCodes' ? e.currency_code) and (not (pv.rules ? 'merchantNames') or exists (select 1 from jsonb_array_elements_text(pv.rules->'merchantNames') n where lower(e.merchant_name)=lower(n))) order by pv.effective_from desc,pv.version desc,pv.id desc limit 1) v on true where e.tenant_id=$1 and e.legal_entity_id=$3 and e.id=$4 on conflict(tenant_id,expense_id) do update set policy_version_id=excluded.policy_version_id,state=excluded.state,snapshot=excluded.snapshot,evaluated_at=now() returning id,expense_id,state,snapshot,evaluated_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, entity, expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var result = new ExpensePolicyEvaluationDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), System.Text.Json.JsonDocument.Parse(r.GetString(3)).RootElement.Clone(), r.GetFieldValue<DateTimeOffset>(4)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePolicyEvaluationDto> EvaluatePolicyAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var expenseCommand = new NpgsqlCommand("select id,amount,currency_code,payment_source,employee_id,category_version_id,expense_date,expense_state,evidence_state,business_purpose,merchant_name,version from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(expenseCommand, tenant.Value, entity, expenseId);
        await using var expenseReader = await expenseCommand.ExecuteReaderAsync(ct);
        if (!await expenseReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
        var amount = expenseReader.GetDecimal(1);
        var currency = expenseReader.GetString(2).Trim();
        var paymentSource = expenseReader.GetString(3);
        var employee = expenseReader.IsDBNull(4) ? (Guid?)null : expenseReader.GetGuid(4);
        var categoryVersionId = expenseReader.GetGuid(5);
        var expenseDate = expenseReader.GetFieldValue<DateOnly>(6);
        var evidenceState = expenseReader.GetString(8);
        var businessPurpose = expenseReader.IsDBNull(9) ? null : expenseReader.GetString(9);
        var merchant = expenseReader.GetString(10);
        var expenseVersion = expenseReader.GetInt64(11);
        await expenseReader.CloseAsync();

        Guid? policyVersionId = null;
        JsonElement rules = JsonDocument.Parse("{}").RootElement.Clone();
        await using (var policyCommand = new NpgsqlCommand("select pv.id,pv.rules::text from business_expenses.expense_policy_version pv where pv.tenant_id=$1 and pv.legal_entity_id=$2 and pv.status='Published' and pv.effective_from <= $3 and (pv.effective_to is null or pv.effective_to >= $3) and (not (pv.rules ? 'paymentSources') or pv.rules->'paymentSources' ? $4::text) and (not (pv.rules ? 'employeeIds') or ($5::uuid is not null and pv.rules->'employeeIds' ? $5::text)) and (not (pv.rules ? 'categoryVersionIds') or pv.rules->'categoryVersionIds' ? $6::text) and (not (pv.rules ? 'currencyCodes') or pv.rules->'currencyCodes' ? $7::text) and (not (pv.rules ? 'merchantNames') or exists (select 1 from jsonb_array_elements_text(pv.rules->'merchantNames') n where lower($8::text)=lower(n))) order by pv.effective_from desc,pv.version desc,pv.id desc limit 1", scope.Connection, scope.Transaction))
        {
            Add(policyCommand, tenant.Value, entity, expenseDate, paymentSource, Db(employee), categoryVersionId, currency, merchant);
            await using var policyReader = await policyCommand.ExecuteReaderAsync(ct);
            if (await policyReader.ReadAsync(ct))
            {
                policyVersionId = policyReader.GetGuid(0);
                rules = JsonDocument.Parse(policyReader.GetString(1)).RootElement.Clone();
            }
        }

        string? missingReceiptReason = null;
        await using (var exceptionCommand = new NpgsqlCommand("select reason from business_expenses.expense_missing_receipt_declaration where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and status='Declared' order by declared_at desc limit 1", scope.Connection, scope.Transaction))
        {
            Add(exceptionCommand, tenant.Value, entity, expenseId);
            missingReceiptReason = (string?)await exceptionCommand.ExecuteScalarAsync(ct);
        }

        var findings = new List<string>();
        var blocked = false;
        var warning = false;
        var baseAmount = amount;
        await using (var previousEvaluation = new NpgsqlCommand("select snapshot->>'baseAmount' from business_expenses.expense_policy_evaluation where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction))
        {
            Add(previousEvaluation, tenant.Value, entity, expenseId);
            var previousBase = await previousEvaluation.ExecuteScalarAsync(ct) as string;
            if (decimal.TryParse(previousBase, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedBase)) baseAmount = parsedBase;
        }
        var adjustedAmount = baseAmount;
        var typedReductions = new List<object>();
        if (rules.TryGetProperty("maxAmount", out var max) && max.ValueKind == JsonValueKind.Number && amount > max.GetDecimal()) { blocked = true; findings.Add("MaxAmountExceeded"); }
        if (rules.TryGetProperty("minAmount", out var min) && min.ValueKind == JsonValueKind.Number && amount < min.GetDecimal()) { blocked = true; findings.Add("MinAmountNotMet"); }
        if (rules.TryGetProperty("requiresBusinessPurpose", out var purpose) && purpose.ValueKind == JsonValueKind.True && string.IsNullOrWhiteSpace(businessPurpose)) { blocked = true; findings.Add("BusinessPurposeRequired"); }
        var receiptRequired = rules.TryGetProperty("receiptRequired", out var required) && required.ValueKind == JsonValueKind.True;
        if (rules.TryGetProperty("receiptThreshold", out var threshold) && threshold.ValueKind == JsonValueKind.Number && amount > threshold.GetDecimal()) receiptRequired = true;
        if (receiptRequired && evidenceState == "Missing")
        {
            var allowsMissingReceiptException = !rules.TryGetProperty("allowMissingReceiptException", out var exceptionAllowed)
                || exceptionAllowed.ValueKind != JsonValueKind.False;
            if (string.IsNullOrWhiteSpace(missingReceiptReason)) { blocked = true; findings.Add("ReceiptRequired"); }
            else if (!allowsMissingReceiptException) { blocked = true; findings.Add("ReceiptExceptionNotAllowed"); }
            else { warning = true; findings.Add("ReceiptException"); }
        }
        if (rules.TryGetProperty("blockedMerchantNames", out var blockedMerchants) && blockedMerchants.ValueKind == JsonValueKind.Array && blockedMerchants.EnumerateArray().Any(x => string.Equals(x.GetString(), merchant, StringComparison.OrdinalIgnoreCase))) { blocked = true; findings.Add("MerchantBlocked"); }
        if (rules.TryGetProperty("blockedPaymentSources", out var blockedSources) && blockedSources.ValueKind == JsonValueKind.Array && blockedSources.EnumerateArray().Any(x => string.Equals(x.GetString(), paymentSource, StringComparison.Ordinal))) { blocked = true; findings.Add("PaymentSourceBlocked"); }
        if (rules.TryGetProperty("blockedCurrencyCodes", out var blockedCurrencies) && blockedCurrencies.ValueKind == JsonValueKind.Array && blockedCurrencies.EnumerateArray().Any(x => string.Equals(x.GetString(), currency, StringComparison.OrdinalIgnoreCase))) { blocked = true; findings.Add("CurrencyBlocked"); }
        if (rules.TryGetProperty("blockedEmployeeIds", out var blockedEmployees) && blockedEmployees.ValueKind == JsonValueKind.Array && employee.HasValue && blockedEmployees.EnumerateArray().Any(x => Guid.TryParse(x.GetString(), out var id) && id == employee.Value)) { blocked = true; findings.Add("EmployeeBlocked"); }
        if (rules.TryGetProperty("blockedCategoryVersionIds", out var blockedCategories) && blockedCategories.ValueKind == JsonValueKind.Array && blockedCategories.EnumerateArray().Any(x => Guid.TryParse(x.GetString(), out var id) && id == categoryVersionId)) { blocked = true; findings.Add("CategoryBlocked"); }
        if (rules.TryGetProperty("requiresParticipants", out var participantsRequired) && participantsRequired.ValueKind == JsonValueKind.True)
        {
            await using var participantCountCommand = new NpgsqlCommand("select count(*) from business_expenses.expense_participant where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction);
            Add(participantCountCommand, tenant.Value, entity, expenseId);
            if (Convert.ToInt64(await participantCountCommand.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == 0) { blocked = true; findings.Add("ParticipantsRequired"); }
        }
        var typedFacts = (HasMileage: false, HasPerDiem: false, TripStart: expenseDate, TripEnd: expenseDate);
        await using (var typedCommand = new NpgsqlCommand("select exists(select 1 from business_expenses.mileage_expense_detail where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3), exists(select 1 from business_expenses.per_diem_expense_detail where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3), coalesce((select trip_start_at::date from business_expenses.per_diem_expense_detail where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3),$4::date), coalesce((select trip_end_at::date from business_expenses.per_diem_expense_detail where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3),$4::date)", scope.Connection, scope.Transaction))
        {
            Add(typedCommand, tenant.Value, entity, expenseId, expenseDate);
            await using var typedReader = await typedCommand.ExecuteReaderAsync(ct);
            if (await typedReader.ReadAsync(ct)) typedFacts = (typedReader.GetBoolean(0), typedReader.GetBoolean(1), typedReader.GetFieldValue<DateOnly>(2), typedReader.GetFieldValue<DateOnly>(3));
        }
        if (employee.HasValue && typedFacts.HasMileage && rules.TryGetProperty("fuelCategoryVersionIds", out var fuelCategories) && rules.TryGetProperty("mileageFuelHandling", out var fuelHandling) && fuelHandling.ValueKind == JsonValueKind.String)
        {
            var categoryIds = fuelCategories.EnumerateArray().Select(value => Guid.Parse(value.GetString()!)).ToArray();
            await using var fuelConflict = new NpgsqlCommand("select coalesce(sum(e.amount),0) from business_expenses.expense e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.employee_id=$4 and e.expense_date=$5 and e.id<>$3 and e.category_version_id=any($6::uuid[]) and e.currency_code=$7 and e.expense_state not in ('Reversed','Cancelled')", scope.Connection, scope.Transaction);
            Add(fuelConflict, tenant.Value, entity, expenseId, employee.Value, expenseDate, categoryIds, currency);
            var fuelAmount = Convert.ToDecimal(await fuelConflict.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            if (fuelAmount > 0m)
            {
                var handling = fuelHandling.GetString();
                if (handling is "Disallow" or "AllowException") { blocked = true; findings.Add(handling == "AllowException" ? "FuelReceiptExceptionRequired" : "FuelReceiptConflict"); }
                else if (handling == "Reduce")
                {
                    var reduction = Math.Min(adjustedAmount, fuelAmount);
                    typedReductions.Add(new { code = "FuelReceiptReduced", sourceAmount = fuelAmount, reductionAmount = reduction });
                    if (reduction >= adjustedAmount)
                    {
                        blocked = true;
                        findings.Add("FuelReceiptFullyOffset");
                        adjustedAmount = amount;
                    }
                    else
                    {
                        adjustedAmount -= reduction;
                        warning = true;
                        findings.Add("FuelReceiptReduced");
                    }
                }
            }
        }
        if (employee.HasValue && typedFacts.HasPerDiem && rules.TryGetProperty("actualMealCategoryVersionIds", out var mealCategories) && rules.TryGetProperty("perDiemActualMealHandling", out var mealHandling) && mealHandling.ValueKind == JsonValueKind.String)
        {
            var categoryIds = mealCategories.EnumerateArray().Select(value => Guid.Parse(value.GetString()!)).ToArray();
            await using var mealConflict = new NpgsqlCommand("select coalesce(sum(e.amount),0) from business_expenses.expense e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.employee_id=$4 and e.expense_date between $5 and $6 and e.id<>$3 and e.category_version_id=any($7::uuid[]) and e.currency_code=$8 and e.expense_state not in ('Reversed','Cancelled')", scope.Connection, scope.Transaction);
            Add(mealConflict, tenant.Value, entity, expenseId, employee.Value, typedFacts.TripStart, typedFacts.TripEnd, categoryIds, currency);
            var mealAmount = Convert.ToDecimal(await mealConflict.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            if (mealAmount > 0m)
            {
                var handling = mealHandling.GetString();
                if (handling is "Disallow" or "AllowException") { blocked = true; findings.Add(handling == "AllowException" ? "ActualMealExceptionRequired" : "ActualMealConflict"); }
                else if (handling == "Reduce")
                {
                    var reduction = Math.Min(adjustedAmount, mealAmount);
                    typedReductions.Add(new { code = "ActualMealReduced", sourceAmount = mealAmount, reductionAmount = reduction });
                    if (reduction >= adjustedAmount)
                    {
                        blocked = true;
                        findings.Add("ActualMealFullyOffset");
                        adjustedAmount = amount;
                    }
                    else
                    {
                        adjustedAmount -= reduction;
                        warning = true;
                        findings.Add("ActualMealReduced");
                    }
                }
            }
        }
        if (adjustedAmount != amount)
        {
            await ApplyPolicyAdjustedAmountAsync(scope, tenant, entity, expenseId, adjustedAmount, typedFacts.HasMileage, typedFacts.HasPerDiem, ct);
            amount = adjustedAmount;
        }
        if (findings.Count == 0) findings.Add("Passed");
        var state = blocked ? "Blocked" : warning ? "Warning" : "Passed";
        var snapshot = JsonSerializer.SerializeToDocument(new { evidenceState, amount, baseAmount, adjustedAmount = amount, currencyCode = currency, paymentSource, employeeId = employee, categoryVersionId, expenseVersion, policyVersionId, rules, missingReceiptExceptionReason = missingReceiptReason, findings, typedReductions, evaluatedAt = DateTimeOffset.UtcNow }).RootElement.Clone();
        await using var save = new NpgsqlCommand("insert into business_expenses.expense_policy_evaluation(tenant_id,id,legal_entity_id,expense_id,policy_version_id,state,snapshot) values($1,$2,$3,$4,$5,$6,$7::jsonb) on conflict(tenant_id,expense_id) do update set policy_version_id=excluded.policy_version_id,state=excluded.state,snapshot=excluded.snapshot,evaluated_at=now() returning id,expense_id,state,snapshot::text,evaluated_at", scope.Connection, scope.Transaction);
        Add(save, tenant.Value, Guid.CreateVersion7(), entity, expenseId, Db(policyVersionId), state, snapshot.GetRawText());
        await using var resultReader = await save.ExecuteReaderAsync(ct);
        if (!await resultReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_evaluation_failed");
        var result = new ExpensePolicyEvaluationDto(resultReader.GetGuid(0), resultReader.GetGuid(1), resultReader.GetString(2), JsonDocument.Parse(resultReader.GetString(3)).RootElement.Clone(), resultReader.GetFieldValue<DateTimeOffset>(4));
        await resultReader.CloseAsync();
        await scope.Transaction.CommitAsync(ct);
        return result;
    }

    public async Task<ExpensePage<ExpenseReportDto>> ListReportsAsync(TenantId tenant, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or title ilike '%'||$4||'%')"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_report where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at from business_expenses.expense_report where {where} order by created_at desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpenseReportDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetString(6), r.GetInt64(7), r.GetFieldValue<DateTimeOffset>(8))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }
    public async Task<ExpenseReportDto?> GetReportAsync(TenantId tenant, Guid entity, Guid reportId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at from business_expenses.expense_report where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, reportId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        ExpenseReportDto? result = null;
        if (await reader.ReadAsync(ct)) result = new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8));
        await reader.CloseAsync();
        await scope.Transaction.CommitAsync(ct);
        return result;
    }
    public async Task<string> ExportReportsCsvAsync(TenantId tenant, Guid entity, string? state, string? search, CancellationToken ct)
    { var builder = new StringBuilder("id,title,state,total_amount,currency_code,created_at\n"); for (var pageNumber = 1; ; pageNumber++) { var page = await ListReportsAsync(tenant, entity, state, search, pageNumber, 200, ct); foreach (var item in page.Items) builder.Append(Csv(item.Id.ToString("D"))).Append(',').Append(Csv(item.Title)).Append(',').Append(Csv(item.State)).Append(',').Append(item.TotalAmount.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Csv(item.CurrencyCode)).Append(',').Append(item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)).Append('\n'); if (page.Items.Count < page.PageSize) break; } return builder.ToString(); }
    private static string Csv(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) ? '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"' : value;
    public async Task<ExpenseExceptionSummaryDto> GetExceptionSummaryAsync(TenantId tenant, Guid entity, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select (select count(*) from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and expense_state='Submitted' and approval_state='Pending'),(select count(*) from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and expense_state in ('Draft','Submitted') and evidence_state='Missing'),(select count(*) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and legal_entity_id=$2 and status in ('Pending','PartiallySettled')),(select coalesce(sum(amount-settled_amount),0) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and legal_entity_id=$2 and status in ('Pending','PartiallySettled')),(select count(*) from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and state in ('Requested','Pending')),(select coalesce(sum(amount-settled_amount),0) from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and state in ('Requested','Pending')),(select count(*) from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and state in ('Issued','PartiallySettled')),(select coalesce(sum(amount-applied_amount-returned_amount-written_off_amount),0) from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and state in ('Issued','PartiallySettled'))", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_unavailable"); var result = new ExpenseExceptionSummaryDto(checked((int)r.GetInt64(0)), checked((int)r.GetInt64(1)), checked((int)r.GetInt64(2)), r.GetDecimal(3), checked((int)r.GetInt64(4)), r.GetDecimal(5), checked((int)r.GetInt64(6)), r.GetDecimal(7)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    /// <summary>Emits one durable reminder event per unresolved employee action; source records remain authoritative.</summary>
    public async Task<int> ProcessReminderNotificationsAsync(TenantId tenant, Guid entity, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var candidates = new NpgsqlCommand("""
            select 'MissingReceipt'::text as reminder_type,e.id,e.employee_id,e.amount,e.currency_code
            from business_expenses.expense e
            where e.tenant_id=$1 and e.legal_entity_id=$2 and e.employee_id is not null and e.expense_state in ('Draft','Submitted') and e.evidence_state='Missing'
            union all
            select 'ApprovalPending',e.id,e.employee_id,e.amount,e.currency_code
            from business_expenses.expense e
            where e.tenant_id=$1 and e.legal_entity_id=$2 and e.employee_id is not null and e.expense_state='Submitted' and e.approval_state='Pending'
            union all
            select 'AdvanceDue',a.id,a.employee_id,a.amount-a.applied_amount-a.returned_amount-a.written_off_amount,a.currency_code
            from business_expenses.business_advance a
            where a.tenant_id=$1 and a.legal_entity_id=$2 and a.employee_id is not null and a.state in ('Issued','PartiallySettled') and a.settlement_due_date is not null and a.settlement_due_date <= current_date
            union all
            select 'EmployeeRepaymentDue',r.id,r.employee_id,r.amount-r.settled_amount,r.currency_code
            from business_expenses.expense_employee_repayment r
            where r.tenant_id=$1 and r.legal_entity_id=$2 and r.state in ('Requested','Pending')
            order by reminder_type,id
            limit 500
            """, scope.Connection, scope.Transaction);
        Add(candidates, tenant.Value, entity);
        var inserted = 0;
        await using var reader = await candidates.ExecuteReaderAsync(ct);
        var rows = new List<(string Type, Guid Id, Guid EmployeeId, decimal Amount, string Currency)>();
        while (await reader.ReadAsync(ct)) rows.Add((reader.GetString(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4).Trim()));
        await reader.CloseAsync();
        foreach (var row in rows)
        {
            var reminderId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"expenses:reminder:{row.Type}:{row.Id:D}"))[..16]);
            await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) select $1,'ExpenseReminderV1',jsonb_build_object('ReminderType',$2::text,'LegalEntityId',$3::text,'EmployeeId',$4::text,'SourceId',$5::text,'Amount',$6::numeric,'CurrencyCode',$7::text),$8,now(),$1,$1,'expenses','Pending',0 where not exists(select 1 from platform.outbox_message where tenant_id=$8 and message_type='ExpenseReminderV1' and causation_id=$1)", scope.Connection, scope.Transaction);
            Add(outbox, reminderId, row.Type, entity, row.EmployeeId, row.Id, row.Amount, row.Currency, tenant.Value);
            inserted += await outbox.ExecuteNonQueryAsync(ct);
        }
        await scope.Transaction.CommitAsync(ct);
        return inserted;
    }
    public async Task<ExpensePage<ExpenseSpendReportRowDto>> GetSpendReportAsync(TenantId tenant, Guid entity, string groupBy, DateOnly? from, DateOnly? to, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var normalized = groupBy.Trim().ToLowerInvariant();
        var expression = normalized switch
        {
            "category" => "coalesce(c.code, 'UNCATEGORIZED')",
            "paymentsource" or "payment_source" => "e.payment_source",
            "employee" => "coalesce(e.employee_id::text, 'UNASSIGNED')",
            _ => throw new ExpenseRepositoryException("expenses.report_group_invalid")
        };
        await using var scope = await OpenAsync(tenant, ct);
        var where = "e.tenant_id=$1 and e.legal_entity_id=$2 and e.expense_state='Finalized' and ($3::date is null or e.expense_date >= $3) and ($4::date is null or e.expense_date <= $4) and ($5::text is null or " + expression + " ilike '%'||$5||'%')";
        await using var count = new NpgsqlCommand($"select count(*) from (select {expression} from business_expenses.expense e left join business_expenses.expense_category_version cv on cv.tenant_id=e.tenant_id and cv.id=e.category_version_id left join business_expenses.expense_category c on c.tenant_id=cv.tenant_id and c.id=cv.category_id where {where} group by {expression}) grouped", scope.Connection, scope.Transaction);
        Add(count, tenant.Value, entity, Db(from), Db(to), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select {expression},coalesce(sum(e.amount),0),coalesce(sum(coalesce(e.base_amount,e.amount)),0),coalesce(sum(coalesce(e.tax_amount,0)),0),count(*) from business_expenses.expense e left join business_expenses.expense_category_version cv on cv.tenant_id=e.tenant_id and cv.id=e.category_version_id left join business_expenses.expense_category c on c.tenant_id=cv.tenant_id and c.id=cv.category_id where {where} group by {expression} order by {expression} offset $6 limit $7", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, Db(from), Db(to), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize);
        var rows = new List<ExpenseSpendReportRowDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(normalized, reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), checked((int)reader.GetInt64(4))));
        await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct);
        return new(rows, total, page, pageSize);
    }
    public async Task<ExpenseReportDto> CreateReportAsync(TenantId tenant, CreateExpenseReportRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Title)) throw new ExpenseRepositoryException("expenses.report_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_report(tenant_id,id,legal_entity_id,employee_id,title,currency_code) values($1,$2,$3,$4,$5,$6) returning id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, Db(request.EmployeeId), request.Title.Trim(), request.CurrencyCode.Trim().ToUpperInvariant()); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_invalid"); var result = new ExpenseReportDto(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetString(6), r.GetInt64(7), r.GetFieldValue<DateTimeOffset>(8)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseImportBatchDto> ImportExpensesAsync(TenantId tenant, ImportExpensesRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Rows is null || request.Rows.Count is < 1 or > 500) throw new ExpenseRepositoryException("expenses.import_invalid");
        var externalReferences = request.Rows.Select(row => row.ExternalReference.Trim()).ToArray();
        if (externalReferences.Any(string.IsNullOrWhiteSpace) || externalReferences.Distinct(StringComparer.OrdinalIgnoreCase).Count() != externalReferences.Length) throw new ExpenseRepositoryException("expenses.import_duplicate_reference");
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        await using var scope = await OpenAsync(tenant, ct);
        await using var existing = new NpgsqlCommand("select id,payload_hash from business_expenses.expense_import_batch where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", scope.Connection, scope.Transaction); Add(existing, tenant.Value, request.LegalEntityId, request.IdempotencyKey.Trim());
        await using var existingReader = await existing.ExecuteReaderAsync(ct);
        if (await existingReader.ReadAsync(ct)) { var id = existingReader.GetGuid(0); var hash = existingReader.GetString(1); await existingReader.CloseAsync(); await scope.Transaction.CommitAsync(ct); if (!string.Equals(hash, payloadHash, StringComparison.Ordinal)) throw new ExpenseRepositoryException("expenses.import_idempotency_conflict"); return (await GetExpenseImportAsync(tenant, request.LegalEntityId, id, ct))!; }
        await existingReader.CloseAsync();
        var batchId = Guid.CreateVersion7();
        await using var insertBatch = new NpgsqlCommand("insert into business_expenses.expense_import_batch(tenant_id,id,legal_entity_id,idempotency_key,payload_hash,state,total_rows,created_count,error_count) values($1,$2,$3,$4,$5,'Processing',$6,0,0)", scope.Connection, scope.Transaction); Add(insertBatch, tenant.Value, batchId, request.LegalEntityId, request.IdempotencyKey.Trim(), payloadHash, request.Rows.Count); await insertBatch.ExecuteNonQueryAsync(ct); var createdAt = DateTimeOffset.UtcNow;
        var created = 0; var errors = 0; var rowNumber = 0;
        foreach (var row in request.Rows)
        {
            rowNumber++;
            var raw = JsonSerializer.Serialize(row); string? error = null;
            if (string.IsNullOrWhiteSpace(row.ExternalReference) || string.IsNullOrWhiteSpace(row.MerchantName) || row.Amount <= 0m || row.CurrencyCode.Trim().Length != 3 || row.CategoryVersionId == Guid.Empty) error = "expenses.import_row_invalid";
            if (error is null && row.EmployeeId.HasValue && hr is not null && !await hr.EmployeeBelongsToLegalEntityAsync(tenant, row.EmployeeId.Value, request.LegalEntityId, ct))
                error = "expenses.employee_scope_invalid";
            if (error is null)
            {
                await using var duplicate = new NpgsqlCommand("select 1 from business_expenses.expense_import_row r join business_expenses.expense_import_batch b on b.tenant_id=r.tenant_id and b.id=r.batch_id where r.tenant_id=$1 and b.legal_entity_id=$2 and lower(r.external_reference)=lower($3) limit 1", scope.Connection, scope.Transaction);
                Add(duplicate, tenant.Value, request.LegalEntityId, row.ExternalReference.Trim());
                if (await duplicate.ExecuteScalarAsync(ct) is not null) error = "expenses.import_duplicate_external_reference";
            }
            if (error is null) { await using var category = new NpgsqlCommand("select 1 from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and v.id=$2 and c.legal_entity_id=$3 and c.status='Active' and v.status='Published'", scope.Connection, scope.Transaction); Add(category, tenant.Value, row.CategoryVersionId, request.LegalEntityId); if (await category.ExecuteScalarAsync(ct) is null) error = "expenses.import_category_invalid"; }
            Guid? expenseId = null;
            if (error is null) { expenseId = Guid.CreateVersion7(); await using var insert = new NpgsqlCommand("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Draft','Pending','NotFinalized','NotSettled','Missing','NotEvaluated',$11,$12,1,now(),now())", scope.Connection, scope.Transaction); Add(insert, tenant.Value, expenseId.Value, request.LegalEntityId, Db(row.EmployeeId), row.MerchantName.Trim(), row.ExpenseDate, row.Amount, row.CurrencyCode.Trim().ToUpperInvariant(), row.PaymentSource.Trim(), row.CategoryVersionId, Db(row.BusinessPurpose), Db(row.Description)); await insert.ExecuteNonQueryAsync(ct); created++; } else errors++;
            await using var rowInsert = new NpgsqlCommand("insert into business_expenses.expense_import_row(tenant_id,batch_id,row_number,external_reference,expense_id,status,error_code,raw_payload) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb)", scope.Connection, scope.Transaction); Add(rowInsert, tenant.Value, batchId, rowNumber, row.ExternalReference.Trim(), Db(expenseId), error is null ? "Created" : "Error", Db(error), raw); await rowInsert.ExecuteNonQueryAsync(ct);
        }
        await using var complete = new NpgsqlCommand("update business_expenses.expense_import_batch set state=case when $4=0 then 'Completed' else 'CompletedWithErrors' end,created_count=$4,error_count=$5,completed_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(complete, tenant.Value, request.LegalEntityId, batchId, created, errors); await complete.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); return new(batchId, request.LegalEntityId, request.IdempotencyKey.Trim(), errors == 0 ? "Completed" : "CompletedWithErrors", request.Rows.Count, created, errors, createdAt, DateTimeOffset.UtcNow);
    }
    public async Task<ExpenseImportBatchDto?> GetExpenseImportAsync(TenantId tenant, Guid entity, Guid batchId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,idempotency_key,state,total_rows,created_count,error_count,created_at,completed_at from business_expenses.expense_import_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, batchId); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? ReadImportBatch(reader) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<ExpenseImportRowDto>> ListExpenseImportRowsAsync(TenantId tenant, Guid entity, Guid batchId, string? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "r.tenant_id=$1 and r.batch_id=$2 and exists(select 1 from business_expenses.expense_import_batch b where b.tenant_id=r.tenant_id and b.id=r.batch_id and b.legal_entity_id=$3) and ($4::text is null or r.status=$4)";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_import_row r where {where}", scope.Connection, scope.Transaction);
        Add(count, tenant.Value, batchId, entity, Db(string.IsNullOrWhiteSpace(status) ? null : status.Trim()));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select r.batch_id,r.row_number,r.external_reference,r.expense_id,r.status,r.error_code,r.raw_payload::text from business_expenses.expense_import_row r where {where} order by r.row_number offset $5 limit $6", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, batchId, entity, Db(string.IsNullOrWhiteSpace(status) ? null : status.Trim()), (page - 1) * pageSize, pageSize);
        var items = new List<ExpenseImportRowDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), JsonDocument.Parse(reader.GetString(6)).RootElement.Clone()));
        await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct);
        return new(items, total, page, pageSize);
    }
    public async Task<ExpenseReportItemDto> AddReportItemAsync(TenantId tenant, Guid entity, Guid reportId, AddExpenseReportItemRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) throw new ExpenseRepositoryException("expenses.report_item_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("with report as (select id,currency_code,state from business_expenses.expense_report where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update), inserted as (insert into business_expenses.expense_report_item(tenant_id,id,legal_entity_id,report_id,expense_id,amount) select $1,$4,$2,report.id,$5, $6 from report join business_expenses.expense e on e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$5 and e.currency_code=report.currency_code and e.expense_state='Finalized' and report.state='Draft' on conflict(tenant_id,report_id,expense_id) do update set amount=excluded.amount returning id,report_id,expense_id,amount,created_at) select id,report_id,expense_id,amount,created_at from inserted", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, reportId, Guid.CreateVersion7(), request.ExpenseId, request.Amount);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_item_not_editable");
        var result = new ExpenseReportItemDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetFieldValue<DateTimeOffset>(4)); await reader.CloseAsync();
        await using var total = new NpgsqlCommand("update business_expenses.expense_report set total_amount=(select coalesce(sum(amount),0) from business_expenses.expense_report_item where tenant_id=$1 and legal_entity_id=$2 and report_id=$3),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(total, tenant.Value, entity, reportId); await total.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpenseReportDto> RemoveReportItemAsync(TenantId tenant, Guid entity, Guid reportId, Guid expenseId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var remove = new NpgsqlCommand("delete from business_expenses.expense_report_item where tenant_id=$1 and legal_entity_id=$2 and report_id=$3 and expense_id=$4 and exists(select 1 from business_expenses.expense_report where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Draft')", scope.Connection, scope.Transaction);
        Add(remove, tenant.Value, entity, reportId, expenseId);
        if (await remove.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.report_item_not_editable");
        await using var update = new NpgsqlCommand("update business_expenses.expense_report set total_amount=(select coalesce(sum(amount),0) from business_expenses.expense_report_item where tenant_id=$1 and legal_entity_id=$2 and report_id=$3),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 returning id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at", scope.Connection, scope.Transaction);
        Add(update, tenant.Value, entity, reportId);
        await using var reader = await update.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_not_found");
        var result = new ExpenseReportDto(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8));
        await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }
    public async Task<ExpensePage<ExpenseReportItemDto>> ListReportItemsAsync(TenantId tenant, Guid entity, Guid reportId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and report_id=$3";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_report_item where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, reportId); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select id,report_id,expense_id,amount,created_at from business_expenses.expense_report_item where {where} order by created_at,id offset $4 limit $5", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, reportId, (page - 1) * pageSize, pageSize); var list = new List<ExpenseReportItemDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetFieldValue<DateTimeOffset>(4))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize);
    }
    public async Task<ExpenseReportDto> TransitionReportAsync(TenantId tenant, Guid entity, Guid reportId, ExpenseActionRequest request, string action, CancellationToken ct)
    {
        var next = action switch { "submit" => "Submitted", "approve" => "Approved", "reject" => "Rejected", "finalize" => "Finalized", _ => throw new ExpenseRepositoryException("expenses.report_action_invalid") };
        await using var scope = await OpenAsync(tenant, ct);
        await using var lockCommand = new NpgsqlCommand("select state,version from business_expenses.expense_report where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(lockCommand, tenant.Value, entity, reportId);
        await using var lockReader = await lockCommand.ExecuteReaderAsync(ct); if (!await lockReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_not_found"); var state = lockReader.GetString(0); var version = lockReader.GetInt64(1); await lockReader.CloseAsync();
        if (version != request.Version) throw new ExpenseRepositoryException("expenses.report_concurrent_change");
        var allowed = (action, state) switch { ("submit", "Draft") or ("approve", "Submitted") or ("reject", "Submitted") or ("finalize", "Approved") => true, _ => false };
        if (!allowed) throw new ExpenseRepositoryException("expenses.report_transition_invalid");
        if (action == "submit")
        {
            await using var itemCheck = new NpgsqlCommand("select count(*) from business_expenses.expense_report_item where tenant_id=$1 and legal_entity_id=$2 and report_id=$3", scope.Connection, scope.Transaction); Add(itemCheck, tenant.Value, entity, reportId);
            if (Convert.ToInt32(await itemCheck.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == 0) throw new ExpenseRepositoryException("expenses.report_empty");
        }
        await using var update = new NpgsqlCommand("update business_expenses.expense_report set state=$4,version=version+1,submitted_at=case when $4='Submitted' then now() else submitted_at end,finalized_at=case when $4='Finalized' then now() else finalized_at end where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 returning id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at", scope.Connection, scope.Transaction); Add(update, tenant.Value, entity, reportId, next, request.Version);
        await using var reader = await update.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_concurrent_change"); var result = new ExpenseReportDto(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8)); await reader.CloseAsync();
        await using var history = new NpgsqlCommand("insert into business_expenses.expense_report_history(tenant_id,id,legal_entity_id,report_id,action,to_state) values($1,$2,$3,$4,$5,jsonb_build_object('state',$5,'version',$6::bigint,'reason',$7::text))", scope.Connection, scope.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, reportId, action, result.Version, Db(request.Reason)); await history.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct); return result;
    }
    public async Task<IReadOnlyList<ExpenseReportHistoryDto>> ListReportHistoryAsync(TenantId tenant, Guid entity, Guid reportId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,report_id,action,to_state::text,occurred_at from business_expenses.expense_report_history where tenant_id=$1 and legal_entity_id=$2 and report_id=$3 order by occurred_at,id", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, reportId); var list = new List<ExpenseReportHistoryDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), System.Text.Json.JsonDocument.Parse(reader.GetString(3)).RootElement.Clone(), reader.GetFieldValue<DateTimeOffset>(4))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list;
    }
    public async Task<BusinessAdvanceDto> CreateAdvanceAsync(TenantId tenant, CreateBusinessAdvanceRequest request, CancellationToken ct)
    { if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Purpose)) throw new ExpenseRepositoryException("expenses.advance_invalid"); if (hr is not null && !await hr.EmployeeBelongsToLegalEntityAsync(tenant, request.EmployeeId, request.LegalEntityId, ct)) throw new ExpenseRepositoryException("expenses.advance_employee_scope_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.business_advance(tenant_id,id,legal_entity_id,employee_id,purpose,amount,currency_code,settlement_due_date) values($1,$2,$3,$4,$5,$6,$7,$8) returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, request.EmployeeId, request.Purpose.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), Db(request.SettlementDueDate)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_invalid"); var result = ReadAdvance(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<BusinessAdvanceDto>> ListAdvancesAsync(TenantId tenant, Guid entity, string? state, Guid? employeeId, bool overdue, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::uuid is null or employee_id=$4) and ($5::boolean = false or (settlement_due_date < current_date and state in ('Issued','PartiallyIssued','PartiallySettled') and issued_amount > applied_amount + returned_amount + written_off_amount))"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.business_advance where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(employeeId), overdue); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount from business_expenses.business_advance where {where} order by settlement_due_date nulls last,created_at desc offset $6 limit $7", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(employeeId), overdue, (page - 1) * pageSize, pageSize); var list = new List<BusinessAdvanceDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(ReadAdvance(r)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }
    public async Task<BusinessAdvanceDto> TransitionAdvanceAsync(TenantId tenant, Guid entity, Guid id, long version, string action, CancellationToken ct)
    { var next = action switch { "approve" => "Approved", "settle" => "Settled", _ => throw new ExpenseRepositoryException("expenses.advance_action_invalid") }; await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.business_advance set state=$4,version=version+1,settled_at=case when $4='Settled' then now() else settled_at end where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 and state <> 'Settled' and ($4 <> 'Settled' or (state='PartiallySettled' and applied_amount+returned_amount+written_off_amount >= issued_amount)) returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id, next, version); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_transition_invalid"); var result = ReadAdvance(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<BusinessAdvanceDto> IssueAdvanceAsync(TenantId tenant, Guid entity, Guid advanceId, Guid actorId, Guid membershipId, IssueBusinessAdvanceRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m) throw new ExpenseRepositoryException("expenses.advance_amount_invalid");
        var idempotencyKey = $"expenses-advance:{advanceId:D}:{request.Amount.ToString(CultureInfo.InvariantCulture)}";
        string currency;
        var alreadyCompleted = false;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var load = new NpgsqlCommand("select amount,issued_amount,currency_code,state,version from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
            Add(load, tenant.Value, entity, advanceId);
            await using var reader = await load.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found");
            var amount = reader.GetDecimal(0);
            var issued = reader.GetDecimal(1);
            currency = reader.GetString(2);
            var state = reader.GetString(3);
            var version = reader.GetInt64(4);
            await reader.CloseAsync();
            await using var reservation = new NpgsqlCommand("insert into business_expenses.business_advance_issuance(tenant_id,id,legal_entity_id,advance_id,idempotency_key,amount) values($1,$2,$3,$4,$5,$6) on conflict(tenant_id,advance_id,idempotency_key) do nothing", scope.Connection, scope.Transaction);
            Add(reservation, tenant.Value, Guid.CreateVersion7(), entity, advanceId, idempotencyKey, request.Amount);
            await reservation.ExecuteNonQueryAsync(ct);
            await using var existing = new NpgsqlCommand("select amount,state from business_expenses.business_advance_issuance where tenant_id=$1 and advance_id=$2 and idempotency_key=$3 for update", scope.Connection, scope.Transaction);
            Add(existing, tenant.Value, advanceId, idempotencyKey);
            await using var existingReader = await existing.ExecuteReaderAsync(ct);
            if (!await existingReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_issuance_unavailable");
            var reservedAmount = existingReader.GetDecimal(0);
            var reservedState = existingReader.GetString(1);
            await existingReader.CloseAsync();
            if (reservedAmount != request.Amount) throw new ExpenseRepositoryException("expenses.advance_idempotency_conflict");
            if (reservedState == "Completed") { alreadyCompleted = true; await scope.Transaction.CommitAsync(ct); }
            if (!alreadyCompleted)
            {
                var outstanding = amount - issued;
                if (state is not ("Approved" or "IssuancePending" or "PartiallyIssued") || request.Version != version || request.Amount > outstanding)
                    throw new ExpenseRepositoryException("expenses.advance_not_issuable");
                if (state != "IssuancePending")
                {
                    await using var pending = new NpgsqlCommand("update business_expenses.business_advance set state='IssuancePending',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4", scope.Connection, scope.Transaction);
                    Add(pending, tenant.Value, entity, advanceId, request.Version);
                    if (await pending.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.advance_concurrent");
                }
                await scope.Transaction.CommitAsync(ct);
            }
        }
        if (alreadyCompleted) return (await GetAdvanceAsync(tenant, entity, advanceId, ct))!;
        BusinessAdvancePaymentResult payment;
        try { payment = await (advancePayments ?? throw new ExpenseRepositoryException("expenses.payments_capability_unavailable")).IssueBusinessAdvanceAsync(tenant, actorId, membershipId, Guid.CreateVersion7(), entity, advanceId, request.PaymentAccountId, request.Method, request.Amount, currency, request.PaymentDate, request.PostingDate, idempotencyKey, ct); }
        catch { await ResetAdvanceIssuanceAsync(tenant, entity, advanceId, ct); throw; }
        if (!string.Equals(payment.State, "Completed", StringComparison.OrdinalIgnoreCase) || payment.Amount <= 0m || payment.Amount > request.Amount || !string.Equals(payment.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
        {
            await ResetAdvanceIssuanceAsync(tenant, entity, advanceId, ct);
            throw new ExpenseRepositoryException("expenses.payment_result_mismatch");
        }
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockIssuance = new NpgsqlCommand("select state from business_expenses.business_advance_issuance where tenant_id=$1 and advance_id=$2 and idempotency_key=$3 for update", scope.Connection, scope.Transaction);
            Add(lockIssuance, tenant.Value, advanceId, idempotencyKey);
            var issuanceState = (string?)await lockIssuance.ExecuteScalarAsync(ct) ?? throw new ExpenseRepositoryException("expenses.advance_issuance_unavailable");
            if (issuanceState == "Completed")
            {
                await using var completedAdvance = new NpgsqlCommand("select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
                Add(completedAdvance, tenant.Value, entity, advanceId); await using var completedReader = await completedAdvance.ExecuteReaderAsync(ct); if (!await completedReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found"); var completedResult = ReadAdvance(completedReader); await completedReader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return completedResult;
            }
            await using var markIssuance = new NpgsqlCommand("update business_expenses.business_advance_issuance set state='Completed',payment_id=$4,completed_at=now() where tenant_id=$1 and advance_id=$2 and idempotency_key=$3 and state <> 'Completed'", scope.Connection, scope.Transaction);
            Add(markIssuance, tenant.Value, advanceId, idempotencyKey, payment.PaymentId); if (await markIssuance.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.advance_issuance_conflict");
            await using var complete = new NpgsqlCommand("update business_expenses.business_advance set issued_amount=issued_amount+$4,state=case when issued_amount+$4 >= amount then 'Issued' else 'PartiallyIssued' end,issued_at=coalesce(issued_at,now()),issuance_payment_id=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='IssuancePending' and issued_amount+$4 <= amount returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount", scope.Connection, scope.Transaction);
            Add(complete, tenant.Value, entity, advanceId, payment.Amount, payment.PaymentId);
            await using var row = await complete.ExecuteReaderAsync(ct);
            if (!await row.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_completion_conflict");
            var result = ReadAdvance(row); await row.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
        }
    }
    private async Task ResetAdvanceIssuanceAsync(TenantId tenant, Guid entity, Guid advanceId, CancellationToken ct) { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("update business_expenses.business_advance set state='Approved',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='IssuancePending'", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, advanceId); await command.ExecuteNonQueryAsync(ct); await using var failed = new NpgsqlCommand("update business_expenses.business_advance_issuance set state='Failed' where tenant_id=$1 and advance_id=$2 and state='Pending'", scope.Connection, scope.Transaction); Add(failed, tenant.Value, advanceId); await failed.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); }
    public async Task<BusinessAdvanceDto> ApplyAdvanceAsync(TenantId tenant, Guid entity, Guid advanceId, ApplyBusinessAdvanceRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0 || request.ExpenseId == Guid.Empty) throw new ExpenseRepositoryException("expenses.advance_application_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var advance = new NpgsqlCommand("select employee_id,amount,issued_amount,applied_amount,returned_amount,currency_code,state,written_off_amount from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(advance, tenant.Value, entity, advanceId); await using var ar = await advance.ExecuteReaderAsync(ct);
        if (!await ar.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found");
        var employee = ar.GetGuid(0); var issued = ar.GetDecimal(2); var applied = ar.GetDecimal(3); var returned = ar.GetDecimal(4); var currency = ar.GetString(5); var state = ar.GetString(6); var writtenOff = ar.GetDecimal(7); await ar.CloseAsync();
        await using var expense = new NpgsqlCommand("select employee_id,currency_code,expense_date,expense_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(expense, tenant.Value, entity, request.ExpenseId); await using var er = await expense.ExecuteReaderAsync(ct);
        if (!await er.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
        var expenseEmployee = er.IsDBNull(0) ? (Guid?)null : er.GetGuid(0); var expenseCurrency = er.GetString(1); var expenseDate = er.GetFieldValue<DateOnly>(2); var expenseState = er.GetString(3); await er.CloseAsync();
        if (expenseEmployee != employee || expenseState is not ("Submitted" or "Approved" or "Finalized")) throw new ExpenseRepositoryException("expenses.advance_expense_invalid");
        var normalizedExpenseCurrency = expenseCurrency.Trim().ToUpperInvariant(); var normalizedAdvanceCurrency = currency.Trim().ToUpperInvariant();
        decimal advanceEquivalent = request.Amount; string? fxSnapshot = null;
        if (!string.Equals(normalizedAdvanceCurrency, normalizedExpenseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (financeBase is null) throw new ExpenseRepositoryException("expenses.advance_fx_capability_unavailable");
            var advanceContext = await financeBase.ResolveCalculationContextAsync(tenant, new LegalEntityId(entity), normalizedAdvanceCurrency, FinancialCalculationPurpose.Accounting, expenseDate, ct) ?? throw new ExpenseRepositoryException("expenses.advance_fx_context_unavailable");
            var expenseContext = await financeBase.ResolveCalculationContextAsync(tenant, new LegalEntityId(entity), normalizedExpenseCurrency, FinancialCalculationPurpose.Accounting, expenseDate, ct) ?? throw new ExpenseRepositoryException("expenses.advance_fx_context_unavailable");
            var advanceRate = ParseRate(advanceContext, normalizedAdvanceCurrency); var expenseRate = ParseRate(expenseContext, normalizedExpenseCurrency);
            var derivedRate = expenseRate / advanceRate; var derivedEquivalent = RoundAmount(request.Amount * derivedRate, advanceContext);
            if (request.ExchangeRate is { } suppliedRate && Math.Abs(suppliedRate - derivedRate) > 0.00000001m) throw new ExpenseRepositoryException("expenses.advance_fx_rate_conflict");
            if (request.AdvanceEquivalentAmount is { } suppliedEquivalent && suppliedEquivalent != derivedEquivalent) throw new ExpenseRepositoryException("expenses.advance_fx_amount_conflict");
            advanceEquivalent = request.AdvanceEquivalentAmount ?? derivedEquivalent;
            fxSnapshot = JsonSerializer.Serialize(new { advanceContext, expenseContext, rate = derivedRate, advanceEquivalentAmount = advanceEquivalent });
        }
        else if (request.ExpenseCurrencyCode is not null && !string.Equals(request.ExpenseCurrencyCode.Trim(), normalizedExpenseCurrency, StringComparison.OrdinalIgnoreCase))
            throw new ExpenseRepositoryException("expenses.advance_currency_invalid");
        await using var existingApplication = new NpgsqlCommand("select amount,expense_currency_code,advance_equivalent_amount from business_expenses.business_advance_application where tenant_id=$1 and advance_id=$2 and expense_id=$3 for update", scope.Connection, scope.Transaction);
        Add(existingApplication, tenant.Value, advanceId, request.ExpenseId);
        await using var existingReader = await existingApplication.ExecuteReaderAsync(ct);
        if (await existingReader.ReadAsync(ct))
        {
            var existingAmount = existingReader.GetDecimal(0);
            var existingCurrency = existingReader.GetString(1).Trim();
            var existingEquivalent = existingReader.GetDecimal(2);
            await existingReader.CloseAsync();
            if (existingAmount != request.Amount || !string.Equals(existingCurrency, normalizedExpenseCurrency, StringComparison.OrdinalIgnoreCase) || existingEquivalent != advanceEquivalent)
                throw new ExpenseRepositoryException("expenses.advance_application_idempotency_conflict");
            await scope.Transaction.CommitAsync(ct);
            return (await GetAdvanceAsync(tenant, entity, advanceId, ct))!;
        }
        await existingReader.CloseAsync();
        if (state is not ("Issued" or "PartiallyIssued" or "PartiallySettled") || applied + returned + writtenOff + advanceEquivalent > issued) throw new ExpenseRepositoryException("expenses.advance_amount_exceeded");
        await using var insert = new NpgsqlCommand("insert into business_expenses.business_advance_application(tenant_id,id,legal_entity_id,advance_id,expense_id,amount,expense_currency_code,advance_equivalent_amount,fx_snapshot) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb) on conflict(tenant_id,advance_id,expense_id) do update set amount=excluded.amount,expense_currency_code=excluded.expense_currency_code,advance_equivalent_amount=excluded.advance_equivalent_amount,fx_snapshot=excluded.fx_snapshot", scope.Connection, scope.Transaction);
        Add(insert, tenant.Value, Guid.CreateVersion7(), entity, advanceId, request.ExpenseId, request.Amount, normalizedExpenseCurrency, advanceEquivalent, (object?)fxSnapshot ?? DBNull.Value); await insert.ExecuteNonQueryAsync(ct);
        await using var update = new NpgsqlCommand("update business_expenses.business_advance set applied_amount=(select coalesce(sum(advance_equivalent_amount),0) from business_expenses.business_advance_application where tenant_id=$1 and advance_id=$2),state=case when (select coalesce(sum(advance_equivalent_amount),0) from business_expenses.business_advance_application where tenant_id=$1 and advance_id=$2)+returned_amount+written_off_amount >= issued_amount then 'Settled' else 'PartiallySettled' end,version=version+1 where tenant_id=$1 and legal_entity_id=$3 and id=$2 returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount", scope.Connection, scope.Transaction);
        Add(update, tenant.Value, advanceId, entity); await using var ur = await update.ExecuteReaderAsync(ct); if (!await ur.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found"); var result = ReadAdvance(ur); await ur.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    private static decimal ParseRate(FinancialCalculationContextSnapshotContract context, string currency)
    {
        if (string.Equals(context.BaseCurrencyCode, currency, StringComparison.OrdinalIgnoreCase)) return 1m;
        if (context.ExchangeRate is null || !decimal.TryParse(context.ExchangeRate.Rate, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate <= 0m) throw new ExpenseRepositoryException("expenses.advance_fx_rate_missing");
        return rate;
    }

    private static decimal RoundAmount(decimal amount, FinancialCalculationContextSnapshotContract context)
    { return Math.Round(amount, Math.Clamp(context.RoundingPolicy.MoneyScale, 0, 8), MidpointRounding.AwayFromZero); }
    public async Task<BusinessAdvanceWriteOffDto> RequestAdvanceWriteOffAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid advanceId, RequestBusinessAdvanceWriteOffRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
        if (request.Amount <= 0m || string.IsNullOrWhiteSpace(request.Reason) || request.Version <= 0) throw new ExpenseRepositoryException("expenses.advance_writeoff_invalid");
        var effectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"advance-writeoff:{advanceId:D}:{request.Amount.ToString(CultureInfo.InvariantCulture)}:{effectiveDate:yyyyMMdd}" : request.IdempotencyKey.Trim();
        BusinessAdvanceDto advance;
        await using (var readScope = await OpenAsync(tenant, ct))
        {
            await using var command = new NpgsqlCommand("select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", readScope.Connection, readScope.Transaction);
            Add(command, tenant.Value, entity, advanceId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found");
            advance = ReadAdvance(reader); await reader.CloseAsync();
            if (advance.Version != request.Version || advance.State is not ("Issued" or "PartiallyIssued" or "PartiallySettled") || request.Amount > advance.IssuedAmount - advance.AppliedAmount - advance.ReturnedAmount - advance.WrittenOffAmount)
                throw new ExpenseRepositoryException("expenses.advance_writeoff_not_eligible");
            await readScope.Transaction.CommitAsync(ct);
        }
        if (financeBase is null) throw new ExpenseRepositoryException("expenses.advance_fx_capability_unavailable");
        var context = await financeBase.ResolveCalculationContextAsync(tenant, new LegalEntityId(entity), advance.CurrencyCode.Trim(), FinancialCalculationPurpose.Accounting, effectiveDate, ct) ?? throw new ExpenseRepositoryException("expenses.advance_fx_context_unavailable");
        var baseCurrency = context.BaseCurrencyCode.Trim().ToUpperInvariant();
        var baseAmount = string.Equals(advance.CurrencyCode.Trim(), baseCurrency, StringComparison.OrdinalIgnoreCase)
            ? request.Amount
            : CalculateBaseAmount(request.Amount, context);
        var fxSnapshot = JsonSerializer.Serialize(context);
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var insert = new NpgsqlCommand("insert into business_expenses.business_advance_write_off(tenant_id,id,legal_entity_id,advance_id,amount,currency_code,base_amount,base_currency_code,fx_snapshot,reason,effective_date,idempotency_key,request_version,requested_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10,$11,$12,$13,$14) on conflict(tenant_id,legal_entity_id,idempotency_key) do nothing", scope.Connection, scope.Transaction);
            Add(insert, tenant.Value, Guid.CreateVersion7(), entity, advanceId, request.Amount, advance.CurrencyCode.Trim().ToUpperInvariant(), baseAmount, baseCurrency, fxSnapshot, request.Reason.Trim(), effectiveDate, idempotencyKey, request.Version, actorId);
            await insert.ExecuteNonQueryAsync(ct);
            await using var load = new NpgsqlCommand("select id,advance_id,legal_entity_id,amount,currency_code,base_amount,base_currency_code,reason,effective_date,state,approval_state,approval_request_id,requested_by,approved_by,ledger_fact_id,ledger_entry_id,version,created_at,approved_at,fx_snapshot::text from business_expenses.business_advance_write_off where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", scope.Connection, scope.Transaction);
            Add(load, tenant.Value, entity, idempotencyKey); await using var reader = await load.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_writeoff_unavailable");
            var result = ReadWriteOff(reader); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct);
            if (result.ApprovalRequestId.HasValue) return result;
            var approval = await approvals.RequestAsync(tenant, actorId, membershipId, entity, "expenses", "business_advance_write_off", result.Id, request.Version, null, request.Reason, "expenses.reimbursement.manage", ct);
            await using var linkScope = await OpenAsync(tenant, ct);
            await using var link = new NpgsqlCommand("update business_expenses.business_advance_write_off set approval_request_id=$4,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and approval_request_id is null and state='PendingApproval'", linkScope.Connection, linkScope.Transaction);
            Add(link, tenant.Value, entity, result.Id, approval.Id); if (await link.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.advance_writeoff_link_conflict");
            await using var history = new NpgsqlCommand("insert into business_expenses.business_advance_write_off_history(tenant_id,id,legal_entity_id,write_off_id,action,to_state) values($1,$2,$3,$4,'Requested',jsonb_build_object('approvalRequestId',$5::text,'amount',$6::numeric,'reason',$7::text))", linkScope.Connection, linkScope.Transaction);
            Add(history, tenant.Value, Guid.CreateVersion7(), entity, result.Id, approval.Id, request.Amount, request.Reason.Trim()); await history.ExecuteNonQueryAsync(ct); await linkScope.Transaction.CommitAsync(ct);
            return (await GetWriteOffAsync(tenant, entity, result.Id, ct))!;
        }
    }

    public async Task<BusinessAdvanceWriteOffDto> DecideAdvanceWriteOffAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid advanceId, DecideBusinessAdvanceWriteOffRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new ExpenseRepositoryException("expenses.approvals_capability_unavailable");
        if (request.ApprovalRequestId == Guid.Empty || request.Decision is not ("Approved" or "Rejected")) throw new ExpenseRepositoryException("expenses.advance_writeoff_decision_invalid");
        BusinessAdvanceWriteOffDto pending;
        await using (var preflight = await OpenAsync(tenant, ct))
        {
            await using var command = new NpgsqlCommand("select id,advance_id,legal_entity_id,amount,currency_code,base_amount,base_currency_code,reason,effective_date,state,approval_state,approval_request_id,requested_by,approved_by,ledger_fact_id,ledger_entry_id,version,created_at,approved_at,fx_snapshot::text from business_expenses.business_advance_write_off where tenant_id=$1 and legal_entity_id=$2 and advance_id=$3 and approval_request_id=$4", preflight.Connection, preflight.Transaction);
            Add(command, tenant.Value, entity, advanceId, request.ApprovalRequestId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_writeoff_not_found"); pending = ReadWriteOff(reader); await reader.CloseAsync(); await preflight.Transaction.CommitAsync(ct);
            if (pending.State == "Approved" && pending.ApprovalState == "Approved") return pending;
            if (pending.State == "Rejected" && pending.ApprovalState == "Rejected") return pending;
            if (pending.State != "PendingApproval" || pending.ApprovalState != "Pending") throw new ExpenseRepositoryException("expenses.advance_writeoff_stale");
        }
        PlatformApprovalRequestResult approval;
        try { approval = await approvals.DecideAsync(tenant, actorId, membershipId, request.ApprovalRequestId, request.Decision, request.Reason, ct); }
        catch (InvalidOperationException ex) when (ex.Message == "platform.approval_stale_or_unauthorized")
        { approval = await approvals.GetAsync(tenant, request.ApprovalRequestId, ct) ?? throw new ExpenseRepositoryException("expenses.advance_writeoff_approval_not_found"); }
        if (approval.OwnerModule != "expenses" || approval.OwnerEntityType != "business_advance_write_off" || approval.OwnerEntityId != pending.Id || approval.LegalEntityId != entity) throw new ExpenseRepositoryException("expenses.advance_writeoff_scope_invalid");
        await transactions.ExecuteAsync<bool>(tenant, async (context, token) =>
        {
            await using var lockWriteOff = new NpgsqlCommand("select amount,currency_code,base_amount,base_currency_code,reason,effective_date,state,approval_state,version from business_expenses.business_advance_write_off where tenant_id=$1 and legal_entity_id=$2 and id=$3 and approval_request_id=$4 for update", context.Connection, context.Transaction);
            Add(lockWriteOff, tenant.Value, entity, pending.Id, request.ApprovalRequestId); await using var wr = await lockWriteOff.ExecuteReaderAsync(token);
            if (!await wr.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.advance_writeoff_not_found");
            var amount = wr.GetDecimal(0); var currency = wr.GetString(1).Trim(); var baseAmount = wr.GetDecimal(2); var baseCurrency = wr.GetString(3).Trim(); var reason = wr.GetString(4); var effectiveDate = wr.GetFieldValue<DateOnly>(5); var state = wr.GetString(6); var approvalState = wr.GetString(7); var version = wr.GetInt64(8); await wr.CloseAsync();
            if (state == "Approved" && approvalState == "Approved") return true;
            if (approval.State != request.Decision) throw new ExpenseRepositoryException("expenses.advance_writeoff_approval_state_mismatch");
            if (approval.State == "Rejected")
            {
                await using var reject = new NpgsqlCommand("update business_expenses.business_advance_write_off set state='Rejected',approval_state='Rejected',approved_by=$4,approved_at=now(),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 and state='PendingApproval'", context.Connection, context.Transaction); Add(reject, tenant.Value, entity, pending.Id, actorId, version); if (await reject.ExecuteNonQueryAsync(token) != 1) throw new ExpenseRepositoryException("expenses.advance_writeoff_stale");
                await using var history = new NpgsqlCommand("insert into business_expenses.business_advance_write_off_history(tenant_id,id,legal_entity_id,write_off_id,action,to_state) values($1,$2,$3,$4,'Rejected',jsonb_build_object('reason',$5::text,'approvedBy',$6::text))", context.Connection, context.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, pending.Id, request.Reason, actorId); await history.ExecuteNonQueryAsync(token); return true;
            }
            await using var advance = new NpgsqlCommand("select employee_id,issued_amount,applied_amount,returned_amount,written_off_amount,state,version from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction); Add(advance, tenant.Value, entity, advanceId); await using var ar = await advance.ExecuteReaderAsync(token); if (!await ar.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.advance_not_found"); var employee = ar.GetGuid(0); var issued = ar.GetDecimal(1); var applied = ar.GetDecimal(2); var returned = ar.GetDecimal(3); var writtenOff = ar.GetDecimal(4); var advanceState = ar.GetString(5); var advanceVersion = ar.GetInt64(6); await ar.CloseAsync();
            if (approval.ResourceVersion != advanceVersion || advanceState is not ("Issued" or "PartiallyIssued" or "PartiallySettled") || amount > issued - applied - returned - writtenOff) throw new ExpenseRepositoryException("expenses.advance_writeoff_stale");
            var factId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"expenses:advance-writeoff:{pending.Id:D}"))[..16]);
            var payload = new SettlementWriteOffPayload(pending.Id, employee, advanceId, "Receivable", amount.ToString(CultureInfo.InvariantCulture), currency, baseAmount, baseCurrency, "BUSINESS_ADVANCE_WRITE_OFF", reason);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new LegalEntityId(entity), AccountingFactTypes.SettlementWriteOff, 1, "expenses", $"advance-writeoff:{pending.Id:D}", $"expenses-advance-writeoff:{pending.Id:D}", effectiveDate, effectiveDate, payload, Guid.CreateVersion7(), actorId);
            LedgerPostingResult posting;
            try { posting = await ledgerPosting.PostSettlementWriteOffAsync(context, fact, payload, token); }
            catch (Exception) { throw new ExpenseRepositoryException("expenses.advance_writeoff_ledger_failed"); }
            await using var updateAdvance = new NpgsqlCommand("update business_expenses.business_advance set written_off_amount=written_off_amount+$4,state=case when applied_amount+returned_amount+written_off_amount+$4 >= issued_amount then 'Settled' else 'PartiallySettled' end,settled_at=case when applied_amount+returned_amount+written_off_amount+$4 >= issued_amount then now() else settled_at end,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5", context.Connection, context.Transaction); Add(updateAdvance, tenant.Value, entity, advanceId, amount, advanceVersion); if (await updateAdvance.ExecuteNonQueryAsync(token) != 1) throw new ExpenseRepositoryException("expenses.advance_writeoff_concurrent");
            await using var updateWriteOff = new NpgsqlCommand("update business_expenses.business_advance_write_off set state='Approved',approval_state='Approved',approved_by=$4,ledger_fact_id=$5,ledger_entry_id=$6,approved_at=now(),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$7 and state='PendingApproval'", context.Connection, context.Transaction); Add(updateWriteOff, tenant.Value, entity, pending.Id, actorId, posting.FactId, posting.JournalEntryId, version); if (await updateWriteOff.ExecuteNonQueryAsync(token) != 1) throw new ExpenseRepositoryException("expenses.advance_writeoff_concurrent");
            await using var approvedHistory = new NpgsqlCommand("insert into business_expenses.business_advance_write_off_history(tenant_id,id,legal_entity_id,write_off_id,action,to_state) values($1,$2,$3,$4,'Approved',jsonb_build_object('ledgerFactId',$5::text,'ledgerEntryId',$6::text,'approvedBy',$7::text))", context.Connection, context.Transaction); Add(approvedHistory, tenant.Value, Guid.CreateVersion7(), entity, pending.Id, posting.FactId, posting.JournalEntryId, actorId); await approvedHistory.ExecuteNonQueryAsync(token); return true;
        }, ct);
        return (await GetWriteOffAsync(tenant, entity, pending.Id, ct))!;
    }

    private async Task<BusinessAdvanceWriteOffDto?> GetWriteOffAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,advance_id,legal_entity_id,amount,currency_code,base_amount,base_currency_code,reason,effective_date,state,approval_state,approval_request_id,requested_by,approved_by,ledger_fact_id,ledger_entry_id,version,created_at,approved_at,fx_snapshot::text from business_expenses.business_advance_write_off where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? ReadWriteOff(reader) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    private static BusinessAdvanceWriteOffDto ReadWriteOff(NpgsqlDataReader reader)
    { JsonElement? snapshot = reader.IsDBNull(19) ? null : JsonDocument.Parse(reader.GetString(19)).RootElement.Clone(); return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetDecimal(5), reader.GetString(6).Trim(), reader.GetString(7), reader.GetFieldValue<DateOnly>(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetGuid(11), reader.IsDBNull(12) ? null : reader.GetGuid(12), reader.IsDBNull(13) ? null : reader.GetGuid(13), reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.IsDBNull(15) ? null : reader.GetGuid(15), reader.GetInt64(16), reader.GetFieldValue<DateTimeOffset>(17), reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18), snapshot); }

    public async Task<BusinessAdvanceDto> ReturnAdvanceAsync(TenantId tenant, Guid entity, Guid advanceId, ReturnBusinessAdvanceRequest request, CancellationToken ct)
    { if (request.Amount <= 0) throw new ExpenseRepositoryException("expenses.advance_return_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var update = new NpgsqlCommand("update business_expenses.business_advance set returned_amount=returned_amount+$4,state=case when applied_amount+returned_amount+$4+written_off_amount >= issued_amount then 'Settled' else 'PartiallySettled' end,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 and state in ('Issued','PartiallyIssued','PartiallySettled') and applied_amount+returned_amount+$4+written_off_amount <= issued_amount returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount", scope.Connection, scope.Transaction); Add(update, tenant.Value, entity, advanceId, request.Amount, request.Version); await using var r = await update.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_return_invalid"); var result = ReadAdvance(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseCardTransactionDto> ImportCardTransactionAsync(TenantId tenant, Guid entity, ImportExpenseCardTransactionRequest request, CancellationToken ct)
    {
        var state = string.IsNullOrWhiteSpace(request.InitialState) ? "Unmatched" : request.InitialState.Trim();
        var merchant = NormalizeMerchantName(request.MerchantName);
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var providerReference = request.ProviderReference.Trim();
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(providerReference) || string.IsNullOrWhiteSpace(merchant) || state is not ("Unmatched" or "Pending" or "Cleared" or "Credited" or "Reversed")) throw new ExpenseRepositoryException("expenses.card_transaction_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var existing = new NpgsqlCommand("select id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where tenant_id=$1 and provider_reference=$2 for update", scope.Connection, scope.Transaction);
        Add(existing, tenant.Value, providerReference);
        await using var reader = await existing.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var current = ReadCard(reader);
            await reader.CloseAsync();
            if (current.LegalEntityId != entity) throw new ExpenseRepositoryException("expenses.card_transaction_scope_conflict");
            var terminal = current.State is ("Cleared" or "Matched" or "Allocated" or "Personal" or "Refunded" or "Credited" or "Reversed" or "Disputed" or "Ignored");
            // A provider retry may correct descriptive metadata after clearing, but it may not
            // mutate the financial identity of a posted transaction. Amount/currency changes
            // require an explicit credit/refund/dispute adjustment with its own provider key.
            if (terminal && (current.Amount != request.Amount || !string.Equals(current.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)))
                throw new ExpenseRepositoryException("expenses.card_provider_conflict");
            if (!terminal)
            {
                await using var update = new NpgsqlCommand("update business_expenses.expense_card_transaction set merchant_name=$3,transaction_date=$4,amount=$5,currency_code=$6,state=$7 where tenant_id=$1 and id=$2", scope.Connection, scope.Transaction);
                Add(update, tenant.Value, current.Id, merchant, request.TransactionDate, request.Amount, currency, state);
                await update.ExecuteNonQueryAsync(ct);
                current = (await GetCardTransactionWithinScopeAsync(scope, tenant, entity, current.Id, ct))!;
            }
            await scope.Transaction.CommitAsync(ct);
            return current;
        }
        await reader.CloseAsync();
        await using var insert = new NpgsqlCommand("insert into business_expenses.expense_card_transaction(tenant_id,id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state) values($1,$2,$3,$4,$5,$6,$7,$8,$9) returning id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id", scope.Connection, scope.Transaction);
        Add(insert, tenant.Value, Guid.CreateVersion7(), entity, providerReference, merchant, request.TransactionDate, request.Amount, currency, state);
        await using var resultReader = await insert.ExecuteReaderAsync(ct);
        if (!await resultReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_invalid");
        var result = ReadCard(resultReader);
        await resultReader.CloseAsync();
        await scope.Transaction.CommitAsync(ct);
        return result;
    }

    private static async Task<ExpenseCardTransactionDto?> GetCardTransactionWithinScopeAsync(Scope scope, TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = await reader.ReadAsync(ct) ? ReadCard(reader) : null;
        await reader.CloseAsync();
        return result;
    }

    public async Task<ExpenseDto> CreateExpenseFromCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, CreateExpenseFromCardTransactionRequest request, CancellationToken ct, Guid? expectedEmployeeId = null)
    {
        if (request.CategoryVersionId == Guid.Empty) throw new ExpenseRepositoryException("expenses.card_category_required");
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId);
        await using var reader = await card.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var merchant = reader.GetString(0); var date = reader.GetFieldValue<DateOnly>(1); var amount = reader.GetDecimal(2); var currency = reader.GetString(3); var state = reader.GetString(4); var employee = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5); var matched = reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6);
        await reader.CloseAsync();
        if (expectedEmployeeId.HasValue && employee != expectedEmployeeId) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        if (matched.HasValue) throw new ExpenseRepositoryException("expenses.card_transaction_already_matched");
        if (state is not ("Unmatched" or "Cleared")) throw new ExpenseRepositoryException(state == "Pending" ? "expenses.card_transaction_pending" : "expenses.card_transaction_not_createable");
        await using var category = new NpgsqlCommand("select 1 from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and c.status='Active' and v.id=$3 and v.status='Published'", scope.Connection, scope.Transaction);
        Add(category, tenant.Value, entity, request.CategoryVersionId);
        if (await category.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.card_category_invalid");
        var expenseId = Guid.CreateVersion7();
        await using var insert = new NpgsqlCommand("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,'CorporateCard',$9,'Draft','Pending','NotFinalized','NotSettled','Missing','NotEvaluated',$10,$11,1,now(),now())", scope.Connection, scope.Transaction);
        Add(insert, tenant.Value, expenseId, entity, Db(employee), merchant, date, amount, currency, request.CategoryVersionId, Db(request.BusinessPurpose), Db(request.Description));
        await insert.ExecuteNonQueryAsync(ct);
        await using var link = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Matched',matched_expense_id=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and matched_expense_id is null returning 1", scope.Connection, scope.Transaction);
        Add(link, tenant.Value, entity, cardId, expenseId);
        if (await link.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.card_transaction_concurrent");
        await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'card.created',jsonb_build_object('cardTransactionId',$5::text,'providerReference',$6::text))", scope.Connection, scope.Transaction);
        Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, cardId, merchant);
        await history.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
        return (await GetAsync(tenant, entity, expenseId, ct))!;
    }
    public async Task<ExpenseCardTransactionDto> ClearCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Cleared' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state in ('Pending','Unmatched') returning id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, cardId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_clearable"); var result = ReadCard(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseCardTransactionDto> IgnoreCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Ignored' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and matched_expense_id is null and state in ('Pending','Unmatched','Cleared') returning id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, cardId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_ignorable"); var result = ReadCard(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseCardAdjustmentDto> RecordCardAdjustmentAsync(TenantId tenant, Guid entity, Guid cardId, RecordExpenseCardAdjustmentRequest request, CancellationToken ct)
    {
        var kind = request.Kind.Trim(); var providerReference = request.ProviderReference.Trim(); var currency = request.CurrencyCode.Trim().ToUpperInvariant(); var reason = request.Reason.Trim();
        if (kind is not ("Credit" or "Refund" or "Dispute") || string.IsNullOrWhiteSpace(providerReference) || request.Amount <= 0 || currency.Length != 3 || reason.Length < 5) throw new ExpenseRepositoryException("expenses.card_adjustment_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var existing = new NpgsqlCommand("select id,card_transaction_id,kind,provider_reference,amount,currency_code,state,reason,created_at,applied_at from business_expenses.expense_card_adjustment where tenant_id=$1 and legal_entity_id=$2 and provider_reference=$3", scope.Connection, scope.Transaction); Add(existing, tenant.Value, entity, providerReference); await using var er = await existing.ExecuteReaderAsync(ct);
        if (await er.ReadAsync(ct))
        {
            var previous = ReadCardAdjustment(er); await er.CloseAsync(); if (previous.CardTransactionId != cardId || previous.Kind != kind || previous.Amount != request.Amount || !string.Equals(previous.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)) throw new ExpenseRepositoryException("expenses.card_adjustment_idempotency_conflict"); await scope.Transaction.CommitAsync(ct); return previous;
        }
        await er.CloseAsync();
        await using var card = new NpgsqlCommand("select amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(card, tenant.Value, entity, cardId); await using var cr = await card.ExecuteReaderAsync(ct); if (!await cr.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found"); var cardAmount = cr.GetDecimal(0); var cardCurrency = cr.GetString(1); var cardState = cr.GetString(2); await cr.CloseAsync(); if (!string.Equals(cardCurrency, currency, StringComparison.OrdinalIgnoreCase) || cardState is "Personal" or "Ignored") throw new ExpenseRepositoryException("expenses.card_adjustment_invalid");
        await using var total = new NpgsqlCommand("select coalesce(sum(amount),0) from business_expenses.expense_card_adjustment where tenant_id=$1 and card_transaction_id=$2 and kind in ('Credit','Refund') and state='Applied'", scope.Connection, scope.Transaction); Add(total, tenant.Value, cardId); var credited = Convert.ToDecimal(await total.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); if (kind is "Credit" or "Refund" && credited + request.Amount > cardAmount) throw new ExpenseRepositoryException("expenses.card_adjustment_exceeds_transaction");
        var id = Guid.CreateVersion7(); await using var insert = new NpgsqlCommand("insert into business_expenses.expense_card_adjustment(tenant_id,id,legal_entity_id,card_transaction_id,kind,provider_reference,amount,currency_code,state,reason,applied_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Applied',$9,now()) on conflict(tenant_id,provider_reference) do nothing returning id,card_transaction_id,kind,provider_reference,amount,currency_code,state,reason,created_at,applied_at", scope.Connection, scope.Transaction); Add(insert, tenant.Value, id, entity, cardId, kind, providerReference, request.Amount, currency, reason); await using var ir = await insert.ExecuteReaderAsync(ct);
        if (!await ir.ReadAsync(ct))
        {
            await ir.CloseAsync();
            await using var concurrent = new NpgsqlCommand("select id,card_transaction_id,kind,provider_reference,amount,currency_code,state,reason,created_at,applied_at from business_expenses.expense_card_adjustment where tenant_id=$1 and legal_entity_id=$2 and provider_reference=$3", scope.Connection, scope.Transaction);
            Add(concurrent, tenant.Value, entity, providerReference);
            await using var concurrentReader = await concurrent.ExecuteReaderAsync(ct);
            if (!await concurrentReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_adjustment_invalid");
            var previous = ReadCardAdjustment(concurrentReader);
            await concurrentReader.CloseAsync();
            if (previous.CardTransactionId != cardId || previous.Kind != kind || previous.Amount != request.Amount || !string.Equals(previous.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)) throw new ExpenseRepositoryException("expenses.card_adjustment_idempotency_conflict");
            await scope.Transaction.CommitAsync(ct);
            return previous;
        }
        var result = ReadCardAdjustment(ir); await ir.CloseAsync();
        if (kind == "Dispute") { await using var dispute = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Disputed' where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(dispute, tenant.Value, entity, cardId); await dispute.ExecuteNonQueryAsync(ct); }
        else if (credited + request.Amount >= cardAmount) { var correctedState = kind == "Credit" ? "Credited" : "Refunded"; await using var correction = new NpgsqlCommand("update business_expenses.expense_card_transaction set state=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(correction, tenant.Value, entity, cardId, correctedState); await correction.ExecuteNonQueryAsync(ct); }
        await scope.Transaction.CommitAsync(ct); return result;
    }
    public async Task<ExpensePage<ExpenseCardTransactionDto>> ListCardTransactionsAsync(TenantId tenant, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or provider_reference ilike '%'||$4||'%' or merchant_name ilike '%'||$4||'%')"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_card_transaction where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where {where} order by transaction_date desc,id desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpenseCardTransactionDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(ReadCard(r)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }

    public async Task<ExpenseCardReconciliationDto> GetCardReconciliationAsync(TenantId tenant, Guid entity, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from is not null && to is not null && from > to) throw new ExpenseRepositoryException("expenses.card_reconciliation_date_range_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::date is null or transaction_date >= $3) and ($4::date is null or transaction_date <= $4)";
        await using var command = new NpgsqlCommand($"select count(*),coalesce(sum(amount),0),coalesce(sum(amount) filter (where state='Matched'),0),coalesce(sum(amount) filter (where state='Allocated'),0),coalesce(sum(amount) filter (where state='Personal'),0),coalesce(sum(amount) filter (where state='Credited'),0),coalesce(sum(amount) filter (where state='Refunded'),0),coalesce(sum(amount) filter (where state='Disputed'),0),coalesce(sum(amount) filter (where state='Reversed'),0),coalesce(sum(amount) filter (where state in ('Unmatched','Pending')),0) from business_expenses.expense_card_transaction where {where}", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, Db(from), Db(to));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_reconciliation_unavailable");
        var result = new ExpenseCardReconciliationDto(reader.GetInt64(0) > int.MaxValue ? int.MaxValue : (int)reader.GetInt64(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9));
        await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpensePage<ExpenseCardAdjustmentDto>> ListCardAdjustmentsAsync(TenantId tenant, Guid entity, ExpenseCardAdjustmentFilter filter, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "a.tenant_id=$1 and a.legal_entity_id=$2 and ($3::uuid is null or a.card_transaction_id=$3) and ($4::text is null or a.kind=$4) and ($5::text is null or a.state=$5) and ($6::text is null or a.provider_reference ilike '%'||$6||'%' or a.reason ilike '%'||$6||'%')";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_card_adjustment a where {where}", scope.Connection, scope.Transaction);
        Add(count, tenant.Value, entity, Db(filter.CardTransactionId), Db(string.IsNullOrWhiteSpace(filter.Kind) ? null : filter.Kind.Trim()), Db(string.IsNullOrWhiteSpace(filter.State) ? null : filter.State.Trim()), Db(string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim()));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand($"select a.id,a.card_transaction_id,a.kind,a.provider_reference,a.amount,a.currency_code,a.state,a.reason,a.created_at,a.applied_at from business_expenses.expense_card_adjustment a where {where} order by a.created_at desc,a.id desc offset $7 limit $8", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, Db(filter.CardTransactionId), Db(string.IsNullOrWhiteSpace(filter.Kind) ? null : filter.Kind.Trim()), Db(string.IsNullOrWhiteSpace(filter.State) ? null : filter.State.Trim()), Db(string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim()), (page - 1) * pageSize, pageSize);
        var items = new List<ExpenseCardAdjustmentDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(ReadCardAdjustment(reader));
        await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task<ExpenseCardTransactionDto> MatchCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, MatchExpenseCardTransactionRequest request, CancellationToken ct)
    {
        if (request.ExpenseId == Guid.Empty) throw new ExpenseRepositoryException("expenses.card_match_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId);
        await using var cr = await card.ExecuteReaderAsync(ct);
        if (!await cr.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var amount = cr.GetDecimal(0); var currency = cr.GetString(1); var state = cr.GetString(2); await cr.CloseAsync();
        if (state is not ("Unmatched" or "Cleared" or "Allocated")) throw new ExpenseRepositoryException("expenses.card_transaction_not_matchable");
        await using var expense = new NpgsqlCommand("select amount,currency_code,expense_state,version from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(expense, tenant.Value, entity, request.ExpenseId);
        await using var er = await expense.ExecuteReaderAsync(ct);
        if (!await er.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
        var expenseAmount = er.GetDecimal(0); var expenseCurrency = er.GetString(1); var expenseState = er.GetString(2); var expenseVersion = er.GetInt64(3); await er.CloseAsync();
        if (expenseState is not ("Draft" or "Submitted")) throw new ExpenseRepositoryException("expenses.expense_not_matchable");
        if (!string.Equals(currency, expenseCurrency, StringComparison.OrdinalIgnoreCase) || amount != expenseAmount) throw new ExpenseRepositoryException("expenses.card_amount_mismatch");
        if (request.ExpenseVersion.HasValue && request.ExpenseVersion.Value != expenseVersion) throw new ExpenseRepositoryException("expenses.concurrent_change_or_not_editable");
        await using var update = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Matched',matched_expense_id=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(update, tenant.Value, entity, cardId, request.ExpenseId); await update.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
        return (await GetCardTransactionAsync(tenant, entity, cardId, ct))!;
    }

    public async Task<ExpenseCardTransactionDto> UnmatchDraftCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select state,matched_expense_id from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId);
        await using var reader = await card.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var state = reader.GetString(0); var matchedExpense = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
        await reader.CloseAsync();
        if (state is not ("Matched" or "Allocated") && !matchedExpense.HasValue) throw new ExpenseRepositoryException("expenses.card_transaction_not_matched");
        await using var finalized = new NpgsqlCommand("select exists(select 1 from business_expenses.expense_card_allocation a join business_expenses.expense e on e.tenant_id=a.tenant_id and e.legal_entity_id=a.legal_entity_id and e.id=a.expense_id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.card_transaction_id=$3 and e.expense_state <> 'Draft') or exists(select 1 from business_expenses.expense e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$4 and e.expense_state <> 'Draft')", scope.Connection, scope.Transaction);
        Add(finalized, tenant.Value, entity, cardId, Db(matchedExpense));
        if (Convert.ToBoolean(await finalized.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture)) throw new ExpenseRepositoryException("expenses.card_transaction_not_unmatchable");
        await using var deleteAllocations = new NpgsqlCommand("delete from business_expenses.expense_card_allocation where tenant_id=$1 and legal_entity_id=$2 and card_transaction_id=$3", scope.Connection, scope.Transaction);
        Add(deleteAllocations, tenant.Value, entity, cardId); await deleteAllocations.ExecuteNonQueryAsync(ct);
        await using var update = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Cleared',matched_expense_id=null where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state in ('Matched','Allocated') returning id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id", scope.Connection, scope.Transaction);
        Add(update, tenant.Value, entity, cardId); await using var resultReader = await update.ExecuteReaderAsync(ct); if (!await resultReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_concurrent"); var result = ReadCard(resultReader); await resultReader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpenseCardTransactionDto> AllocateCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, AllocateExpenseCardTransactionRequest request, CancellationToken ct)
    {
        if (request.ExpenseId == Guid.Empty || request.Amount <= 0m) throw new ExpenseRepositoryException("expenses.card_allocation_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId); await using var cr = await card.ExecuteReaderAsync(ct);
        if (!await cr.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var cardAmount = cr.GetDecimal(0); var cardCurrency = cr.GetString(1); var cardState = cr.GetString(2); await cr.CloseAsync();
        if (cardState is not ("Unmatched" or "Cleared" or "Allocated")) throw new ExpenseRepositoryException("expenses.card_transaction_not_allocatable");
        await using var expense = new NpgsqlCommand("select amount,currency_code,expense_state,version from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(expense, tenant.Value, entity, request.ExpenseId); await using var er = await expense.ExecuteReaderAsync(ct);
        if (!await er.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
        var expenseAmount = er.GetDecimal(0); var expenseCurrency = er.GetString(1); var expenseState = er.GetString(2); var expenseVersion = er.GetInt64(3); await er.CloseAsync();
        if (expenseState is not ("Draft" or "Submitted") || request.Amount > expenseAmount || !string.Equals(cardCurrency, expenseCurrency, StringComparison.OrdinalIgnoreCase)) throw new ExpenseRepositoryException("expenses.card_allocation_mismatch");
        if (request.ExpenseVersion.HasValue && request.ExpenseVersion.Value != expenseVersion) throw new ExpenseRepositoryException("expenses.concurrent_change_or_not_editable");
        await using var allocation = new NpgsqlCommand("insert into business_expenses.expense_card_allocation(tenant_id,id,legal_entity_id,card_transaction_id,expense_id,amount) values($1,$2,$3,$4,$5,$6) on conflict(tenant_id,card_transaction_id,expense_id) do update set amount=excluded.amount", scope.Connection, scope.Transaction);
        Add(allocation, tenant.Value, Guid.CreateVersion7(), entity, cardId, request.ExpenseId, request.Amount); await allocation.ExecuteNonQueryAsync(ct);
        await using var total = new NpgsqlCommand("select coalesce(sum(amount),0) from business_expenses.expense_card_allocation where tenant_id=$1 and card_transaction_id=$2", scope.Connection, scope.Transaction); Add(total, tenant.Value, cardId); var allocated = Convert.ToDecimal(await total.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        if (allocated > cardAmount) throw new ExpenseRepositoryException("expenses.card_allocation_exceeds_transaction");
        await using var update = new NpgsqlCommand("update business_expenses.expense_card_transaction set state=case when $4=$5 then 'Allocated' else 'Unmatched' end,matched_expense_id=case when $4=$5 then $6 else null end where tenant_id=$1 and legal_entity_id=$2 and id=$3 returning id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id", scope.Connection, scope.Transaction);
        Add(update, tenant.Value, entity, cardId, allocated, cardAmount, request.ExpenseId); await using var resultReader = await update.ExecuteReaderAsync(ct); if (!await resultReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found"); var result = ReadCard(resultReader); await resultReader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpenseCardTransactionDto> MarkCardTransactionPersonalAsync(TenantId tenant, Guid entity, Guid cardId, MarkExpenseCardPersonalRequest request, CancellationToken ct)
    {
        if (request.EmployeeId == Guid.Empty) throw new ExpenseRepositoryException("expenses.card_employee_required");
        if (hr is not null && !await hr.EmployeeBelongsToLegalEntityAsync(tenant, request.EmployeeId, entity, ct)) throw new ExpenseRepositoryException("expenses.card_employee_scope_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select provider_reference,amount,currency_code,state,employee_id from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId); await using var r = await card.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var reference = r.GetString(0); var amount = r.GetDecimal(1); var currency = r.GetString(2); var state = r.GetString(3); var existingEmployee = r.IsDBNull(4) ? (Guid?)null : r.GetGuid(4); await r.CloseAsync();
        if (state is not ("Unmatched" or "Cleared" or "Personal")) throw new ExpenseRepositoryException("expenses.card_transaction_not_personalizable");
        if (state == "Personal" && existingEmployee != request.EmployeeId) throw new ExpenseRepositoryException("expenses.card_transaction_personal_conflict");
        await using var update = new NpgsqlCommand("update business_expenses.expense_card_transaction set state='Personal',employee_id=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(update, tenant.Value, entity, cardId, request.EmployeeId); await update.ExecuteNonQueryAsync(ct);
        await using var repayment = new NpgsqlCommand("insert into business_expenses.expense_employee_repayment(tenant_id,id,legal_entity_id,employee_id,source_reference,amount,currency_code,state) select $1,$2,$3,$4,$5,$6,$7,'Requested' where not exists(select 1 from business_expenses.expense_employee_repayment where tenant_id=$1 and source_reference=$5)", scope.Connection, scope.Transaction);
        Add(repayment, tenant.Value, Guid.CreateVersion7(), entity, request.EmployeeId, $"corporate-card:{cardId:D}", amount, currency); await repayment.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
        return (await GetCardTransactionAsync(tenant, entity, cardId, ct))!;
    }

    public async Task<ExpensePage<ExpenseEmployeeRepaymentDto>> ListEmployeeRepaymentsAsync(TenantId tenant, Guid entity, Guid? employeeId, string? state, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or employee_id=$3) and ($4::text is null or state=$4)"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_employee_repayment where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(employeeId), Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,source_reference,amount,settled_amount,currency_code,state,created_at,recorded_at,payment_id,version from business_expenses.expense_employee_repayment where {where} order by created_at desc,id desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(employeeId), Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpenseEmployeeRepaymentDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.GetString(6), r.GetString(7), r.GetFieldValue<DateTimeOffset>(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9), r.IsDBNull(10) ? null : r.GetGuid(10), r.GetInt64(11))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize);
    }

    public async Task<ExpenseEmployeeRepaymentDto> SettleEmployeeRepaymentAsync(TenantId tenant, Guid entity, Guid repaymentId, Guid actorId, Guid membershipId, SettleEmployeeRepaymentRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m) throw new ExpenseRepositoryException("expenses.repayment_amount_invalid");
        decimal outstanding; string currency; Guid employee; long currentVersion; decimal settled;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockCommand = new NpgsqlCommand("select employee_id,amount,settled_amount,currency_code,state,version from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(lockCommand, tenant.Value, entity, repaymentId); await using var reader = await lockCommand.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.repayment_not_found"); employee = reader.GetGuid(0); var amount = reader.GetDecimal(1); settled = reader.GetDecimal(2); currency = reader.GetString(3); var state = reader.GetString(4); currentVersion = reader.GetInt64(5); await reader.CloseAsync(); outstanding = amount - settled; if (state is "Recorded" or "Failed" && outstanding <= 0m) return (await GetRepaymentAsync(tenant, entity, repaymentId, ct))!; if (currentVersion != request.Version || request.Amount > outstanding) throw new ExpenseRepositoryException("expenses.repayment_concurrent_or_amount_invalid"); await using var pending = new NpgsqlCommand("update business_expenses.expense_employee_repayment set state='Pending',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4 and state in ('Requested','Pending','Failed')", scope.Connection, scope.Transaction); Add(pending, tenant.Value, entity, repaymentId, request.Version); if (await pending.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.repayment_concurrent"); await scope.Transaction.CommitAsync(ct);
        }
        EmployeeRepaymentPaymentResult payment;
        try
        {
            var paymentAccountId = request.PaymentAccountId;
            if (paymentAccountId == Guid.Empty || paymentAccountId == Guid.Parse("00000000-0000-0000-0000-000000000001"))
            {
                await using var accScope = await OpenAsync(tenant, ct);
                await using var accCmd = new NpgsqlCommand("select id from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and status='Active' and account_type='Bank' order by created_at limit 1", accScope.Connection, accScope.Transaction);
                Add(accCmd, tenant.Value, entity);
                var resolved = await accCmd.ExecuteScalarAsync(ct);
                if (resolved is Guid g) paymentAccountId = g;
                await accScope.Transaction.CommitAsync(ct);
            }
            payment = await (repaymentPayments ?? throw new ExpenseRepositoryException("expenses.payments_capability_unavailable")).ReceiveEmployeeRepaymentAsync(tenant, actorId, membershipId, Guid.CreateVersion7(), entity, repaymentId, paymentAccountId, request.Method, request.Amount, currency, request.PaymentDate, request.PostingDate, $"expenses-repayment:{repaymentId:D}:{request.Amount.ToString(CultureInfo.InvariantCulture)}", ct);
        }
        catch { await SetRepaymentFailedAsync(tenant, entity, repaymentId, ct); throw; }
        if (!string.Equals(payment.State, "Completed", StringComparison.OrdinalIgnoreCase) || payment.Amount != request.Amount || !string.Equals(payment.CurrencyCode.Trim(), currency, StringComparison.OrdinalIgnoreCase))
        {
            await SetRepaymentFailedAsync(tenant, entity, repaymentId, ct);
            throw new ExpenseRepositoryException("expenses.payment_result_mismatch");
        }
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var complete = new NpgsqlCommand("update business_expenses.expense_employee_repayment set settled_amount=settled_amount+$4,state=case when settled_amount+$4 >= amount then 'Recorded' else 'Pending' end,recorded_at=case when settled_amount+$4 >= amount then now() else recorded_at end,payment_id=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Pending' and settled_amount+$4 <= amount", scope.Connection, scope.Transaction); Add(complete, tenant.Value, entity, repaymentId, payment.Amount, payment.PaymentId); if (await complete.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.repayment_completion_conflict"); await scope.Transaction.CommitAsync(ct);
        }
        return (await GetRepaymentAsync(tenant, entity, repaymentId, ct))!;
    }

    private async Task SetRepaymentFailedAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("update business_expenses.expense_employee_repayment set state='Failed',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Pending'", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, id); await command.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); }
    private async Task<ExpenseEmployeeRepaymentDto?> GetRepaymentAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,employee_id,source_reference,amount,settled_amount,currency_code,state,created_at,recorded_at,payment_id,version from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? new ExpenseEmployeeRepaymentDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.GetInt64(11)) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    public async Task<ExpenseRatePolicyDto> CreateRatePolicyAsync(TenantId tenant, CreateExpenseRatePolicyRequest request, CancellationToken ct)
    {
        var kind = request.Kind.Trim(); var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (kind is not ("Mileage" or "PerDiem") || request.Rate < 0 || currency.Length != 3 || request.EffectiveTo < request.EffectiveFrom || request.BreakfastReduction < 0 || request.LunchReduction < 0 || request.DinnerReduction < 0) throw new ExpenseRepositoryException("expenses.rate_policy_invalid");
        await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7();
        await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_rate_policy(tenant_id,id,legal_entity_id,kind,version,effective_from,effective_to,vehicle_type,location_code,currency_code,rate,breakfast_reduction,lunch_reduction,dinner_reduction) values($1,$2,$3,$4,coalesce((select max(version)+1 from business_expenses.expense_rate_policy where tenant_id=$1 and legal_entity_id=$3 and kind=$4),1),$5,$6,$7,$8,$9,$10,$11,$12,$13) returning id,legal_entity_id,kind,version,status,effective_from,effective_to,vehicle_type,location_code,currency_code,rate,breakfast_reduction,lunch_reduction,dinner_reduction", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, id, request.LegalEntityId, kind, request.EffectiveFrom, Db(request.EffectiveTo), Db(request.VehicleType), Db(request.LocationCode), currency, request.Rate, request.BreakfastReduction, request.LunchReduction, request.DinnerReduction); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.rate_policy_invalid"); var result = ReadRatePolicy(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpenseRatePolicyDto> PublishRatePolicyAsync(TenantId tenant, Guid entity, Guid id, PublishExpenseRatePolicyRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_rate_policy set status='Published',effective_to=coalesce($4,effective_to) where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning id,legal_entity_id,kind,version,status,effective_from,effective_to,vehicle_type,location_code,currency_code,rate,breakfast_reduction,lunch_reduction,dinner_reduction", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id, Db(request.EffectiveTo)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.rate_policy_not_publishable"); var result = ReadRatePolicy(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<ExpenseRatePolicyDto>> ListRatePoliciesAsync(TenantId tenant, Guid entity, string? kind, string? status, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or kind=$3) and ($4::text is null or status=$4)"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_rate_policy where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(kind) ? null : kind.Trim()), Db(string.IsNullOrWhiteSpace(status) ? null : status.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var command = new NpgsqlCommand($"select id,legal_entity_id,kind,version,status,effective_from,effective_to,vehicle_type,location_code,currency_code,rate,breakfast_reduction,lunch_reduction,dinner_reduction from business_expenses.expense_rate_policy where {where} order by effective_from desc,version desc,id desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(kind) ? null : kind.Trim()), Db(string.IsNullOrWhiteSpace(status) ? null : status.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpenseRatePolicyDto>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(ReadRatePolicy(reader)); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }

    public async Task<MileageExpenseDetailDto> SetMileageDetailAsync(TenantId tenant, Guid entity, Guid expenseId, SetMileageDetailRequest request, CancellationToken ct)
    {
        var distanceUnit = request.DistanceUnit.Trim().ToUpperInvariant() switch
        {
            "KM" or "KILOMETER" or "KILOMETERS" => "KM",
            "MI" or "MILE" or "MILES" => "MI",
            _ => string.Empty
        };
        if (request.Distance <= 0 || request.Rate < 0 || distanceUnit.Length == 0) throw new ExpenseRepositoryException("expenses.mileage_invalid");
        if (!request.RatePolicyVersionId.HasValue) throw new ExpenseRepositoryException("expenses.rate_policy_required");
        await using var scope = await OpenAsync(tenant, ct); var rate = request.Rate; Guid? ratePolicyId = request.RatePolicyVersionId;
        if (ratePolicyId.HasValue)
        {
            await using var policy = new NpgsqlCommand("select p.rate from business_expenses.expense_rate_policy p join business_expenses.expense e on e.tenant_id=p.tenant_id and e.legal_entity_id=p.legal_entity_id where p.tenant_id=$1 and p.legal_entity_id=$2 and p.id=$3 and p.kind='Mileage' and p.status='Published' and p.currency_code=e.currency_code and p.effective_from <= e.expense_date and (p.effective_to is null or p.effective_to >= e.expense_date) and e.id=$4 and (p.vehicle_type is null or p.vehicle_type=$5)", scope.Connection, scope.Transaction); Add(policy, tenant.Value, entity, ratePolicyId.Value, expenseId, Db(request.VehicleType)); var value = await policy.ExecuteScalarAsync(ct); if (value is null) throw new ExpenseRepositoryException("expenses.rate_policy_not_applicable"); rate = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        // Rate policies are defined per kilometre; the original UOM and the
        // normalized eligible distance remain persisted as separate facts.
        var calculatedDistance = decimal.Round(request.Distance * (distanceUnit == "MI" ? 1.609344m : 1m) * (request.RoundTrip ? 2m : 1m), 4, MidpointRounding.AwayFromZero);
        var amount = decimal.Round(calculatedDistance * rate, 4, MidpointRounding.AwayFromZero);
        await using var cmd = new NpgsqlCommand("with e as (select id from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft') insert into business_expenses.mileage_expense_detail(tenant_id,id,legal_entity_id,expense_id,distance,distance_unit,rate,vehicle_type,route,rate_policy_version_id,round_trip,calculated_distance,calculated_amount) select $1,coalesce((select id from business_expenses.mileage_expense_detail where tenant_id=$1 and expense_id=$3),$4),$2,$3,$5,$6,$7,$8,$9,$10,$11,$12,$13 from e on conflict(tenant_id,expense_id) do update set distance=excluded.distance,distance_unit=excluded.distance_unit,rate=excluded.rate,vehicle_type=excluded.vehicle_type,route=excluded.route,rate_policy_version_id=excluded.rate_policy_version_id,round_trip=excluded.round_trip,calculated_distance=excluded.calculated_distance,calculated_amount=excluded.calculated_amount returning id,expense_id,distance,distance_unit,rate,calculated_amount,vehicle_type,route,rate_policy_version_id,round_trip,calculated_distance", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, entity, expenseId, Guid.CreateVersion7(), request.Distance, distanceUnit, rate, Db(request.VehicleType), Db(request.Route), Db(ratePolicyId), request.RoundTrip, calculatedDistance, amount); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.mileage_not_editable"); var result = new MileageExpenseDetailDto(r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetBoolean(9), r.GetDecimal(10)); await r.CloseAsync(); await UpdateTypedExpenseAmountAsync(scope, tenant, entity, expenseId, amount, ct); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<MileageExpenseDetailDto?> GetMileageDetailAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,expense_id,distance,distance_unit,rate,calculated_amount,vehicle_type,route,rate_policy_version_id,round_trip,calculated_distance from business_expenses.mileage_expense_detail where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, expenseId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        MileageExpenseDetailDto? result = null;
        if (await reader.ReadAsync(ct)) result = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetBoolean(9), reader.IsDBNull(10) ? null : reader.GetDecimal(10));
        await reader.CloseAsync();
        await scope.Transaction.CommitAsync(ct);
        return result;
    }

    public async Task<MileageExpenseDetailDto> SetMileageDetailSelfAsync(TenantId tenant, Guid membershipId, Guid entity, Guid expenseId, SetMileageDetailRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        var expense = await GetAsync(tenant, entity, expenseId, ct);
        if (expense is null || expense.EmployeeId != employeeId) throw new ExpenseRepositoryException("expenses.expense_not_found");
        return await SetMileageDetailAsync(tenant, entity, expenseId, request, ct);
    }

    public async Task<MileageExpenseDetailDto?> GetMileageDetailSelfAsync(TenantId tenant, Guid membershipId, Guid entity, Guid expenseId, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        var expense = await GetAsync(tenant, entity, expenseId, ct);
        if (expense is null || expense.EmployeeId != employeeId) throw new ExpenseRepositoryException("expenses.expense_not_found");
        return await GetMileageDetailAsync(tenant, entity, expenseId, ct);
    }

    public async Task<PerDiemExpenseDetailDto> SetPerDiemDetailAsync(TenantId tenant, Guid entity, Guid expenseId, SetPerDiemDetailRequest request, CancellationToken ct)
    {
        var effDays = request.DailyLines is { Count: > 0 } dl ? dl.Sum(l => l.PartialDayFactor) : request.Days;
        if (effDays <= 0 || request.DailyRate < 0 || request.ProvidedMeals < 0) throw new ExpenseRepositoryException("expenses.per_diem_invalid");
        if (!request.RatePolicyVersionId.HasValue) throw new ExpenseRepositoryException("expenses.rate_policy_required");
        if (request.DailyLines is { Count: > 0 } requestedLines)
        {
            var previousDate = (DateOnly?)null;
            foreach (var line in requestedLines)
            {
                if (line.PartialDayFactor <= 0m || line.PartialDayFactor > 1m || (previousDate.HasValue && line.Date != previousDate.Value.AddDays(1)))
                    throw new ExpenseRepositoryException("expenses.per_diem_daily_line_invalid");
                previousDate = line.Date;
            }
        }
        else if (request.ProvidedMeals > (int)Math.Ceiling(request.Days) * 3)
        {
            throw new ExpenseRepositoryException("expenses.per_diem_meals_invalid");
        }
        if (request.TripStartAt.HasValue && request.TripEndAt.HasValue && request.TripEndAt <= request.TripStartAt)
            throw new ExpenseRepositoryException("expenses.per_diem_trip_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        var rate = request.DailyRate;
        Guid? ratePolicyId = request.RatePolicyVersionId;
        decimal bRed = 0m, lRed = 0m, dRed = 0m;
        DateOnly expenseDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await using (var getExp = new NpgsqlCommand("select expense_date from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft'", scope.Connection, scope.Transaction))
        {
            Add(getExp, tenant.Value, entity, expenseId);
            var ed = await getExp.ExecuteScalarAsync(ct);
            if (ed is null) throw new ExpenseRepositoryException("expenses.per_diem_not_editable");
            expenseDate = (DateOnly)ed;
        }

        if (ratePolicyId.HasValue)
        {
            await using var policy = new NpgsqlCommand(@"select p.rate, p.breakfast_reduction, p.lunch_reduction, p.dinner_reduction
                from business_expenses.expense_rate_policy p
                join business_expenses.expense e on e.tenant_id=p.tenant_id and e.legal_entity_id=p.legal_entity_id and e.id=$4
                where p.tenant_id=$1 and p.legal_entity_id=$2 and p.id=$3 and p.kind='PerDiem' and p.status='Published'
                  and p.currency_code=e.currency_code
                  and p.effective_from <= $5 and (p.effective_to is null or p.effective_to >= $5)
                  and (p.location_code is null or p.location_code=$6)", scope.Connection, scope.Transaction);
            Add(policy, tenant.Value, entity, ratePolicyId.Value, expenseId, expenseDate, Db(request.Destination));
            await using var pr = await policy.ExecuteReaderAsync(ct);
            if (!await pr.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.rate_policy_not_applicable");
            rate = pr.GetDecimal(0);
            bRed = pr.GetDecimal(1);
            lRed = pr.GetDecimal(2);
            dRed = pr.GetDecimal(3);
            await pr.CloseAsync();
        }

        var tripStartAt = request.TripStartAt ?? new DateTimeOffset(expenseDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var tripEndAt = request.TripEndAt ?? tripStartAt.AddDays((double)Math.Max(0m, Math.Ceiling(effDays) - 1m));
        if (tripEndAt <= tripStartAt) throw new ExpenseRepositoryException("expenses.per_diem_trip_invalid");

        var effBRed = bRed;
        var effLRed = lRed;
        var effDRed = dRed;

        var lines = new List<PerDiemDailyLineDto>();
        decimal calculatedAmount = 0m;
        decimal totalDays = request.Days;
        int totalMeals = request.ProvidedMeals;

        if (request.DailyLines is { Count: > 0 } userLines)
        {
            totalDays = 0m;
            totalMeals = 0;
            foreach (var l in userLines)
            {
                var factor = l.PartialDayFactor;
                if (l.Date < DateOnly.FromDateTime(tripStartAt.UtcDateTime) || l.Date > DateOnly.FromDateTime(tripEndAt.UtcDateTime)) throw new ExpenseRepositoryException("expenses.per_diem_daily_line_invalid");
                totalDays += factor;
                if (l.BreakfastProvided) totalMeals++;
                if (l.LunchProvided) totalMeals++;
                if (l.DinnerProvided) totalMeals++;

                var baseDaily = decimal.Round(rate * factor, 4, MidpointRounding.AwayFromZero);
                var reduction = (l.BreakfastProvided ? effBRed : 0m) + (l.LunchProvided ? effLRed : 0m) + (l.DinnerProvided ? effDRed : 0m);
                var finalDaily = decimal.Round(Math.Max(0m, baseDaily - reduction), 4, MidpointRounding.AwayFromZero);
                lines.Add(new(l.Date, factor, l.BreakfastProvided, l.LunchProvided, l.DinnerProvided, rate, reduction, finalDaily));
                calculatedAmount += finalDaily;
            }
        }
        else
        {
            var wholeDays = (int)Math.Floor(request.Days);
            var partialFactor = request.Days - wholeDays;
            var totalLinesCount = wholeDays + (partialFactor > 0m ? 1 : 0);
            var remainingProvidedMeals = request.ProvidedMeals;

            for (var i = 0; i < totalLinesCount; i++)
            {
                var factor = (i == totalLinesCount - 1 && partialFactor > 0m) ? partialFactor : 1.0m;
                var lineDate = expenseDate.AddDays(i);

                var bf = false;
                var lu = false;
                var di = false;

                if (remainingProvidedMeals > 0) { bf = true; remainingProvidedMeals--; }
                if (remainingProvidedMeals > 0) { lu = true; remainingProvidedMeals--; }
                if (remainingProvidedMeals > 0) { di = true; remainingProvidedMeals--; }

                var baseDaily = decimal.Round(rate * factor, 4, MidpointRounding.AwayFromZero);
                var reduction = (bf ? effBRed : 0m) + (lu ? effLRed : 0m) + (di ? effDRed : 0m);
                var finalDaily = decimal.Round(Math.Max(0m, baseDaily - reduction), 4, MidpointRounding.AwayFromZero);
                lines.Add(new(lineDate, factor, bf, lu, di, rate, reduction, finalDaily));
                calculatedAmount += finalDaily;
            }
        }

        calculatedAmount = decimal.Round(calculatedAmount, 4, MidpointRounding.AwayFromZero);
        var linesJson = System.Text.Json.JsonSerializer.Serialize(lines);

        await using var cmd = new NpgsqlCommand(@"insert into business_expenses.per_diem_expense_detail(
            tenant_id,id,legal_entity_id,expense_id,days,daily_rate,provided_meals,destination,rate_policy_version_id,calculated_amount,daily_lines,trip_start_at,trip_end_at
        ) select $1,coalesce((select id from business_expenses.per_diem_expense_detail where tenant_id=$1 and expense_id=$3),$4),$2,$3,$5,$6,$7,$8,$9,$10,$11::jsonb,$12,$13
        from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft'
        on conflict(tenant_id,expense_id) do update set
            days=excluded.days,
            daily_rate=excluded.daily_rate,
            provided_meals=excluded.provided_meals,
            destination=excluded.destination,
            rate_policy_version_id=excluded.rate_policy_version_id,
            calculated_amount=excluded.calculated_amount,
            daily_lines=excluded.daily_lines,
            trip_start_at=excluded.trip_start_at,
            trip_end_at=excluded.trip_end_at
        returning id,expense_id,days,daily_rate,provided_meals,calculated_amount,destination,rate_policy_version_id,trip_start_at,trip_end_at", scope.Connection, scope.Transaction);

        Add(cmd, tenant.Value, entity, expenseId, Guid.CreateVersion7(), totalDays, rate, totalMeals, Db(request.Destination), Db(ratePolicyId), calculatedAmount, linesJson, tripStartAt, tripEndAt);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.per_diem_not_editable");
        var result = new PerDiemExpenseDetailDto(r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2), r.GetDecimal(3), r.GetInt32(4), r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetGuid(7), lines, r.GetFieldValue<DateTimeOffset>(8), r.GetFieldValue<DateTimeOffset>(9));
        await r.CloseAsync();
        await UpdateTypedExpenseAmountAsync(scope, tenant, entity, expenseId, calculatedAmount, ct);
        await scope.Transaction.CommitAsync(ct);
        return result;
    }

    public async Task<PerDiemExpenseDetailDto> SetPerDiemDetailSelfAsync(TenantId tenant, Guid membershipId, Guid entity, Guid expenseId, SetPerDiemDetailRequest request, CancellationToken ct)
    {
        var employeeId = await ResolveSelfEmployeeAsync(tenant, membershipId, entity, ct);
        var expense = await GetAsync(tenant, entity, expenseId, ct);
        if (expense is null || expense.EmployeeId != employeeId) throw new ExpenseRepositoryException("expenses.expense_not_found");
        return await SetPerDiemDetailAsync(tenant, entity, expenseId, request, ct);
    }

    public async Task<ExpenseDimensionDto> SetDimensionAsync(TenantId tenant, Guid entity, Guid expenseId, SetExpenseDimensionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DimensionCode) || string.IsNullOrWhiteSpace(request.ValueCode)) throw new ExpenseRepositoryException("expenses.dimension_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_dimension(tenant_id,id,legal_entity_id,expense_id,dimension_code,value_code) select $1,$2,$3,id,$4,$5 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$6 and expense_state='Draft' on conflict(tenant_id,expense_id,dimension_code) do update set value_code=excluded.value_code returning id,expense_id,dimension_code,value_code", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, Guid.CreateVersion7(), entity, request.DimensionCode.Trim().ToUpperInvariant(), request.ValueCode.Trim(), expenseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.dimension_not_editable");
        var result = new ExpenseDimensionDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3)); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<IReadOnlyList<ExpenseDimensionDto>> ListDimensionsAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,dimension_code,value_code from business_expenses.expense_dimension where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by dimension_code", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseDimensionDto>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3))); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }

    private static async Task UpdateTypedExpenseAmountAsync(Scope scope, TenantId tenant, Guid entity, Guid expenseId, decimal amount, CancellationToken ct)
    { await using var update = new NpgsqlCommand("update business_expenses.expense set amount=$4,updated_at=now(),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft'", scope.Connection, scope.Transaction); Add(update, tenant.Value, entity, expenseId, amount); if (await update.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.typed_detail_not_editable"); }

    private static async Task ApplyPolicyAdjustedAmountAsync(Scope scope, TenantId tenant, Guid entity, Guid expenseId, decimal amount, bool hasMileage, bool hasPerDiem, CancellationToken ct)
    {
        if (hasMileage)
        {
            await using var mileage = new NpgsqlCommand("update business_expenses.mileage_expense_detail set calculated_amount=$4 where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction);
            Add(mileage, tenant.Value, entity, expenseId, amount);
            if (await mileage.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.typed_detail_not_editable");
        }
        else if (hasPerDiem)
        {
            await using var perDiem = new NpgsqlCommand("update business_expenses.per_diem_expense_detail set calculated_amount=$4 where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction);
            Add(perDiem, tenant.Value, entity, expenseId, amount);
            if (await perDiem.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.typed_detail_not_editable");
        }

        await using var expense = new NpgsqlCommand("update business_expenses.expense set amount=$4,updated_at=now(),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state in ('Draft','Submitted')", scope.Connection, scope.Transaction);
        Add(expense, tenant.Value, entity, expenseId, amount);
        if (await expense.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.typed_detail_not_editable");
    }

    private async Task<ExpenseCardTransactionDto?> GetCardTransactionAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id); await using var r = await cmd.ExecuteReaderAsync(ct); var result = await r.ReadAsync(ct) ? ReadCard(r) : null; await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseReimbursementBatchDto> PrepareReimbursementBatchAsync(TenantId tenant, Guid entity, string currency, CancellationToken ct)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        await using var scope = await OpenAsync(tenant, ct);
        var batchId = Guid.CreateVersion7();
        await using var cmd = new NpgsqlCommand("with pending as (select coalesce(sum(o.amount-o.settled_amount),0) total from business_expenses.expense_reimbursement_obligation o where o.tenant_id=$1 and o.legal_entity_id=$2 and o.status in ('Pending','PartiallySettled') and o.currency_code=$3 and not exists(select 1 from business_expenses.expense_reimbursement_allocation a join business_expenses.expense_reimbursement_batch b on b.tenant_id=a.tenant_id and b.id=a.batch_id where a.tenant_id=o.tenant_id and a.obligation_id=o.id and b.state in ('Prepared','Submitted'))) insert into business_expenses.expense_reimbursement_batch(tenant_id,id,legal_entity_id,state,currency_code,total_amount) select $1,$4,$2,'Prepared',$3,total from pending where total > 0 returning id,legal_entity_id,state,currency_code,total_amount,version,created_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, normalizedCurrency, batchId);
        await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.reimbursement_empty");
        var result = new ExpenseReimbursementBatchDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetInt64(5), r.GetFieldValue<DateTimeOffset>(6)); await r.CloseAsync();
        await using var allocation = new NpgsqlCommand("insert into business_expenses.expense_reimbursement_allocation(tenant_id,id,legal_entity_id,batch_id,obligation_id,amount) select o.tenant_id,gen_random_uuid(),o.legal_entity_id,$4,o.id,o.amount-o.settled_amount from business_expenses.expense_reimbursement_obligation o where o.tenant_id=$1 and o.legal_entity_id=$2 and o.status in ('Pending','PartiallySettled') and o.currency_code=$3 and not exists(select 1 from business_expenses.expense_reimbursement_allocation a join business_expenses.expense_reimbursement_batch b on b.tenant_id=a.tenant_id and b.id=a.batch_id where a.tenant_id=o.tenant_id and a.obligation_id=o.id and b.state in ('Prepared','Submitted'))", scope.Connection, scope.Transaction); Add(allocation, tenant.Value, entity, normalizedCurrency, batchId); await allocation.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct); return result;
    }
    private static BusinessAdvanceDto ReadAdvance(NpgsqlDataReader r)
    {
        var dueDate = r.IsDBNull(13) ? (DateOnly?)null : r.GetFieldValue<DateOnly>(13);
        var writtenOff = r.IsDBNull(14) ? 0m : r.GetDecimal(14);
        var outstanding = r.GetDecimal(5) - r.GetDecimal(6) - r.GetDecimal(7) - writtenOff;
        var overdue = dueDate is { } due && due < DateOnly.FromDateTime(DateTime.UtcNow.Date) && outstanding > 0m && r.GetString(9) is "Issued" or "PartiallyIssued" or "PartiallySettled";
        return new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetString(8), r.GetString(9), r.GetInt64(10), r.GetFieldValue<DateTimeOffset>(11), r.IsDBNull(12) ? null : r.GetGuid(12), dueDate, overdue, writtenOff);
    }
    private static ExpenseImportBatchDto ReadImportBatch(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8));
    private static ExpenseOcrJobDto ReadOcrJob(NpgsqlDataReader r, Guid expenseId, Guid evidenceId)
    {
        JsonElement? suggestions = null;
        if (!r.IsDBNull(3)) suggestions = JsonDocument.Parse(r.GetString(3)).RootElement.Clone();
        return new(r.GetGuid(0), expenseId, evidenceId, r.GetString(1), r.GetInt32(2), suggestions, r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8));
    }
    private static async Task<(string FileReference, string Status)?> ReadOcrEvidenceAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid expenseId, Guid evidenceId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select file_reference,status from business_expenses.expense_evidence where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity, expenseId, evidenceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            await reader.CloseAsync();
            return null;
        }

        var result = (reader.GetString(0), reader.GetString(1));
        await reader.CloseAsync();
        return result;
    }
    private async Task<BusinessAdvanceDto?> GetAdvanceAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id,settlement_due_date,written_off_amount from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? ReadAdvance(reader) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }
    private static ExpenseCardTransactionDto ReadCard(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.GetDecimal(5), r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.IsDBNull(9) ? null : r.GetGuid(9));
    private static ExpenseCardAdjustmentDto ReadCardAdjustment(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetFieldValue<DateTimeOffset>(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9));
    private static ExpenseRatePolicyDto ReadRatePolicy(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetFieldValue<DateOnly>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetString(9), r.GetDecimal(10), r.GetDecimal(11), r.GetDecimal(12), r.GetDecimal(13));
    private static ExpenseApprovalDto ToExpenseApproval(PlatformApprovalRequestResult result, Guid expenseId) => new(result.Id, expenseId, result.State, result.LegalEntityId, result.ResourceVersion, result.PolicyVersionId, result.DecidedBy, result.DecisionReason, result.CreatedAt, result.DecidedAt);
    public async Task<ExpenseReimbursementBatchDto> PayReimbursementBatchAsync(TenantId tenant, Guid entity, Guid batchId,
        Guid actorId, Guid membershipId, Guid paymentAccountId, string method, DateOnly paymentDate, DateOnly postingDate, long version, CancellationToken ct)
    {
        decimal amount; string currency;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockCommand = new NpgsqlCommand("select total_amount,currency_code,state,version from business_expenses.expense_reimbursement_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
            Add(lockCommand, tenant.Value, entity, batchId);
            await using var reader = await lockCommand.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.reimbursement_batch_not_found");
            amount = reader.GetDecimal(0); currency = reader.GetString(1);
            var state = reader.GetString(2); var currentVersion = reader.GetInt64(3);
            await reader.CloseAsync();
            if (state == "Paid") return await GetReimbursementBatchAsync(tenant, entity, batchId, ct)
                ?? throw new ExpenseRepositoryException("expenses.reimbursement_batch_not_found");
            var versionMatches = currentVersion == version || (state == "Submitted" && currentVersion == version + 1);
            if (state is not ("Prepared" or "Failed" or "Submitted") || !versionMatches || amount <= 0m)
                throw new ExpenseRepositoryException("expenses.reimbursement_batch_not_payable");
            if (state is "Prepared" or "Failed")
            {
                await using var mark = new NpgsqlCommand("update business_expenses.expense_reimbursement_batch set state='Submitted',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4", scope.Connection, scope.Transaction);
                Add(mark, tenant.Value, entity, batchId, version);
                if (await mark.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.reimbursement_batch_concurrent");
            }
            await scope.Transaction.CommitAsync(ct);
        }
        ExpensePaymentResult payment;
        try
        {
            if (paymentAccountId == Guid.Empty || paymentAccountId == Guid.Parse("00000000-0000-0000-0000-000000000001"))
            {
                await using var accScope = await OpenAsync(tenant, ct);
                await using var accCmd = new NpgsqlCommand("select id from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and status='Active' and account_type='Bank' order by created_at limit 1", accScope.Connection, accScope.Transaction);
                Add(accCmd, tenant.Value, entity);
                var resolved = await accCmd.ExecuteScalarAsync(ct);
                if (resolved is Guid g) paymentAccountId = g;
                await accScope.Transaction.CommitAsync(ct);
            }
            payment = await (expensePayments ?? throw new ExpenseRepositoryException("expenses.payments_capability_unavailable")).PayExpenseReimbursementAsync(tenant, actorId, membershipId, batchId, entity, paymentAccountId, method, amount, currency, paymentDate, postingDate, $"expenses-reimbursement:{batchId:D}", ct);
            if (!string.Equals(payment.State, "Completed", StringComparison.OrdinalIgnoreCase) || payment.Amount != amount || !string.Equals(payment.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
                throw new ExpenseRepositoryException("expenses.payment_result_mismatch");
        }
        catch
        {
            await SetBatchStateAsync(tenant, entity, batchId, "Failed", ct);
            await EmitReimbursementOutcomeAsync(tenant, entity, batchId, "ExpenseReimbursementFailedV1", amount, currency, ct);
            throw;
        }
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var settleBatch = new NpgsqlCommand("update business_expenses.expense_reimbursement_batch set state='Paid',paid_at=now(),version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Submitted'", scope.Connection, scope.Transaction);
            Add(settleBatch, tenant.Value, entity, batchId);
            if (await settleBatch.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.reimbursement_batch_completion_conflict");
            await using var settleObligations = new NpgsqlCommand("update business_expenses.expense_reimbursement_obligation o set settled_amount=o.amount,status='Settled' where o.tenant_id=$1 and o.legal_entity_id=$2 and exists(select 1 from business_expenses.expense_reimbursement_allocation a where a.tenant_id=o.tenant_id and a.batch_id=$3 and a.obligation_id=o.id)", scope.Connection, scope.Transaction);
            Add(settleObligations, tenant.Value, entity, batchId);
            await settleObligations.ExecuteNonQueryAsync(ct);
            await using var settleExpenses = new NpgsqlCommand("update business_expenses.expense e set settlement_state='Settled',version=version+1,updated_at=now() where e.tenant_id=$1 and e.legal_entity_id=$2 and exists(select 1 from business_expenses.expense_reimbursement_allocation a join business_expenses.expense_reimbursement_obligation o on o.tenant_id=a.tenant_id and o.id=a.obligation_id where a.tenant_id=e.tenant_id and a.batch_id=$3 and o.expense_id=e.id)", scope.Connection, scope.Transaction);
            Add(settleExpenses, tenant.Value, entity, batchId);
            await settleExpenses.ExecuteNonQueryAsync(ct);
            await using var recipients = new NpgsqlCommand("select coalesce(jsonb_agg(distinct o.employee_id) filter(where o.employee_id is not null),'[]'::jsonb) from business_expenses.expense_reimbursement_obligation o join business_expenses.expense_reimbursement_allocation a on a.tenant_id=o.tenant_id and a.obligation_id=o.id where a.tenant_id=$1 and a.batch_id=$2", scope.Connection, scope.Transaction);
            Add(recipients, tenant.Value, batchId);
            var employeeIds = (string?)await recipients.ExecuteScalarAsync(ct) ?? "[]";
            await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) select $1,'ExpenseReimbursementPaidV1',jsonb_build_object('BatchId',$2::text,'LegalEntityId',$3::text,'EmployeeIds',$4::jsonb,'Amount',$5::numeric,'CurrencyCode',$6::text),$7,$8,$2,$2,'expenses','Pending',0 where not exists (select 1 from platform.outbox_message where tenant_id=$7 and message_type='ExpenseReimbursementPaidV1' and causation_id=$2)", scope.Connection, scope.Transaction);
            Add(outbox, Guid.CreateVersion7(), batchId, entity, employeeIds, amount, currency, tenant.Value, DateTimeOffset.UtcNow);
            await outbox.ExecuteNonQueryAsync(ct);
            await scope.Transaction.CommitAsync(ct);
        }
        return (await GetReimbursementBatchAsync(tenant, entity, batchId, ct))!;
    }

    private async Task SetBatchStateAsync(TenantId tenant, Guid entity, Guid batchId, string state, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("update business_expenses.expense_reimbursement_batch set state=$4,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Submitted'", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, batchId, state); await command.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); }
    private async Task EmitReimbursementOutcomeAsync(TenantId tenant, Guid entity, Guid batchId, string messageType, decimal amount, string currency, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var recipients = new NpgsqlCommand("select coalesce(jsonb_agg(distinct o.employee_id) filter(where o.employee_id is not null),'[]'::jsonb) from business_expenses.expense_reimbursement_obligation o join business_expenses.expense_reimbursement_allocation a on a.tenant_id=o.tenant_id and a.obligation_id=o.id where a.tenant_id=$1 and a.batch_id=$2", scope.Connection, scope.Transaction);
        Add(recipients, tenant.Value, batchId);
        var employeeIds = (string?)await recipients.ExecuteScalarAsync(ct) ?? "[]";
        await using var outbox = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,status,attempts) select $1,$2,jsonb_build_object('BatchId',$3::text,'LegalEntityId',$4::text,'EmployeeIds',$5::jsonb,'Amount',$6::numeric,'CurrencyCode',$7::text),$8,$9,$3,$3,'expenses','Pending',0 where not exists (select 1 from platform.outbox_message where tenant_id=$8 and message_type=$2 and causation_id=$3)", scope.Connection, scope.Transaction);
        Add(outbox, Guid.CreateVersion7(), messageType, batchId, entity, employeeIds, amount, currency, tenant.Value, DateTimeOffset.UtcNow);
        await outbox.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<ExpenseReimbursementBatchDto>> ListReimbursementBatchesAsync(TenantId tenant, Guid entity, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("select id,legal_entity_id,state,currency_code,total_amount,version,created_at from business_expenses.expense_reimbursement_batch where tenant_id=$1 and legal_entity_id=$2 order by created_at desc", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, entity);
        var list = new List<ExpenseReimbursementBatchDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetInt64(5), r.GetFieldValue<DateTimeOffset>(6)));
        await r.CloseAsync();
        await scope.Transaction.CommitAsync(ct);
        return list;
    }

    private async Task<ExpenseReimbursementBatchDto?> GetReimbursementBatchAsync(TenantId tenant, Guid entity, Guid batchId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,state,currency_code,total_amount,version,created_at from business_expenses.expense_reimbursement_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, batchId); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? new ExpenseReimbursementBatchDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetInt64(5), reader.GetFieldValue<DateTimeOffset>(6)) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

}

public sealed class ExpenseRepositoryException(string code) : Exception(code) { public string Code { get; } = code; }
