-- Forward-fix the authorization assignment key so tenant-scoped assignments can
-- omit scope_id while legal-entity and branch assignments remain unique.

DO $$
DECLARE
    primary_key_name text;
BEGIN
    ALTER TABLE platform_authz.membership_role_assignment
        ADD COLUMN IF NOT EXISTS id uuid;

    UPDATE platform_authz.membership_role_assignment
       SET id = gen_random_uuid()
     WHERE id IS NULL;

    ALTER TABLE platform_authz.membership_role_assignment
        ALTER COLUMN id SET DEFAULT gen_random_uuid(),
        ALTER COLUMN id SET NOT NULL;

    SELECT constraint_name
      INTO primary_key_name
      FROM information_schema.table_constraints
     WHERE table_schema = 'platform_authz'
       AND table_name = 'membership_role_assignment'
       AND constraint_type = 'PRIMARY KEY';

    IF primary_key_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE platform_authz.membership_role_assignment DROP CONSTRAINT %I', primary_key_name);
    END IF;

    ALTER TABLE platform_authz.membership_role_assignment
        ADD CONSTRAINT membership_role_assignment_pkey PRIMARY KEY (id);
END
$$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_membership_role_assignment_scope
    ON platform_authz.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id)
    NULLS NOT DISTINCT;
