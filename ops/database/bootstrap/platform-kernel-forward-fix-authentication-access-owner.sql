-- Grant only the read boundary required by the bypass-RLS authentication access
-- context function. The API role remains unable to read across tenant boundaries.

GRANT USAGE ON SCHEMA platform_iam, platform_tenancy TO erp_dispatcher;
GRANT SELECT ON platform_iam.membership, platform_tenancy.tenant TO erp_dispatcher;
