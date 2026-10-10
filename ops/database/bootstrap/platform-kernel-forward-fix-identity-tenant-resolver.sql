CREATE OR REPLACE FUNCTION platform_iam.resolve_identity_tenant(p_identity_id uuid)
RETURNS TABLE (tenant_id uuid, status text)
LANGUAGE sql
SECURITY DEFINER
SET search_path = platform_iam, platform_tenancy, pg_catalog
AS $$
    SELECT i.tenant_id, t.status
      FROM platform_iam.identity i
      JOIN platform_tenancy.tenant t ON t.id = i.tenant_id
     WHERE i.id = p_identity_id
     LIMIT 1
$$;
GRANT EXECUTE ON FUNCTION platform_iam.resolve_identity_tenant(uuid) TO erp_app;
REVOKE ALL ON FUNCTION platform_iam.resolve_identity_tenant(uuid) FROM PUBLIC;
