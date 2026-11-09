create table if not exists business_expenses.expense_policy_exception (
 tenant_id uuid not null,
 id uuid not null,
 legal_entity_id uuid not null,
 expense_id uuid not null,
 finding_code text not null,
 reason text not null,
 state text not null default 'Pending',
 approval_request_id uuid,
 requested_by uuid not null,
 approved_by uuid,
 version bigint not null default 1,
 created_at timestamptz not null default now(),
 decided_at timestamptz,
 primary key (tenant_id,id),
 unique (tenant_id,expense_id,finding_code),
 foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id),
 check (state in ('Pending','Approved','Rejected')),
 check (length(trim(finding_code)) > 0),
 check (length(trim(reason)) >= 5)
);

create index if not exists ix_expenses_policy_exception_scope on business_expenses.expense_policy_exception(tenant_id,legal_entity_id,expense_id,state,created_at desc);
alter table business_expenses.expense_policy_exception enable row level security;
alter table business_expenses.expense_policy_exception force row level security;
drop policy if exists expense_policy_exception_tenant_isolation on business_expenses.expense_policy_exception;
create policy expense_policy_exception_tenant_isolation on business_expenses.expense_policy_exception
 using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update on business_expenses.expense_policy_exception to erp_app;
