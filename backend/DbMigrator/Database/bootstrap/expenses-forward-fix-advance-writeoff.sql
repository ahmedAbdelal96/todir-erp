alter table business_expenses.business_advance
    add column if not exists written_off_amount numeric(19,4) not null default 0;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'business_advance_written_off_amount_check') then
        alter table business_expenses.business_advance add constraint business_advance_written_off_amount_check check (written_off_amount >= 0);
    end if;
end $$;

create table if not exists business_expenses.business_advance_write_off (
    tenant_id uuid not null,
    id uuid not null,
    legal_entity_id uuid not null,
    advance_id uuid not null,
    amount numeric(19,4) not null,
    currency_code char(3) not null,
    base_amount numeric(19,4) not null,
    base_currency_code char(3) not null,
    fx_snapshot jsonb,
    reason text not null,
    effective_date date not null,
    idempotency_key text not null,
    request_version bigint not null,
    approval_request_id uuid,
    approval_state text not null default 'Pending',
    state text not null default 'PendingApproval',
    requested_by uuid,
    approved_by uuid,
    ledger_fact_id uuid,
    ledger_entry_id uuid,
    version bigint not null default 1,
    created_at timestamptz not null default now(),
    approved_at timestamptz,
    primary key (tenant_id,id),
    unique (tenant_id,legal_entity_id,idempotency_key),
    foreign key (tenant_id,advance_id) references business_expenses.business_advance(tenant_id,id),
    check (amount > 0),
    check (base_amount > 0),
    check (length(currency_code)=3 and length(base_currency_code)=3),
    check (approval_state in ('Pending','Approved','Rejected')),
    check (state in ('PendingApproval','Approved','Rejected'))
);

create table if not exists business_expenses.business_advance_write_off_history (
    tenant_id uuid not null,
    id uuid not null,
    legal_entity_id uuid not null,
    write_off_id uuid not null,
    action text not null,
    to_state jsonb not null,
    occurred_at timestamptz not null default now(),
    primary key (tenant_id,id),
    foreign key (tenant_id,write_off_id) references business_expenses.business_advance_write_off(tenant_id,id)
);

create or replace function business_expenses.reject_write_off_history_mutation()
returns trigger language plpgsql as $$
begin
    raise exception using errcode = '55000', message = 'expenses.advance_write_off_history_immutable';
end $$;

drop trigger if exists business_advance_write_off_history_immutable
    on business_expenses.business_advance_write_off_history;
create trigger business_advance_write_off_history_immutable
    before update or delete on business_expenses.business_advance_write_off_history
    for each row execute function business_expenses.reject_write_off_history_mutation();

alter table business_expenses.business_advance_write_off enable row level security;
alter table business_expenses.business_advance_write_off force row level security;
alter table business_expenses.business_advance_write_off_history enable row level security;
alter table business_expenses.business_advance_write_off_history force row level security;
drop policy if exists tenant_isolation on business_expenses.business_advance_write_off;
drop policy if exists tenant_isolation on business_expenses.business_advance_write_off_history;
create policy tenant_isolation on business_expenses.business_advance_write_off using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
create policy tenant_isolation on business_expenses.business_advance_write_off_history using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
grant select, insert, update on business_expenses.business_advance_write_off to erp_app;
grant select, insert on business_expenses.business_advance_write_off_history to erp_app;

alter table finance_ledger.fact_producer_registration no force row level security;
alter table finance_ledger.ledger_entity_configuration no force row level security;
insert into finance_ledger.fact_producer_registration (tenant_id,legal_entity_id,id,producer_module,fact_type,fact_version,status,registered_at)
select distinct c.tenant_id,c.legal_entity_id,md5(c.tenant_id::text || ':' || c.legal_entity_id::text || ':expenses:SettlementWriteOff:1')::uuid,'expenses','SettlementWriteOff',1,'Active',now()
from finance_ledger.ledger_entity_configuration c
on conflict (tenant_id,legal_entity_id,producer_module,fact_type,fact_version) do nothing;
alter table finance_ledger.fact_producer_registration force row level security;
alter table finance_ledger.ledger_entity_configuration force row level security;
