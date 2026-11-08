alter table business_expenses.business_advance drop constraint if exists business_advance_state_check;
alter table business_expenses.business_advance add constraint business_advance_state_check check (state in ('Requested','Approved','IssuancePending','Issued','PartiallyIssued','PartiallySettled','Settled','Rejected'));
alter table business_expenses.business_advance add column if not exists issued_amount numeric(19,4) not null default 0;
alter table business_expenses.business_advance add column if not exists issuance_payment_id uuid;
alter table business_expenses.business_advance drop constraint if exists business_advance_issued_amount_check;
alter table business_expenses.business_advance add constraint business_advance_issued_amount_check check (issued_amount >= 0 and issued_amount <= amount) not valid;
create index if not exists ix_expenses_advance_issuance_payment on business_expenses.business_advance(tenant_id,issuance_payment_id);

create table if not exists business_expenses.business_advance_issuance (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, advance_id uuid not null,
 idempotency_key text not null, amount numeric(19,4) not null, payment_id uuid, state text not null default 'Pending',
 created_at timestamptz not null default now(), completed_at timestamptz,
 primary key (tenant_id,id), unique (tenant_id,advance_id,idempotency_key),
 foreign key (tenant_id,advance_id) references business_expenses.business_advance(tenant_id,id),
 check(amount > 0), check(state in ('Pending','Completed','Failed'))
);
alter table business_expenses.business_advance_issuance enable row level security;
alter table business_expenses.business_advance_issuance force row level security;
drop policy if exists tenant_isolation on business_expenses.business_advance_issuance;
create policy tenant_isolation on business_expenses.business_advance_issuance using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update,delete on business_expenses.business_advance_issuance to erp_app;
create index if not exists ix_expenses_advance_issuance_scope on business_expenses.business_advance_issuance(tenant_id,legal_entity_id,advance_id,state);
