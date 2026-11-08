create table if not exists business_expenses.expense_participant (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null,
 party_id uuid, display_name text not null, role text not null default 'MealParticipant', created_at timestamptz not null default now(),
 primary key (tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(length(trim(display_name)) > 0)
);
create table if not exists business_expenses.expense_policy_evaluation (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null,
 policy_version_id uuid, state text not null default 'Pending', snapshot jsonb not null default '{}'::jsonb, evaluated_at timestamptz not null default now(),
 primary key (tenant_id,id), unique(tenant_id,expense_id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(state in ('Pending','Passed','Warning','Blocked'))
);
create table if not exists business_expenses.expense_report (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, employee_id uuid, title text not null,
 state text not null default 'Draft', total_amount numeric(19,4) not null default 0, currency_code char(3) not null, version bigint not null default 1,
 created_at timestamptz not null default now(), submitted_at timestamptz, finalized_at timestamptz,
 primary key (tenant_id,id), check(state in ('Draft','Submitted','Approved','Rejected','Finalized')), check(total_amount >= 0), check(length(currency_code)=3)
);
create table if not exists business_expenses.expense_report_item (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, report_id uuid not null, expense_id uuid not null, amount numeric(19,4) not null,
 primary key (tenant_id,id), unique(tenant_id,report_id,expense_id), foreign key (tenant_id,report_id) references business_expenses.expense_report(tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(amount > 0)
);
create table if not exists business_expenses.business_advance (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, employee_id uuid not null, purpose text not null,
 amount numeric(19,4) not null, applied_amount numeric(19,4) not null default 0, returned_amount numeric(19,4) not null default 0, currency_code char(3) not null,
 state text not null default 'Requested', version bigint not null default 1, created_at timestamptz not null default now(), issued_at timestamptz, settled_at timestamptz,
 primary key (tenant_id,id), check(amount > 0), check(applied_amount >= 0 and returned_amount >= 0 and applied_amount + returned_amount <= amount), check(state in ('Requested','Approved','Issued','PartiallySettled','Settled','Rejected')), check(length(currency_code)=3)
);
create table if not exists business_expenses.business_advance_application (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, advance_id uuid not null, expense_id uuid not null, amount numeric(19,4) not null, created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique(tenant_id,advance_id,expense_id), foreign key (tenant_id,advance_id) references business_expenses.business_advance(tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(amount > 0)
);
create table if not exists business_expenses.expense_card_transaction (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, provider_reference text not null, merchant_name text not null, transaction_date date not null,
 amount numeric(19,4) not null, currency_code char(3) not null, state text not null default 'Unmatched', employee_id uuid, matched_expense_id uuid, created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique(tenant_id,provider_reference), check(amount > 0), check(state in ('Unmatched','Matched','Personal','Allocated','Ignored')), check(length(currency_code)=3)
);
create table if not exists business_expenses.expense_employee_repayment (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, employee_id uuid not null, source_reference text not null, amount numeric(19,4) not null, currency_code char(3) not null,
 state text not null default 'Requested', created_at timestamptz not null default now(), recorded_at timestamptz, primary key (tenant_id,id), check(amount > 0), check(state in ('Requested','Pending','Recorded','Failed')), check(length(currency_code)=3)
);
create table if not exists business_expenses.mileage_expense_detail (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, distance numeric(19,4) not null, distance_unit text not null default 'KM', rate numeric(19,4) not null, vehicle_type text, route text,
 primary key (tenant_id,id), unique(tenant_id,expense_id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(distance > 0 and rate >= 0)
);
create table if not exists business_expenses.per_diem_expense_detail (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, days numeric(19,4) not null, daily_rate numeric(19,4) not null, provided_meals integer not null default 0, destination text,
 primary key (tenant_id,id), unique(tenant_id,expense_id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check(days > 0 and daily_rate >= 0 and provided_meals >= 0)
);
create table if not exists business_expenses.expense_dimension (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null, dimension_code text not null, value_code text not null,
 primary key (tenant_id,id), unique(tenant_id,expense_id,dimension_code), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id)
);
create table if not exists business_expenses.expense_reimbursement_batch (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, state text not null default 'Draft', currency_code char(3) not null, total_amount numeric(19,4) not null default 0, version bigint not null default 1,
 created_at timestamptz not null default now(), paid_at timestamptz, primary key (tenant_id,id), check(state in ('Draft','Prepared','Submitted','Paid','Failed')), check(total_amount >= 0), check(length(currency_code)=3)
);
create table if not exists business_expenses.expense_reimbursement_allocation (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, batch_id uuid not null, obligation_id uuid not null, amount numeric(19,4) not null,
 primary key (tenant_id,id), unique(tenant_id,batch_id,obligation_id), foreign key (tenant_id,batch_id) references business_expenses.expense_reimbursement_batch(tenant_id,id), foreign key (tenant_id,obligation_id) references business_expenses.expense_reimbursement_obligation(tenant_id,id), check(amount > 0)
);

do $$ declare t text; begin
  foreach t in array array['expense_participant','expense_policy_evaluation','expense_report','expense_report_item','business_advance','business_advance_application','expense_card_transaction','expense_employee_repayment','mileage_expense_detail','per_diem_expense_detail','expense_dimension','expense_reimbursement_batch','expense_reimbursement_allocation'] loop
    execute format('alter table business_expenses.%I enable row level security', t);
    execute format('alter table business_expenses.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_expenses.%I', t);
    execute format('create policy tenant_isolation on business_expenses.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_expenses.%I to erp_app', t);
  end loop;
end $$;
create index if not exists ix_expenses_reports_scope on business_expenses.expense_report(tenant_id,legal_entity_id,state,created_at desc);
create index if not exists ix_expenses_advances_scope on business_expenses.business_advance(tenant_id,legal_entity_id,state,created_at desc);
create index if not exists ix_expenses_cards_scope on business_expenses.expense_card_transaction(tenant_id,legal_entity_id,state,transaction_date desc);
create unique index if not exists ux_expenses_repayment_source on business_expenses.expense_employee_repayment(tenant_id,source_reference);
create index if not exists ix_expenses_reimbursements_scope on business_expenses.expense_reimbursement_batch(tenant_id,legal_entity_id,state,created_at desc);
