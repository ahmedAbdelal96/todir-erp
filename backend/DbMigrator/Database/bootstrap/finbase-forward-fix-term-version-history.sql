CREATE UNIQUE INDEX IF NOT EXISTS ux_finbase_payment_term_version_number
    ON foundation_finbase.payment_term_version (tenant_id, payment_term_id, version_number);
