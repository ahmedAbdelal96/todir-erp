-- Preserve the original draft-delete function checksum while adding explicit
-- actor and membership attribution to the append-only deletion tombstone.
alter table business_expenses.expense_deletion_history
    add column if not exists actor_id uuid,
    add column if not exists membership_id uuid;

create or replace function business_expenses.delete_draft_expense_with_actor(
    p_tenant_id uuid,
    p_legal_entity_id uuid,
    p_expense_id uuid,
    p_actor_id uuid,
    p_membership_id uuid
)
returns boolean
language plpgsql
security definer
set search_path = business_expenses, pg_catalog
as $$
declare
    deleted boolean;
begin
    if nullif(current_setting('app.tenant_id', true), '')::uuid is distinct from p_tenant_id then
        raise exception 'expenses.tenant_context_required' using errcode = '42501';
    end if;

    -- Keep the legal-entity boundary inside the privileged function itself;
    -- callers cannot turn a valid tenant id into a cross-entity deletion.
    if not exists (
        select 1
          from business_expenses.expense
         where tenant_id = p_tenant_id
           and legal_entity_id = p_legal_entity_id
           and id = p_expense_id
           and expense_state = 'Draft'
    ) then
        return false;
    end if;

    deleted := business_expenses.delete_draft_expense(p_tenant_id, p_legal_entity_id, p_expense_id);
    if deleted then
        update business_expenses.expense_deletion_history
           set actor_id = p_actor_id,
               membership_id = p_membership_id
         where tenant_id = p_tenant_id
           and legal_entity_id = p_legal_entity_id
           and expense_id = p_expense_id
           and action = 'draft.deleted'
           and actor_id is null;
    end if;
    return deleted;
end;
$$;

alter function business_expenses.delete_draft_expense_with_actor(uuid, uuid, uuid, uuid, uuid) owner to erp_owner;
revoke all on function business_expenses.delete_draft_expense_with_actor(uuid, uuid, uuid, uuid, uuid) from public;
grant execute on function business_expenses.delete_draft_expense_with_actor(uuid, uuid, uuid, uuid, uuid) to erp_app;
