-- Manufacturing execution request identity is retained separately from the business idempotency key.
-- Legacy rows remain nullable; new execution writes always populate the hash and reject key reuse with a different payload.
alter table business_manufacturing.production_material_event
    add column if not exists request_hash text;
alter table business_manufacturing.production_output
    add column if not exists request_hash text;

create index if not exists production_material_event_request_hash_idx
    on business_manufacturing.production_material_event(tenant_id, source_key, request_hash);
create index if not exists production_output_request_hash_idx
    on business_manufacturing.production_output(tenant_id, idempotency_key, request_hash);
