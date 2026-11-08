using Npgsql;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Ledger;
using Shared.Contracts.Payments;
using Shared.Contracts.Platform;
using Modules.Expenses.DTOs;

namespace Modules.Expenses.Repositories;

public sealed class ExpenseRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, ITransactionalLedgerPostingCapability ledgerPosting, IExpensePaymentCapability? expensePayments = null, IPlatformFileReferenceCapability? files = null, IEmployeeRepaymentPaymentCapability? repaymentPayments = null, IBusinessAdvancePaymentCapability? advancePayments = null, IPlatformFileUploadCapability? fileUploads = null, IPlatformApprovalCapability? approvals = null)
{
    public async Task<ExpensePage<ExpenseDto>> ListAsync(TenantId tenant, Guid entity, Guid? employeeId, string? state, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); var offset = (page - 1) * pageSize;
        await using var scope = await OpenAsync(tenant, ct);
        const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or employee_id=$3) and ($4::text is null or expense_state=$4) and ($5::text is null or merchant_name ilike '%'||$5||'%' or coalesce(description,'') ilike '%'||$5||'%')";
        await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(employeeId), Db(state), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,finalized_at from business_expenses.expense where {where} order by created_at desc,id desc offset $6 limit $7", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(employeeId), Db(state), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), offset, pageSize); var items = new List<ExpenseDto>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(ReadExpense(reader)); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(items, total, page, pageSize);
    }

    public async Task<ExpenseDto?> GetAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,finalized_at from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id); await using var r = await cmd.ExecuteReaderAsync(ct); var result = await r.ReadAsync(ct) ? ReadExpense(r) : null; await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(TenantId tenant, Guid entity, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select c.id,c.legal_entity_id,c.code,c.name,c.status,(select v.id from business_expenses.expense_category_version v where v.tenant_id=c.tenant_id and v.category_id=c.id and v.status='Published' order by v.effective_from desc,v.version desc limit 1) from business_expenses.expense_category c where c.tenant_id=$1 and c.legal_entity_id=$2 order by c.code", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity); var list = new List<ExpenseCategoryDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), GuidValue(r, 5))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpenseCategoryDto> CreateCategoryAsync(TenantId tenant, CreateExpenseCategoryRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ExpenseRepositoryException("expenses.category_invalid"); await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_category(tenant_id,id,legal_entity_id,code,name) values($1,$2,$3,$4,$5) returning id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, request.LegalEntityId, request.Code.Trim().ToUpperInvariant(), request.Name.Trim()); await cmd.ExecuteScalarAsync(ct); await scope.Transaction.CommitAsync(ct); return (await ListCategoriesAsync(tenant, request.LegalEntityId, ct)).Single(x => x.Id == id); }
    public async Task<ExpenseCategoryVersionDto> CreateCategoryVersionAsync(TenantId tenant, Guid entity, Guid categoryId, CreateExpenseCategoryVersionRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_category_version(tenant_id,id,category_id,version,status,requires_receipt,receipt_threshold,effective_from,effective_to) select $1,$2,id,coalesce((select max(version)+1 from business_expenses.expense_category_version where tenant_id=$1 and category_id=$3),1),'Draft',$4,$5,$6,$7 from business_expenses.expense_category where tenant_id=$1 and legal_entity_id=$8 and id=$3 returning id,category_id,version,status,requires_receipt,receipt_threshold,effective_from,effective_to", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, categoryId, request.RequiresReceipt, Db(request.ReceiptThreshold), request.EffectiveFrom, Db(request.EffectiveTo), entity); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.category_not_found"); var result = new ExpenseCategoryVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetBoolean(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseCategoryVersionDto> PublishCategoryVersionAsync(TenantId tenant, Guid entity, Guid versionId, PublishExpenseCategoryVersionRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_category_version v set status='Published',effective_to=coalesce($4,effective_to) from business_expenses.expense_category c where v.tenant_id=$1 and v.id=$2 and v.category_id=c.id and c.legal_entity_id=$3 and v.status='Draft' returning v.id,v.category_id,v.version,v.status,v.requires_receipt,v.receipt_threshold,v.effective_from,v.effective_to", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, versionId, entity, Db(request.EffectiveTo)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.category_version_not_publishable"); var result = new ExpenseCategoryVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetBoolean(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    public async Task<ExpenseDto> CreateAsync(TenantId tenant, CreateExpenseRequest request, CancellationToken ct)
    { if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.MerchantName) || string.IsNullOrWhiteSpace(request.CurrencyCode)) throw new ExpenseRepositoryException("expenses.invalid_amount"); await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense(tenant_id,id,legal_entity_id,employee_id,merchant_name,expense_date,amount,currency_code,payment_source,category_version_id,expense_state,approval_state,accounting_state,settlement_state,evidence_state,policy_state,business_purpose,description,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Draft','Pending','NotFinalized','NotSettled','Missing','NotEvaluated',$11,$12,1,now(),now())", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, request.LegalEntityId, Db(request.EmployeeId), request.MerchantName.Trim(), request.ExpenseDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.PaymentSource.Trim(), request.CategoryVersionId, Db(request.BusinessPurpose), Db(request.Description)); await cmd.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); return (await GetAsync(tenant, request.LegalEntityId, id, ct))!; }

    public async Task<ExpenseDto> UpdateAsync(TenantId tenant, Guid entity, Guid id, UpdateExpenseRequest request, CancellationToken ct)
    { if (request.Amount <= 0) throw new ExpenseRepositoryException("expenses.invalid_amount"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense set merchant_name=$4,expense_date=$5,amount=$6,currency_code=$7,category_version_id=$8,business_purpose=$9,description=$10,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$11 and expense_state='Draft' returning id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id, request.MerchantName.Trim(), request.ExpenseDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.CategoryVersionId, Db(request.BusinessPurpose), Db(request.Description), request.Version); if (await cmd.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.concurrent_change_or_not_editable"); await scope.Transaction.CommitAsync(ct); return (await GetAsync(tenant, entity, id, ct))!; }

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
        await using var command = new NpgsqlCommand("select e.version,e.approval_state,e.expense_state,e.approval_request_id,pe.policy_version_id,(select membership_id from business_hr.employee he where he.tenant_id=e.tenant_id and he.id=e.employee_id) from business_expenses.expense e left join business_expenses.expense_policy_evaluation pe on pe.tenant_id=e.tenant_id and pe.legal_entity_id=e.legal_entity_id and pe.expense_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, expenseId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
        var version = reader.GetInt64(0); var approvalState = reader.GetString(1); var expenseState = reader.GetString(2); var linkedApprovalId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3); var policyVersionId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4); var employeeMembership = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
        await reader.CloseAsync();
        if (expenseState != "Submitted" || approvalState != "Pending") throw new ExpenseRepositoryException("expenses.approval_request_invalid_state");
        if (employeeMembership.HasValue && employeeMembership.Value != membershipId) throw new ExpenseRepositoryException("expenses.approval_request_actor_scope_invalid");
        await scope.Transaction.CommitAsync(ct);
        if (linkedApprovalId.HasValue)
        {
            var existing = await approvals.GetAsync(tenant, linkedApprovalId.Value, ct) ?? throw new ExpenseRepositoryException("expenses.approval_request_not_found");
            if (existing.OwnerModule != "expenses" || existing.OwnerEntityType != "expense" || existing.OwnerEntityId != expenseId || existing.LegalEntityId != entity || existing.ResourceVersion != version)
                throw new ExpenseRepositoryException("expenses.approval_scope_invalid");
            return ToExpenseApproval(existing, expenseId);
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
        await using (var preflight = await OpenAsync(tenant, ct))
        {
            await using var preflightCommand = new NpgsqlCommand("select e.version,e.expense_state,e.approval_state,e.approval_request_id from business_expenses.expense e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3", preflight.Connection, preflight.Transaction);
            Add(preflightCommand, tenant.Value, entity, expenseId);
            await using var reader = await preflightCommand.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            var state = reader.GetString(1); var approvalState = reader.GetString(2); var linkedRequest = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            await reader.CloseAsync(); await preflight.Transaction.CommitAsync(ct);
            if (state != "Submitted" || approvalState != "Pending" || linkedRequest != request.RequestId) throw new ExpenseRepositoryException("expenses.approval_scope_or_state_invalid");
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
        await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,$5,jsonb_build_object('expenseState','Submitted','approvalState',$5,'approvalRequestId',$6::text))", scope.Connection, scope.Transaction);
        Add(history, tenant.Value, Guid.CreateVersion7(), entity, expenseId, approval.State, approval.Id);
        await history.ExecuteNonQueryAsync(ct);
        await scope.Transaction.CommitAsync(ct);
        return ToExpenseApproval(approval, expenseId);
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
            await using var row = new NpgsqlCommand("select e.amount,e.currency_code,e.expense_date,e.payment_source,e.employee_id,e.expense_state,e.approval_state,e.version,e.category_version_id,e.evidence_state,c.code from business_expenses.expense e join business_expenses.expense_category_version cv on cv.tenant_id=e.tenant_id and cv.id=e.category_version_id join business_expenses.expense_category c on c.tenant_id=cv.tenant_id and c.id=cv.category_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 for update", context.Connection, context.Transaction);
            Add(row, tenant.Value, entity, id);
            await using var reader = await row.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            var amount = reader.GetDecimal(0); var currency = reader.GetString(1); var date = reader.GetFieldValue<DateOnly>(2); var source = reader.GetString(3); var employee = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4); var state = reader.GetString(5); var approval = reader.GetString(6); var version = reader.GetInt64(7); var categoryVersionId = reader.GetGuid(8); var evidenceState = reader.GetString(9); var category = reader.GetString(10);
            await reader.CloseAsync();
            if (version != request.Version || state != "Submitted" || (approvals is null && approval != "Approved")) throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency");
            var policyState = "Passed";
            Guid? applicablePolicyVersion = null;
            await using (var applicable = new NpgsqlCommand("select id from business_expenses.expense_policy_version where tenant_id=$1 and legal_entity_id=$2 and status='Published' and effective_from <= $3 and (effective_to is null or effective_to >= $3) order by effective_from desc,version desc,id desc limit 1", context.Connection, context.Transaction))
            { Add(applicable, tenant.Value, entity, date); var applicableValue = await applicable.ExecuteScalarAsync(token); applicablePolicyVersion = applicableValue is Guid g ? g : null; }
            Guid? approvalRequestId = null;
            await using (var approvalLink = new NpgsqlCommand("select approval_request_id from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(approvalLink, tenant.Value, entity, id); var value = await approvalLink.ExecuteScalarAsync(token); approvalRequestId = value is Guid g ? g : null; }
            if (applicablePolicyVersion.HasValue)
            {
                await using var policy = new NpgsqlCommand("select state,policy_version_id,snapshot::text from business_expenses.expense_policy_evaluation where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", context.Connection, context.Transaction);
                Add(policy, tenant.Value, entity, id); await using var policyReader = await policy.ExecuteReaderAsync(token);
                if (!await policyReader.ReadAsync(token)) throw new ExpenseRepositoryException("expenses.policy_evaluation_required");
                policyState = policyReader.GetString(0); var evaluatedVersion = policyReader.IsDBNull(1) ? (Guid?)null : policyReader.GetGuid(1); var snapshot = System.Text.Json.JsonDocument.Parse(policyReader.GetString(2)).RootElement.Clone(); await policyReader.CloseAsync();
                if (policyState == "Blocked") throw new ExpenseRepositoryException("expenses.policy_blocked");
                if (evaluatedVersion != applicablePolicyVersion || !SnapshotMatches(snapshot, version, amount, currency, source, employee, categoryVersionId, evidenceState)) throw new ExpenseRepositoryException("expenses.policy_evaluation_stale");
            }
            if (approvals is not null)
            {
                if (!approvalRequestId.HasValue) throw new ExpenseRepositoryException("expenses.platform_approval_required");
                var platformApproval = await approvals.GetAsync(tenant, approvalRequestId.Value, token);
                if (platformApproval is null || platformApproval.State != "Approved" || platformApproval.OwnerModule != "expenses" || platformApproval.OwnerEntityType != "expense" || platformApproval.OwnerEntityId != id || platformApproval.LegalEntityId != entity || platformApproval.PolicyVersionId != applicablePolicyVersion || platformApproval.ResourceVersion < version - 1 || platformApproval.ResourceVersion > version)
                    throw new ExpenseRepositoryException("expenses.platform_approval_stale_or_invalid");
            }
            var factId = StableFactId(id);
            var payload = new ExpenseRecognizedPayload(amount, currency, category, source == "EmployeePaid" ? "EmployeeReimbursementPayable" : "Cash");
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new LegalEntityId(entity), AccountingFactTypes.ExpenseRecognized, 1, "expenses", id.ToString("D"), $"expense-finalize:{id:D}", date, date, payload, Guid.CreateVersion7(), Guid.Empty);
            var posted = await ledgerPosting.PostExpenseAsync(context, fact, payload, token);
            await using (var update = new NpgsqlCommand("update business_expenses.expense set expense_state='Finalized',accounting_state='Posted',accounting_fact_id=$4,ledger_entry_id=$5,settlement_state=case when payment_source='EmployeePaid' then 'ReimbursementPending' else 'NotApplicable' end,policy_state=$6,version=version+1,finalized_at=now(),updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$7 and expense_state='Submitted' and approval_state='Approved'", context.Connection, context.Transaction))
            { Add(update, tenant.Value, entity, id, posted.FactId, posted.JournalEntryId, policyState, request.Version); if (await update.ExecuteNonQueryAsync(token) == 0) throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency"); }
            await using (var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'finalize',jsonb_build_object('expenseState','Finalized','approvalState','Approved','accountingState','Posted','ledgerEntryId',$5::text))", context.Connection, context.Transaction))
            { Add(history, tenant.Value, Guid.CreateVersion7(), entity, id, posted.JournalEntryId); await history.ExecuteNonQueryAsync(token); }
            await using var obligation = new NpgsqlCommand("insert into business_expenses.expense_reimbursement_obligation(tenant_id,id,legal_entity_id,expense_id,employee_id,amount,currency_code,status) select tenant_id,$4,legal_entity_id,id,employee_id,amount,currency_code,'Pending' from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and payment_source='EmployeePaid' and employee_id is not null on conflict(tenant_id,expense_id) do nothing", context.Connection, context.Transaction);
            Add(obligation, tenant.Value, entity, id, Guid.CreateVersion7()); await obligation.ExecuteNonQueryAsync(token);
            return posted;
        }, ct);
        return (await GetAsync(tenant, entity, id, ct))!;
    }

    private async Task<ExpenseDto> ReverseAsync(TenantId tenant, Guid entity, Guid id, ExpenseActionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ExpenseRepositoryException("expenses.reversal_reason_required");
        Guid ledgerEntry; long version;
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockCommand = new NpgsqlCommand("select ledger_entry_id,version,expense_state,accounting_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(lockCommand, tenant.Value, entity, id);
            await using var reader = await lockCommand.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found");
            if (reader.IsDBNull(0) || reader.GetString(2) != "Finalized" || reader.GetString(3) != "Posted" || reader.GetInt64(1) != request.Version) throw new ExpenseRepositoryException("expenses.invalid_transition_or_concurrency");
            ledgerEntry = reader.GetGuid(0); version = reader.GetInt64(1); await reader.CloseAsync();
            await using var pending = new NpgsqlCommand("update business_expenses.expense set accounting_state='ReversalPending',version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$4", scope.Connection, scope.Transaction); Add(pending, tenant.Value, entity, id, version); await pending.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct);
        }
        var reversal = await ledgerPosting.ReverseAsync(tenant, new LegalEntityId(entity), ledgerEntry, DateOnly.FromDateTime(DateTime.UtcNow), null, request.Reason!, Guid.Empty, Guid.CreateVersion7(), ct);
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var update = new NpgsqlCommand("update business_expenses.expense set expense_state='Reversed',accounting_state='Reversed',reversal_entry_id=$4,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and accounting_state='ReversalPending'", scope.Connection, scope.Transaction); Add(update, tenant.Value, entity, id, reversal.JournalEntryId); if (await update.ExecuteNonQueryAsync(ct) == 0) throw new ExpenseRepositoryException("expenses.reversal_state_conflict");
            await using var history = new NpgsqlCommand("insert into business_expenses.expense_history(tenant_id,id,legal_entity_id,expense_id,action,to_state) values($1,$2,$3,$4,'reverse',jsonb_build_object('expenseState','Reversed','accountingState','Reversed','reversalEntryId',$5::text))", scope.Connection, scope.Transaction); Add(history, tenant.Value, Guid.CreateVersion7(), entity, id, reversal.JournalEntryId); await history.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct);
        }
        return (await GetAsync(tenant, entity, id, ct))!;
    }

    public async Task<IReadOnlyList<ExpenseEvidenceDto>> ListEvidenceAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,evidence_type,file_reference,status,created_at from business_expenses.expense_evidence where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by created_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseEvidenceDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
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
        await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_evidence(tenant_id,id,legal_entity_id,expense_id,evidence_type,file_reference,status,created_at) select $1,$2,$3,id,$4,$5,'Ready',now() from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$6 returning id,expense_id,evidence_type,file_reference,status,created_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, entity, request.EvidenceType.Trim(), fileReference, expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var result = new ExpenseEvidenceDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5)); await r.CloseAsync(); await using var mark = new NpgsqlCommand("update business_expenses.expense set evidence_state='Provided',updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(mark, tenant.Value, entity, expenseId); await mark.ExecuteNonQueryAsync(ct); await using var resolve = new NpgsqlCommand("update business_expenses.expense_missing_receipt_declaration set status='Resolved',resolved_at=now() where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and status='Declared'", scope.Connection, scope.Transaction); Add(resolve, tenant.Value, entity, expenseId); await resolve.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<ExpenseEvidenceUploadIntentDto> CreateEvidenceUploadIntentAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, CreateExpenseEvidenceUploadRequest request, CancellationToken ct)
    {
        if (fileUploads is null) throw new ExpenseRepositoryException("expenses.files_capability_unavailable");
        if (request.SizeBytes <= 0 || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentType)) throw new ExpenseRepositoryException("expenses.file_upload_invalid");
        await EnsureExpenseExistsAsync(tenant, entity, expenseId, ct);
        var intent = await fileUploads.CreateUploadIntentAsync(tenant, actorId, membershipId, "expenses", "expense", expenseId, request.FileName.Trim(), request.ContentType.Trim(), request.SizeBytes, request.Classification.Trim(), ct);
        return intent is null ? throw new ExpenseRepositoryException("expenses.file_upload_unavailable") : new(intent.FileRef, intent.Url, intent.Fields, intent.ContentType, intent.SizeBytes, intent.ExpiresAt);
    }

    public async Task<ExpenseEvidenceDto> CompleteEvidenceUploadAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid entity, Guid expenseId, CompleteExpenseEvidenceUploadRequest request, CancellationToken ct)
    {
        if (fileUploads is null) throw new ExpenseRepositoryException("expenses.files_capability_unavailable");
        var completion = await fileUploads.CompleteUploadAsync(tenant, actorId, membershipId, "expenses", "expense", expenseId, request.FileRef, ct);
        if (completion is null || !completion.Ready) throw new ExpenseRepositoryException(completion?.ReasonCode ?? "expenses.file_not_ready");
        return await AddEvidenceAsync(tenant, entity, expenseId, new AddExpenseEvidenceRequest(request.EvidenceType, request.FileRef.ToString("D")), ct);
    }

    public async Task<MissingReceiptDeclarationDto> DeclareMissingReceiptAsync(TenantId tenant, Guid actorId, Guid entity, Guid expenseId, DeclareMissingReceiptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5) throw new ExpenseRepositoryException("expenses.missing_receipt_reason_required");
        await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7();
        await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_missing_receipt_declaration(tenant_id,id,legal_entity_id,expense_id,reason,status,declared_by,declared_at) select $1,$2,$3,id,$4,'Declared',$5,now() from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$6 on conflict(tenant_id,expense_id) do update set reason=excluded.reason,status='Declared',declared_by=excluded.declared_by,declared_at=now(),resolved_at=null returning id,expense_id,reason,status,declared_by,declared_at,resolved_at", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, id, entity, request.Reason.Trim(), actorId, expenseId); await using var reader = await cmd.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var result = ReadMissingReceipt(reader); await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<MissingReceiptDeclarationDto?> GetMissingReceiptDeclarationAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,reason,status,declared_by,declared_at,resolved_at from business_expenses.expense_missing_receipt_declaration where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); await using var reader = await cmd.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? ReadMissingReceipt(reader) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    private async Task EnsureExpenseExistsAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select 1 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); if (await cmd.ExecuteScalarAsync(ct) is null) throw new ExpenseRepositoryException("expenses.expense_not_found"); await scope.Transaction.CommitAsync(ct); }

    public async Task<SignedPlatformFileDownload> CreateEvidenceDownloadAsync(TenantId tenant, Guid entity, Guid expenseId, Guid evidenceId, CancellationToken ct)
    {
        if (files is null) throw new ExpenseRepositoryException("expenses.files_capability_unavailable");
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select file_reference from business_expenses.expense_evidence where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, expenseId, evidenceId); var value = await command.ExecuteScalarAsync(ct); await scope.Transaction.CommitAsync(ct); if (value is not string reference || !Guid.TryParse(reference, out var fileRef)) throw new ExpenseRepositoryException("expenses.file_reference_invalid");
        var download = await files.CreateReadyDownloadAsync(tenant, fileRef, "expenses", "expense", expenseId, TimeSpan.FromMinutes(5), ct); return download ?? throw new ExpenseRepositoryException("expenses.file_not_ready");
    }

    private async Task<Scope> OpenAsync(TenantId tenant, CancellationToken ct) { var c = await dataSource.OpenConnectionAsync(ct); var t = await c.BeginTransactionAsync(ct); await using var set = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", c, t); Add(set, tenant.Value.ToString("D")); await set.ExecuteNonQueryAsync(ct); return new(c, t); }
    private static ExpenseDto ReadExpense(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), GuidValue(r, 2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.GetDecimal(5), r.GetString(6), r.GetString(7), r.GetGuid(8), r.GetString(9), r.GetString(10), r.GetString(11), r.GetString(12), r.GetString(13), r.GetString(14), Text(r, 15), Text(r, 16), r.GetInt64(17), r.GetFieldValue<DateTimeOffset>(18), r.IsDBNull(19) ? null : r.GetFieldValue<DateTimeOffset>(19));
    private static Guid? GuidValue(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i); private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i); private static object Db(object? v) => v ?? DBNull.Value; private static void Add(NpgsqlCommand c, params object?[] values) { foreach (var value in values) c.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static MissingReceiptDeclarationDto ReadMissingReceipt(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetGuid(4), r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6));
    private static bool SnapshotMatches(System.Text.Json.JsonElement snapshot, long version, decimal amount, string currency, string source, Guid? employee, Guid categoryVersionId, string evidenceState)
    { return snapshot.TryGetProperty("expenseVersion", out var v) && (v.GetInt64() == version || v.GetInt64() == version - 1) && snapshot.TryGetProperty("amount", out var a) && a.GetDecimal() == amount && snapshot.TryGetProperty("currencyCode", out var c) && string.Equals(c.GetString(), currency, StringComparison.OrdinalIgnoreCase) && snapshot.TryGetProperty("paymentSource", out var p) && string.Equals(p.GetString(), source, StringComparison.Ordinal) && snapshot.TryGetProperty("evidenceState", out var es) && string.Equals(es.GetString(), evidenceState, StringComparison.Ordinal) && snapshot.TryGetProperty("categoryVersionId", out var cv) && Guid.TryParse(cv.GetString(), out var category) && category == categoryVersionId && ((employee is null && (!snapshot.TryGetProperty("employeeId", out var e) || e.ValueKind == System.Text.Json.JsonValueKind.Null)) || (employee is not null && snapshot.TryGetProperty("employeeId", out var actual) && Guid.TryParse(actual.GetString(), out var actualEmployee) && actualEmployee == employee)); }
    private static void ValidatePolicyRules(System.Text.Json.JsonElement rules)
    {
        if (rules.ValueKind != System.Text.Json.JsonValueKind.Object) throw new ExpenseRepositoryException("expenses.policy_rules_invalid");
        foreach (var property in rules.EnumerateObject())
        {
            if (property.NameEquals("maxAmount")) { if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Number || property.Value.GetDecimal() < 0) throw new ExpenseRepositoryException("expenses.policy_max_amount_invalid"); continue; }
            if (property.NameEquals("receiptRequired")) { if (property.Value.ValueKind != System.Text.Json.JsonValueKind.True && property.Value.ValueKind != System.Text.Json.JsonValueKind.False) throw new ExpenseRepositoryException("expenses.policy_receipt_rule_invalid"); continue; }
            throw new ExpenseRepositoryException("expenses.policy_rule_unsupported");
        }
    }
    private static Guid StableFactId(Guid expenseId) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"expenses:{expenseId:D}")).AsSpan(0, 16));
    private sealed class Scope(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable { public NpgsqlConnection Connection { get; } = connection; public NpgsqlTransaction Transaction { get; } = transaction; public async ValueTask DisposeAsync() { await Transaction.DisposeAsync(); await Connection.DisposeAsync(); } }
    public async Task<IReadOnlyList<ExpenseItemizationDto>> ListItemizationsAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,description,amount,tax_amount,currency_code from business_expenses.expense_itemization where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseItemizationDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpenseItemizationDto> AddItemizationAsync(TenantId tenant, Guid entity, Guid expenseId, CreateExpenseItemizationRequest request, CancellationToken ct)
    { if (request.Amount < 0 || request.TaxAmount < 0 || string.IsNullOrWhiteSpace(request.Description)) throw new ExpenseRepositoryException("expenses.itemization_invalid"); await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_itemization(tenant_id,id,legal_entity_id,expense_id,description,amount,currency_code,tax_amount) select $1,$2,$3,id,$4,$5,$6,$7 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$8 and expense_state='Draft' returning id,expense_id,description,amount,tax_amount,currency_code", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, entity, request.Description.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.TaxAmount, expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.itemization_not_editable"); var result = new ExpenseItemizationDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task DeleteItemizationAsync(TenantId tenant, Guid entity, Guid expenseId, Guid itemizationId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("delete from business_expenses.expense_itemization where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 and id=$4 and exists(select 1 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft')", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId, itemizationId); if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new ExpenseRepositoryException("expenses.itemization_not_editable"); await scope.Transaction.CommitAsync(ct); }

    public async Task<ExpenseParticipantDto> AddParticipantAsync(TenantId tenant, Guid entity, Guid expenseId, AddExpenseParticipantRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new ExpenseRepositoryException("expenses.participant_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_participant(tenant_id,id,legal_entity_id,expense_id,party_id,display_name,role) select $1,$2,$3,id,$4,$5,$6 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$3 and id=$7 and expense_state='Draft' returning id,expense_id,party_id,display_name,role", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), entity, Db(request.PartyId), request.DisplayName.Trim(), request.Role.Trim(), expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.participant_not_editable"); var result = new ExpenseParticipantDto(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<IReadOnlyList<ExpenseParticipantDto>> ListParticipantsAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,expense_id,party_id,display_name,role from business_expenses.expense_participant where tenant_id=$1 and legal_entity_id=$2 and expense_id=$3 order by created_at,id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, expenseId); var list = new List<ExpenseParticipantDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return list; }
    public async Task<ExpensePolicyDto> CreatePolicyAsync(TenantId tenant, CreateExpensePolicyRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ExpenseRepositoryException("expenses.policy_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_policy(tenant_id,id,legal_entity_id,code,name) values($1,$2,$3,$4,$5) returning id,legal_entity_id,code,name,status", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, request.Code.Trim().ToUpperInvariant(), request.Name.Trim()); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_invalid"); var result = new ExpensePolicyDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePolicyVersionDto> CreatePolicyVersionAsync(TenantId tenant, Guid entity, Guid policyId, CreateExpensePolicyVersionRequest request, CancellationToken ct)
    { ValidatePolicyRules(request.Rules); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_policy_version(tenant_id,id,legal_entity_id,policy_id,version,rules,effective_from,effective_to) select $1,$2,p.legal_entity_id,p.id,coalesce((select max(version)+1 from business_expenses.expense_policy_version where tenant_id=$1 and policy_id=$3),1),$4::jsonb,$5,$6 from business_expenses.expense_policy p where p.tenant_id=$1 and p.legal_entity_id=$7 and p.id=$3 returning id,policy_id,version,status,effective_from,effective_to,rules::text", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), policyId, request.Rules.GetRawText(), request.EffectiveFrom, Db(request.EffectiveTo), entity); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_not_found"); var result = new ExpensePolicyVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5), System.Text.Json.JsonDocument.Parse(r.GetString(6)).RootElement.Clone()); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePolicyVersionDto> PublishPolicyVersionAsync(TenantId tenant, Guid entity, Guid versionId, PublishExpensePolicyVersionRequest request, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.expense_policy_version v set status='Published',effective_to=coalesce($4,effective_to) where v.tenant_id=$1 and v.id=$2 and v.legal_entity_id=$3 and v.status='Draft' returning v.id,v.policy_id,v.version,v.status,v.effective_from,v.effective_to,v.rules::text", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, versionId, entity, Db(request.EffectiveTo)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.policy_version_not_publishable"); var result = new ExpensePolicyVersionDto(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5), System.Text.Json.JsonDocument.Parse(r.GetString(6)).RootElement.Clone()); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePolicyEvaluationDto> EvaluatePolicyAsync(TenantId tenant, Guid entity, Guid expenseId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); var id = Guid.CreateVersion7(); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_policy_evaluation(tenant_id,id,legal_entity_id,expense_id,policy_version_id,state,snapshot) select $1,$2,$3,e.id,v.id,case when jsonb_typeof(v.rules->'maxAmount')='number' and e.amount > (v.rules->>'maxAmount')::numeric then 'Blocked' when coalesce((v.rules->>'receiptRequired')::boolean,false) and e.evidence_state='Missing' then 'Warning' else 'Passed' end,jsonb_build_object('evidenceState',e.evidence_state,'amount',e.amount,'currencyCode',e.currency_code,'paymentSource',e.payment_source,'employeeId',e.employee_id,'categoryVersionId',e.category_version_id,'expenseVersion',e.version,'policyVersionId',v.id,'rules',coalesce(v.rules,'{}'::jsonb),'evaluatedAt',now()) from business_expenses.expense e left join lateral (select pv.id,pv.rules from business_expenses.expense_policy_version pv join business_expenses.expense_policy p on p.tenant_id=pv.tenant_id and p.id=pv.policy_id where pv.tenant_id=e.tenant_id and pv.legal_entity_id=e.legal_entity_id and pv.status='Published' and pv.effective_from <= e.expense_date and (pv.effective_to is null or pv.effective_to >= e.expense_date) order by pv.effective_from desc,pv.version desc,pv.id desc limit 1) v on true where e.tenant_id=$1 and e.legal_entity_id=$3 and e.id=$4 on conflict(tenant_id,expense_id) do update set policy_version_id=excluded.policy_version_id,state=excluded.state,snapshot=excluded.snapshot,evaluated_at=now() returning id,expense_id,state,snapshot,evaluated_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, id, entity, expenseId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var result = new ExpensePolicyEvaluationDto(r.GetGuid(0), r.GetGuid(1), r.GetString(2), System.Text.Json.JsonDocument.Parse(r.GetString(3)).RootElement.Clone(), r.GetFieldValue<DateTimeOffset>(4)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<ExpenseReportDto>> ListReportsAsync(TenantId tenant, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or title ilike '%'||$4||'%')"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_report where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at from business_expenses.expense_report where {where} order by created_at desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpenseReportDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetString(6), r.GetInt64(7), r.GetFieldValue<DateTimeOffset>(8))); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }
    public async Task<string> ExportReportsCsvAsync(TenantId tenant, Guid entity, string? state, string? search, CancellationToken ct)
    { var builder = new StringBuilder("id,title,state,total_amount,currency_code,created_at\n"); for (var pageNumber = 1; ; pageNumber++) { var page = await ListReportsAsync(tenant, entity, state, search, pageNumber, 200, ct); foreach (var item in page.Items) builder.Append(Csv(item.Id.ToString("D"))).Append(',').Append(Csv(item.Title)).Append(',').Append(Csv(item.State)).Append(',').Append(item.TotalAmount.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Csv(item.CurrencyCode)).Append(',').Append(item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)).Append('\n'); if (page.Items.Count < page.PageSize) break; } return builder.ToString(); }
    private static string Csv(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) ? '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"' : value;
    public async Task<ExpenseExceptionSummaryDto> GetExceptionSummaryAsync(TenantId tenant, Guid entity, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select (select count(*) from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and expense_state='Submitted' and approval_state='Pending'),(select count(*) from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and expense_state in ('Draft','Submitted') and evidence_state='Missing'),(select count(*) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and legal_entity_id=$2 and status in ('Pending','PartiallySettled')),(select coalesce(sum(amount-settled_amount),0) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and legal_entity_id=$2 and status in ('Pending','PartiallySettled')),(select count(*) from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and state in ('Requested','Pending')),(select coalesce(sum(amount),0) from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and state in ('Requested','Pending')),(select count(*) from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and state in ('Issued','PartiallySettled')),(select coalesce(sum(amount-applied_amount-returned_amount),0) from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and state in ('Issued','PartiallySettled'))", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_unavailable"); var result = new ExpenseExceptionSummaryDto(checked((int)r.GetInt64(0)), checked((int)r.GetInt64(1)), checked((int)r.GetInt64(2)), r.GetDecimal(3), checked((int)r.GetInt64(4)), r.GetDecimal(5), checked((int)r.GetInt64(6)), r.GetDecimal(7)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseReportDto> CreateReportAsync(TenantId tenant, CreateExpenseReportRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Title)) throw new ExpenseRepositoryException("expenses.report_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_report(tenant_id,id,legal_entity_id,employee_id,title,currency_code) values($1,$2,$3,$4,$5,$6) returning id,legal_entity_id,employee_id,title,state,total_amount,currency_code,version,created_at", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, Db(request.EmployeeId), request.Title.Trim(), request.CurrencyCode.Trim().ToUpperInvariant()); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.report_invalid"); var result = new ExpenseReportDto(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetString(6), r.GetInt64(7), r.GetFieldValue<DateTimeOffset>(8)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<BusinessAdvanceDto> CreateAdvanceAsync(TenantId tenant, CreateBusinessAdvanceRequest request, CancellationToken ct)
    { if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Purpose)) throw new ExpenseRepositoryException("expenses.advance_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.business_advance(tenant_id,id,legal_entity_id,employee_id,purpose,amount,currency_code) values($1,$2,$3,$4,$5,$6,$7) returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, request.EmployeeId, request.Purpose.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant()); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_invalid"); var result = ReadAdvance(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<BusinessAdvanceDto>> ListAdvancesAsync(TenantId tenant, Guid entity, string? state, Guid? employeeId, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::uuid is null or employee_id=$4)"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.business_advance where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(employeeId)); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id from business_expenses.business_advance where {where} order by created_at desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(employeeId), (page - 1) * pageSize, pageSize); var list = new List<BusinessAdvanceDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(ReadAdvance(r)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }
    public async Task<BusinessAdvanceDto> TransitionAdvanceAsync(TenantId tenant, Guid entity, Guid id, long version, string action, CancellationToken ct)
    { var next = action switch { "approve" => "Approved", "settle" => "Settled", _ => throw new ExpenseRepositoryException("expenses.advance_action_invalid") }; await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_expenses.business_advance set state=$4,version=version+1,settled_at=case when $4='Settled' then now() else settled_at end where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 and state <> 'Settled' and ($4 <> 'Settled' or (state='PartiallySettled' and applied_amount+returned_amount >= issued_amount)) returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, id, next, version); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_transition_invalid"); var result = ReadAdvance(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
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
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var lockIssuance = new NpgsqlCommand("select state from business_expenses.business_advance_issuance where tenant_id=$1 and advance_id=$2 and idempotency_key=$3 for update", scope.Connection, scope.Transaction);
            Add(lockIssuance, tenant.Value, advanceId, idempotencyKey);
            var issuanceState = (string?)await lockIssuance.ExecuteScalarAsync(ct) ?? throw new ExpenseRepositoryException("expenses.advance_issuance_unavailable");
            if (issuanceState == "Completed")
            {
                await using var completedAdvance = new NpgsqlCommand("select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
                Add(completedAdvance, tenant.Value, entity, advanceId); await using var completedReader = await completedAdvance.ExecuteReaderAsync(ct); if (!await completedReader.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found"); var completedResult = ReadAdvance(completedReader); await completedReader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return completedResult;
            }
            await using var markIssuance = new NpgsqlCommand("update business_expenses.business_advance_issuance set state='Completed',payment_id=$4,completed_at=now() where tenant_id=$1 and advance_id=$2 and idempotency_key=$3 and state <> 'Completed'", scope.Connection, scope.Transaction);
            Add(markIssuance, tenant.Value, advanceId, idempotencyKey, payment.PaymentId); if (await markIssuance.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.advance_issuance_conflict");
            await using var complete = new NpgsqlCommand("update business_expenses.business_advance set issued_amount=issued_amount+$4,state=case when issued_amount+$4 >= amount then 'Issued' else 'PartiallyIssued' end,issued_at=coalesce(issued_at,now()),issuance_payment_id=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='IssuancePending' and issued_amount+$4 <= amount returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id", scope.Connection, scope.Transaction);
            Add(complete, tenant.Value, entity, advanceId, payment.Amount, payment.PaymentId);
            await using var row = await complete.ExecuteReaderAsync(ct);
            if (!await row.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_completion_conflict");
            var result = ReadAdvance(row); await row.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
        }
    }
    private async Task ResetAdvanceIssuanceAsync(TenantId tenant, Guid entity, Guid advanceId, CancellationToken ct) { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("update business_expenses.business_advance set state='Approved',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='IssuancePending'", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, advanceId); await command.ExecuteNonQueryAsync(ct); await using var failed = new NpgsqlCommand("update business_expenses.business_advance_issuance set state='Failed' where tenant_id=$1 and advance_id=$2 and state='Pending'", scope.Connection, scope.Transaction); Add(failed, tenant.Value, advanceId); await failed.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); }
    public async Task<BusinessAdvanceDto> ApplyAdvanceAsync(TenantId tenant, Guid entity, Guid advanceId, ApplyBusinessAdvanceRequest request, CancellationToken ct)
    { if (request.Amount <= 0 || request.ExpenseId == Guid.Empty) throw new ExpenseRepositoryException("expenses.advance_application_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var advance = new NpgsqlCommand("select employee_id,amount,issued_amount,applied_amount,returned_amount,currency_code,state from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(advance, tenant.Value, entity, advanceId); await using var ar = await advance.ExecuteReaderAsync(ct); if (!await ar.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found"); var employee = ar.GetGuid(0); var total = ar.GetDecimal(1); var issued = ar.GetDecimal(2); var applied = ar.GetDecimal(3); var returned = ar.GetDecimal(4); var currency = ar.GetString(5); var state = ar.GetString(6); await ar.CloseAsync(); if (state is not ("Issued" or "PartiallyIssued" or "PartiallySettled") || applied + returned + request.Amount > issued) throw new ExpenseRepositoryException("expenses.advance_amount_exceeded"); await using var expense = new NpgsqlCommand("select employee_id,currency_code,expense_state from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction); Add(expense, tenant.Value, entity, request.ExpenseId); await using var er = await expense.ExecuteReaderAsync(ct); if (!await er.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.expense_not_found"); var expenseEmployee = er.IsDBNull(0) ? (Guid?)null : er.GetGuid(0); var expenseCurrency = er.GetString(1); var expenseState = er.GetString(2); await er.CloseAsync(); if (expenseEmployee != employee || !string.Equals(currency, expenseCurrency, StringComparison.OrdinalIgnoreCase) || expenseState is not ("Submitted" or "Approved" or "Finalized")) throw new ExpenseRepositoryException("expenses.advance_expense_invalid"); await using var insert = new NpgsqlCommand("insert into business_expenses.business_advance_application(tenant_id,id,legal_entity_id,advance_id,expense_id,amount) values($1,$2,$3,$4,$5,$6) on conflict(tenant_id,advance_id,expense_id) do update set amount=excluded.amount", scope.Connection, scope.Transaction); Add(insert, tenant.Value, Guid.CreateVersion7(), entity, advanceId, request.ExpenseId, request.Amount); await insert.ExecuteNonQueryAsync(ct); await using var update = new NpgsqlCommand("update business_expenses.business_advance set applied_amount=(select coalesce(sum(amount),0) from business_expenses.business_advance_application where tenant_id=$1 and advance_id=$2),state=case when (select coalesce(sum(amount),0) from business_expenses.business_advance_application where tenant_id=$1 and advance_id=$2)+returned_amount >= issued_amount then 'Settled' else 'PartiallySettled' end,version=version+1 where tenant_id=$1 and legal_entity_id=$3 and id=$2 returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id", scope.Connection, scope.Transaction); Add(update, tenant.Value, advanceId, entity); await using var ur = await update.ExecuteReaderAsync(ct); if (!await ur.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_not_found"); var result = ReadAdvance(ur); await ur.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<BusinessAdvanceDto> ReturnAdvanceAsync(TenantId tenant, Guid entity, Guid advanceId, ReturnBusinessAdvanceRequest request, CancellationToken ct)
    { if (request.Amount <= 0) throw new ExpenseRepositoryException("expenses.advance_return_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var update = new NpgsqlCommand("update business_expenses.business_advance set returned_amount=returned_amount+$4,state=case when applied_amount+returned_amount+$4 >= issued_amount then 'Settled' else 'PartiallySettled' end,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and version=$5 and state in ('Issued','PartiallyIssued','PartiallySettled') and applied_amount+returned_amount+$4 <= issued_amount returning id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id", scope.Connection, scope.Transaction); Add(update, tenant.Value, entity, advanceId, request.Amount, request.Version); await using var r = await update.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.advance_return_invalid"); var result = ReadAdvance(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpenseCardTransactionDto> ImportCardTransactionAsync(TenantId tenant, Guid entity, ImportExpenseCardTransactionRequest request, CancellationToken ct)
    { if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.ProviderReference)) throw new ExpenseRepositoryException("expenses.card_transaction_invalid"); await using var scope = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_expenses.expense_card_transaction(tenant_id,id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict(tenant_id,provider_reference) do update set merchant_name=excluded.merchant_name,amount=excluded.amount returning id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, Guid.CreateVersion7(), entity, request.ProviderReference.Trim(), request.MerchantName.Trim(), request.TransactionDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant()); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_invalid"); var result = ReadCard(r); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }
    public async Task<ExpensePage<ExpenseCardTransactionDto>> ListCardTransactionsAsync(TenantId tenant, Guid entity, string? state, string? search, int page, int pageSize, CancellationToken ct)
    { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenAsync(tenant, ct); const string where = "tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or provider_reference ilike '%'||$4||'%' or merchant_name ilike '%'||$4||'%')"; await using var count = new NpgsqlCommand($"select count(*) from business_expenses.expense_card_transaction where {where}", scope.Connection, scope.Transaction); Add(count, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim())); var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); await using var cmd = new NpgsqlCommand($"select id,legal_entity_id,provider_reference,merchant_name,transaction_date,amount,currency_code,state,employee_id,matched_expense_id from business_expenses.expense_card_transaction where {where} order by transaction_date desc,id desc offset $5 limit $6", scope.Connection, scope.Transaction); Add(cmd, tenant.Value, entity, Db(string.IsNullOrWhiteSpace(state) ? null : state.Trim()), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), (page - 1) * pageSize, pageSize); var list = new List<ExpenseCardTransactionDto>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(ReadCard(r)); await r.CloseAsync(); await scope.Transaction.CommitAsync(ct); return new(list, total, page, pageSize); }

    public async Task<ExpenseCardTransactionDto> MatchCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, MatchExpenseCardTransactionRequest request, CancellationToken ct)
    {
        if (request.ExpenseId == Guid.Empty) throw new ExpenseRepositoryException("expenses.card_match_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId);
        await using var cr = await card.ExecuteReaderAsync(ct);
        if (!await cr.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var amount = cr.GetDecimal(0); var currency = cr.GetString(1); var state = cr.GetString(2); await cr.CloseAsync();
        if (state is not ("Unmatched" or "Allocated")) throw new ExpenseRepositoryException("expenses.card_transaction_not_matchable");
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

    public async Task<ExpenseCardTransactionDto> AllocateCardTransactionAsync(TenantId tenant, Guid entity, Guid cardId, AllocateExpenseCardTransactionRequest request, CancellationToken ct)
    {
        if (request.ExpenseId == Guid.Empty || request.Amount <= 0m) throw new ExpenseRepositoryException("expenses.card_allocation_invalid");
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId); await using var cr = await card.ExecuteReaderAsync(ct);
        if (!await cr.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var cardAmount = cr.GetDecimal(0); var cardCurrency = cr.GetString(1); var cardState = cr.GetString(2); await cr.CloseAsync();
        if (cardState is not ("Unmatched" or "Allocated")) throw new ExpenseRepositoryException("expenses.card_transaction_not_allocatable");
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
        await using var scope = await OpenAsync(tenant, ct);
        await using var card = new NpgsqlCommand("select provider_reference,amount,currency_code,state from business_expenses.expense_card_transaction where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", scope.Connection, scope.Transaction);
        Add(card, tenant.Value, entity, cardId); await using var r = await card.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.card_transaction_not_found");
        var reference = r.GetString(0); var amount = r.GetDecimal(1); var currency = r.GetString(2); var state = r.GetString(3); await r.CloseAsync();
        if (state is not ("Unmatched" or "Personal")) throw new ExpenseRepositoryException("expenses.card_transaction_not_personalizable");
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
        try { payment = await (repaymentPayments ?? throw new ExpenseRepositoryException("expenses.payments_capability_unavailable")).ReceiveEmployeeRepaymentAsync(tenant, actorId, membershipId, Guid.CreateVersion7(), entity, repaymentId, request.PaymentAccountId, request.Method, request.Amount, currency, request.PaymentDate, request.PostingDate, $"expenses-repayment:{repaymentId:D}:{request.Amount.ToString(CultureInfo.InvariantCulture)}", ct); }
        catch { await SetRepaymentFailedAsync(tenant, entity, repaymentId, ct); throw; }
        await using (var scope = await OpenAsync(tenant, ct))
        {
            await using var complete = new NpgsqlCommand("update business_expenses.expense_employee_repayment set settled_amount=settled_amount+$4,state=case when settled_amount+$4 >= amount then 'Recorded' else 'Pending' end,recorded_at=case when settled_amount+$4 >= amount then now() else recorded_at end,payment_id=$5,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Pending' and settled_amount+$4 <= amount", scope.Connection, scope.Transaction); Add(complete, tenant.Value, entity, repaymentId, payment.Amount, payment.PaymentId); if (await complete.ExecuteNonQueryAsync(ct) != 1) throw new ExpenseRepositoryException("expenses.repayment_completion_conflict"); await scope.Transaction.CommitAsync(ct);
        }
        return (await GetRepaymentAsync(tenant, entity, repaymentId, ct))!;
    }

    private async Task SetRepaymentFailedAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("update business_expenses.expense_employee_repayment set state='Failed',version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Pending'", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, id); await command.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); }
    private async Task<ExpenseEmployeeRepaymentDto?> GetRepaymentAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct) { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,employee_id,source_reference,amount,settled_amount,currency_code,state,created_at,recorded_at,payment_id,version from business_expenses.expense_employee_repayment where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? new ExpenseEmployeeRepaymentDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.GetInt64(11)) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

    public async Task<MileageExpenseDetailDto> SetMileageDetailAsync(TenantId tenant, Guid entity, Guid expenseId, SetMileageDetailRequest request, CancellationToken ct)
    {
        if (request.Distance <= 0 || request.Rate < 0 || string.IsNullOrWhiteSpace(request.DistanceUnit)) throw new ExpenseRepositoryException("expenses.mileage_invalid");
        var amount = decimal.Round(request.Distance * request.Rate, 4, MidpointRounding.AwayFromZero);
        await using var scope = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("with e as (select id from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft') insert into business_expenses.mileage_expense_detail(tenant_id,id,legal_entity_id,expense_id,distance,distance_unit,rate,vehicle_type,route) select $1,coalesce((select id from business_expenses.mileage_expense_detail where tenant_id=$1 and expense_id=$3),$4),$2,$3,$5,$6,$7,$8,$9 from e on conflict(tenant_id,expense_id) do update set distance=excluded.distance,distance_unit=excluded.distance_unit,rate=excluded.rate,vehicle_type=excluded.vehicle_type,route=excluded.route returning id,expense_id,distance,distance_unit,rate,distance*rate,vehicle_type,route", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, entity, expenseId, Guid.CreateVersion7(), request.Distance, request.DistanceUnit.Trim().ToUpperInvariant(), request.Rate, Db(request.VehicleType), Db(request.Route)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.mileage_not_editable"); var result = new MileageExpenseDetailDto(r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)); await r.CloseAsync(); await UpdateTypedExpenseAmountAsync(scope, tenant, entity, expenseId, amount, ct); await scope.Transaction.CommitAsync(ct); return result;
    }

    public async Task<PerDiemExpenseDetailDto> SetPerDiemDetailAsync(TenantId tenant, Guid entity, Guid expenseId, SetPerDiemDetailRequest request, CancellationToken ct)
    {
        if (request.Days <= 0 || request.DailyRate < 0 || request.ProvidedMeals < 0) throw new ExpenseRepositoryException("expenses.per_diem_invalid");
        var amount = decimal.Round(Math.Max(0, request.Days * request.DailyRate - request.ProvidedMeals * request.DailyRate / 3m), 4, MidpointRounding.AwayFromZero);
        await using var scope = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("insert into business_expenses.per_diem_expense_detail(tenant_id,id,legal_entity_id,expense_id,days,daily_rate,provided_meals,destination) select $1,coalesce((select id from business_expenses.per_diem_expense_detail where tenant_id=$1 and expense_id=$3),$4),$2,$3,$5,$6,$7,$8 from business_expenses.expense where tenant_id=$1 and legal_entity_id=$2 and id=$3 and expense_state='Draft' on conflict(tenant_id,expense_id) do update set days=excluded.days,daily_rate=excluded.daily_rate,provided_meals=excluded.provided_meals,destination=excluded.destination returning id,expense_id,days,daily_rate,provided_meals,days*daily_rate-(provided_meals*daily_rate/3),destination", scope.Connection, scope.Transaction);
        Add(cmd, tenant.Value, entity, expenseId, Guid.CreateVersion7(), request.Days, request.DailyRate, request.ProvidedMeals, Db(request.Destination)); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ExpenseRepositoryException("expenses.per_diem_not_editable"); var result = new PerDiemExpenseDetailDto(r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2), r.GetDecimal(3), r.GetInt32(4), r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetString(6)); await r.CloseAsync(); await UpdateTypedExpenseAmountAsync(scope, tenant, entity, expenseId, amount, ct); await scope.Transaction.CommitAsync(ct); return result;
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
    private static BusinessAdvanceDto ReadAdvance(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetString(8), r.GetString(9), r.GetInt64(10), r.GetFieldValue<DateTimeOffset>(11), r.IsDBNull(12) ? null : r.GetGuid(12));
    private async Task<BusinessAdvanceDto?> GetAdvanceAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    {
        await using var scope = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,employee_id,purpose,amount,issued_amount,applied_amount,returned_amount,currency_code,state,version,created_at,issuance_payment_id from business_expenses.business_advance where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction);
        Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? ReadAdvance(reader) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result;
    }
    private static ExpenseCardTransactionDto ReadCard(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.GetDecimal(5), r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.IsDBNull(9) ? null : r.GetGuid(9));
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
            payment = await (expensePayments ?? throw new ExpenseRepositoryException("expenses.payments_capability_unavailable")).PayExpenseReimbursementAsync(tenant, actorId, membershipId, batchId, entity, paymentAccountId, method, amount, currency, paymentDate, postingDate, $"expenses-reimbursement:{batchId:D}", ct);
        }
        catch
        {
            await SetBatchStateAsync(tenant, entity, batchId, "Failed", ct);
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
            await scope.Transaction.CommitAsync(ct);
        }
        return (await GetReimbursementBatchAsync(tenant, entity, batchId, ct))!;
    }

    private async Task SetBatchStateAsync(TenantId tenant, Guid entity, Guid batchId, string state, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("update business_expenses.expense_reimbursement_batch set state=$4,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Submitted'", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, batchId, state); await command.ExecuteNonQueryAsync(ct); await scope.Transaction.CommitAsync(ct); }
    private async Task<ExpenseReimbursementBatchDto?> GetReimbursementBatchAsync(TenantId tenant, Guid entity, Guid batchId, CancellationToken ct)
    { await using var scope = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,state,currency_code,total_amount,version,created_at from business_expenses.expense_reimbursement_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3", scope.Connection, scope.Transaction); Add(command, tenant.Value, entity, batchId); await using var reader = await command.ExecuteReaderAsync(ct); var result = await reader.ReadAsync(ct) ? new ExpenseReimbursementBatchDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetInt64(5), reader.GetFieldValue<DateTimeOffset>(6)) : null; await reader.CloseAsync(); await scope.Transaction.CommitAsync(ct); return result; }

}

public sealed class ExpenseRepositoryException(string code) : Exception(code) { public string Code { get; } = code; }
