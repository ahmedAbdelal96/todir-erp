create table if not exists platform.approval_delegation (
    tenant_id uuid not null,
    id uuid not null,
    legal_entity_id uuid not null,
    delegator_id uuid not null,
    delegate_id uuid not null,
    permission_code text not null,
    effective_from timestamptz not null,
    effective_to timestamptz not null,
    status text not null default 'Active',
    reason text,
    created_by uuid not null,
    created_at timestamptz not null default now(),
    primary key (tenant_id, id),
    foreign key (tenant_id, legal_entity_id) references platform.legal_entity(tenant_id, id),
    check (delegator_id <> delegate_id),
    check (effective_to > effective_from),
    check (status in ('Active','Revoked')),
    unique (tenant_id, legal_entity_id, delegator_id, delegate_id, permission_code, effective_from)
);

create index if not exists ix_platform_approval_delegation_active
    on platform.approval_delegation(tenant_id, legal_entity_id, delegate_id, permission_code, effective_from, effective_to)
    where status = 'Active';

alter table platform.approval_delegation enable row level security;
alter table platform.approval_delegation force row level security;
drop policy if exists approval_delegation_tenant_isolation on platform.approval_delegation;
create policy approval_delegation_tenant_isolation on platform.approval_delegation
    using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

grant select, insert, update on platform.approval_delegation to erp_app;
