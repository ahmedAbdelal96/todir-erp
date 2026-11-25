-- Source-module budget control is a Planning-owned ledger.  The source modules
-- enlist it inside their own transaction through a Core capability; they never
-- write this schema directly.
alter table business_planning.budget_reservation
  add column if not exists reservation_kind text not null default 'Manual',
  add column if not exists source_currency char(3);

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'budget_reservation_kind_check') then
    alter table business_planning.budget_reservation
      add constraint budget_reservation_kind_check check (reservation_kind in ('Manual','Precommitment','Commitment'));
  end if;
end $$;

alter table business_planning.budget_control_policy
  add column if not exists effective_from date,
  add column if not exists effective_to date;

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'budget_control_policy_effective_dates_check') then
    alter table business_planning.budget_control_policy
      add constraint budget_control_policy_effective_dates_check check (effective_to is null or effective_from is null or effective_from <= effective_to);
  end if;
end $$;

create index if not exists ix_budget_reservation_source
  on business_planning.budget_reservation(tenant_id,source_module,source_document_id,source_line_id,state,created_at desc);
create index if not exists ix_budget_reservation_kind
  on business_planning.budget_reservation(tenant_id,bucket_id,reservation_kind,state);
