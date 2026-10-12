-- Finalize FORCE RLS only after all module-owned foreign keys exist.
DO $$
DECLARE
    item record;
BEGIN
    FOR item IN
        SELECT namespace.nspname AS schema_name, relation.relname AS table_name
        FROM pg_class AS relation
        JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
        WHERE relation.relkind = 'r'
          AND relation.relrowsecurity
          AND namespace.nspname <> 'platform_migration'
          AND EXISTS
          (
              SELECT 1
              FROM pg_attribute AS attribute
              WHERE attribute.attrelid = relation.oid
                AND attribute.attname = 'tenant_id'
                AND NOT attribute.attisdropped
          )
    LOOP
        EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', item.schema_name, item.table_name);
    END LOOP;
END
$$;
