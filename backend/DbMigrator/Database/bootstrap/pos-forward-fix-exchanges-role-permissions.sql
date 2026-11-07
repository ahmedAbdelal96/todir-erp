DO $$
DECLARE
    current_tenant uuid;
BEGIN
    FOR current_tenant IN SELECT id FROM platform.tenant LOOP
        PERFORM set_config('app.tenant_id', current_tenant::text, false);
        EXECUTE 'INSERT INTO platform.role_permission (tenant_id, role_id, permission_code)
            SELECT role.tenant_id, role.id, ''pos.exchange.create''
            FROM platform.role AS role
            WHERE role.tenant_id = $1 AND role.code = ''administrator''
            ON CONFLICT DO NOTHING'
        USING current_tenant;
    END LOOP;
END $$;
