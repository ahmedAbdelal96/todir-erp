-- Persist the exact account determination used by each posted line.
ALTER TABLE finance_ledger.journal_entry_line NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_mapping NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_mapping_version NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal_entry_line
    ADD COLUMN IF NOT EXISTS account_role text,
    ADD COLUMN IF NOT EXISTS account_mapping_id uuid,
    ADD COLUMN IF NOT EXISTS account_mapping_version_id uuid,
    ADD COLUMN IF NOT EXISTS determination_context jsonb;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'journal_entry_line_mapping_fk') THEN
        ALTER TABLE finance_ledger.journal_entry_line
            ADD CONSTRAINT journal_entry_line_mapping_fk
            FOREIGN KEY (tenant_id, legal_entity_id, account_mapping_id)
            REFERENCES finance_ledger.account_mapping (tenant_id, legal_entity_id, id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'journal_entry_line_mapping_version_fk') THEN
        ALTER TABLE finance_ledger.journal_entry_line
            ADD CONSTRAINT journal_entry_line_mapping_version_fk
            FOREIGN KEY (tenant_id, legal_entity_id, account_mapping_version_id)
            REFERENCES finance_ledger.account_mapping_version (tenant_id, legal_entity_id, id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS journal_entry_line_mapping_evidence_idx
    ON finance_ledger.journal_entry_line (tenant_id, legal_entity_id, account_mapping_id, account_mapping_version_id);

GRANT SELECT, INSERT, UPDATE ON finance_ledger.journal_entry_line TO erp_app;
ALTER TABLE finance_ledger.journal_entry_line FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_mapping FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_mapping_version FORCE ROW LEVEL SECURITY;
