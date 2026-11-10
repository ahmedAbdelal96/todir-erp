create table if not exists business_expenses.expense_rate_policy (
    tenant_id uuid not null,
    id uuid not null,
    legal_entity_id uuid not null,
    kind text not null,
    version integer not null,
    status text not null default 'Draft',
    effective_from date not null,
    effective_to date,
    vehicle_type text,
    location_code text,
    currency_code char(3) not null default 'EGP',
    rate numeric(19,4) not null,
    breakfast_reduction numeric(19,4) not null default 0,
    lunch_reduction numeric(19,4) not null default 0,
    dinner_reduction numeric(19,4) not null default 0,
    created_at timestamptz not null default now(),
    primary key (tenant_id,id),
    unique (tenant_id,legal_entity_id,kind,version),
    check (kind in ('Mileage','PerDiem')),
    check (status in ('Draft','Published','Retired')),
    check (effective_to is null or effective_to >= effective_from),
    check (rate >= 0),
    check (breakfast_reduction >= 0 and lunch_reduction >= 0 and dinner_reduction >= 0),
    check (length(currency_code)=3)
);

alter table business_expenses.mileage_expense_detail add column if not exists rate_policy_version_id uuid;
alter table business_expenses.per_diem_expense_detail add column if not exists rate_policy_version_id uuid;
alter table business_expenses.per_diem_expense_detail add column if not exists daily_lines jsonb;
alter table business_expenses.per_diem_expense_detail add column if not exists calculated_amount numeric(19,4);
create index if not exists ix_expenses_rate_policy_effective on business_expenses.expense_rate_policy(tenant_id,legal_entity_id,kind,status,effective_from,effective_to);
alter table business_expenses.expense_rate_policy enable row level security;
alter table business_expenses.expense_rate_policy force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_rate_policy;
create policy tenant_isolation on business_expenses.expense_rate_policy
    using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
grant select,insert,update on business_expenses.expense_rate_policy to erp_app;
grant select,insert,update,delete on all tables in schema business_expenses to erp_app;
