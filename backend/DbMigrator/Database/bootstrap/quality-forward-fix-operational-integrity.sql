alter table business_quality.quality_plan_characteristic
    add column if not exists evidence_required boolean not null default false;

alter table business_quality.quality_restriction
    add column if not exists owner_execution_status text not null default 'NotRequired',
    add column if not exists owner_idempotency_key text,
    add column if not exists released_quantity numeric(28,10) not null default 0;

alter table business_quality.quality_disposition
    add column if not exists approval_required boolean not null default true,
    add column if not exists approved_by uuid,
    add column if not exists approved_at timestamptz,
    add column if not exists owner_execution_status text not null default 'NotRequired',
    add column if not exists owner_operation_id uuid;

create index if not exists ix_quality_restriction_owner_state
    on business_quality.quality_restriction(tenant_id,owner_execution_status,status);
create unique index if not exists ux_quality_restriction_owner_key
    on business_quality.quality_restriction(tenant_id,owner_idempotency_key)
    where owner_idempotency_key is not null;
create unique index if not exists ux_quality_result_without_sample
    on business_quality.inspection_result(tenant_id,inspection_id,characteristic_id)
    where sample_id is null;
create unique index if not exists ux_quality_result_with_sample
    on business_quality.inspection_result(tenant_id,inspection_id,characteristic_id,sample_id)
    where sample_id is not null;
