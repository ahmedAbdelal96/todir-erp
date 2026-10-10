CREATE INDEX IF NOT EXISTS ix_identity_tenant_name_prefix
    ON platform_iam.identity (tenant_id, lower(name) text_pattern_ops);
