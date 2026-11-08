create table if not exists business_expenses.expense_policy (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null,
 status text not null default 'Active', created_at timestamptz not null default now(), primary key (tenant_id,id),
 unique (tenant_id,legal_entity_id,code), check (status in ('Active','Inactive'))
);
create table if not exists business_expenses.expense_policy_version (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, policy_id uuid not null, version integer not null,
 status text not null default 'Draft', rules jsonb not null default '{}'::jsonb, effective_from date not null, effective_to date,
 created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,policy_id,version),
 foreign key (tenant_id,policy_id) references business_expenses.expense_policy(tenant_id,id),
 check (status in ('Draft','Published','Retired')), check (effective_to is null or effective_to >= effective_from),
 check (jsonb_typeof(rules) = 'object')
);
alter table business_expenses.expense_policy enable row level security;
alter table business_expenses.expense_policy force row level security;
alter table business_expenses.expense_policy_version enable row level security;
alter table business_expenses.expense_policy_version force row level security;
drop policy if exists expense_policy_tenant_isolation on business_expenses.expense_policy;
create policy expense_policy_tenant_isolation on business_expenses.expense_policy using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
drop policy if exists expense_policy_version_tenant_isolation on business_expenses.expense_policy_version;
create policy expense_policy_version_tenant_isolation on business_expenses.expense_policy_version using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
grant select,insert,update on business_expenses.expense_policy, business_expenses.expense_policy_version to erp_app;
create index if not exists ix_expenses_policy_versions_effective on business_expenses.expense_policy_version(tenant_id,legal_entity_id,status,effective_from,effective_to);
