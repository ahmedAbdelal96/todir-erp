CREATE TABLE IF NOT EXISTS platform.mobile_session (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    security_stamp uuid NOT NULL,
    mfa_verified_at timestamptz,
    status text NOT NULL DEFAULT 'Active',
    revoked_at timestamptz,
    revoked_reason text,
    CONSTRAINT mobile_session_pkey PRIMARY KEY (id),
    CONSTRAINT mobile_session_status_check CHECK (status IN ('Active', 'Revoked', 'Expired')),
    CONSTRAINT mobile_session_expiry_check CHECK (expires_at > created_at),
    CONSTRAINT mobile_session_tenant_identity_fkey FOREIGN KEY (tenant_id, identity_id) REFERENCES platform.identity (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT mobile_session_tenant_membership_fkey FOREIGN KEY (tenant_id, membership_id) REFERENCES platform.membership (tenant_id, id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_mobile_session_tenant_identity_status ON platform.mobile_session (tenant_id, identity_id, status);
CREATE INDEX IF NOT EXISTS ix_mobile_session_expiry ON platform.mobile_session (status, expires_at);
ALTER TABLE platform.mobile_session ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.mobile_session FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON platform.mobile_session;
CREATE POLICY tenant_isolation ON platform.mobile_session USING (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid) WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid);
GRANT SELECT, INSERT, UPDATE ON TABLE platform.mobile_session TO erp_app;
