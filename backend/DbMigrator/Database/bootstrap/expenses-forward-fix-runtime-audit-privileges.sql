-- Tighten the runtime role after the earlier draft-grant correction.
-- Audit/history rows are append-only facts; draft cleanup is performed by the
-- tenant-checked SECURITY DEFINER function below, not by direct table grants.
revoke update on all tables in schema business_expenses from erp_app;
revoke delete on all tables in schema business_expenses from erp_app;

-- These are the only business tables updated by the Expenses runtime paths.
grant update on business_expenses.business_advance to erp_app;
grant update on business_expenses.business_advance_application to erp_app;
grant update on business_expenses.business_advance_issuance to erp_app;
grant update on business_expenses.business_advance_write_off to erp_app;
grant update on business_expenses.expense to erp_app;
grant update on business_expenses.expense_card_transaction to erp_app;
grant update on business_expenses.expense_category to erp_app;
grant update on business_expenses.expense_category_version to erp_app;
grant update on business_expenses.expense_employee_repayment to erp_app;
grant update on business_expenses.expense_evidence to erp_app;
grant update on business_expenses.expense_import_batch to erp_app;
grant update on business_expenses.expense_missing_receipt_declaration to erp_app;
grant update on business_expenses.expense_ocr_job to erp_app;
grant update on business_expenses.expense_policy_exception to erp_app;
grant update on business_expenses.expense_policy_evaluation to erp_app;
grant update on business_expenses.expense_policy_version to erp_app;
grant update on business_expenses.expense_rate_policy to erp_app;
grant update on business_expenses.expense_reimbursement_batch to erp_app;
grant update on business_expenses.expense_reimbursement_obligation to erp_app;
grant update on business_expenses.expense_report to erp_app;

-- Direct deletes are limited to draft report/card/itemization maintenance.
grant delete on business_expenses.expense_card_allocation to erp_app;
grant delete on business_expenses.expense_itemization to erp_app;
grant delete on business_expenses.expense_report_item to erp_app;

-- These facts must never be changed or removed by the runtime role.
revoke update, delete on business_expenses.expense_history from erp_app;
revoke update, delete on business_expenses.expense_report_history from erp_app;
revoke update, delete on business_expenses.business_advance_write_off_history from erp_app;
revoke update, delete on business_expenses.expense_card_adjustment from erp_app;
grant insert, select on business_expenses.expense_history to erp_app;
grant insert, select on business_expenses.expense_report_history to erp_app;
grant insert, select on business_expenses.business_advance_write_off_history to erp_app;
grant insert, select on business_expenses.expense_card_adjustment to erp_app;

-- Draft deletion leaves an append-only tombstone because the deleted expense
-- row cannot remain as the parent of its historical lifecycle rows.
create table if not exists business_expenses.expense_deletion_history (
    tenant_id uuid not null,
    id uuid not null,
    legal_entity_id uuid not null,
    expense_id uuid not null,
    action text not null,
    state_snapshot jsonb not null,
    occurred_at timestamptz not null default now(),
    primary key (tenant_id, id)
);
alter table business_expenses.expense_deletion_history enable row level security;
alter table business_expenses.expense_deletion_history force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_deletion_history;
create policy tenant_isolation on business_expenses.expense_deletion_history
    using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
revoke update, delete on business_expenses.expense_deletion_history from erp_app;
grant insert, select on business_expenses.expense_deletion_history to erp_app;

create or replace function business_expenses.delete_draft_expense(
    p_tenant_id uuid,
    p_legal_entity_id uuid,
    p_expense_id uuid
)
returns boolean
language plpgsql
security definer
set search_path = business_expenses, pg_catalog
as $$
declare
    current_state text;
    deleted_count integer := 0;
begin
    if nullif(current_setting('app.tenant_id', true), '')::uuid is distinct from p_tenant_id then
        raise exception 'expenses.tenant_context_required' using errcode = '42501';
    end if;

    select expense_state
      into current_state
      from business_expenses.expense
     where tenant_id = p_tenant_id
       and legal_entity_id = p_legal_entity_id
       and id = p_expense_id
     for update;

    if not found then
        return false;
    end if;

    if current_state <> 'Draft' then
        raise exception 'expenses.cannot_delete_non_draft';
    end if;

    insert into business_expenses.expense_deletion_history(
        tenant_id, id, legal_entity_id, expense_id, action, state_snapshot)
    select tenant_id, gen_random_uuid(), legal_entity_id, id, 'draft.deleted',
           jsonb_build_object(
               'expenseState', expense_state,
               'approvalState', approval_state,
               'accountingState', accounting_state,
               'settlementState', settlement_state,
               'version', version)
      from business_expenses.expense
     where tenant_id = p_tenant_id
       and legal_entity_id = p_legal_entity_id
       and id = p_expense_id;

    update business_expenses.expense_card_transaction
       set matched_expense_id = null,
           state = 'Unmatched'
     where tenant_id = p_tenant_id
       and matched_expense_id = p_expense_id;

    delete from business_expenses.expense_card_allocation where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.business_advance_application where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_report_item where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_missing_receipt_declaration where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_policy_exception where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_ocr_job where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_evidence where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.mileage_expense_detail where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.per_diem_expense_detail where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_dimension where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_itemization where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_participant where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_policy_evaluation where tenant_id = p_tenant_id and expense_id = p_expense_id;
    delete from business_expenses.expense_history where tenant_id = p_tenant_id and expense_id = p_expense_id;

    delete from business_expenses.expense
     where tenant_id = p_tenant_id
       and legal_entity_id = p_legal_entity_id
       and id = p_expense_id
       and expense_state = 'Draft';
    get diagnostics deleted_count = row_count;
    return deleted_count > 0;
end;
$$;

alter function business_expenses.delete_draft_expense(uuid, uuid, uuid) owner to erp_owner;
revoke all on function business_expenses.delete_draft_expense(uuid, uuid, uuid) from public;
grant execute on function business_expenses.delete_draft_expense(uuid, uuid, uuid) to erp_app;
