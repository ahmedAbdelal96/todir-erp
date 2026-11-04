-- A missing tenant context must deny rows, not fail with an empty-string UUID cast.
-- Keep the policy transaction-local callers safe when a pooled read connection has
-- not yet been assigned a tenant.
DO $$
DECLARE
    table_name text;
BEGIN
    FOR table_name IN
        SELECT c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'finance_payments'
          AND c.relkind = 'r'
          AND EXISTS (
              SELECT 1
              FROM pg_attribute a
              WHERE a.attrelid = c.oid
                AND a.attname = 'tenant_id'
                AND NOT a.attisdropped
          )
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON finance_payments.%I', table_name);
        EXECUTE format(
            'CREATE POLICY tenant_isolation ON finance_payments.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
            table_name
        );
    END LOOP;
END $$;
