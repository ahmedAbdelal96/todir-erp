-- BC02: approved budget changes become effective on an explicit date.
-- Existing rows are effective from the migration day so historical data is
-- not silently backdated.

alter table business_planning.budget_revision
  add column if not exists effective_from date not null default current_date;
alter table business_planning.budget_transfer
  add column if not exists effective_from date not null default current_date;

create index if not exists ix_planning_budget_revision_current
  on business_planning.budget_revision(tenant_id,baseline_id,state,effective_from);
create index if not exists ix_planning_budget_transfer_current
  on business_planning.budget_transfer(tenant_id,baseline_id,state,effective_from);
