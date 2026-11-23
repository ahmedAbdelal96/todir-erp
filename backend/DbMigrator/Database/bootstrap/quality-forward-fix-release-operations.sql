create table if not exists business_quality.quality_restriction_release_operation (
    tenant_id uuid not null,
    id uuid not null,
    restriction_id uuid not null,
    idempotency_key text not null,
    request_hash text not null,
    quantity numeric(28,10) not null,
    unit_of_measure_id uuid not null,
    reason text not null,
    operational_date date not null,
    posting_date date not null,
    state text not null default 'Pending',
    owner_operation_id uuid,
    error_code text,
    created_by uuid not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (tenant_id,id),
    unique (tenant_id,restriction_id,idempotency_key),
    foreign key (tenant_id,restriction_id) references business_quality.quality_restriction(tenant_id,id),
    check (quantity > 0),
    check (state in ('Pending','Executing','Confirmed','Failed','Unknown'))
);
create index if not exists ix_quality_release_operations on business_quality.quality_restriction_release_operation(tenant_id,restriction_id,state,created_at desc,id desc);
alter table business_quality.quality_restriction_release_operation enable row level security;
alter table business_quality.quality_restriction_release_operation force row level security;
drop policy if exists tenant_isolation on business_quality.quality_restriction_release_operation;
create policy tenant_isolation on business_quality.quality_restriction_release_operation
    using (tenant_id = nullif(current_setting('app.tenant_id',true),'')::uuid)
    with check (tenant_id = nullif(current_setting('app.tenant_id',true),'')::uuid);
grant select,insert,update,delete on business_quality.quality_restriction_release_operation to erp_app;
