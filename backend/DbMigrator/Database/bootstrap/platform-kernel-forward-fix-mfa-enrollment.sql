CREATE TABLE IF NOT EXISTS platform.mfa_enrollment_challenge (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL,
    token_hash text NOT NULL,
    protected_secret text NOT NULL,
    recovery_code_digests text[] NOT NULL,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    status text NOT NULL DEFAULT 'Pending',
    confirmed_at timestamptz,
    CONSTRAINT mfa_enrollment_challenge_pkey PRIMARY KEY (id),
    CONSTRAINT mfa_enrollment_challenge_status_check CHECK (status IN ('Pending', 'Confirmed', 'Expired')),
    CONSTRAINT mfa_enrollment_challenge_expiry_check CHECK (expires_at > created_at),
    CONSTRAINT mfa_enrollment_challenge_token_uq UNIQUE (token_hash),
    CONSTRAINT mfa_enrollment_challenge_tenant_identity_fkey FOREIGN KEY (tenant_id, identity_id)
        REFERENCES platform.identity (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_mfa_enrollment_challenge_identity_status
    ON platform.mfa_enrollment_challenge (tenant_id, identity_id, status, expires_at);
ALTER TABLE platform.mfa_enrollment_challenge ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.mfa_enrollment_challenge FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON platform.mfa_enrollment_challenge;
CREATE POLICY tenant_isolation ON platform.mfa_enrollment_challenge
    USING (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid)
    WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.mfa_enrollment_challenge TO erp_app;
