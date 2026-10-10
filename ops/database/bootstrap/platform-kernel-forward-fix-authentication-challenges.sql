-- Authentication foundation forward-fix. This migration adds only additive,
-- global pre-auth state and a server-derived active access-context read surface.

ALTER TABLE platform_iam.identity
    ADD COLUMN IF NOT EXISTS failed_login_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS locked_until timestamptz;

CREATE TABLE IF NOT EXISTS platform_iam.auth_challenge
(
    id uuid PRIMARY KEY,
    token_digest text NOT NULL UNIQUE,
    identity_id uuid NOT NULL REFERENCES platform_iam.identity (id),
    next_step text NOT NULL CHECK (next_step IN ('VerifyMfa', 'SelectOrganization')),
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_auth_challenge_identity_step
    ON platform_iam.auth_challenge (identity_id, next_step, expires_at)
    WHERE consumed_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_auth_challenge_expiry
    ON platform_iam.auth_challenge (expires_at)
    WHERE consumed_at IS NULL;

GRANT SELECT, INSERT, UPDATE, DELETE ON platform_iam.auth_challenge TO erp_app;

CREATE OR REPLACE FUNCTION platform_iam.list_active_access_contexts(p_identity_id uuid)
RETURNS TABLE
(
    id uuid,
    tenant_id uuid,
    identity_id uuid,
    display_name text,
    status text,
    default_legal_entity_id uuid,
    default_branch_id uuid,
    locale text,
    timezone text,
    created_at timestamptz,
    version bigint,
    organization_name text
)
LANGUAGE sql
SECURITY DEFINER
SET search_path = platform_iam, platform_tenancy, pg_catalog
AS $$
    SELECT m.id, m.tenant_id, m.identity_id, m.display_name, m.status,
           m.default_legal_entity_id, m.default_branch_id, m.locale,
           m.timezone, m.created_at, m.version, t.display_name
      FROM platform_iam.membership m
      JOIN platform_tenancy.tenant t ON t.id = m.tenant_id
     WHERE m.identity_id = p_identity_id
       AND m.status = 'Active'
       AND t.status = 'Active'
$$;
GRANT EXECUTE ON FUNCTION platform_iam.list_active_access_contexts(uuid) TO erp_app;

REVOKE ALL ON FUNCTION platform_iam.list_active_access_contexts(uuid) FROM PUBLIC;
