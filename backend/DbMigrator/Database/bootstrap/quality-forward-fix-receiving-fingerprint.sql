create unique index if not exists ux_quality_receiving_source_fingerprint
    on business_quality.inspection(tenant_id,source_fingerprint)
    where event_type='Receiving' and source_fingerprint is not null;
