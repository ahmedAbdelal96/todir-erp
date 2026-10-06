-- Forward-fix for the tenant context read surface. Keep the original platform-kernel
-- schema checksum stable; additive operational changes are tracked independently.

CREATE OR REPLACE FUNCTION platform_iam.list_active_memberships(p_identity_id uuid)
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
    version bigint
)
LANGUAGE sql
SECURITY DEFINER
SET search_path = platform_iam, pg_catalog
AS $$
    SELECT m.id, m.tenant_id, m.identity_id, m.display_name, m.status,
           m.default_legal_entity_id, m.default_branch_id, m.locale,
           m.timezone, m.created_at, m.version
      FROM platform_iam.membership m
     WHERE m.identity_id = p_identity_id
       AND m.status = 'Active'
$$;
GRANT EXECUTE ON FUNCTION platform_iam.list_active_memberships(uuid) TO erp_app;
