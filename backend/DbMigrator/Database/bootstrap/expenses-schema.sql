create schema if not exists business_expenses authorization erp_owner;
revoke all on schema business_expenses from public;
grant usage on schema business_expenses to erp_app;

create table if not exists business_expenses.expense_category (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null, status text not null default 'Active', created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), check(status in ('Active','Archived'))
);
create table if not exists business_expenses.expense_category_version (
 tenant_id uuid not null, id uuid not null, category_id uuid not null, version integer not null, status text not null default 'Draft', requires_receipt boolean not null default true, receipt_threshold numeric(19,4), effective_from date not null, effective_to date,
 primary key (tenant_id,id), unique (tenant_id,category_id,version), foreign key (tenant_id,category_id) references business_expenses.expense_category(tenant_id,id), check(status in ('Draft','Published','Retired')), check(receipt_threshold is null or receipt_threshold >= 0), check(effective_to is null or effective_to >= effective_from)
);
create table if not exists business_expenses.expense (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, employee_id uuid, merchant_name text not null, expense_date date not null, amount numeric(19,4) not null, currency_code char(3) not null, payment_source text not null, category_version_id uuid not null, expense_state text not null default 'Draft', approval_state text not null default 'Pending', accounting_state text not null default 'NotFinalized', settlement_state text not null default 'NotSettled', evidence_state text not null default 'Missing', policy_state text not null default 'NotEvaluated', business_purpose text, description text, project_id uuid, task_id uuid, cost_category text, billable boolean not null default false, version bigint not null default 1, created_at timestamptz not null default now(), updated_at timestamptz not null default now(), finalized_at timestamptz,
 primary key (tenant_id,id), foreign key (tenant_id,category_version_id) references business_expenses.expense_category_version(tenant_id,id), check(amount > 0), check(length(currency_code)=3), check(payment_source in ('EmployeePaid','CompanyPaid','CorporateCard','PettyCash')), check(expense_state in ('Draft','Submitted','Finalized','Reversed','Rejected')), check(approval_state in ('Pending','Approved','Rejected')), check(accounting_state in ('NotFinalized','Pending','Posted','Reversed')), check(settlement_state in ('NotSettled','ReimbursementPending','Settled','NotApplicable'))
);
create table if not exists business_expenses.expense_history (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, action text not null, from_state jsonb, to_state jsonb not null, actor_id uuid, occurred_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id)
);
create table if not exists business_expenses.expense_evidence (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, evidence_type text not null, file_reference text not null, status text not null default 'Pending', created_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id)
);
create table if not exists business_expenses.expense_itemization (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, description text not null, amount numeric(19,4) not null, currency_code char(3) not null, tax_amount numeric(19,4) not null default 0, primary key (tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(amount >= 0), check(tax_amount >= 0)
);
create table if not exists business_expenses.expense_reimbursement_obligation (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, employee_id uuid not null, amount numeric(19,4) not null, settled_amount numeric(19,4) not null default 0, currency_code char(3) not null, status text not null default 'Pending', created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,expense_id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(amount > 0), check(settled_amount >= 0 and settled_amount <= amount), check(status in ('Pending','PartiallySettled','Settled','Failed'))
);

create index if not exists ix_expenses_expense_list on business_expenses.expense(tenant_id,legal_entity_id,created_at desc,id desc);
create index if not exists ix_expenses_expense_state on business_expenses.expense(tenant_id,legal_entity_id,expense_state,created_at desc);
create index if not exists ix_expenses_evidence_expense on business_expenses.expense_evidence(tenant_id,legal_entity_id,expense_id,created_at);

do $$ declare t text; begin
  foreach t in array array['expense_category','expense_category_version','expense','expense_history','expense_evidence','expense_itemization','expense_reimbursement_obligation'] loop
    execute format('alter table business_expenses.%I enable row level security', t);
    execute format('alter table business_expenses.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_expenses.%I', t);
    execute format('create policy tenant_isolation on business_expenses.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_expenses.%I to erp_app', t);
  end loop;
end $$;
