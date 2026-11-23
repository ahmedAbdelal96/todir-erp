alter table business_purchasing.supplier_return_request
    add column if not exists idempotency_key text;
create unique index if not exists ux_supplier_return_quality_idempotency
    on business_purchasing.supplier_return_request(tenant_id,legal_entity_id,idempotency_key)
    where idempotency_key is not null;
