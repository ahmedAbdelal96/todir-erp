SET search_path = finance_subledger, public;
SET row_security = off;

DO $$
DECLARE
    legacy_policy boolean;
    legacy_reason_codes boolean;
    disabled_legacy_policy boolean;
    legacy_write_off_records boolean;
    legacy_opening_batches boolean;
BEGIN
    IF to_regclass('finance_subledger.write_off_policy') IS NULL THEN
        RETURN;
    END IF;

    SELECT NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='finance_subledger' AND table_name='write_off_policy'
          AND column_name='automatic_write_off_enabled'
    ) INTO legacy_policy;

    IF NOT legacy_policy THEN
        RETURN;
    END IF;

    ALTER TABLE finance_subledger.write_off_policy NO FORCE ROW LEVEL SECURITY;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='finance_subledger' AND table_name='write_off_policy'
          AND column_name='reason_codes'
    ) INTO legacy_reason_codes;

    IF EXISTS (SELECT 1 FROM finance_subledger.write_off_policy) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema='finance_subledger' AND table_name='write_off_policy'
              AND column_name='enabled'
        ) THEN
            EXECUTE 'SELECT EXISTS (SELECT 1 FROM finance_subledger.write_off_policy WHERE NOT enabled)'
                INTO disabled_legacy_policy;
            IF disabled_legacy_policy THEN
                RAISE EXCEPTION 'Legacy disabled write-off policies need an explicit policy conversion before this upgrade.';
            END IF;
        END IF;
        IF legacy_reason_codes THEN
            EXECUTE 'SELECT EXISTS (SELECT 1 FROM finance_subledger.write_off_policy WHERE cardinality(reason_codes) > 0)'
                INTO disabled_legacy_policy;
            IF disabled_legacy_policy THEN
                RAISE EXCEPTION 'Legacy write-off reason-code restrictions need an explicit policy conversion before this upgrade.';
            END IF;
        END IF;
    END IF;

    IF to_regclass('finance_subledger.write_off_record') IS NOT NULL THEN
        ALTER TABLE finance_subledger.write_off_record NO FORCE ROW LEVEL SECURITY;
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM finance_subledger.write_off_record)' INTO legacy_write_off_records;
        IF legacy_write_off_records THEN
            RAISE EXCEPTION 'Legacy write-off records require Ledger-fact reconciliation before this upgrade.';
        END IF;
    END IF;
    IF to_regclass('finance_subledger.opening_subledger_batch') IS NOT NULL THEN
        ALTER TABLE finance_subledger.opening_subledger_batch NO FORCE ROW LEVEL SECURITY;
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM finance_subledger.opening_subledger_batch)' INTO legacy_opening_batches;
        IF legacy_opening_batches THEN
            RAISE EXCEPTION 'Legacy opening batches require explicit conversion before this upgrade.';
        END IF;
    END IF;

    ALTER TABLE finance_subledger.write_off_policy
        ADD COLUMN IF NOT EXISTS approval_threshold numeric(20,6) NOT NULL DEFAULT 0,
        ADD COLUMN IF NOT EXISTS automatic_write_off_enabled boolean NOT NULL DEFAULT false;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='finance_subledger' AND table_name='write_off_policy'
          AND column_name='require_approval_above'
    ) THEN
        EXECUTE 'UPDATE finance_subledger.write_off_policy SET approval_threshold=COALESCE(require_approval_above,0)';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='finance_subledger' AND table_name='write_off_policy'
          AND column_name='enabled'
    ) THEN
        ALTER TABLE finance_subledger.write_off_policy ALTER COLUMN enabled SET DEFAULT true;
    END IF;
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='finance_subledger' AND table_name='write_off_policy'
          AND column_name='require_separation_of_duties'
    ) THEN
        ALTER TABLE finance_subledger.write_off_policy ALTER COLUMN require_separation_of_duties SET DEFAULT true;
    END IF;
    IF legacy_reason_codes THEN
        ALTER TABLE finance_subledger.write_off_policy ALTER COLUMN reason_codes SET DEFAULT ARRAY[]::text[];
    END IF;

    ALTER TABLE finance_subledger.write_off_policy
        DROP CONSTRAINT IF EXISTS write_off_policy_tenant_id_legal_entity_id_direction_key;
    CREATE UNIQUE INDEX IF NOT EXISTS ux_subledger_write_off_policy_currency
        ON finance_subledger.write_off_policy (tenant_id,legal_entity_id,direction,currency_code);

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid='finance_subledger.write_off_policy'::regclass
          AND conname='ck_subledger_write_off_policy_semantics'
    ) THEN
        ALTER TABLE finance_subledger.write_off_policy
            ADD CONSTRAINT ck_subledger_write_off_policy_semantics
            CHECK (maximum_amount > 0 AND approval_threshold >= 0
                AND approval_threshold <= maximum_amount
                AND automatic_write_off_enabled = false) NOT VALID;
    END IF;

    ALTER TABLE finance_subledger.write_off_policy ENABLE ROW LEVEL SECURITY;
    ALTER TABLE finance_subledger.write_off_policy FORCE ROW LEVEL SECURITY;
    IF to_regclass('finance_subledger.write_off_record') IS NOT NULL THEN
        ALTER TABLE finance_subledger.write_off_record ENABLE ROW LEVEL SECURITY;
        ALTER TABLE finance_subledger.write_off_record FORCE ROW LEVEL SECURITY;
    END IF;
    IF to_regclass('finance_subledger.opening_subledger_batch') IS NOT NULL THEN
        ALTER TABLE finance_subledger.opening_subledger_batch ENABLE ROW LEVEL SECURITY;
        ALTER TABLE finance_subledger.opening_subledger_batch FORCE ROW LEVEL SECURITY;
    END IF;
END;
$$;

RESET row_security;
RESET search_path;
