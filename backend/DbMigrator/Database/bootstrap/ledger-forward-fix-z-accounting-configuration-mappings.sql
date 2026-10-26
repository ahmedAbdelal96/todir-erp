ALTER TABLE finance_ledger.account_mapping_version
    ADD COLUMN IF NOT EXISTS accounting_configuration_version_id uuid;

ALTER TABLE finance_ledger.accounting_configuration_version
    ADD COLUMN IF NOT EXISTS based_on_configuration_version_id uuid;

UPDATE finance_ledger.account_mapping_version v
SET accounting_configuration_version_id = evidence.configuration_version_id
FROM (
    SELECT l.tenant_id, l.legal_entity_id, l.account_mapping_version_id,
           (array_agg(DISTINCT e.accounting_configuration_version_id))[1] AS configuration_version_id
    FROM finance_ledger.journal_entry_line l
    JOIN finance_ledger.journal_entry e
      ON e.tenant_id = l.tenant_id AND e.legal_entity_id = l.legal_entity_id AND e.id = l.journal_entry_id
    WHERE l.account_mapping_version_id IS NOT NULL
      AND e.accounting_configuration_version_id IS NOT NULL
    GROUP BY l.tenant_id, l.legal_entity_id, l.account_mapping_version_id
    HAVING count(DISTINCT e.accounting_configuration_version_id) = 1
) evidence
WHERE v.tenant_id = evidence.tenant_id
  AND v.legal_entity_id = evidence.legal_entity_id
  AND v.id = evidence.account_mapping_version_id
  AND v.accounting_configuration_version_id IS NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM finance_ledger.journal_entry_line l
        JOIN finance_ledger.journal_entry e
          ON e.tenant_id = l.tenant_id AND e.legal_entity_id = l.legal_entity_id AND e.id = l.journal_entry_id
        WHERE l.account_mapping_version_id IS NOT NULL
          AND e.accounting_configuration_version_id IS NOT NULL
        GROUP BY l.tenant_id, l.legal_entity_id, l.account_mapping_version_id
        HAVING count(DISTINCT e.accounting_configuration_version_id) > 1
    ) THEN
        RAISE EXCEPTION 'ledger.mapping_version_has_multiple_historical_configurations';
    END IF;
END $$;

UPDATE finance_ledger.account_mapping_version v
SET accounting_configuration_version_id = c.active_configuration_version_id
FROM finance_ledger.ledger_entity_configuration c
WHERE c.tenant_id = v.tenant_id
  AND c.legal_entity_id = v.legal_entity_id
  AND v.accounting_configuration_version_id IS NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM finance_ledger.account_mapping_version WHERE accounting_configuration_version_id IS NULL) THEN
        RAISE EXCEPTION 'ledger.account_mapping_configuration_backfill_incomplete';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'account_mapping_version_configuration_fk' AND conrelid = 'finance_ledger.account_mapping_version'::regclass) THEN
        ALTER TABLE finance_ledger.account_mapping_version
            ADD CONSTRAINT account_mapping_version_configuration_fk
            FOREIGN KEY (tenant_id, legal_entity_id, accounting_configuration_version_id)
            REFERENCES finance_ledger.accounting_configuration_version(tenant_id, legal_entity_id, id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'accounting_configuration_based_on_fk' AND conrelid = 'finance_ledger.accounting_configuration_version'::regclass) THEN
        ALTER TABLE finance_ledger.accounting_configuration_version
            ADD CONSTRAINT accounting_configuration_based_on_fk
            FOREIGN KEY (tenant_id, legal_entity_id, based_on_configuration_version_id)
            REFERENCES finance_ledger.accounting_configuration_version(tenant_id, legal_entity_id, id);
    END IF;
END $$;

CREATE OR REPLACE FUNCTION finance_ledger.prevent_used_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_TABLE_NAME = 'posting_rule_version' AND (OLD.status = 'Active' OR EXISTS (
        SELECT 1 FROM finance_ledger.journal_entry e WHERE e.tenant_id = OLD.tenant_id AND e.posting_rule_version_id = OLD.id
    )) THEN
        RAISE EXCEPTION 'ledger.posting_rule_version_immutable' USING ERRCODE = '55000';
    END IF;
    IF TG_TABLE_NAME = 'accounting_configuration_version'
       AND (OLD.status <> 'Draft' OR EXISTS (
           SELECT 1 FROM finance_ledger.journal_entry e WHERE e.tenant_id = OLD.tenant_id AND e.accounting_configuration_version_id = OLD.id
       )) AND NOT (
        TG_OP = 'UPDATE'
        AND OLD.status = 'Active'
        AND NEW.status = 'Retired'
        AND NEW.version_number = OLD.version_number
        AND NEW.based_on_configuration_version_id IS NOT DISTINCT FROM OLD.based_on_configuration_version_id
        AND NEW.effective_from = OLD.effective_from
        AND NEW.effective_to IS NOT DISTINCT FROM OLD.effective_to
        AND NEW.snapshot_hash = OLD.snapshot_hash
        AND NEW.created_at = OLD.created_at
    ) THEN
        RAISE EXCEPTION 'ledger.accounting_configuration_immutable' USING ERRCODE = '55000';
    END IF;
    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END; $$;

UPDATE finance_ledger.accounting_configuration_version v
SET status = 'Retired'
WHERE v.status = 'Active'
  AND v.id <> coalesce(
      (SELECT c.active_configuration_version_id FROM finance_ledger.ledger_entity_configuration c
       WHERE c.tenant_id = v.tenant_id AND c.legal_entity_id = v.legal_entity_id),
      (SELECT current.id FROM finance_ledger.accounting_configuration_version current
       WHERE current.tenant_id = v.tenant_id AND current.legal_entity_id = v.legal_entity_id AND current.status = 'Active'
       ORDER BY current.version_number DESC LIMIT 1)
  );

CREATE UNIQUE INDEX IF NOT EXISTS ux_ledger_one_active_configuration
    ON finance_ledger.accounting_configuration_version (tenant_id, legal_entity_id)
    WHERE status = 'Active';

ALTER TABLE finance_ledger.account_mapping_version
    ALTER COLUMN accounting_configuration_version_id SET NOT NULL;

CREATE INDEX IF NOT EXISTS ix_ledger_mapping_configuration_role
    ON finance_ledger.account_mapping_version (tenant_id, legal_entity_id, accounting_configuration_version_id, mapping_id, status);

CREATE OR REPLACE FUNCTION finance_ledger.prevent_active_mapping_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status <> 'Draft' OR EXISTS (
        SELECT 1 FROM finance_ledger.journal_entry_line l
        WHERE l.tenant_id = OLD.tenant_id
          AND l.legal_entity_id = OLD.legal_entity_id
          AND l.account_mapping_version_id = OLD.id
    ) THEN
        RAISE EXCEPTION 'ledger.account_mapping_version_immutable' USING ERRCODE = '55000';
    END IF;
    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END; $$;

DROP TRIGGER IF EXISTS account_mapping_version_immutable ON finance_ledger.account_mapping_version;
CREATE TRIGGER account_mapping_version_immutable
    BEFORE UPDATE OR DELETE ON finance_ledger.account_mapping_version
    FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_active_mapping_version_mutation();
