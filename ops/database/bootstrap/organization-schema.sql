-- Organization persistence owned by platform_org.

CREATE TABLE IF NOT EXISTS platform_org.legal_entity
(
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    code text NOT NULL,
    legal_name text NOT NULL,
    trade_name text,
    country_code text NOT NULL,
    base_currency_code text NOT NULL,
    timezone text NOT NULL,
    locale text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL,
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, code),
    FOREIGN KEY (tenant_id) REFERENCES platform_tenancy.tenant (id)
);

CREATE TABLE IF NOT EXISTS platform_org.branch
(
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    timezone_override text,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL,
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    FOREIGN KEY (tenant_id, legal_entity_id)
        REFERENCES platform_org.legal_entity (tenant_id, id)
);

CREATE INDEX IF NOT EXISTS ix_legal_entity_tenant_status
    ON platform_org.legal_entity (tenant_id, status);
CREATE INDEX IF NOT EXISTS ix_branch_tenant_legal_entity_status
    ON platform_org.branch (tenant_id, legal_entity_id, status);

ALTER TABLE platform_org.legal_entity ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_org.legal_entity FORCE ROW LEVEL SECURITY;
ALTER TABLE platform_org.branch ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_org.branch FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON platform_org.legal_entity;
CREATE POLICY tenant_isolation ON platform_org.legal_entity
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

DROP POLICY IF EXISTS tenant_isolation ON platform_org.branch;
CREATE POLICY tenant_isolation ON platform_org.branch
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
