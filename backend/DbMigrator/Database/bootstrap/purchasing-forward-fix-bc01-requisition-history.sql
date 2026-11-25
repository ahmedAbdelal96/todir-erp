-- BC01: durable Purchasing requisition lifecycle audit evidence.
create table if not exists business_purchasing.purchase_requisition_state_history (
    tenant_id uuid not null,
    legal_entity_id uuid not null,
    requisition_id uuid not null,
    id uuid not null,
    from_state text,
    to_state text not null,
    reason text not null,
    actor_id uuid not null,
    created_at timestamptz not null default now(),
    primary key (tenant_id,id),
    foreign key (tenant_id,legal_entity_id,requisition_id)
        references business_purchasing.purchase_requisition(tenant_id,legal_entity_id,id)
);

alter table business_purchasing.purchase_requisition_state_history enable row level security;
alter table business_purchasing.purchase_requisition_state_history force row level security;
drop policy if exists tenant_isolation on business_purchasing.purchase_requisition_state_history;
create policy tenant_isolation on business_purchasing.purchase_requisition_state_history
    using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
create index if not exists ix_purchase_requisition_history on business_purchasing.purchase_requisition_state_history(tenant_id,requisition_id,created_at,id);
grant select,insert on business_purchasing.purchase_requisition_state_history to erp_app;
