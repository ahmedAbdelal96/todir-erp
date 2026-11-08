create table if not exists platform.approval_request (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, owner_module text not null, owner_entity_type text not null, owner_entity_id uuid not null,
 resource_version bigint not null, policy_version_id uuid, state text not null default 'Pending', requested_by uuid not null, request_reason text,
 decided_by uuid, decision_reason text, created_at timestamptz not null default now(), decided_at timestamptz,
 primary key (tenant_id,id), unique (tenant_id,owner_module,owner_entity_type,owner_entity_id,resource_version),
 check (state in ('Pending','Approved','Rejected')), check (resource_version > 0),
 check ((state='Pending' and decided_by is null and decided_at is null) or (state <> 'Pending' and decided_by is not null and decided_at is not null))
);
create index if not exists ix_platform_approval_scope on platform.approval_request(tenant_id,legal_entity_id,state,created_at desc);
alter table platform.approval_request enable row level security;
alter table platform.approval_request force row level security;
drop policy if exists approval_request_tenant_isolation on platform.approval_request;
create policy approval_request_tenant_isolation on platform.approval_request using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid);
grant select,insert,update on platform.approval_request to erp_app;
