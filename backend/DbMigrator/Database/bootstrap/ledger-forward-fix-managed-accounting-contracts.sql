-- Ledger managed/full setup, readiness, producer registration, and contract metadata.
-- This is additive so an existing accounting history remains untouched.
SET search_path = finance_ledger, public;
SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_ledger.ledger_entity_configuration (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    mode text NOT NULL DEFAULT 'Managed',
    setup_template_code text NOT NULL,
    active_configuration_version_id uuid,
    initialized_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, legal_entity_id),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, active_configuration_version_id) REFERENCES finance_ledger.accounting_configuration_version(tenant_id, legal_entity_id, id),
    CHECK (mode IN ('Managed','Full'))
);

CREATE TABLE IF NOT EXISTS finance_ledger.fact_producer_registration (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    producer_module text NOT NULL,
    fact_type text NOT NULL,
    fact_version integer NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    registered_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, producer_module, fact_type, fact_version),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (fact_version > 0),
    CHECK (status IN ('Active','Retired'))
);

CREATE INDEX IF NOT EXISTS ledger_entity_configuration_mode_idx ON finance_ledger.ledger_entity_configuration (tenant_id, mode);
CREATE INDEX IF NOT EXISTS fact_producer_registration_fact_idx ON finance_ledger.fact_producer_registration (tenant_id, legal_entity_id, fact_type, fact_version, status);

ALTER TABLE finance_ledger.ledger_entity_configuration ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.ledger_entity_configuration FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fact_producer_registration ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fact_producer_registration FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_policy WHERE polname = 'ledger_entity_configuration_tenant_policy' AND polrelid = 'finance_ledger.ledger_entity_configuration'::regclass) THEN
        CREATE POLICY ledger_entity_configuration_tenant_policy ON finance_ledger.ledger_entity_configuration USING (current_setting('app.tenant_id', true) IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid) WITH CHECK (current_setting('app.tenant_id', true) IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policy WHERE polname = 'fact_producer_registration_tenant_policy' AND polrelid = 'finance_ledger.fact_producer_registration'::regclass) THEN
        CREATE POLICY fact_producer_registration_tenant_policy ON finance_ledger.fact_producer_registration USING (current_setting('app.tenant_id', true) IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid) WITH CHECK (current_setting('app.tenant_id', true) IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid);
    END IF;
END $$;

GRANT SELECT, INSERT, UPDATE ON finance_ledger.ledger_entity_configuration TO erp_app;
GRANT SELECT, INSERT, UPDATE ON finance_ledger.fact_producer_registration TO erp_app;
