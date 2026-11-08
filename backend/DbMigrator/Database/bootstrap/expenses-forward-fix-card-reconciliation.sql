alter table business_expenses.expense_card_transaction
    drop constraint if exists expense_card_transaction_state_check;

alter table business_expenses.expense_card_transaction
    add constraint expense_card_transaction_state_check
    check (state in ('Unmatched','Pending','Cleared','Matched','Personal','Allocated','Refunded','Disputed','Ignored'));

create table if not exists business_expenses.expense_card_adjustment (
    tenant_id uuid not null,
    id uuid not null,
    legal_entity_id uuid not null,
    card_transaction_id uuid not null,
    kind text not null,
    provider_reference text not null,
    amount numeric(19,4) not null,
    currency_code char(3) not null,
    state text not null default 'Applied',
    reason text not null,
    created_at timestamptz not null default now(),
    applied_at timestamptz,
    primary key (tenant_id,id),
    unique (tenant_id,provider_reference),
    foreign key (tenant_id,card_transaction_id) references business_expenses.expense_card_transaction(tenant_id,id),
    check (kind in ('Credit','Refund','Dispute')),
    check (state in ('Pending','Applied','Rejected')),
    check (amount > 0),
    check (length(currency_code)=3),
    check (state <> 'Applied' or applied_at is not null)
);

create index if not exists ix_expenses_card_adjustments_transaction on business_expenses.expense_card_adjustment(tenant_id,legal_entity_id,card_transaction_id,created_at desc);

alter table business_expenses.expense_card_adjustment enable row level security;
alter table business_expenses.expense_card_adjustment force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_card_adjustment;
create policy tenant_isolation on business_expenses.expense_card_adjustment
    using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
grant select,insert,update on business_expenses.expense_card_adjustment to erp_app;
