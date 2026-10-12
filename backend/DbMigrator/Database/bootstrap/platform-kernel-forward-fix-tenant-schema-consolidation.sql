-- Consolidate databases created by the retired split platform schema layout.
--
-- The current baseline creates one platform schema. This forward-fix is
-- intentionally guarded so a fresh database is a no-op. On an existing
-- development database it copies every retired platform row into its
-- canonical table, verifies that every source primary key is present, and
-- only then removes the retired schemas. No tenant row is discarded.
DO $$
DECLARE
    retired_schema text;
    consolidation_table_name text;
    insert_sql text;
    key_predicate text;
    missing_rows boolean;
    retired_schemas constant text[] := ARRAY[
        'platform_tenancy',
        'platform_iam',
        'platform_org',
        'platform_authz',
        'platform_audit',
        'platform_messaging',
        'platform_entitlements',
        'platform_numbering',
        'platform_import',
        'platform_notifications',
        'platform_settings',
        'platform_scheduling',
        'platform_modules',
        'platform_custom_fields',
        'platform_files'
    ];
    ordered_tables constant text[] := ARRAY[
        'tenant',
        'legal_entity',
        'branch',
        'identity',
        'membership',
        'role',
        'role_permission',
        'membership_role_assignment',
        'auth_challenge',
        'mfa_recovery_code',
        'support_access_grant',
        'audit_entry',
        'entitlement_snapshot',
        'file_record',
        'import_batch',
        'import_chunk_job',
        'import_error',
        'inbox_message',
        'message_error',
        'outbox_message',
        'sequence_definition',
        'notification',
        'notification_delivery',
        'setting_definition',
        'setting_value',
        'job_schedule',
        'qrtz_job_details',
        'qrtz_triggers',
        'qrtz_blob_triggers',
        'qrtz_calendars',
        'qrtz_cron_triggers',
        'qrtz_fired_triggers',
        'qrtz_paused_trigger_grps',
        'qrtz_scheduler_state',
        'qrtz_simple_triggers',
        'qrtz_simprop_triggers',
        'module_instance',
        'module_operation',
        'custom_field_definition'
    ];
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = ANY (retired_schemas)) THEN
        RETURN;
    END IF;

    -- The retired tables may already be FORCE RLS protected. Migration-owner
    -- access is still intentionally NOBYPASSRLS, so remove FORCE only for this
    -- atomic copy and restore it on the canonical tables before commit.
    FOR retired_schema, consolidation_table_name IN
        SELECT namespace.nspname, relation.relname
          FROM pg_class AS relation
          JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
         WHERE relation.relkind = 'r'
           AND namespace.nspname = ANY (retired_schemas)
    LOOP
        EXECUTE format('alter table %I.%I no force row level security', retired_schema, consolidation_table_name);
    END LOOP;

    FOREACH consolidation_table_name IN ARRAY ordered_tables LOOP
        IF to_regclass(format('platform.%I', consolidation_table_name)) IS NOT NULL THEN
            EXECUTE format('alter table platform.%I no force row level security', consolidation_table_name);
        END IF;
    END LOOP;

    FOREACH consolidation_table_name IN ARRAY ordered_tables LOOP
        FOREACH retired_schema IN ARRAY retired_schemas LOOP
            IF to_regclass(format('%I.%I', retired_schema, consolidation_table_name)) IS NULL
               OR to_regclass(format('platform.%I', consolidation_table_name)) IS NULL THEN
                CONTINUE;
            END IF;

            insert_sql := format(
                'insert into platform.%I select * from %I.%I on conflict do nothing',
                consolidation_table_name,
                retired_schema,
                consolidation_table_name
            );
            EXECUTE insert_sql;

            SELECT string_agg(
                       format('target.%I = source.%I', key_usage.column_name, key_usage.column_name),
                       ' and ' ORDER BY key_usage.ordinal_position
                   )
              INTO key_predicate
              FROM information_schema.table_constraints AS constraint_info
              JOIN information_schema.key_column_usage AS key_usage
                ON key_usage.constraint_name = constraint_info.constraint_name
               AND key_usage.constraint_schema = constraint_info.constraint_schema
             WHERE constraint_info.constraint_type = 'PRIMARY KEY'
               AND constraint_info.table_schema = retired_schema
               AND constraint_info.table_name = consolidation_table_name;

            IF key_predicate IS NULL THEN
                RAISE EXCEPTION 'Retired table %.% has no primary key; refusing an unverified consolidation', retired_schema, consolidation_table_name;
            END IF;

            EXECUTE format(
                'select exists (select 1 from %I.%I source where not exists (select 1 from platform.%I target where %s))',
                retired_schema,
                consolidation_table_name,
                consolidation_table_name,
                key_predicate
            ) INTO missing_rows;
            IF missing_rows THEN
                RAISE EXCEPTION 'Canonical copy is incomplete for %.%', retired_schema, consolidation_table_name;
            END IF;
        END LOOP;
    END LOOP;

    -- This is the destructive half of an expand/contract migration. It is
    -- reached only after every retired row has a canonical primary-key match.
    FOREACH retired_schema IN ARRAY retired_schemas LOOP
        IF to_regnamespace(retired_schema) IS NOT NULL THEN
            EXECUTE format('drop schema %I cascade', retired_schema);
        END IF;
    END LOOP;

    FOREACH consolidation_table_name IN ARRAY ordered_tables LOOP
        IF to_regclass(format('platform.%I', consolidation_table_name)) IS NOT NULL
           AND EXISTS (
               SELECT 1
                 FROM pg_class AS relation
                 JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = 'platform'
                  AND relation.relname = consolidation_table_name
                  AND relation.relrowsecurity
           ) THEN
            EXECUTE format('alter table platform.%I force row level security', consolidation_table_name);
        END IF;
    END LOOP;
END
$$;
