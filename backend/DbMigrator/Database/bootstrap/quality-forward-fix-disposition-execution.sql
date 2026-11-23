alter table business_quality.quality_disposition
    add column if not exists unit_of_measure_id uuid,
    add column if not exists operational_date date,
    add column if not exists posting_date date,
    add column if not exists owner_execution_status text not null default 'NotRequired',
    add column if not exists owner_operation_id uuid,
    add column if not exists owner_idempotency_key text,
    add column if not exists execution_error_code text,
    add column if not exists row_version bigint not null default 1;
create unique index if not exists ux_quality_disposition_owner_key
    on business_quality.quality_disposition(tenant_id,owner_idempotency_key)
    where owner_idempotency_key is not null;
