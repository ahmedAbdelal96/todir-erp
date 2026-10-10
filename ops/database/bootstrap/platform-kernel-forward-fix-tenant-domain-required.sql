DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM platform_tenancy.tenant WHERE login_domain IS NULL OR normalized_login_domain IS NULL) THEN
        RAISE EXCEPTION 'tenant.login_domain_required: every tenant must be provisioned with a login domain';
    END IF;
END $$;

ALTER TABLE platform_tenancy.tenant
    ALTER COLUMN login_domain SET NOT NULL,
    ALTER COLUMN normalized_login_domain SET NOT NULL;

ALTER TABLE platform_tenancy.tenant
    ADD CONSTRAINT ck_tenant_login_domain_normalized CHECK (
        login_domain = lower(trim(login_domain))
        AND normalized_login_domain = login_domain
        AND position('@' in login_domain) = 0
        AND position('/' in login_domain) = 0
        AND position(':' in login_domain) = 0
    );
