CREATE TABLE IF NOT EXISTS platform.web_session (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    last_activity_at timestamptz NOT NULL,
    inactivity_expires_at timestamptz NOT NULL,
    reauthentication_expires_at timestamptz NOT NULL,
    security_stamp uuid NOT NULL,
    mfa_verified_at timestamptz,
    status text NOT NULL,
    revoked_at timestamptz,
    revoked_reason text,
    CONSTRAINT web_session_pkey PRIMARY KEY (id),
    CONSTRAINT web_session_status_check CHECK (status IN ('Active', 'Revoked', 'Expired')),
    CONSTRAINT web_session_tenant_id_identity_id_fkey FOREIGN KEY (tenant_id, identity_id)
        REFERENCES platform.identity (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT web_session_tenant_id_membership_id_fkey FOREIGN KEY (tenant_id, membership_id)
        REFERENCES platform.membership (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT web_session_expiry_order_check CHECK (inactivity_expires_at <= reauthentication_expires_at)
);

CREATE INDEX IF NOT EXISTS ix_web_session_tenant_identity_status
    ON platform.web_session (tenant_id, identity_id, status);
CREATE INDEX IF NOT EXISTS ix_web_session_inactivity_expiry
    ON platform.web_session (status, inactivity_expires_at);

ALTER TABLE platform.web_session ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.web_session FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON platform.web_session;
CREATE POLICY tenant_isolation ON platform.web_session
    USING (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid)
    WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid);

GRANT SELECT, INSERT, UPDATE ON TABLE platform.web_session TO erp_app;
