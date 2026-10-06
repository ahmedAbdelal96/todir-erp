-- Identity is global; membership is the tenant-scoped authorization subject.

CREATE TABLE IF NOT EXISTS platform_iam.identity
(
    id uuid PRIMARY KEY,
    normalized_email text NOT NULL UNIQUE,
    email_verified boolean NOT NULL DEFAULT false,
    password_hash text,
    phone text,
    status text NOT NULL CHECK (status IN ('Active', 'Suspended', 'Revoked')),
    created_at timestamptz NOT NULL,
    last_login_at timestamptz,
    security_stamp uuid NOT NULL,
    version bigint NOT NULL
);
ALTER TABLE platform_iam.identity ADD COLUMN IF NOT EXISTS password_hash text;
ALTER TABLE platform_iam.identity ADD COLUMN IF NOT EXISTS phone text;

CREATE TABLE IF NOT EXISTS platform_iam.membership
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL REFERENCES platform_iam.identity (id),
    display_name text NOT NULL,
    status text NOT NULL CHECK (status IN ('Invited', 'Active', 'Suspended', 'Revoked')),
    default_legal_entity_id uuid,
    default_branch_id uuid,
    locale text,
    timezone text,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL,
    UNIQUE (tenant_id, identity_id),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform_tenancy.tenant (id),
    FOREIGN KEY (tenant_id, default_legal_entity_id)
        REFERENCES platform_org.legal_entity (tenant_id, id),
    FOREIGN KEY (tenant_id, default_branch_id)
        REFERENCES platform_org.branch (tenant_id, id)
);

CREATE INDEX IF NOT EXISTS ix_membership_tenant_status
    ON platform_iam.membership (tenant_id, status);
CREATE INDEX IF NOT EXISTS ix_membership_identity_status
    ON platform_iam.membership (identity_id, status);

ALTER TABLE platform_iam.membership ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_iam.membership FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON platform_iam.membership;
CREATE POLICY tenant_isolation ON platform_iam.membership
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
