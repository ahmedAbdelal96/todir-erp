-- Replace the rejected global-identity/multi-membership login model with one
-- tenant-bound account and a minimal control-plane domain resolver.

ALTER TABLE platform_tenancy.tenant
    ADD COLUMN IF NOT EXISTS login_domain text,
    ADD COLUMN IF NOT EXISTS normalized_login_domain text;

ALTER TABLE platform_iam.identity
    ADD COLUMN IF NOT EXISTS tenant_id uuid,
    ADD COLUMN IF NOT EXISTS email text,
    ADD COLUMN IF NOT EXISTS name text,
    ADD COLUMN IF NOT EXISTS login_prefix text,
    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS failed_login_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS locked_until timestamptz;

DO $$
DECLARE
    ambiguous record;
    missing record;
BEGIN
    SELECT i.id, string_agg(m.tenant_id::text, ',' ORDER BY m.tenant_id) AS tenant_ids
      INTO ambiguous
      FROM platform_iam.identity i
      JOIN platform_iam.membership m ON m.identity_id = i.id
     GROUP BY i.id
    HAVING count(DISTINCT m.tenant_id) > 1
     LIMIT 1;
    IF ambiguous.id IS NOT NULL THEN
        RAISE EXCEPTION 'identity.tenant_migration_ambiguous: identity % has memberships in tenants %', ambiguous.id, ambiguous.tenant_ids;
    END IF;

    SELECT id INTO missing
      FROM platform_tenancy.tenant
     WHERE login_domain IS NULL OR normalized_login_domain IS NULL
     LIMIT 1;
    IF missing.id IS NOT NULL THEN
        RAISE EXCEPTION 'tenant.login_domain_required: tenant % has no configured login domain', missing.id;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM platform_tenancy.tenant
         WHERE login_domain <> lower(trim(login_domain))
            OR login_domain ~ '[[:space:]@/:]'
            OR normalized_login_domain <> lower(trim(login_domain))
    ) THEN
        RAISE EXCEPTION 'tenant.login_domain_invalid: an existing tenant has an invalid login domain';
    END IF;

    IF EXISTS (SELECT 1 FROM platform_tenancy.tenant GROUP BY normalized_login_domain HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'tenant.login_domain_duplicate: existing tenants contain duplicate login domains';
    END IF;
END $$;

UPDATE platform_iam.identity i
   SET tenant_id = m.tenant_id,
       email = COALESCE(i.email, lower(i.normalized_email)),
       name = COALESCE(i.name, m.display_name),
       login_prefix = COALESCE(i.login_prefix, split_part(lower(i.normalized_email), '@', 1))
  FROM platform_iam.membership m
 WHERE m.identity_id = i.id
   AND i.tenant_id IS NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM platform_iam.identity WHERE tenant_id IS NULL) THEN
        RAISE EXCEPTION 'identity.tenant_required: one or more identity records do not have exactly one tenant';
    END IF;
END $$;

ALTER TABLE platform_iam.identity
    ALTER COLUMN tenant_id SET NOT NULL,
    ALTER COLUMN email SET NOT NULL,
    ALTER COLUMN name SET NOT NULL,
    ALTER COLUMN login_prefix SET NOT NULL;

ALTER TABLE platform_iam.identity
    ADD CONSTRAINT fk_identity_tenant FOREIGN KEY (tenant_id) REFERENCES platform_tenancy.tenant (id),
    ADD CONSTRAINT ck_identity_email_domain CHECK (position('@' in email) > 1),
    ADD CONSTRAINT ux_identity_tenant_id UNIQUE (tenant_id, id),
    ADD CONSTRAINT ux_identity_tenant_prefix UNIQUE (tenant_id, login_prefix);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_normalized_login_domain
    ON platform_tenancy.tenant (normalized_login_domain);
CREATE INDEX IF NOT EXISTS ix_identity_tenant_status_name
    ON platform_iam.identity (tenant_id, status, name, id);
CREATE INDEX IF NOT EXISTS ix_identity_tenant_email
    ON platform_iam.identity (tenant_id, normalized_email);

ALTER TABLE platform_iam.membership
    ADD CONSTRAINT fk_membership_identity_tenant FOREIGN KEY (tenant_id, identity_id)
        REFERENCES platform_iam.identity (tenant_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_membership_identity
    ON platform_iam.membership (identity_id);

ALTER TABLE platform_iam.identity ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_iam.identity FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON platform_iam.identity;
CREATE POLICY tenant_isolation ON platform_iam.identity
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

DO $$
DECLARE constraint_name text;
BEGIN
    FOR constraint_name IN
        SELECT conname
          FROM pg_constraint
         WHERE conrelid = 'platform_iam.auth_challenge'::regclass
           AND pg_get_constraintdef(oid) LIKE '%next_step%'
    LOOP
        EXECUTE format('ALTER TABLE platform_iam.auth_challenge DROP CONSTRAINT %I', constraint_name);
    END LOOP;
END $$;

ALTER TABLE platform_iam.auth_challenge
    ADD COLUMN IF NOT EXISTS mfa_verified boolean NOT NULL DEFAULT false;
ALTER TABLE platform_iam.auth_challenge
    ADD CONSTRAINT ck_auth_challenge_next_step CHECK (next_step IN ('VerifyMfa', 'ChangePassword'));

DROP FUNCTION IF EXISTS platform_iam.list_active_access_contexts(uuid);
DROP FUNCTION IF EXISTS platform_iam.list_active_memberships(uuid);
DROP TABLE IF EXISTS platform_iam.membership_invitation;

CREATE OR REPLACE FUNCTION platform_iam.resolve_login_domain(p_domain text)
RETURNS TABLE (tenant_id uuid, status text, login_domain text)
LANGUAGE sql
SECURITY DEFINER
SET search_path = platform_tenancy, pg_catalog
AS $$
    SELECT t.id, t.status, t.login_domain
      FROM platform_tenancy.tenant t
     WHERE t.normalized_login_domain = lower(trim(p_domain))
     LIMIT 1
$$;

GRANT SELECT, INSERT, UPDATE, DELETE ON platform_iam.identity TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON platform_tenancy.tenant TO erp_app;
GRANT USAGE ON SCHEMA platform_tenancy, platform_iam TO erp_dispatcher;
GRANT SELECT ON platform_tenancy.tenant TO erp_dispatcher;
GRANT EXECUTE ON FUNCTION platform_iam.resolve_login_domain(text) TO erp_app;
REVOKE ALL ON FUNCTION platform_iam.resolve_login_domain(text) FROM PUBLIC;
