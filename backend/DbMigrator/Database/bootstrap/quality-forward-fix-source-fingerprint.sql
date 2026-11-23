create unique index if not exists ux_quality_inspection_source_fingerprint
    on business_quality.inspection(tenant_id,source_fingerprint)
    where source_fingerprint is not null;
