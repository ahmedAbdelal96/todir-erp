CREATE TABLE IF NOT EXISTS foundation_finbase.financial_policy_version (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    version bigint NOT NULL,
    rounding_policy_id uuid NOT NULL,
    exchange_rate_max_age_days integer NOT NULL,
    manual_rate_override_allowed boolean NOT NULL,
    captured_at timestamptz NOT NULL,
    CONSTRAINT financial_policy_version_pk PRIMARY KEY (tenant_id, legal_entity_id, version),
    CONSTRAINT financial_policy_version_age_check CHECK (exchange_rate_max_age_days BETWEEN 0 AND 3650),
    CONSTRAINT financial_policy_version_number_check CHECK (version > 0)
);

ALTER TABLE foundation_finbase.financial_policy_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE foundation_finbase.financial_policy_version FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON foundation_finbase.financial_policy_version
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT ON TABLE foundation_finbase.financial_policy_version TO erp_app;

CREATE OR REPLACE FUNCTION foundation_finbase.protect_financial_policy_version()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.financial_policy_history_immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_financial_policy_version ON foundation_finbase.financial_policy_version;
CREATE TRIGGER trg_finbase_protect_financial_policy_version
    BEFORE UPDATE OR DELETE ON foundation_finbase.financial_policy_version
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_financial_policy_version();
